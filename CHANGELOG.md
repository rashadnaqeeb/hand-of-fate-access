# Changelog

Player-facing changes to Hand of Fate Access, newest first. An entry describes what a player will hear or notice: a new or changed announcement or sound, new controls, a fixed silence. Internal work (refactors, tests, tooling, docs) does not get entries.

New changes go under Unreleased as they are made; the release skill rolls that section into a version heading when a release is cut.

## Unreleased

* The wall collision bump is now louder.
* Walls to the left and right now pan closer as you approach them, to try and make close walls more distinct.

## 1.1.0 - 2026-06-11

* Trap spear launchers now play a telegraph cue when a spear fires down a lane that covers where you stand: the block cue if the spear can be reflected and you hold a reflect-capable counter, otherwise the dodge cue. Spears aimed elsewhere stay quiet, a multi-spear volley cracks once, and the flight voice still tracks every spear in the air.
* A trap on its safe beat now continues to sound safe when you stand on it.
* In trap rooms only the single nearest hazard sounds at a time. Combat is unchanged.
* Beacon pings like room exits and pickup objects now slow down when they're further than the minimum volume threshold.
* One loudness scale for every positioned sound: beacons and hazard loops now range on the enemy locator's validated curve, full within melee reach and fading by twenty units, so a given loudness means the same distance whatever the sound. Far beacons drop to the locator's faint floor (quieter than before), and hazard loops still fade to true silence, just farther out and more gradually.
* In trap rooms, to avoid overwhelming noise, chests no longer loop. instead, use left stick click or l to locate them.
* Added support for several unsupported traps, specifically spinning blades. Traps remain very difficult to handle, and I'm always open to suggestions on improving the experience.
* The mod now survives "Reset progress".
* A recharge chime now plays the moment an equipped ability becomes usable again in a fight: in the left ear for the weapon's ability, in the right ear for the artefact's, matching the button layout.
* The enemy ping's loudness now reads as distance: full volume means the enemy is within melee swing reach (the game's default attack range, 3 units), fading down to a faint floor at 20 units and beyond. Previously the ping stayed loud everywhere (it never dropped below roughly half volume), so near and far enemies sounded much the same.
* The Dealer fight is now cued like the other bosses: his hand swipe, lightning, and pulse attacks each play the dodge cue as they begin, and the lightning's rotating beams carry the same hazard voice as every other beam.
* The Dealer's missile duel speaks through the telegraph cues: the block cue means press counter now, the dodge cue means press dodge now, each fired the moment the game shows its own button prompt.
* During the missile duel the environmental sounds (wall tones, hazard voices, beacons, projectile voices, enemy ping) go quiet, like pause: the game moves the player onto a cinematic platform there, so their bearings would mislead. The duel's own cues stay live.
* The enemy ping no longer answers an enemy that damage cannot currently touch (the Dealer outside his vulnerability windows, a boss mid-teleport, an escaping goblin), so silence now means "nothing attackable right now" and during the Dealer's vulnerability the ping points at the thing to hit. Combo challenges keep pinging everything, since their enemies are unkillable by rule but meant to be hit.
* Chained melee combos (Ratman Jack's flurry, the minotaurs) now play a block-or-dodge cue for every follow-up swing. Previously only the first swing of the combo was cued and the remaining swings landed unannounced.
* The Hermit's dash attack now plays a dodge cue as the dash launches. Previously it had no telegraph at all.

## 1.0.0 - 2026-06-11

Initial release.

