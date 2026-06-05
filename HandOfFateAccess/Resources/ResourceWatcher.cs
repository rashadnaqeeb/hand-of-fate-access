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
	/// loop), never mid-event. An encounter's result panel already narrates its outcomes
	/// in the game's own words, so changes are suppressed while an encounter is resolving
	/// and not in combat; combat changes DO speak, since the player wants to hear hits
	/// land. Changes during the game's own loading setup are skipped via IsLoading.
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
			_pending.Add(new KeyValuePair<ResourceKind, int>(kind, diff));
		}

		private void Flush() {
			if (_pending.Count == 0) return;
			// Suppress only while an encounter is resolving and not in combat: the result
			// panel narrates those outcomes. Combat (with or without an owning encounter)
			// still announces, and so do plain map/passive changes.
			bool suppress = Encounter.Instance != null && CombatEncounter.Instance == null;
			if (!suppress) {
				foreach (KeyValuePair<ResourceKind, int> change in _pending) {
					string line = ResourceText.Delta(change.Key, change.Value);
					if (!string.IsNullOrEmpty(line))
						SpeechPipeline.SpeakQueued(line);
				}
			}
			_pending.Clear();
		}

		private void Resubscribe(Player p) {
			// Skip the old player if it was destroyed (Unity's == reports a destroyed
			// object as null): its resource objects are GC'd with it, taking our handlers.
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
