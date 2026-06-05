using HandOfFateAccess.Screens;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The routing/diff core: base changes and overlay push/pop each announce only
	/// when the resulting top changes and is announceable.
	/// </summary>
	public class ScreenStackTests {
		[Fact]
		public void Starts_unknown_with_no_overlays() {
			var stack = new ScreenStack();
			Assert.Equal(ScreenId.Unknown, stack.Top);
			Assert.Equal(ScreenId.Unknown, stack.Base);
		}

		[Fact]
		public void SetBase_announces_the_new_screen() {
			var stack = new ScreenStack();
			Assert.Equal("Main menu", stack.SetBase(ScreenId.MainMenu));
			Assert.Equal(ScreenId.MainMenu, stack.Top);
		}

		[Fact]
		public void SetBase_to_transient_state_is_silent() {
			var stack = new ScreenStack();
			Assert.Null(stack.SetBase(ScreenId.Loading));
			Assert.Equal(ScreenId.Loading, stack.Top);
		}

		[Fact]
		public void SetBase_to_same_screen_does_not_reannounce() {
			var stack = new ScreenStack();
			stack.SetBase(ScreenId.Map);
			Assert.Null(stack.SetBase(ScreenId.Map));
		}

		[Fact]
		public void PushOverlay_announces_the_overlay() {
			var stack = new ScreenStack();
			stack.SetBase(ScreenId.CardTable);
			Assert.Equal("Encounter", stack.PushOverlay(ScreenId.Encounter));
			Assert.Equal(ScreenId.Encounter, stack.Top);
		}

		[Fact]
		public void PopOverlay_revealing_base_reannounces_base() {
			var stack = new ScreenStack();
			stack.SetBase(ScreenId.CardTable);
			stack.PushOverlay(ScreenId.Encounter);
			Assert.Equal("Card table", stack.PopOverlay(ScreenId.Encounter));
			Assert.Equal(ScreenId.CardTable, stack.Top);
		}

		[Fact]
		public void PopOverlay_revealing_another_overlay_reannounces_it() {
			var stack = new ScreenStack();
			stack.SetBase(ScreenId.CardTable);
			stack.PushOverlay(ScreenId.Encounter);
			stack.PushOverlay(ScreenId.Combat);
			Assert.Equal("Encounter", stack.PopOverlay(ScreenId.Combat));
			Assert.Equal(ScreenId.Encounter, stack.Top);
		}

		[Fact]
		public void PopOverlay_not_present_is_silent_noop() {
			var stack = new ScreenStack();
			stack.SetBase(ScreenId.CardTable);
			Assert.Null(stack.PopOverlay(ScreenId.Shop));
			Assert.Equal(ScreenId.CardTable, stack.Top);
		}

		[Fact]
		public void SetBase_under_active_overlay_is_silent_but_changes_revealed_base() {
			var stack = new ScreenStack();
			stack.SetBase(ScreenId.CardTable);
			stack.PushOverlay(ScreenId.Shop);
			// Base swaps underneath; top is still the shop, so nothing is spoken.
			Assert.Null(stack.SetBase(ScreenId.Map));
			Assert.Equal(ScreenId.Shop, stack.Top);
			// Popping the overlay now reveals the new base, which is announced.
			Assert.Equal("Map", stack.PopOverlay(ScreenId.Shop));
		}

		[Fact]
		public void ScreensTopDown_orders_overlays_above_base_for_dispatch() {
			var stack = new ScreenStack();
			stack.SetBase(ScreenId.CardTable);
			stack.PushOverlay(ScreenId.Encounter);
			stack.PushOverlay(ScreenId.Combat);
			Assert.Equal(
				new[] { ScreenId.Combat, ScreenId.Encounter, ScreenId.CardTable },
				stack.ScreensTopDown());
		}
	}
}
