using System;
using System.Collections.Generic;
using HandOfFateAccess.Localization;
using HandOfFateAccess.Util;
using UnityEngine;

namespace HandOfFateAccess.Glossary {
	/// <summary>
	/// Injects the "Sound glossary" option into the pause menu's root button list, so the
	/// player finds it by arrowing like any other option and activates it with any device.
	/// Built entirely on the game's public menu API (no reflection): the pause root's
	/// controller exposes its <c>ButtonSet</c> and <c>UISelectableGroup</c>; an existing
	/// button is cloned (keeping its visuals, collider and selectable wiring), the game
	/// wiring the clone inherited is stripped (its <c>PauseMenuButton</c>, the label's
	/// <c>UILocalize</c> which would overwrite our text, the copied nav links and binding
	/// key), the label is set to the glossary title, and the clone is spliced into the
	/// selectOnUp/Down chain one row below the bottom item, preserving the chain's wrap.
	/// Its ClickAction only requests the open (the pump opens and speaks; hooks store
	/// state); its CancelAction mirrors every other pause item: resume the game.
	///
	/// The injected control is held as a live reference (the acceptable cache). The menu
	/// can be destroyed by a scene change, so injection re-runs whenever the pause root
	/// is up and the button is missing - one attempt per pause session, logged on
	/// failure, never spammed.
	/// </summary>
	internal sealed class GlossaryButton {
		private UISelectableItem _item;
		private UILabel _label;
		private bool _attemptedThisPause;

		public void Pump(bool paused) {
			if (!paused) {
				_attemptedThisPause = false;
				return;
			}
			if (_item != null) {
				// The game's own pause buttons follow a language switch through their
				// UILocalize, which the clone deliberately lost; keep its caption on the
				// active language instead (the setter is a no-op while unchanged).
				_label.text = Strings.GlossaryTitle;
				return;
			}
			if (_attemptedThisPause) return;

			PauseMenuManager.State state = MenuManager.Instance.PauseMenuManager.CurrentState;
			if (state == null || state.Name != PauseMenuManager.State.StateName.Pause) return;

			_attemptedThisPause = true;
			try {
				Inject(state.Controller);
				Log.Info("glossary button injected into pause menu");
			} catch (Exception ex) {
				Log.Error("glossary button injection failed: " + ex);
			}
		}

		private void Inject(PauseMenuController controller) {
			PauseMenuButton source = FirstActiveButton(controller.ButtonSet);
			UISelectableItem sourceItem = source.SelectableItem;

			// Walk the authored selectOnDown chain to its bottom item, remembering the
			// runner-up for the row spacing and whether the chain wraps back to the top.
			UISelectable bottom = sourceItem;
			UISelectable above = null;
			var visited = new HashSet<UISelectable> { sourceItem };
			while (bottom.selectOnDown != null && visited.Add(bottom.selectOnDown)) {
				above = bottom;
				bottom = bottom.selectOnDown;
			}
			UISelectable wrap = bottom.selectOnDown;   // the top item when the chain wraps, else null

			GameObject clone = UnityEngine.Object.Instantiate(source.gameObject);
			clone.name = "HoFAccessGlossaryButton";
			Transform t = clone.transform;
			t.parent = source.transform.parent;
			t.localScale = source.transform.localScale;
			t.localRotation = source.transform.localRotation;
			// One row below the bottom item, at the chain's own spacing.
			Vector3 step = above != null
				? bottom.transform.localPosition - above.transform.localPosition
				: new Vector3(0f, -80f, 0f);
			t.localPosition = bottom.transform.localPosition + step;

			UnityEngine.Object.Destroy(clone.GetComponent<PauseMenuButton>());
			foreach (UILocalize localize in clone.GetComponentsInChildren<UILocalize>(true))
				UnityEngine.Object.Destroy(localize);

			UISelectableItem item = clone.GetComponent<UISelectableItem>();
			item.bindingKey = null;
			item.selectOnLeft = null;
			item.selectOnRight = null;

			// The first (only) label on a pause button is its caption. Crashing here on
			// an unexpected hierarchy is caught and logged by Pump.
			UILabel label = clone.GetComponentsInChildren<UILabel>(true)[0];
			label.text = Strings.GlossaryTitle;

			// Splice into the nav chain below the bottom item, keeping the wrap.
			bottom.selectOnDown = item;
			item.selectOnUp = bottom;
			item.selectOnDown = wrap;
			if (wrap != null && wrap.selectOnUp == bottom)
				wrap.selectOnUp = item;

			controller.SelectableGroup.Add(item);
			item.ClickAction = _ => GlossaryState.RequestOpen();
			item.CancelAction = _ => {
				UIManager.Instance.UpdateUIInputs(false);
				MenuManager.Instance.PauseMenuManager.SetState(PauseMenuManager.State.StateName.None);
			};
			// The pause menu's selector sprite follows items through this delegate, which
			// the game wires per item in PauseScreen.Awake; mirror that for the clone.
			item.OnSelectEvent += (selectable, selected) => {
				var handler = controller.OnPauseMenuItemSelected;
				if (handler != null) handler(selectable, selected);
			};

			_item = item;
			_label = label;
		}

		private static PauseMenuButton FirstActiveButton(PauseMenuButtonSet set) {
			foreach (PauseMenuButton button in set.Buttons)
				if (button != null && button.gameObject.activeSelf)
					return button;
			throw new InvalidOperationException("no active pause menu button to clone");
		}
	}
}
