using TMPro;
using UnityEngine;
using VContainer;

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

		private DungeonManager dungeonManager;
		private SOManager soManager;

		[Inject]
		public void Construct(DungeonManager dungeonManager, SOManager soManager)
		{
			this.dungeonManager = dungeonManager;
			this.soManager = soManager;
		}

		public override bool IsFullscreen => true;

		public void Continue()
		{
			dungeonManager.Continue();
		}

		protected override void OnInit()
		{
			cardGrid = GetComponentInChildren<UICardDataGrid>(true);
			cardGrid.Init();

			itemGrid = GetComponentInChildren<UIItemDataGrid>(true);
			itemGrid.SetData(soManager.DungeonItemBuffer.Data);
			itemGrid.Init();
		}

		public override void UpdateUI()
		{
			DungeonRecord record = dungeonManager.Result;

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
			ItemDataBuffer buffer = soManager.DungeonItemBuffer;
			for (int i = 0; i < itemGrid.Slots.Count && i < buffer.Data.Count; i++)
			{
				ItemData itemData = buffer.Data[i];
				int count = buffer.itemCountDic.TryGetValue(itemData.ID, out int c) ? c : 1;
				itemGrid.Slots[i].SetSlot(itemData, count);
			}
		}
	}
}
