using System;
using InControl;
using UnityEngine;

namespace HandOfFateAccess.Input {
	/// <summary>
	/// A discrete action fired on a key press or controller button press: the status
	/// key now, and map affordances (frontier, suggested next step, commit) in Phase 5.
	/// Any of the bound keyboard keys or controller buttons triggers it, so one action
	/// covers both schemes ("/" or right-stick click for status).
	///
	/// Edge-triggered: it fires on the frame the input goes down, not while held. An
	/// optional activation gate scopes it to a context (e.g. the map screen); when the
	/// gate is closed the action is inert and the input falls through to the game.
	/// </summary>
	internal sealed class ButtonAction : IInputBinding {
		private readonly string _name;
		// System.Action qualified: the game assembly defines a global-namespace Action
		// type that otherwise shadows it.
		private readonly System.Action _onPressed;
		private readonly KeyCode[] _keys;
		private readonly InputControlType[] _buttons;
		private readonly Func<bool> _isActive;

		/// <param name="isActive">Gate for when the action is live; null means always.</param>
		public ButtonAction(string name, System.Action onPressed, KeyCode[] keys,
				InputControlType[] buttons, Func<bool> isActive = null) {
			_name = name;
			_onPressed = onPressed;
			_keys = keys;
			_buttons = buttons;
			_isActive = isActive;
		}

		public string Name => _name;

		public void Poll() {
			if (_isActive != null && !_isActive()) return;
			if (Triggered())
				_onPressed();
		}

		private bool Triggered() {
			foreach (KeyCode key in _keys)
				if (UnityEngine.Input.GetKeyDown(key)) return true;
			foreach (InputControlType button in _buttons)
				if (Gamepad.WasPressed(button)) return true;
			return false;
		}
	}
}
