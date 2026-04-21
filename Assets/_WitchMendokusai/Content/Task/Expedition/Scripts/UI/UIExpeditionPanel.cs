using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class UIExpeditionPanel : UIBase
	{
		[SerializeField] private GameObject listView;
		[SerializeField] private GameObject activeView;
		[SerializeField] private UIExpeditionSlot slotPrefab;
		[SerializeField] private Transform slotsRoot;
		[SerializeField] private TMP_Text remainingText;
		[SerializeField] private Button completeButton;

		private readonly List<UIExpeditionSlot> slots = new();

		public override void Init() { }

		public override void UpdateUI()
		{
			ExpeditionManager em = DataManager.Instance.ExpeditionManager;

			if (em.HasActive)
			{
				listView.SetActive(false);
				activeView.SetActive(true);
				RefreshActiveView(em.Active);
			}
			else
			{
				listView.SetActive(true);
				activeView.SetActive(false);
				RebuildSlots();
			}
		}

		protected override void OnOpen() => UpdateUI();

		private void RefreshActiveView(RuntimeExpedition active)
		{
			if (active.IsComplete)
			{
				remainingText.text = "완료!";
				completeButton.gameObject.SetActive(true);
			}
			else
			{
				TimeSpan remaining = active.Remaining;
				remainingText.text = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
				completeButton.gameObject.SetActive(false);
			}
		}

		private void RebuildSlots()
		{
			foreach (UIExpeditionSlot slot in slots)
				Destroy(slot.gameObject);
			slots.Clear();

			ForEach<ExpeditionSO>(so =>
			{
				UIExpeditionSlot slot = Instantiate(slotPrefab, slotsRoot);
				slot.SetData(so, () =>
				{
					DataManager.Instance.ExpeditionManager.StartExpedition(so);
					UpdateUI();
				});
				slots.Add(slot);
			});
		}

		public void OnCompleteButtonClicked()
		{
			if (DataManager.Instance.ExpeditionManager.TryComplete(out List<DataSOWithPercentage> loot))
			{
				GameLogic.SpawnLootItem(loot, Player.Instance.transform.position);
				UpdateUI();
			}
		}
	}
}
