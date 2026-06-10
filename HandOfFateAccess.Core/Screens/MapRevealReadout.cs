using System.Collections.Generic;
using HandOfFateAccess.Localization;
using HandOfFateAccess.UI;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Composes the spoken line for map cards flipped face-up by a reveal effect
	/// (Explorer's Helmet, an encounter's reveal reward...). A sighted player watches the
	/// camera fly to each card and sees it flip; this reads each revealed slot's cards,
	/// every one led by "revealed" so the boundary between cards is unmistakable. The
	/// slots arrive already face-up, so each reads with its full identity.
	/// </summary>
	public static class MapRevealReadout {
		public static string Compose(IList<MapSlotInfo> slots) {
			var message = new Message();
			if (slots == null)
				return "";
			foreach (MapSlotInfo slot in slots) {
				string line = new MapSlotElement(slot).Describe().Resolve();
				if (line.Length > 0)
					message.Add(Strings.MapRevealed + " " + line);
			}
			return message.Resolve();
		}
	}
}
