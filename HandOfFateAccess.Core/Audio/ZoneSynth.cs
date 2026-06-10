using System;

namespace HandOfFateAccess.Audio {
	/// <summary>
	/// Renders the four zone-hazard loops: the voice of a place the player must not stand
	/// (ground fire, blast areas, mines, traps). A pulsed, buzzy TONE, deliberately the only
	/// tonal pulse in the combat mix so it cannot be confused with the wall wind (smooth
	/// broadband noise) or the projectile tumble (pulsed noise). The zone's state is carried
	/// by pulse rate and brightness, never by a different instrument:
	///   primed - once-a-second hard pulse: a trap idling on its proximity trigger; damage is
	///            off, but approaching fires it, so its quiet must not read as absence.
	///   arming - slow soft throb: the zone exists but damage is off; leaving is free.
	///   active - mid-rate buzz: standing in it hurts.
	///   inside - fast hard rattle: you are in it, get out now, away from the sound.
	/// The bearing pitch-shift (down to 0.57x at due south) also slows a loop's pulse, so the
	/// rates are spaced so their shifted ranges never overlap: a southern active zone can
	/// never sound like a northern arming one, and a southern arming throb (1.15 Hz) stays
	/// above the primed pulse (1 Hz).
	///
	/// Seamless by construction: the loop is exactly one second and every component, the
	/// integer-Hz tone partials and the integer-rate pulse envelope, completes a whole number
	/// of cycles in it, so the wrap is sample-continuous with no click. Pure and engine-free,
	/// rendered once at startup and registered as looping clips.
	/// </summary>
	public static class ZoneSynth {
		public const string PrimedKey = "zone_primed";
		public const string ArmingKey = "zone_arming";
		public const string ActiveKey = "zone_active";
		public const string InsideKey = "zone_inside";

		/// <summary>Pulses per second per state. Integer values keep the one-second loop
		/// seamless; the spacing keeps the states separable under the bearing pitch-shift.</summary>
		public const int PrimedPulseHz = 1;
		public const int ArmingPulseHz = 2;
		public const int ActivePulseHz = 6;
		public const int InsidePulseHz = 18;

		// The tone: a low buzz at an integer fundamental (whole cycles per loop second) with
		// harmonic stacks per state - fewer partials read as soft, more as harsh. Peak gain
		// leaves headroom under the cue samples and the wind.
		private const int ToneHz = 160;
		private const float PeakGain = 0.7f;

		// Primed reads as coiled menace: the slowest rate (nothing is moving) but a hard,
		// bright pulse, the opposite pairing from arming's soft throb, so the two slow states
		// cannot blur even when the pitch-shift brings their rates near each other.
		public static float[] RenderPrimed(int sampleRate) =>
			Render(sampleRate, PrimedPulseHz, new[] { 1f, 0.5f, 0.33f, 0.25f }, pulseShape: 8f);

		public static float[] RenderArming(int sampleRate) =>
			Render(sampleRate, ArmingPulseHz, new[] { 1f, 0.25f }, pulseShape: 2f);

		public static float[] RenderActive(int sampleRate) =>
			Render(sampleRate, ActivePulseHz, new[] { 1f, 0.5f, 0.33f, 0.25f }, pulseShape: 4f);

		public static float[] RenderInside(int sampleRate) =>
			Render(sampleRate, InsidePulseHz, new[] { 1f, 0.6f, 0.45f, 0.35f, 0.28f }, pulseShape: 1.5f);

		/// <summary>
		/// One second of pulsed tone: <paramref name="harmonics"/> are the partial amplitudes
		/// at 1x, 2x, ... the fundamental; <paramref name="pulseShape"/> is the exponent on the
		/// per-pulse sine bump (higher = narrower, sharper pulses; lower = wider, closer to a
		/// continuous buzz). The envelope is zero at every pulse boundary, including the loop
		/// wrap, so the loop is click-free by construction.
		/// </summary>
		public static float[] Render(int sampleRate, int pulseHz, float[] harmonics, float pulseShape) {
			if (sampleRate <= 0) sampleRate = 44100;
			var samples = new float[sampleRate];

			// Normalize the harmonic stack so the summed peak sits at PeakGain regardless of
			// how many partials a state uses.
			float sum = 0f;
			for (int h = 0; h < harmonics.Length; h++) sum += harmonics[h];
			float gain = PeakGain / sum;

			double toneStep = 2.0 * Math.PI * ToneHz / sampleRate;
			double pulseStep = Math.PI * pulseHz / sampleRate;   // half-cycle per pulse: |sin| bumps
			for (int n = 0; n < samples.Length; n++) {
				double env = Math.Pow(Math.Abs(Math.Sin(n * pulseStep)), pulseShape);
				double tone = 0.0;
				for (int h = 0; h < harmonics.Length; h++)
					tone += harmonics[h] * Math.Sin(n * toneStep * (h + 1));
				samples[n] = (float)(gain * env * tone);
			}
			return samples;
		}
	}
}
