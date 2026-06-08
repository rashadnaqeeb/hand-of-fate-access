"""Convert the hand-authored collision sound (collide.ogg) into walltone_collision.wav,
the clip the mod loads for the "walked into a wall" bump.

The source is a 3 s stereo ogg at full scale. This downmixes it to mono (the engine pans
it toward the collision at play time), trims it to a short bump around the impact, fades
the tail, and peak-normalises it to the same -3 dBFS the wall-tone loops use so the cue
sits at a consistent level with them. Needs ffmpeg on PATH for the ogg decode; scipy
handles the rest. Re-runnable; overwrites walltone_collision.wav.
"""
import os
import shutil
import subprocess
import tempfile
import numpy as np
from scipy.io import wavfile

HERE = os.path.dirname(__file__)
SRC = os.path.join(HERE, "..", "sounds", "collide.ogg")
DST = os.path.join(HERE, "..", "sounds", "walltone_collision.wav")

SR = 44100
START = 0.06        # drop the dead air before the hit so the bump is immediate
LENGTH = 0.50       # keep a short bump (the source is 3 s)
FADE_OUT = 0.15     # fade the trimmed tail rather than cutting it
PEAK = 0.7          # ~-3 dBFS, matching the wall-tone loops

ffmpeg = shutil.which("ffmpeg") or "ffmpeg"
tmp = os.path.join(tempfile.gettempdir(), "collide_mono.wav")
subprocess.run(
    [ffmpeg, "-hide_banner", "-loglevel", "error", "-i", SRC,
     "-ac", "1", "-ar", str(SR), "-c:a", "pcm_s16le", tmp, "-y"],
    check=True)
sr, x = wavfile.read(tmp)
os.remove(tmp)
x = x.astype(np.float64) / 32768.0

a = int(START * sr)
seg = x[a:a + int(LENGTH * sr)].copy()
atk = int(0.002 * sr)
seg[:atk] *= np.linspace(0.0, 1.0, atk)            # 2 ms attack, no cut-in click
fo = int(FADE_OUT * sr)
seg[-fo:] *= np.linspace(1.0, 0.0, fo)
seg = seg / np.max(np.abs(seg)) * PEAK

wavfile.write(DST, sr, (seg * 32767.0).astype(np.int16))
print("wrote", os.path.normpath(DST), len(seg), "samples", f"{len(seg) / sr:.2f}s")
