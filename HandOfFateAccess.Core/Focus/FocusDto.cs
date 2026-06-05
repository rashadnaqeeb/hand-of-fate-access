namespace HandOfFateAccess.Focus {
	/// <summary>
	/// Plain snapshot of a focused UI control, extracted by the plugin's adapter
	/// from a live GameObject. Holds no Unity types so Core composition stays
	/// unit-testable. Labels are RAW text (NGUI markup intact); the composer
	/// runs them through TextFilter.
	/// </summary>
	public sealed class FocusDto {
		private static readonly string[] NoLabels = new string[0];

		/// <summary>The focused GameObject's name. Used as a fallback when no label text is present.</summary>
		public string Name { get; }

		/// <summary>Raw text of every UILabel on the focused object and its children, in hierarchy order.</summary>
		public string[] Labels { get; }

		public FocusDto(string name, string[] labels) {
			Name = name;
			Labels = labels ?? NoLabels;
		}
	}
}
