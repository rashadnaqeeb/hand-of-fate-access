"""Generate the four wall-tone wind loops.

The wall tones are a steady, airy wind whose loudness the game swells with proximity
to a wall (so the only movement is the distance cue, not the clip itself: the clips
do not pulse). Each direction is band-passed white noise, the band alone giving it an
airy "woosh" voice rather than bass rumble, wrap-crossfaded so it loops without a
click. The clips are mono; the game pans them at play time (hard left/right for the
side walls, centred for the fore/aft pair), so the four are distinguished by:
  - left / right: identical mid-band wind, separated purely by pan.
  - below: a low band, so it reads as "down".
  - above: a higher band, so it reads as "up" (kept mid-high, not shrill).
The fore/aft pair share the centre of the stereo image, so their bands are spaced
furthest apart; the side pair lean on pan.

Output: 16-bit mono PCM WAV into ../sounds, the format the mod's WavAudio decoder
and the Unity AudioClip loader expect. Re-runnable; overwrites the four files.
"""
import os
import numpy as np
from scipy import signal
from scipy.io import wavfile

SR = 44100
DUR = 4.0                      # loop length, seconds
XF = int(0.30 * SR)            # wrap-crossfade length for a seamless loop
PAD = 2 * XF                   # discarded filter-transient padding at each end
PEAK = 0.7                     # normalised peak (~-3 dBFS), gentle with headroom


def _bandpass(x, lo_hz, hi_hz, order=3):
    wn = [lo_hz / (SR / 2.0), hi_hz / (SR / 2.0)]
    b, a = signal.butter(order, wn, btype="band")
    return signal.filtfilt(b, a, x)


def _noise(n, seed):
    return np.random.default_rng(seed).standard_normal(n)   # white; the band colours it


def make(seed, band):
    lo_hz, hi_hz = band
    n = int(DUR * SR)
    total = n + 2 * PAD
    x = _noise(total, seed)
    x = _bandpass(x, lo_hz, hi_hz)          # the band sets the direction's voice
    x = x[PAD:PAD + n + XF]                  # drop filter transients, keep n + crossfade

    head = x[:n].copy()
    tail = x[n:n + XF]
    fade = np.linspace(0.0, 1.0, XF)
    head[:XF] = head[:XF] * fade + tail * (1.0 - fade)  # seam: tail flows into head

    head /= np.max(np.abs(head))
    head *= PEAK
    return (head * 32767.0).astype(np.int16)


SPECS = {
    "walltone_above": dict(seed=11, band=(800, 1700)),
    "walltone_below": dict(seed=22, band=(250, 600)),
    "walltone_left": dict(seed=33, band=(550, 1150)),
    "walltone_right": dict(seed=44, band=(550, 1150)),
}

out_dir = os.path.join(os.path.dirname(__file__), "..", "sounds")
for name, spec in SPECS.items():
    data = make(**spec)
    path = os.path.join(out_dir, name + ".wav")
    wavfile.write(path, SR, data)
    print("wrote", os.path.normpath(path), len(data), "samples")
