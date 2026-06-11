# Changelog

Player-facing changes to Hand of Fate Access, newest first. An entry describes what a player will hear or notice: a new or changed announcement or sound, new controls, a fixed silence. Internal work (refactors, tests, tooling, docs) does not get entries.

New changes go under Unreleased as they are made; the release skill rolls that section into a version heading when a release is cut.

## Unreleased

- The Dealer fight is now cued like the other bosses: his hand swipe, lightning, and pulse attacks each play the dodge cue as they begin, and the lightning's rotating beams carry the same hazard voice as every other beam.
- The Dealer's missile duel speaks through the telegraph cues: the block cue means press counter now, the dodge cue means press dodge now, each fired the moment the game shows its own button prompt.
- During the missile duel the environmental sounds (wall tones, hazard voices, beacons, projectile voices, enemy ping) go quiet, like pause: the game moves the player onto a cinematic platform there, so their bearings would mislead. The duel's own cues stay live.
- The enemy ping no longer answers an enemy that damage cannot currently touch (the Dealer outside his vulnerability windows, a boss mid-teleport, an escaping goblin), so silence now means "nothing attackable right now" and during the Dealer's vulnerability the ping points at the thing to hit. Combo challenges keep pinging everything, since their enemies are unkillable by rule but meant to be hit.
- Chained melee combos (Ratman Jack's flurry, the minotaurs) now play a block-or-dodge cue for every follow-up swing. Previously only the first swing of the combo was cued and the remaining swings landed unannounced.
- The Hermit's dash attack now plays a dodge cue as the dash launches. Previously it had no telegraph at all.

## 1.0.0 - 2026-06-11

Initial release.
