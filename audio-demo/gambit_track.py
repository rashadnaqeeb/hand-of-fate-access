"""
Gambit audio, take two: persistent-pitch tracking.

Concept (your design):
  1. REVEAL  - the four cards' outcome earcons, fast, back to back.
  2. ESTABLISH - four tones 1 2 3 4, lowest pitch on the left slot to highest on
                 the right, so each slot/card gets a fixed pitch handle.
  3. SHUFFLE - the game's own shuffle (random permutation per move, two moves per
               repetition) sonified: every card keeps its pitch and its tone pans
               continuously to follow it. You track one pitch/slot through it.
               A short hold at the end, then stop.

Each card's PITCH is its identity (fixed); each card's PAN is its live position.
Cards are face down during the shuffle, so no outcome is leaked, only position.

Real Count/Speed live in the game's ChanceShuffle assets (not in source); the
values below are placeholders to calibrate later.

Standalone listening demo, not the mod. Safe to delete.
"""

import os
import math
import random

from gambit_demo import pan_gains, mix, outcome_earcon, write_wav, SR, SLOTS

# --- shuffle parameters (placeholders for the real asset values) ---
COUNT = 5        # repetitions; the game does 2 position-moves per repetition
SPEED = 0.40     # seconds per position-move (the tween duration)
HOLD = 0.25      # hold at the final positions before stopping

# Slot pan positions, spread wider for clearer separation.
PANS = [-1.0, -0.5, 0.5, 1.0]

# Card pitches by starting slot, a full octave apart each (C3 C4 C5 C6).
PITCHES = [130.81, 261.63, 523.25, 1046.50]

# Each slot gets its own instrument character AND its own rhythm, so the ear has
# both timbre and a distinct pulse to lock onto and follow. harm = additive
# harmonic amplitudes (h1, h2, h3, ...); vib/vibd = vibrato rate (Hz) and depth
# (fraction of pitch); trem/tremd = tremolo (pulse) rate (Hz) and depth.
TIMBRES = [
    dict(harm=[1.0], trem=2.0, tremd=0.6),                                       # pure tone, slow throb
    dict(harm=[1.0, 0.0, 0.12, 0.0, 0.04], trem=3.5, tremd=0.6),                 # mellow, gentle pulse
    dict(harm=[1.0, 0.0, 0.33, 0.0, 0.20, 0.0, 0.14], trem=6.0, tremd=0.6),      # reedy, quick pulse
    dict(harm=[1.0, 0.5, 0.33, 0.25, 0.20, 0.16], trem=10.0, tremd=0.6),         # buzzy, fast flutter
]

# Human-readable handle for each slot's voice, for the answer file only.
VOICE_NAMES = [
    "pure tone, slow throb (~2/sec)",
    "mellow tone, gentle pulse (~3.5/sec)",
    "reedy tone, quick pulse (~6/sec)",
    "buzzy tone, fast flutter (~10/sec)",
]


def voice2(dur, freq, pan, amp=0.4, harm=(1.0,), vib=0.0, vibd=0.0,
           trem=0.0, tremd=0.0, fade=0.008):
    """A voice with a harmonic recipe plus optional vibrato (pitch wobble) and
    tremolo (amplitude pulse). freq is constant; pan may be a constant or f(t)."""
    n = int(dur * SR)
    pan_fn = pan if callable(pan) else (lambda t, p=pan: p)
    norm = sum(abs(h) for h in harm) or 1.0
    L = [0.0] * n
    R = [0.0] * n
    phase = 0.0
    fade_n = max(1, int(fade * SR))
    two_pi = 2.0 * math.pi
    for i in range(n):
        t = i / SR
        f = freq * (1.0 + vibd * math.sin(two_pi * vib * t)) if vibd else freq
        phase += two_pi * f / SR
        s = 0.0
        for k, h in enumerate(harm):
            if h:
                s += h * math.sin((k + 1) * phase)
        s /= norm
        env = amp
        if tremd:
            env *= 1.0 - tremd * 0.5 * (1.0 + math.sin(two_pi * trem * t))
        if i < fade_n:
            env *= i / fade_n
        elif i > n - fade_n:
            env *= (n - i) / fade_n
        s *= env
        lg, rg = pan_gains(pan_fn(t))
        L[i] = s * lg
        R[i] = s * rg
    return L, R


def pan_timeline(segments):
    """Build pan(t) from a list of (dur, p0, p1) linear segments."""
    starts = []
    acc = 0.0
    for dur, _, _ in segments:
        starts.append(acc)
        acc += dur
    total = acc

    def fn(t):
        if t >= total:
            return segments[-1][2]
        # few segments; linear scan is fine
        for i in range(len(segments)):
            s = starts[i]
            dur = segments[i][0]
            if s <= t < s + dur:
                _, p0, p1 = segments[i]
                u = (t - s) / dur if dur > 0 else 1.0
                return p0 + (p1 - p0) * u
        return segments[-1][2]

    return fn, total


def build(target_start=None, seed=None, count=COUNT, speed=SPEED):
    buf_l, buf_r = [], []

    rng = random.Random(seed)  # seed None = system random; an int = deterministic
    if target_start is None:
        # Randomized: which slot holds which outcome is undisclosed.
        layout = ["success", "huge_success", "failure", "huge_failure"]
        rng.shuffle(layout)
        target = layout.index("huge_success")
    else:
        # Controlled: the huge success sits in (and is tracked from) target_start.
        target = target_start
        fillers = ["success", "failure", "huge_failure"]
        layout = ["huge_success" if i == target else fillers.pop(0)
                  for i in range(SLOTS)]

    t = 0.4

    # --- REVEAL: fast, back to back (no gaps) ---
    for i, kind in enumerate(layout):
        t = outcome_earcon(buf_l, buf_r, t, kind, PANS[i])
    t += 0.45

    # --- ESTABLISH: ascending 1 2 3 4, each in its own voice at its slot's pan ---
    for i in range(SLOTS):
        l, r = voice2(0.32, PITCHES[i], PANS[i], amp=0.5, **TIMBRES[i])
        mix(buf_l, buf_r, l, r, t)
        t += 0.34
    t += 0.5

    shuffle_start = t

    # --- SHUFFLE: replicate the game's algorithm, build a pan timeline per card ---
    # slots[p] = id of the card currently in position p; start in order.
    slots = list(range(SLOTS))
    cur_pan = {cid: PANS[p] for p, cid in enumerate(slots)}
    segments = {cid: [] for cid in range(SLOTS)}

    moves = count * 2  # two position-moves per repetition, as the game does
    for _ in range(moves):
        rng.shuffle(slots)  # m_cards.Shuffle(): full random permutation
        for p, cid in enumerate(slots):
            target_pan = PANS[p]
            segments[cid].append((speed, cur_pan[cid], target_pan))
            cur_pan[cid] = target_pan

    # final hold
    for cid in range(SLOTS):
        segments[cid].append((HOLD, cur_pan[cid], cur_pan[cid]))

    # render one sustained tone per card across the whole shuffle
    total = moves * speed + HOLD
    for cid in range(SLOTS):
        pan_fn, _ = pan_timeline(segments[cid])
        l, r = voice2(total, PITCHES[cid], pan_fn, amp=0.30, **TIMBRES[cid])
        mix(buf_l, buf_r, l, r, shuffle_start)

    final_slot = slots.index(target)

    lines = []
    lines.append("ANSWER (no peeking until you have guessed)")
    lines.append("")
    lines.append("Track the HUGE SUCCESS.")
    lines.append("It started in slot %d, the %s." % (target + 1, VOICE_NAMES[target]))
    lines.append("It ended in slot %d (pan %+.1f)." % (final_slot + 1, PANS[final_slot]))
    lines.append("")
    lines.append("Reveal layout, left to right:")
    for i in range(SLOTS):
        lines.append("  slot %d: %-13s  (%s)" % (i + 1, layout[i], VOICE_NAMES[i]))
    lines.append("")
    lines.append("Final arrangement after the shuffle, left to right:")
    for p in range(SLOTS):
        cid = slots[p]
        lines.append("  slot %d: %-13s  (%s)" % (p + 1, layout[cid], VOICE_NAMES[cid]))
    answer = "\n".join(lines) + "\n"

    return buf_l, buf_r, answer


if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    l, r, answer = build()
    write_wav(os.path.join(here, "gambit_track.wav"), l, r)
    with open(os.path.join(here, "gambit_answer.txt"), "w") as f:
        f.write(answer)
    print("done; answer written to gambit_answer.txt")
