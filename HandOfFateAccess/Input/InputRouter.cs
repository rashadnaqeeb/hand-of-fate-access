using System;
using System.Collections.Generic;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Input {
	/// <summary>
	/// The mod's input dispatch. Holds the registered bindings and polls each once per
	/// frame from the Update pump. The mod needs its own keys for things the game has no
	/// control for (on-demand status, the map cursor); each is a binding registered here.
	///
	/// A binding's Poll touches live game/device state and can throw, so each is pumped
	/// inside its own try/catch: one bad binding logs and is skipped rather than
	/// stranding every other input for the frame (a silent dead keyboard).
	/// </summary>
	internal sealed class InputRouter {
		private readonly List<IInputBinding> _bindings = new List<IInputBinding>();

		public void Register(IInputBinding binding) {
			_bindings.Add(binding);
		}

		public void Pump() {
			foreach (IInputBinding binding in _bindings) {
				try {
					binding.Poll();
				} catch (Exception ex) {
					Log.Error("input binding '" + binding.Name + "' failed: " + ex);
				}
			}
		}
	}
}
