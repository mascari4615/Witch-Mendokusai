using UnityEngine;

namespace WitchMendokusai
{
	public class UIProfile : MonoBehaviour
	{
		[SerializeField] private UISlot slotUI;

		private Unit currentUnitData;

		private void Awake()
		{
			slotUI.Init();

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

		private void OnObjectBound(PlayerObjectBoundEvent evt)
		{
			currentUnitData = evt.UnitData;
			UpdateUI();
		}

		private void OnDespawned(PlayerDespawnedEvent evt)
		{
			currentUnitData = null;
		}

		public void UpdateUI()
		{
			slotUI.SetSlot(currentUnitData);
			slotUI.UpdateUI();
		}
	}
}
