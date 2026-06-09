// HofSapi: a minimal SAPI-to-PCM shim for HandOfFateAccess.
//
// Tolk (the mod's speech path) hands text to the active screen reader and gives back
// nothing playable: no audio buffer, no pan, no start/stop. The chance gambit needs to
// position a spoken card status in the stereo field and know when each word ends, so it
// renders speech itself. This shim drives SAPI's ISpVoice to render a string into an
// in-memory 16-bit PCM WAV and hands the bytes back; the managed side decodes them
// (Core's WavAudio) and plays them through its own panned voice pool.
//
// All the COM stream work stays here in native code, because Unity 5.3.7's old Mono
// runtime cannot be trusted to marshal SAPI's COM surface. P/Invoked exactly like Tolk:
// x86, __cdecl, bare export names.

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <sapi.h>

// SPDFID_WaveFormatEx is a const GUID declared in sapi.h but defined in sapi.lib.
// Define it here so we link only ole32 (CoCreateInstance, the memory stream) and need
// no SAPI import library. The value is fixed by the SAPI ABI.
static const GUID kWaveFormatExGuid =
    { 0xC31ADBAE, 0x527F, 0x4ff5, { 0xA2, 0x30, 0xF6, 0x2B, 0xB6, 0x1F, 0xF7, 0x0C } };

static ISpVoice* g_voice = NULL;
static bool g_ownsCom = false;

// Creates the SAPI voice on the calling thread. Returns 0 on success, otherwise the
// failing HRESULT (the managed side logs it). Idempotent.
extern "C" __declspec(dllexport) int __cdecl HofSapi_Init() {
    if (g_voice) return 0;

    HRESULT hrCom = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);
    // S_OK: we initialized COM on this thread; S_FALSE: it was already initialized in the
    // same mode; either way we own a matching CoUninitialize. RPC_E_CHANGED_MODE: the
    // thread was already initialized in another apartment (Unity's) -- COM is still usable
    // and we must NOT uninitialize it. Any other failure is real: return it so the managed
    // log names the actual cause rather than the downstream CoCreateInstance error.
    if (hrCom == S_OK || hrCom == S_FALSE) g_ownsCom = true;
    else if (hrCom != RPC_E_CHANGED_MODE) return (int)hrCom;

    HRESULT hr = CoCreateInstance(__uuidof(SpVoice), NULL, CLSCTX_ALL,
                                  __uuidof(ISpVoice), (void**)&g_voice);
    if (FAILED(hr)) {
        g_voice = NULL;
        return (int)hr;
    }
    return 0;
}

extern "C" __declspec(dllexport) void __cdecl HofSapi_Shutdown() {
    if (g_voice) { g_voice->Release(); g_voice = NULL; }
    if (g_ownsCom) { CoUninitialize(); g_ownsCom = false; }
}

// Renders a_text to a 16-bit mono PCM WAV at 22050 Hz. On success returns 0 and sets
// *a_bytes to a CoTaskMemAlloc'd buffer of *a_len bytes holding a complete RIFF/WAVE
// file; the caller frees it with Marshal.FreeCoTaskMem. On failure returns the failing
// HRESULT and leaves the out params zeroed.
extern "C" __declspec(dllexport) int __cdecl HofSapi_Synthesize(
        const wchar_t* a_text, unsigned char** a_bytes, int* a_len) {
    if (a_bytes) *a_bytes = NULL;
    if (a_len) *a_len = 0;
    if (!g_voice || !a_text || !a_bytes || !a_len) return E_POINTER;

    IStream* mem = NULL;
    HRESULT hr = CreateStreamOnHGlobal(NULL, TRUE, &mem);
    if (FAILED(hr)) return (int)hr;

    ISpStream* spStream = NULL;
    hr = CoCreateInstance(__uuidof(SpStream), NULL, CLSCTX_ALL,
                          __uuidof(ISpStream), (void**)&spStream);
    if (FAILED(hr)) { mem->Release(); return (int)hr; }

    WAVEFORMATEX wf;
    ZeroMemory(&wf, sizeof(wf));
    wf.wFormatTag = WAVE_FORMAT_PCM;
    wf.nChannels = 1;
    wf.nSamplesPerSec = 22050;
    wf.wBitsPerSample = 16;
    wf.nBlockAlign = (WORD)(wf.nChannels * wf.wBitsPerSample / 8);
    wf.nAvgBytesPerSec = wf.nSamplesPerSec * wf.nBlockAlign;
    wf.cbSize = 0;

    hr = spStream->SetBaseStream(mem, kWaveFormatExGuid, &wf);
    if (SUCCEEDED(hr)) hr = g_voice->SetOutput(spStream, TRUE);
    if (SUCCEEDED(hr)) hr = g_voice->Speak(a_text, SPF_DEFAULT, NULL);  // synchronous
    // Detach the voice from our stream so its held references drop and a later call does
    // not write into this (about to be released) stream. Harmless if output was never set.
    g_voice->SetOutput(NULL, FALSE);

    if (FAILED(hr)) { spStream->Release(); mem->Release(); return (int)hr; }

    // The base stream now holds raw PCM (its format is out-of-band, no RIFF header). Read
    // it back and wrap it in a canonical 44-byte WAV header so the managed side decodes it
    // with the same path it uses for the on-disk cues.
    STATSTG stat;
    ZeroMemory(&stat, sizeof(stat));
    hr = mem->Stat(&stat, STATFLAG_NONAME);
    if (FAILED(hr)) { spStream->Release(); mem->Release(); return (int)hr; }
    // Guard the narrowing to DWORD (and the later (int)total / out int len): cap well under
    // INT_MAX so 44 + dataLen cannot wrap. No real TTS approaches this; a silent truncation
    // would corrupt the WAV or overrun the buffer.
    if (stat.cbSize.QuadPart > (ULONGLONG)(0x7FFFFFFF - 64)) {
        spStream->Release();
        mem->Release();
        return E_OUTOFMEMORY;
    }
    DWORD dataLen = (DWORD)stat.cbSize.QuadPart;

    DWORD total = 44 + dataLen;
    unsigned char* out = (unsigned char*)CoTaskMemAlloc(total);
    if (!out) { spStream->Release(); mem->Release(); return E_OUTOFMEMORY; }

    DWORD byteRate = wf.nAvgBytesPerSec;
    DWORD chunkSize = 36 + dataLen;
    DWORD fmtLen = 16;
    WORD fmtTag = WAVE_FORMAT_PCM;
    WORD ch = wf.nChannels;
    DWORD sr = wf.nSamplesPerSec;
    WORD blockAlign = wf.nBlockAlign;
    WORD bits = wf.wBitsPerSample;
    memcpy(out + 0, "RIFF", 4);
    memcpy(out + 4, &chunkSize, 4);
    memcpy(out + 8, "WAVE", 4);
    memcpy(out + 12, "fmt ", 4);
    memcpy(out + 16, &fmtLen, 4);
    memcpy(out + 20, &fmtTag, 2);
    memcpy(out + 22, &ch, 2);
    memcpy(out + 24, &sr, 4);
    memcpy(out + 28, &byteRate, 4);
    memcpy(out + 32, &blockAlign, 2);
    memcpy(out + 34, &bits, 2);
    memcpy(out + 36, "data", 4);
    memcpy(out + 40, &dataLen, 4);

    LARGE_INTEGER zero;
    zero.QuadPart = 0;
    hr = mem->Seek(zero, STREAM_SEEK_SET, NULL);
    ULONG read = 0;
    if (SUCCEEDED(hr) && dataLen > 0)
        hr = mem->Read(out + 44, dataLen, &read);

    spStream->Release();
    mem->Release();

    if (FAILED(hr) || read != dataLen) {
        CoTaskMemFree(out);
        return FAILED(hr) ? (int)hr : E_FAIL;
    }

    *a_bytes = out;
    *a_len = (int)total;
    return 0;
}
