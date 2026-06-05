namespace HandOfFateAccess.UI {
	/// <summary>
	/// One encounter-card token stake as structured data: the cards it grants and
	/// the cards it removes, as comma-joined raw titles. The adapter extracts the
	/// titles off the live game model; the "gain"/"lose" wording is applied in Core
	/// (CardElement) so the announcement decision stays testable, per the
	/// adapter/composition split. Either side is empty when that token neither
	/// grants nor removes anything on it.
	/// </summary>
	public sealed class TokenStake {
		/// <summary>Comma-joined titles of cards this token grants; empty if none.</summary>
		public string Gain { get; }

		/// <summary>Comma-joined titles of cards this token removes; empty if none.</summary>
		public string Remove { get; }

		public TokenStake(string gain, string remove) {
			Gain = gain;
			Remove = remove;
		}
	}
}
