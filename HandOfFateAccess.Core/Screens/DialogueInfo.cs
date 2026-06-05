using System.Collections.Generic;
using HandOfFateAccess.UI;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Plain snapshot of a modal dialogue, extracted by the plugin's DialogueReader
	/// from the live Dialogue menu. Holds no Unity types so composition stays
	/// unit-testable. All text is RAW (markup intact); Compose runs it through the
	/// Message/TextFilter pipeline.
	///
	/// The spoken line is the prompt body -- the question the player must answer --
	/// which is the one thing focus does not provide (focus reads the buttons as
	/// selection lands on them). Options are kept only as a fallback for the rare
	/// dialogue with no body, so the modal is never announced silently.
	/// </summary>
	public sealed class DialogueInfo {
		private static readonly string[] NoOptions = new string[0];

		/// <summary>Localized prompt text (the question/message).</summary>
		public string Body { get; }

		/// <summary>Raw button labels, used only when there is no body to read.</summary>
		public IList<string> Options { get; }

		public DialogueInfo(string body, IList<string> options = null) {
			Body = body;
			Options = options ?? NoOptions;
		}

		/// <summary>
		/// The line spoken when the dialogue opens: the prompt body, falling back to
		/// the option labels, then to the generic catalog name. Never empty, so a
		/// modal always announces itself.
		/// </summary>
		public string Compose() {
			string body = new Message().Add(Body).Resolve();
			if (body.Length > 0) return body;

			string options = new Message().AddRange(Options).Resolve();
			if (options.Length > 0) return options;

			return ScreenCatalog.NameOf(ScreenId.Dialogue);
		}
	}
}
