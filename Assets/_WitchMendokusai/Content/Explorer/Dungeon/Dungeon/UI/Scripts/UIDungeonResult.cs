using TMPro;
using UnityEngine;

namespace WitchMendokusai
{
	public class UIDungeonResult : UIPanel
	{
		// Stat
		[SerializeField] private TextMeshProUGUI playTimeText;
		[SerializeField] private TextMeshProUGUI levelText;
		[SerializeField] private TextMeshProUGUI killCountText;
		[SerializeField] private TextMeshProUGUI bossKillCountText;
		[SerializeField] private TextMeshProUGUI nyangText;

		// Info
		private UICardDataGrid cardGrid;
		private UIItemDataGrid itemGrid;

		public override bool IsFullscreen => true;

		public void Continue()
		{
			DungeonManager.Instance.Continue();
		}

		protected override void OnInit()
		{
			cardGrid = GetComponentInChildren<UICardDataGrid>(true);
			cardGrid.Init();

			itemGrid = GetComponentInChildren<UIItemDataGrid>(true);
			itemGrid.SetData(SOManager.Instance.DungeonItemBuffer.Data);
			itemGrid.Init();
		}

		public override void UpdateUI()
		{
			DungeonRecord record = DungeonManager.Instance.Result;

			playTimeText.text = record.PlayTime.ToString(@"mm\:ss");
			levelText.text = record.Level.ToString();
			killCountText.text = record.KillCount.ToString();
			bossKillCountText.text = record.BossKillCount.ToString();
			nyangText.text = record.Nyang.ToString();

			cardGrid.UpdateUI();
			itemGrid.UpdateUI();
			UpdateItemAmounts();
		}
		private void UpdateItemAmounts()
		{
			ItemDataBuffer buffer = SOManager.Instance.DungeonItemBuffer;
			for (int i = 0; i < itemGrid.Slots.Count && i < buffer.Data.Count; i++)
			{
				ItemData itemData = buffer.Data[i];
				int count = buffer.itemCountDic.TryGetValue(itemData.ID, out int c) ? c : 1;
				itemGrid.Slots[i].SetSlot(itemData, count);
			}
		}
	}
}