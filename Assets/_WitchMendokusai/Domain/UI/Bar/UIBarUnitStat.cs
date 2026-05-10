using UnityEngine;

namespace WitchMendokusai
{
	public class UIBarUnitStat : UIBarStat<UnitStatType>
	{
		private void Awake()
		{
			IEventBus eventBus = EventBusBridge.Instance;
			eventBus.Subscribe<PlayerObjectBoundEvent>(OnObjectBound);
			eventBus.Subscribe<PlayerDespawnedEvent>(OnDespawned);
		}

		private void OnDestroy()
		{
			if (EventBusBridge.TryGetInstance(out IEventBus eventBus))
			{
				eventBus.Unsubscribe<PlayerObjectBoundEvent>(OnObjectBound);
				eventBus.Unsubscribe<PlayerDespawnedEvent>(OnDespawned);
			}
		}

		private void OnObjectBound(PlayerObjectBoundEvent evt) => BindStat(evt.UnitStat);
		private void OnDespawned(PlayerDespawnedEvent evt) => BindStat(null);
	}
}
