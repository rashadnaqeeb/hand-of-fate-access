namespace HandOfFateAccess.Combat {
	/// <summary>
	/// The raw result of one frame's wall scan: the distance to the nearest impassable
	/// wall on each side. A plain data carrier with no Unity types, so the Core composer
	/// turns it into tones without reaching into the engine.
	/// <see cref="float.PositiveInfinity"/> means nothing impassable was within probe
	/// range on that side.
	/// </summary>
	internal sealed class WallProbe {
		private readonly float[] _distance;

		public WallProbe(float[] distance) {
			_distance = distance;
		}

		public float DistanceTo(WallSide side) => _distance[(int)side];
	}
}
