"""Generate five randomized gambit rounds and one combined answer key.

Each round is an independent randomized layout and shuffle. Listen to
gambit_round1..5, write down where you think the huge success ends in each,
then open gambit_rounds_answers.txt to score yourself.
"""

import os
from gambit_track import build
from gambit_demo import write_wav

ROUNDS = 5

if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    keys = []
    for n in range(1, ROUNDS + 1):
        l, r, answer = build()
        write_wav(os.path.join(here, "gambit_round%d.wav" % n), l, r)
        keys.append("=========== ROUND %d ===========\n%s" % (n, answer))
    with open(os.path.join(here, "gambit_rounds_answers.txt"), "w") as f:
        f.write("\n".join(keys))
    print("done; %d rounds written, key in gambit_rounds_answers.txt" % ROUNDS)
