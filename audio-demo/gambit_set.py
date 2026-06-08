"""Controlled set: one round per voice as the tracked card, deterministic, faster.

Round k tracks voice k (no randomized target). Each round is a fixed,
reproducible shuffle (seeded), run at a faster move speed than the default to
push trackability. Answers go to gambit_set_answers.txt.
"""

import os
from gambit_track import build, VOICE_NAMES
from gambit_demo import write_wav, SLOTS

SPEED = 0.20   # seconds per move; was 0.40, so about twice as fast
COUNT = 5      # repetitions (10 position-moves)

if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    keys = []
    for k in range(SLOTS):
        l, r, answer = build(target_start=k, seed=100 + k, count=COUNT, speed=SPEED)
        write_wav(os.path.join(here, "gambit_set%d.wav" % (k + 1)), l, r)
        keys.append("===== ROUND %d: track the %s =====\n%s"
                    % (k + 1, VOICE_NAMES[k], answer))
    with open(os.path.join(here, "gambit_set_answers.txt"), "w") as f:
        f.write("\n".join(keys))
    print("done; %d rounds written, key in gambit_set_answers.txt" % SLOTS)
