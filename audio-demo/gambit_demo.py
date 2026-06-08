"""
Audio prototype for the chance-card gambit, rendered for the ear instead of the
eye. No speech: every cue is a tone. Stereo panning only (works on plain
headphones, no HRTF/surround).

Walkthrough of one gambit, four cards laid left to right across the stereo field:
  slot 0 (hard left) ... slot 3 (hard right)

Phases:
  1. REVEAL  - sweep the four slots L->R, each plays an earcon for its outcome.
               Then the target outcome (the huge success) is replayed alone so
               the listener knows which card we are tracking.
  2. SHUFFLE - a sequence of pairwise swaps. Each swap is two tones crossing
               between the two involved slots' pan positions, one swap at a time.
               The card identity travels with the slot, so following the swaps
               that touch your slot tells you where your card ended up.
  3. SELECT  - a neutral cursor sweeps the four slots, then confirms on the slot
               the target landed in, then flips it: you hear the huge-success
               earcon come back, proving the track was correct.

This is a listening demo, not the interactive mod. It is standalone and can be
deleted; it is not part of the build.
"""

import math
import wave
import struct

SR = 44100
SLOTS = 4

# ----------------------------------------------------------------------------
# Synthesis primitives
# ----------------------------------------------------------------------------

def pan_gains(p):
    """Equal-power stereo pan. p in [-1, 1]; -1 = hard left, +1 = hard right."""
    theta = (p + 1.0) * 0.5 * (math.pi / 2.0)
    return math.cos(theta), math.sin(theta)


def _as_fn(x):
    """Allow a constant or a function of time t."""
    if callable(x):
        return x
    return lambda t: x


def voice(dur, freq, pan, amp=0.5, bright=0.0, fade=0.006):
    """One sine voice with optional 2nd-harmonic brightness and a pan that can
    glide over time. Returns (left[], right[]) float lists.

    freq and pan may be constants or callables f(t)."""
    n = int(dur * SR)
    freq_fn = _as_fn(freq)
    pan_fn = _as_fn(pan)
    L = [0.0] * n
    R = [0.0] * n
    phase = 0.0
    fade_n = max(1, int(fade * SR))
    for i in range(n):
        t = i / SR
        f = freq_fn(t)
        phase += 2.0 * math.pi * f / SR
        s = math.sin(phase) + bright * math.sin(2.0 * phase)
        s /= (1.0 + bright)
        env = amp
        if i < fade_n:
            env *= i / fade_n
        elif i > n - fade_n:
            env *= (n - i) / fade_n
        s *= env
        lg, rg = pan_gains(pan_fn(t))
        L[i] = s * lg
        R[i] = s * rg
    return L, R


def mix(buf_l, buf_r, L, R, start_t):
    """Add a voice into the master buffers at start_t seconds, growing them."""
    off = int(start_t * SR)
    end = off + len(L)
    if end > len(buf_l):
        buf_l.extend([0.0] * (end - len(buf_l)))
        buf_r.extend([0.0] * (end - len(buf_r)))
    for i in range(len(L)):
        buf_l[off + i] += L[i]
        buf_r[off + i] += R[i]


# ----------------------------------------------------------------------------
# Earcon vocabulary
# ----------------------------------------------------------------------------

def slot_pan(i):
    return -1.0 + 2.0 * i / (SLOTS - 1)


def outcome_earcon(buf_l, buf_r, t, kind, pan, amp=0.5):
    """Outcome cue used at reveal. Bright/ascending = good, dull/descending = bad.
    Returns the time after the earcon."""
    if kind == "huge_success":      # bright, ascending two notes
        l, r = voice(0.16, 660, pan, amp, bright=0.6); mix(buf_l, buf_r, l, r, t)
        l, r = voice(0.20, 990, pan, amp, bright=0.6); mix(buf_l, buf_r, l, r, t + 0.16)
        return t + 0.36
    if kind == "success":           # single bright note
        l, r = voice(0.30, 660, pan, amp, bright=0.5); mix(buf_l, buf_r, l, r, t)
        return t + 0.30
    if kind == "failure":           # single dull low note
        l, r = voice(0.30, 300, pan, amp, bright=0.0); mix(buf_l, buf_r, l, r, t)
        return t + 0.30
    if kind == "huge_failure":      # dark, descending two notes
        l, r = voice(0.16, 300, pan, amp, bright=0.0); mix(buf_l, buf_r, l, r, t)
        l, r = voice(0.22, 200, pan, amp, bright=0.0); mix(buf_l, buf_r, l, r, t + 0.16)
        return t + 0.38
    raise ValueError(kind)


def swap_earcon(buf_l, buf_r, t, slot_a, slot_b):
    """Two tones crossing between slot_a and slot_b. A soft center tick marks the
    onset. Returns the time after the swap."""
    dur = 0.34
    pa, pb = slot_pan(slot_a), slot_pan(slot_b)
    # voice 1: pa -> pb, voice 2: pb -> pa (the cross)
    pan1 = lambda u, pa=pa, pb=pb: pa + (pb - pa) * (u / dur)
    pan2 = lambda u, pa=pa, pb=pb: pb + (pa - pb) * (u / dur)
    l, r = voice(dur, 520, pan1, amp=0.42); mix(buf_l, buf_r, l, r, t)
    l, r = voice(dur, 470, pan2, amp=0.42); mix(buf_l, buf_r, l, r, t)
    # onset tick (short, center)
    l, r = voice(0.03, 1500, 0.0, amp=0.25); mix(buf_l, buf_r, l, r, t)
    return t + dur


def cursor_tick(buf_l, buf_r, t, pan):
    l, r = voice(0.12, 440, pan, amp=0.4, bright=0.2)
    mix(buf_l, buf_r, l, r, t)
    return t + 0.12


def confirm_chord(buf_l, buf_r, t, pan):
    l, r = voice(0.16, 523, pan, amp=0.45, bright=0.4); mix(buf_l, buf_r, l, r, t)
    l, r = voice(0.28, 784, pan, amp=0.45, bright=0.4); mix(buf_l, buf_r, l, r, t + 0.12)
    return t + 0.40


# ----------------------------------------------------------------------------
# Build the walkthrough
# ----------------------------------------------------------------------------

def build():
    buf_l, buf_r = [], []

    # The four cards as laid out at reveal, left to right.
    layout = ["success", "huge_success", "failure", "huge_failure"]
    target = 1  # we will track the huge success

    t = 0.5  # lead-in silence

    # --- REVEAL: sweep the slots left to right ---
    for i, kind in enumerate(layout):
        t = outcome_earcon(buf_l, buf_r, t, kind, slot_pan(i))
        t += 0.35
    t += 0.4
    # Replay the target alone (this stands in for "you chose the huge success").
    t = outcome_earcon(buf_l, buf_r, t, layout[target], slot_pan(target), amp=0.6)
    t += 0.7

    # --- SHUFFLE: a sequence of pairwise swaps; track where the target goes ---
    swaps = [(0, 1), (1, 3), (2, 3), (0, 3)]
    idx = target
    for a, b in swaps:
        t = swap_earcon(buf_l, buf_r, t, a, b)
        if idx == a:
            idx = b
        elif idx == b:
            idx = a
        t += 0.5
    final = idx  # the slot the tracked card now sits in
    t += 0.4

    # --- SELECT: neutral cursor sweep, then confirm + flip the target slot ---
    for i in range(SLOTS):
        t = cursor_tick(buf_l, buf_r, t, slot_pan(i))
        t += 0.16
    t += 0.35
    t = confirm_chord(buf_l, buf_r, t, slot_pan(final))
    t += 0.3
    # Flip it: the outcome earcon returns. Should be the huge success.
    t = outcome_earcon(buf_l, buf_r, t, layout[target], slot_pan(final), amp=0.6)
    t += 0.6

    print("tracked card landed in slot %d (pan %.2f); flip plays: %s"
          % (final, slot_pan(final), layout[target]))
    return buf_l, buf_r


def write_wav(path, buf_l, buf_r):
    # Normalize to avoid clipping from mixed voices, leave headroom.
    peak = 1e-9
    for v in buf_l:
        if abs(v) > peak: peak = abs(v)
    for v in buf_r:
        if abs(v) > peak: peak = abs(v)
    gain = 0.89 / peak
    with wave.open(path, "w") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        frames = bytearray()
        for i in range(len(buf_l)):
            li = int(max(-1.0, min(1.0, buf_l[i] * gain)) * 32767)
            ri = int(max(-1.0, min(1.0, buf_r[i] * gain)) * 32767)
            frames += struct.pack("<hh", li, ri)
        w.writeframes(bytes(frames))
    print("wrote %s (%.1f s)" % (path, len(buf_l) / SR))


if __name__ == "__main__":
    import os
    here = os.path.dirname(os.path.abspath(__file__))
    l, r = build()
    write_wav(os.path.join(here, "gambit_demo.wav"), l, r)
