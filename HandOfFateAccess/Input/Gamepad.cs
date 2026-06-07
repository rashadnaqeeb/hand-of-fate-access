using InControl;

namespace HandOfFateAccess.Input {
	/// <summary>
	/// The single point for reading the gamepad, via InControl (the library the game
	/// itself uses). Reads the active device, so it follows whichever controller the
	/// player last used. With no controller connected InControl's ActiveDevice is the
	/// null-object device whose controls report not-pressed, so these are safe to call
	/// every frame regardless of hardware.
	/// </summary>
	internal static class Gamepad {
		/// <summary>True on the frame this control transitions to pressed (an edge).</summary>
		public static bool WasPressed(InputControlType control) {
			return InControl.InputManager.ActiveDevice.GetControl(control).WasPressed;
		}
	}
}
