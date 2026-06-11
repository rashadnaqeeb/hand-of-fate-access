# Changelog

Player-facing changes to Hand of Fate Access, newest first. An entry describes what a player will hear or notice: a new or changed announcement or sound, new controls, a fixed silence. Internal work (refactors, tests, tooling, docs) does not get entries.

New changes go under Unreleased as they are made; the release skill rolls that section into a version heading when a release is cut.

## Unreleased

- The second gambit card tone (the violin) is now far easier to tell from its neighbors. It was the same note as the first and third tones (one octave apart from each, the most confusable spacing there is) with an in-between texture; it now sits on a different note entirely and carries a deep pitch warble all its own, so each of the first three tones moves in its own way: the organ throbs, the violin warbles, the guitar plucks.
- The mod now survives "Reset progress": the game's reset destroys every object in the engine (the mod's included) before rebuilding its first scene, which silenced the mod until the game was restarted. The mod's objects are now shielded from that sweep and its screen announcements reattach to the rebuilt game, so speech continues through the reset.
- A recharge chime now plays the moment an equipped ability becomes usable again in a fight: in the left ear for the weapon's ability, in the right ear for the artefact's, matching the bumper layout. It covers everything that gates the button (cooldown, charges, gold and health costs, disabling curses), so the chime always means the button works right now. Abilities already ready when the fight starts are not announced, and once the last enemy falls the chime stays quiet; a recharge that completes between waves sounds when the next wave spawns. The sound glossary gained a matching entry that demos both sides.
- The enemy ping's loudness now reads as distance: full volume means the enemy is within melee swing reach (the game's default attack range, 3 units), fading down to a faint floor at 20 units and beyond. Previously the ping stayed loud everywhere (it never dropped below roughly half volume), so near and far enemies sounded much the same.
- The Dealer fight is now cued like the other bosses: his hand swipe, lightning, and pulse attacks each play the dodge cue as they begin, and the lightning's rotating beams carry the same hazard voice as every other beam.
- The Dealer's missile duel speaks through the telegraph cues: the block cue means press counter now, the dodge cue means press dodge now, each fired the moment the game shows its own button prompt.
- During the missile duel the environmental sounds (wall tones, hazard voices, beacons, projectile voices, enemy ping) go quiet, like pause: the game moves the player onto a cinematic platform there, so their bearings would mislead. The duel's own cues stay live.
- The enemy ping no longer answers an enemy that damage cannot currently touch (the Dealer outside his vulnerability windows, a boss mid-teleport, an escaping goblin), so silence now means "nothing attackable right now" and during the Dealer's vulnerability the ping points at the thing to hit. Combo challenges keep pinging everything, since their enemies are unkillable by rule but meant to be hit.
- Chained melee combos (Ratman Jack's flurry, the minotaurs) now play a block-or-dodge cue for every follow-up swing. Previously only the first swing of the combo was cued and the remaining swings landed unannounced.
- The Hermit's dash attack now plays a dodge cue as the dash launches. Previously it had no telegraph at all.

## 1.0.0 - 2026-06-11

Initial release.
