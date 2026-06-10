using System;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Input;
using HandOfFateAccess.Localization;
using HandOfFateAccess.Speech;
using HandOfFateAccess.Util;
using UnityEngine;

namespace HandOfFateAccess.Glossary {
	/// <summary>
	/// The sound glossary: a speech-only list of the mod's combat sounds, so the player
	/// can learn or re-check what each one means outside a fight. It opens from a real
	/// pause-menu option (<see cref="GlossaryButton"/> injects it), so it is found by
	/// arrowing through the menu and works with keyboard, controller and mouse alike.
	/// While open, up/down speak the entries, confirm plays the current entry's demo,
	/// and cancel (Escape or controller B) closes back to the pause list; a second
	/// cancel then resumes the game as usual. There is no visual beyond the button: the
	/// overlay is its spoken list plus input ownership.
	///
	/// All input arrives through <see cref="GlossaryState"/>, recorded by the OnKey and
	/// DoClick patches at the game's own dispatch (which funnels every device into the
	/// selected control), so the glossary reads no raw input. The pause gate doubles as
	/// the teardown: anything that closes the pause menu under the glossary closes it
	/// silently.
	///
	/// Demo clips are the ones the combat features registered at startup, played at the
	/// catalog's canonical parameters; only the projectile renders are the glossary's
	/// own (live projectiles synthesize per voice, so no clip exists to replay). Demo
	/// scheduling runs on unscaled time: the pause menu freezes Time.time.
	/// </summary>
	internal sealed class SoundGlossary : IInputBinding {
		private const int RenderSampleRate = 44100;

		private readonly Func<bool> _pauseActive;
		private readonly GlossaryMenu _menu = new GlossaryMenu(GlossaryCatalog.Entries);
		private readonly GlossaryButton _button = new GlossaryButton();

		private bool _ready;
		private bool _unavailableLogged;
		private bool _open;

		// The running demo: the current entry's steps, how far through them, when the
		// current step ends, and the live voice when that step loops. Null steps = idle.
		private GlossaryStep[] _steps;
		private int _stepIndex;
		private float _stepEndsAt;
		// Fully qualified: the game's Assembly-CSharp has its own global Voice type.
		private HandOfFateAccess.Audio.Voice _voice = HandOfFateAccess.Audio.Voice.None;

		public string Name => "sound glossary";

		/// <param name="pauseActive">Whether the pause menu is the active screen; the
		/// glossary exists only inside it.</param>
		public SoundGlossary(Func<bool> pauseActive) {
			_pauseActive = pauseActive;
		}

		public void Poll() {
			bool paused = _pauseActive();
			_button.Pump(paused);

			if (!paused) {
				// The pause menu closed under us (resume, forfeit, a scene change): the
				// glossary follows silently; announcing over the screen change is noise.
				if (_open) Close(silent: true);
				GlossaryState.DropPending();
				return;
			}

			if (GlossaryState.ConsumeOpenRequest()) Open();
			if (!_open) return;

			PumpDemo();

			while (GlossaryState.TryDequeueKey(out KeyCode key)) {
				if (key == KeyCode.Escape) {
					Close(silent: false);
					return;
				}
				StopDemo();
				SpeechPipeline.SpeakInterrupt(key == KeyCode.DownArrow
					? _menu.MoveNext().Label : _menu.MovePrevious().Label);
			}

			if (GlossaryState.ConsumePlayRequest()) {
				StopDemo();
				_steps = _menu.Current.Steps;
				_stepIndex = -1;
				AdvanceStep();
			}
		}

		private void Open() {
			EnsureClips();
			_menu.Reset();
			_open = true;
			GlossaryState.Open = true;
			SpeechPipeline.SpeakInterrupt(Strings.GlossaryTitle);
			SpeechPipeline.SpeakQueued(_menu.Current.Label);
		}

		private void Close(bool silent) {
			StopDemo();
			_open = false;
			GlossaryState.Open = false;
			GlossaryState.DropPending();
			if (!silent) SpeechPipeline.SpeakInterrupt(Strings.GlossaryClosed);
		}

		// Advance the running demo: when the current step's hold elapses, stop its
		// loop voice (a one-shot has already played itself out) and start the next.
		private void PumpDemo() {
			if (_steps == null) return;
			if (Time.unscaledTime < _stepEndsAt) return;
			StopVoice();
			AdvanceStep();
		}

		private void AdvanceStep() {
			_stepIndex++;
			if (_stepIndex >= _steps.Length) {
				_steps = null;
				return;
			}
			GlossaryStep step = _steps[_stepIndex];
			if (step.Loop) {
				_voice = AudioEngine.Play(step.ClipKey, step.Params, true);
				_stepEndsAt = Time.unscaledTime + step.HoldSeconds;
			} else {
				AudioEngine.PlayOneShot(step.ClipKey, step.Params);
				if (_stepIndex + 1 < _steps.Length)
					_stepEndsAt = Time.unscaledTime + step.HoldSeconds;
				else
					_steps = null;   // the final one-shot plays itself out
			}
		}

		private void StopDemo() {
			StopVoice();
			_steps = null;
		}

		private void StopVoice() {
			if (_voice.IsValid) AudioEngine.Stop(_voice);
			_voice = HandOfFateAccess.Audio.Voice.None;
		}

		// Register the glossary's own demo renders, once. Every other catalog key was
		// registered by its feature at startup; one whose sample failed to load plays
		// nothing and the backend logs the unregistered key, so the gap is visible.
		// With the audio backend down the glossary still opens and speaks its entries;
		// only the demos are gone, like every other mod sound, logged once here.
		private void EnsureClips() {
			if (_ready) return;
			if (!AudioEngine.IsAvailable) {
				if (!_unavailableLogged) {
					Log.Warn("audio backend unavailable; sound glossary demos disabled");
					_unavailableLogged = true;
				}
				return;
			}
			AudioEngine.Register(GlossaryCatalog.ProjectileKey,
				ProjectileSynth.Render(RenderSampleRate, GlossaryCatalog.ProjectileDemoSeconds, false, 1u),
				1, RenderSampleRate);
			AudioEngine.Register(GlossaryCatalog.ProjectileReflectedKey,
				ProjectileSynth.Render(RenderSampleRate, GlossaryCatalog.ProjectileDemoSeconds, true, 2u),
				1, RenderSampleRate);
			_ready = true;
			Log.Debug("sound glossary ready");
		}
	}
}
