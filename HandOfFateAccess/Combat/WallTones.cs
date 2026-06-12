using HandOfFateAccess.Audio;
using HandOfFateAccess.Util;
using UnityEngine;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// The wall-tone combat aid: while a fight is live, a looping wind for each side
	/// (left, right, ahead, behind) swells as an impassable wall or column closes in, so
	/// a player who cannot see the arena can hear how boxed in they are and which way is
	/// clear. All four sides are decorrelated takes of one synthesized wind
	/// (<see cref="WindSynth"/>, one seed per side), told apart by position: pan for the
	/// side winds, the bearing grammar's pitch axis for the fore/aft pair. A side wind's
	/// pan also slides from its rest position into the ear over the last stretch before
	/// contact, the Core composer's contact-imminent cue.
	///
	/// All four voices run continuously for the whole fight; each frame their volumes and
	/// pans are eased toward the targets the Core composer derives from the live
	/// distances. Driving the running loops rather than starting and stopping them is
	/// deliberate: in cluttered arenas the nearest object on a side flickers across the
	/// range edge and swaps identity constantly, and restarting a loop each time clicks
	/// and lurches. The voice handles are the mod's own audio state, not game state; the
	/// distances behind them are re-measured live every frame.
	/// </summary>
	internal sealed class WallTones {
		private const int RenderSampleRate = 44100;

		// Indexed by (int)WallSide: Right, Left, Above, Below. The keys live on the Core
		// composer, which the sound glossary also reads.
		private static readonly string[] Keys = {
			WallToneComposer.RightKey,
			WallToneComposer.LeftKey,
			WallToneComposer.AboveKey,
			WallToneComposer.BelowKey,
		};

		// Fully qualified: the game's Assembly-CSharp has its own global Voice type that
		// otherwise shadows the audio pool's handle here.
		private readonly HandOfFateAccess.Audio.Voice[] _voice = new HandOfFateAccess.Audio.Voice[4];
		private readonly float[] _volume = new float[4];
		private readonly float[] _pan = new float[4];
		private bool _started;

		/// <summary>Render and register the four wind loops, one seed per side so the
		/// takes are decorrelated and simultaneous sides image separately.</summary>
		public void Initialize() {
			if (!AudioEngine.IsAvailable) {
				Log.Warn("audio backend unavailable; wall tones disabled");
				return;
			}
			for (int i = 0; i < Keys.Length; i++)
				AudioEngine.Register(Keys[i], WindSynth.Render(RenderSampleRate, (uint)(i + 1)), 1, RenderSampleRate);
		}

		/// <summary>
		/// Drive the tones for this frame. Outside a live fight (or before audio is up) the
		/// voices are stopped; inside combat each side's volume and pan ease toward their
		/// live targets.
		/// </summary>
		public void Pump() {
			if (!AudioEngine.IsAvailable) return;

			// The Dealer's missile quick-time counts as outside: the player is teleported
			// onto an authored platform there, so the probe would measure geometry that
			// says nothing about where the player can walk.
			if (!CombatGate.IsLive || DealerQte.IsActive) {
				StopSession();
				return;
			}

			WallProbe probe = CombatArenaReader.Probe(WallToneComposer.Range);
			if (probe == null) {
				StopSession();
				return;
			}

			if (!_started) StartSession();

			float dt = Time.deltaTime;
			for (int i = 0; i < 4; i++)
				Drive((WallSide)i, probe, dt);
		}

		// Open every side's voice at silence and at rest pan; the per-frame easing brings
		// each up from there, so the fight starts without a click.
		private void StartSession() {
			_started = true;
			for (int i = 0; i < 4; i++) {
				var side = (WallSide)i;
				_volume[i] = 0f;
				_pan[i] = WallToneComposer.PanFor(side, float.PositiveInfinity);
				_voice[i] = AudioEngine.Play(Keys[i],
					new SoundParams(_pan[i], WallToneComposer.PitchFor(side), 0f), true);
			}
			Log.Debug("wall tones started");
		}

		private void Drive(WallSide side, WallProbe probe, float dt) {
			int i = (int)side;
			if (!_voice[i].IsValid) return;
			float distance = probe.DistanceTo(side);
			_volume[i] = WallToneComposer.Smooth(_volume[i], WallToneComposer.TargetVolume(distance), dt);
			_pan[i] = WallToneComposer.Smooth(_pan[i], WallToneComposer.PanFor(side, distance), dt);
			AudioEngine.Update(_voice[i], new SoundParams(_pan[i], WallToneComposer.PitchFor(side), _volume[i]));
		}

		private void StopSession() {
			if (!_started) return;
			_started = false;
			for (int i = 0; i < 4; i++) {
				if (_voice[i].IsValid) AudioEngine.Stop(_voice[i]);
				_voice[i] = HandOfFateAccess.Audio.Voice.None;
				_volume[i] = 0f;
			}
			Log.Debug("wall tones stopped");
		}
	}
}
