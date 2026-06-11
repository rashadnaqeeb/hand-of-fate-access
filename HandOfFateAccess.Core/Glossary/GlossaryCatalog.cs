using HandOfFateAccess.Audio;
using HandOfFateAccess.Combat;
using HandOfFateAccess.Localization;

namespace HandOfFateAccess.Glossary {
	/// <summary>
	/// The sound glossary's contents: every combat sound the mod plays, one entry
	/// each, in the order they are worth learning (the reaction cues first, then the
	/// tracking voices, then navigation). Each demo is the canonical authored sound:
	/// centered, at the clip's own pitch, at the feature's peak loudness, so the player
	/// hears the cleanest version of what they will later read bearing and distance
	/// off. The one exception is the wall tones, whose per-side pan IS the sound, so
	/// the four sides play in sequence each at its real stereo position.
	///
	/// Demo clips: the zone loops are authored at exactly one second, so a single
	/// non-looping play is the one-second demo. The projectile has no registered clip
	/// at all (live voices synthesize in real time), so the glossary registers its own
	/// one-second renders under the keys below. The wall tones are continuous loops of
	/// arbitrary length and are the only steps that loop and stop on the hold timer.
	/// </summary>
	public static class GlossaryCatalog {
		/// <summary>Clip keys for the projectile demo renders the glossary registers
		/// itself (see class remarks).</summary>
		public const string ProjectileKey = "glossary_projectile";
		public const string ProjectileReflectedKey = "glossary_projectile_reflected";

		/// <summary>Seconds of tumble in the projectile demo renders.</summary>
		public const float ProjectileDemoSeconds = 1f;

		/// <summary>Seconds each looping step plays before it is stopped.</summary>
		public const float LoopHoldSeconds = 1f;

		// Gap before the recharge demo's second chime: the clip's ~1.26 s plus a beat
		// of silence, so the two sides read as two plays rather than one wide sound.
		private const float RechargeHoldSeconds = 1.6f;

		// The collision bump's demo loudness, matching the plugin's CollisionCue.
		private const float CollisionVolume = 0.7f;

		// Labels are passed as delegates so the array, built once at class load,
		// reads each line through Strings live and follows a language switch.
		public static readonly GlossaryEntry[] Entries = {
			OneShot(() => Strings.GlossaryBlock, AttackCueComposer.BlockKey, AttackCueComposer.MaxVolume),
			OneShot(() => Strings.GlossaryDodge, AttackCueComposer.DodgeKey, AttackCueComposer.MaxVolume),
			OneShot(() => Strings.GlossaryProjectile, ProjectileKey, ProjectileSonifier.MaxVolume),
			OneShot(() => Strings.GlossaryProjectileReflected, ProjectileReflectedKey, ProjectileSonifier.MaxVolume),
			OneShot(() => Strings.GlossaryZonePrimed, ZoneSynth.PrimedKey, ZoneSonifier.MaxVolume),
			OneShot(() => Strings.GlossaryZoneArming, ZoneSynth.ArmingKey, ZoneSonifier.MaxVolume),
			OneShot(() => Strings.GlossaryZoneActive, ZoneSynth.ActiveKey, ZoneSonifier.MaxVolume),
			// Inside is the one zone state that always plays at full volume in a fight.
			OneShot(() => Strings.GlossaryZoneInside, ZoneSynth.InsideKey, 1f),
			new GlossaryEntry(() => Strings.GlossaryWallTones, new[] {
				WallStep(WallSide.Right),
				WallStep(WallSide.Left),
				WallStep(WallSide.Above),
				WallStep(WallSide.Below),
			}),
			OneShot(() => Strings.GlossaryWallCollision, WallToneComposer.CollisionKey, CollisionVolume),
			OneShot(() => Strings.GlossaryEnemyPing, EnemyPingSynth.Key, EnemyPingComposer.MaxVolume),
			OneShot(() => Strings.GlossaryChest, BeaconComposer.ChestKey, BeaconComposer.MaxVolume),
			OneShot(() => Strings.GlossaryExit, BeaconComposer.ExitKey, BeaconComposer.MaxVolume),
			// Like the wall tones, the recharge cue's pan IS its meaning, so the demo
			// plays both sides in the order the label names them: weapon left, then
			// artifact right.
			new GlossaryEntry(() => Strings.GlossaryRecharge, new[] {
				new GlossaryStep(RechargeCueComposer.Key, RechargeCueComposer.Weapon, false, RechargeHoldSeconds),
				new GlossaryStep(RechargeCueComposer.Key, RechargeCueComposer.Artifact, false, 0f),
			}),
		};

		private static GlossaryEntry OneShot(System.Func<string> label, string clipKey, float volume) {
			return new GlossaryEntry(label, new[] {
				new GlossaryStep(clipKey, new SoundParams(0f, 1f, volume), false, 0f),
			});
		}

		private static GlossaryStep WallStep(WallSide side) {
			return new GlossaryStep(
				WallToneComposer.KeyFor(side),
				new SoundParams(WallToneComposer.PanFor(side), 1f, WallToneComposer.MaxVolume),
				true, LoopHoldSeconds);
		}
	}
}
