using System;

namespace HandOfFateAccess.Audio {
	/// <summary>
	/// A second-order low-pass (RBJ cookbook, Butterworth Q) for the rear-muffle wet
	/// path. Direct form 1; retuning preserves the state so a cutoff sweep is
	/// click-free. Pure and engine-free, runs per sample on the audio thread: no
	/// allocation, no locks, no exceptions.
	/// </summary>
	public sealed class BiquadLowPass {
		private const float Q = 0.7071f;

		private float _b0, _b1, _b2, _a1, _a2;
		private float _x1, _x2, _y1, _y2;

		public BiquadLowPass() {
			// Transparent until the first SetCutoff: pass-through coefficients.
			_b0 = 1f;
		}

		/// <summary>Retune to <paramref name="cutoffHz"/> at <paramref name="sampleRate"/>,
		/// keeping the filter state. The cutoff is clamped into the stable range.</summary>
		public void SetCutoff(int sampleRate, float cutoffHz) {
			float max = sampleRate * 0.49f;
			if (cutoffHz < 20f) cutoffHz = 20f;
			else if (cutoffHz > max) cutoffHz = max;

			double w0 = 2.0 * Math.PI * cutoffHz / sampleRate;
			double cosW0 = Math.Cos(w0);
			double alpha = Math.Sin(w0) / (2.0 * Q);
			double a0 = 1.0 + alpha;

			_b1 = (float)((1.0 - cosW0) / a0);
			_b0 = _b2 = _b1 * 0.5f;
			_a1 = (float)(-2.0 * cosW0 / a0);
			_a2 = (float)((1.0 - alpha) / a0);
		}

		/// <summary>Filter one sample.</summary>
		public float Transform(float x) {
			float y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
			_x2 = _x1;
			_x1 = x;
			_y2 = _y1;
			_y1 = y;
			return y;
		}

		/// <summary>Zero the state (a fresh voice must not ring with the last one's tail).</summary>
		public void Reset() {
			_x1 = _x2 = _y1 = _y2 = 0f;
		}

		/// <summary>Zero the state if it ever goes non-finite: NaN propagates through the
		/// feedback taps and would silence the voice permanently, an invisible failure.
		/// Cheap; called once per block.</summary>
		public void SelfHeal() {
			if (float.IsNaN(_y1) || float.IsInfinity(_y1) || float.IsNaN(_y2) || float.IsInfinity(_y2))
				Reset();
		}
	}
}
