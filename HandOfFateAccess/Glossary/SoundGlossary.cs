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
	/// can learn or re-check what each one means outside a fight. Opened with
	/// <see cref="ToggleKey"/> while the pause menu is up (the game is frozen there, so
	/// no live cue competes with the demos); up/down speak the entries, confirm plays the
	/// current entry's demo, and the toggle key closes it. There is no visual: the
	/// overlay is its spoken list plus input ownership, with <see cref="GlossaryState"/>
	/// and the selection patches keeping the arrows and confirm away from the pause menu
	/// underneath. Escape stays with the game, so it closes the pause menu and the
	/// glossary silently follows (the pause gate below).
	///
	/// Demo clips are the ones the combat features registered at startup, played at the
	/// catalog's canonical parameters; only the projectile renders are the glossary's
	/// own (live projectiles synthesize per voice, so no clip exists to replay). Demo
	/// scheduling runs on unscaled time: the pause menu freezes Time.time.
	/// </summary>
	internal sealed class SoundGlossary : IInputBinding {
		private const KeyCode ToggleKey = KeyCode.G;
		private const int RenderSampleRate = 44100;
		// A beat after the pause menu opens, so the hint queues behind the pause
		// announcement and the focus readout rather than crowding them.
		private const float HintDelaySeconds = 2f;

		private readonly Func<bool> _pauseActive;
		private readonly GlossaryMenu _menu = new GlossaryMenu(GlossaryCatalog.Entries);

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

		// The once-per-session discovery hint, scheduled a beat after the first pause.
		private bool _hintDone;
		private bool _wasPaused;
		private float _hintDueAt = -1f;

		public string Name => "sound glossary";

		/// <param name="pauseActive">Whether the pause menu is the active screen; the
		/// glossary exists only inside it.</param>
		public SoundGlossary(Func<bool> pauseActive) {
			_pauseActive = pauseActive;
		}

		public void Poll() {
			bool paused = _pauseActive();
			PumpHint(paused);

			if (!paused) {
				// The pause menu closed under us (Escape, or a quit): the glossary
				// follows silently; announcing over the screen change would be noise.
				if (_open) Close(silent: true);
				return;
			}

			// A key-rebind scan owns the whole keyboard; pressing the toggle key then
			// must bind G, not also open the glossary over the controls screen.
			if (cInput.scanning) return;

			if (!_open) {
				if (UnityEngine.Input.GetKeyDown(ToggleKey)) Open();
				return;
			}

			PumpDemo();

			if (UnityEngine.Input.GetKeyDown(ToggleKey)) {
				Close(silent: false);
			} else if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow)) {
				StopDemo();
				SpeechPipeline.SpeakInterrupt(_menu.MoveNext().Label);
			} else if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow)) {
				StopDemo();
				SpeechPipeline.SpeakInterrupt(_menu.MovePrevious().Label);
			} else if (UnityEngine.Input.GetKeyDown(KeyCode.Return)
					|| UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter)) {
				StopDemo();
				_steps = _menu.Current.Steps;
				_stepIndex = -1;
				AdvanceStep();
			}
		}

		private void Open() {
			if (!EnsureClips()) return;
			_menu.Reset();
			_open = true;
			GlossaryState.Open = true;
			_hintDone = true;   // they found it; never hint again this session
			_hintDueAt = -1f;
			SpeechPipeline.SpeakInterrupt(Strings.GlossaryTitle);
			SpeechPipeline.SpeakQueued(_menu.Current.Label);
		}

		private void Close(bool silent) {
			StopDemo();
			_open = false;
			GlossaryState.Open = false;
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
		private bool EnsureClips() {
			if (_ready) return true;
			if (!AudioEngine.IsAvailable) {
				if (!_unavailableLogged) {
					Log.Warn("audio backend unavailable; sound glossary disabled");
					_unavailableLogged = true;
				}
				return false;
			}
			AudioEngine.Register(GlossaryCatalog.ProjectileKey,
				ProjectileSynth.Render(RenderSampleRate, GlossaryCatalog.ProjectileDemoSeconds, false, 1u),
				1, RenderSampleRate);
			AudioEngine.Register(GlossaryCatalog.ProjectileReflectedKey,
				ProjectileSynth.Render(RenderSampleRate, GlossaryCatalog.ProjectileDemoSeconds, true, 2u),
				1, RenderSampleRate);
			_ready = true;
			Log.Debug("sound glossary ready");
			return true;
		}

		// The glossary has no on-screen button, so it announces itself once per session:
		// a beat after the pause menu first opens, queued so it reads after the pause
		// announcement. Cancelled if the pause closes first; never repeated.
		private void PumpHint(bool paused) {
			if (paused != _wasPaused) {
				_wasPaused = paused;
				_hintDueAt = paused && !_hintDone && AudioEngine.IsAvailable
					? Time.unscaledTime + HintDelaySeconds : -1f;
			}
			if (_hintDueAt < 0f || Time.unscaledTime < _hintDueAt) return;
			_hintDueAt = -1f;
			_hintDone = true;
			SpeechPipeline.SpeakQueued(Strings.GlossaryHint(ToggleKey.ToString()));
		}
	}
}
