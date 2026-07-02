using HandOfFateAccess.Audio;

namespace HandOfFateAccess.Gambit {
	/// <summary>
	/// One gambit voice: a mono buffer (an identity tone or a rendered word) playing
	/// on the shared spatial chain, which applies the equal-power pan plus the
	/// interaural delay - the same placement law as every other positioned sound, so
	/// the card row localizes with the full model, not level alone. The gambit's
	/// stage is the card row, all ahead of the player, so Depth stays 0 (no rear
	/// muffle). This wrapper just holds the engine handle so a caller can swap
	/// buffers (the establish walk replays one voice per slot) and re-pan a playing
	/// loop (the shuffle glides each tone with its card).
	/// </summary>
	internal sealed class GambitVoice {
		// Fully qualified: the game's Assembly-CSharp has its own global Voice type that
		// otherwise shadows the audio pool's handle here.
		private HandOfFateAccess.Audio.Voice _handle = HandOfFateAccess.Audio.Voice.None;
		private float _pan;
		private float _volume;

		/// <summary>Starts playing <paramref name="buffer"/> (mono, at
		/// <paramref name="sourceRate"/>), replacing whatever this voice played before.
		/// Looping voices follow the shuffle; one-shots stop themselves at the end.</summary>
		public void Play(float[] buffer, int sourceRate, bool loop, float pan, float volume) {
			if (_handle.IsValid) AudioEngine.Stop(_handle);
			_pan = pan;
			_volume = volume;
			_handle = AudioEngine.PlayBuffer(buffer, sourceRate, loop, Params());
		}

		/// <summary>Re-aims a playing voice this frame (the shuffle pans the tones live).</summary>
		public void SetPan(float pan) {
			_pan = pan;
			if (_handle.IsValid) AudioEngine.Update(_handle, Params());
		}

		public void Stop() {
			if (_handle.IsValid) {
				AudioEngine.Stop(_handle);
				_handle = HandOfFateAccess.Audio.Voice.None;
			}
		}

		private SoundParams Params() => new SoundParams(_pan, 1f, _volume);
	}
}
