using System;
using System.Collections.Generic;
using HandOfFateAccess.Resources;
using HandOfFateAccess.Speech;
using HandOfFateAccess.Util;
using UnityEngine;

namespace HandOfFateAccess.Resources {
	/// <summary>
	/// Announces live resource changes ("-5 health", "+3 gold") by listening to the
	/// player's own change events, so no polling and no cached values. The Player is
	/// recreated each run, so Pump re-subscribes when the live instance changes and drops
	/// any pending changes from the old one.
	///
	/// Changes are recorded in the event callbacks and spoken from Pump (the update
	/// loop), never mid-event. This is the audio equivalent of the floating "+10"/"-5"
	/// the game pops over a stat card: that popup is the game's only indication of the
	/// amount (the encounter result panel narrates the story but never the numbers, and
	/// combat shows no text at all), so every change is announced, in encounters and
	/// combat alike. Changes are scoped to resources the game is currently showing (their
	/// stat card is on the table), which mirrors when the popup appears: the starting
	/// values written during run setup, before any stat card is dealt, are not announced.
	/// Changes during a save resume are skipped via IsLoading.
	/// </summary>
	internal sealed class ResourceWatcher {
		private Player _subscribed;
		private readonly List<KeyValuePair<ResourceKind, int>> _pending = new List<KeyValuePair<ResourceKind, int>>();

		private readonly PlayerResource.OnUpdatedHandler _onGold;
		private readonly PlayerResource.OnUpdatedHandler _onFood;
		private readonly PlayerResource.OnUpdatedHandler _onIronOre;
		private readonly Player.OnHealthStatUpdatedHandler _onHealth;
		private readonly Player.OnHealthStatUpdatedHandler _onMaxHealth;

		public ResourceWatcher() {
			_onGold = diff => Record(ResourceKind.Gold, diff);
			_onFood = diff => Record(ResourceKind.Food, diff);
			_onIronOre = diff => Record(ResourceKind.IronOre, diff);
			_onHealth = diff => Record(ResourceKind.Health, Mathf.RoundToInt(diff));
			_onMaxHealth = diff => Record(ResourceKind.MaxHealth, Mathf.RoundToInt(diff));
		}

		public void Pump() {
			Player p = Player.Instance;
			if (p != _subscribed)
				Resubscribe(p);
			Flush();
		}

		private void Record(ResourceKind kind, int diff) {
			if (diff == 0) return;
			Player p = Player.Instance;
			if (p == null || p.IsLoading) return;
			// Only announce a resource the game is actually showing the player. Starting
			// values are written by Player.Reset during GameState_Init, before any stat
			// card is on the table and before the run begins; without this a launch or
			// replay would speak "+20 food" with no run in progress.
			if (!ResourceReader.IsVisible(kind)) return;
			_pending.Add(new KeyValuePair<ResourceKind, int>(kind, diff));
		}

		private void Flush() {
			if (_pending.Count == 0) return;
			foreach (KeyValuePair<ResourceKind, int> change in _pending) {
				string line = ResourceText.Delta(change.Key, change.Value);
				if (!string.IsNullOrEmpty(line))
					SpeechPipeline.SpeakQueued(line);
			}
			_pending.Clear();
		}

		private void Resubscribe(Player p) {
			// Skip the old player if it was destroyed (Unity's == reports a destroyed
			// object as null). Its resource objects are then orphaned: no game code ever
			// writes to a player other than Player.Instance, so the stale handlers on the
			// old one never fire again.
			if (_subscribed != null) {
				Player prev = _subscribed;
				prev.Gold.OnUpdated = (PlayerResource.OnUpdatedHandler)Delegate.Remove(prev.Gold.OnUpdated, _onGold);
				prev.Food.OnUpdated = (PlayerResource.OnUpdatedHandler)Delegate.Remove(prev.Food.OnUpdated, _onFood);
				prev.IronOre.OnUpdated = (PlayerResource.OnUpdatedHandler)Delegate.Remove(prev.IronOre.OnUpdated, _onIronOre);
				prev.OnHealthUpdated = (Player.OnHealthStatUpdatedHandler)Delegate.Remove(prev.OnHealthUpdated, _onHealth);
				prev.OnMaxHealthUpdated = (Player.OnHealthStatUpdatedHandler)Delegate.Remove(prev.OnMaxHealthUpdated, _onMaxHealth);
			}

			_subscribed = p;
			_pending.Clear();
			if (p == null) return;

			p.Gold.OnUpdated = (PlayerResource.OnUpdatedHandler)Delegate.Combine(p.Gold.OnUpdated, _onGold);
			p.Food.OnUpdated = (PlayerResource.OnUpdatedHandler)Delegate.Combine(p.Food.OnUpdated, _onFood);
			p.IronOre.OnUpdated = (PlayerResource.OnUpdatedHandler)Delegate.Combine(p.IronOre.OnUpdated, _onIronOre);
			p.OnHealthUpdated = (Player.OnHealthStatUpdatedHandler)Delegate.Combine(p.OnHealthUpdated, _onHealth);
			p.OnMaxHealthUpdated = (Player.OnHealthStatUpdatedHandler)Delegate.Combine(p.OnMaxHealthUpdated, _onMaxHealth);
			Log.Info("resource watcher subscribed to player");
		}
	}
}
