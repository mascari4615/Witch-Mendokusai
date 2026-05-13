using UnityEngine;

namespace WitchMendokusai
{
	public class UIBarUnitStat : UIBarStat<UnitStatType>
	{
		private void Awake()
		{
			EventBusBridge.Subscribe<PlayerObjectBoundEvent>(OnObjectBound);
			EventBusBridge.Subscribe<PlayerDespawnedEvent>(OnDespawned);
		}

		private void OnDestroy()
		{
			EventBusBridge.Unsubscribe<PlayerObjectBoundEvent>(OnObjectBound);
			EventBusBridge.Unsubscribe<PlayerDespawnedEvent>(OnDespawned);
		}

		private void OnObjectBound(PlayerObjectBoundEvent evt) => BindStat(evt.UnitStat);
		private void OnDespawned(PlayerDespawnedEvent evt) => BindStat(null);
	}
}
