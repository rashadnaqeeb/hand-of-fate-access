using InControl;
using UnityEngine;

namespace HandOfFateAccess.Input {
	/// <summary>
	/// A repeating four-way directional input: the map cursor's movement. Reads Ctrl+arrows
	/// on the keyboard and the right stick on the controller (both free on the map; plain
	/// arrows still drive the game's own nav, and Ctrl+arrows are consumed in the OnKey
	/// patch so the game's selection does not also move). Fires once on press, then repeats
	/// while held after a short delay, so sweeping the board is fast without hammering a key.
	///
	/// The stick is read from its raw X/Y with our own dead zone rather than InControl's
	/// configurable threshold, so behaviour does not depend on the game's stick settings.
	/// Edge and repeat timing use unscaled time, safe from the Update loop.
	/// </summary>
	internal sealed class DirectionalAction : IInputBinding {
		private const float StickDeadZone = 0.5f;
		private const float InitialRepeatDelay = 0.4f;
		private const float RepeatInterval = 0.18f;

		private readonly string _name;
		private readonly System.Action<Direction> _onStep;
		private readonly System.Func<bool> _isActive;

		private Direction _lastDir;
		private bool _holding;
		private float _nextRepeat;

		public DirectionalAction(string name, System.Action<Direction> onStep, System.Func<bool> isActive) {
			_name = name;
			_onStep = onStep;
			_isActive = isActive;
		}

		public string Name => _name;

		public void Poll() {
			if (_isActive != null && !_isActive()) {
				_holding = false;
				return;
			}

			Direction dir;
			if (!ReadDirection(out dir)) {
				_holding = false;
				return;
			}

			float now = Time.unscaledTime;
			if (!_holding || dir != _lastDir) {
				_holding = true;
				_lastDir = dir;
				_nextRepeat = now + InitialRepeatDelay;
				_onStep(dir);
			} else if (now >= _nextRepeat) {
				_nextRepeat = now + RepeatInterval;
				_onStep(dir);
			}
		}

		// The direction requested this frame, keyboard first then stick, or false for none.
		private static bool ReadDirection(out Direction dir) {
			dir = Direction.Up;
			if (CtrlHeld()) {
				if (UnityEngine.Input.GetKey(KeyCode.UpArrow)) { dir = Direction.Up; return true; }
				if (UnityEngine.Input.GetKey(KeyCode.DownArrow)) { dir = Direction.Down; return true; }
				if (UnityEngine.Input.GetKey(KeyCode.LeftArrow)) { dir = Direction.Left; return true; }
				if (UnityEngine.Input.GetKey(KeyCode.RightArrow)) { dir = Direction.Right; return true; }
			}

			TwoAxisInputControl stick = InControl.InputManager.ActiveDevice.RightStick;
			float x = stick.X, y = stick.Y;
			if (Mathf.Abs(x) < StickDeadZone && Mathf.Abs(y) < StickDeadZone)
				return false;
			if (Mathf.Abs(y) >= Mathf.Abs(x))
				dir = y > 0f ? Direction.Up : Direction.Down;
			else
				dir = x > 0f ? Direction.Right : Direction.Left;
			return true;
		}

		private static bool CtrlHeld() {
			return UnityEngine.Input.GetKey(KeyCode.LeftControl)
				|| UnityEngine.Input.GetKey(KeyCode.RightControl);
		}
	}
}
