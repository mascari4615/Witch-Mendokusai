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

			EventBusBridge.Subscribe<PlayerObjectBoundEvent>(OnObjectBound);
			EventBusBridge.Subscribe<PlayerDespawnedEvent>(OnDespawned);
		}

		private void OnDestroy()
		{
			EventBusBridge.Unsubscribe<PlayerObjectBoundEvent>(OnObjectBound);
			EventBusBridge.Unsubscribe<PlayerDespawnedEvent>(OnDespawned);
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
