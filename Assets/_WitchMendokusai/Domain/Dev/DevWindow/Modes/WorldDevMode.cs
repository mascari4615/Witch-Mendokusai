using System.Collections.Generic;
using System.Text;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>세계의 이름 변경과 상자 조작 시험 탭. 옛 WorldHandlesPanel, 게임 화면에서 이동 (2026-11-04).</summary>
	public sealed class WorldDevMode : IDevMode
	{
		private const long REFRESH_INTERVAL_MS = 500;

		public string Id => "world";
		public string DisplayName => "세계";
		public VisualElement Root { get; } = new VisualElement();

		private readonly TextField nameField;
		private readonly Label chestLabel;
		private IVisualElementScheduledItem refreshSchedule;

		public WorldDevMode()
		{
			nameField = new TextField("이름") { maxLength = 16 };
			Root.Add(nameField);

			Root.Add(new Button(Rename) { text = "이름 바꾸기" });

			chestLabel = new Label("상자: 가까이 가서 열기");
			chestLabel.style.whiteSpace = WhiteSpace.Normal;
			chestLabel.style.marginTop = 6f;
			Root.Add(chestLabel);

			Root.Add(new Button(OpenNearbyChest) { text = "가까운 상자 열기" });
			Root.Add(new Button(PutOne) { text = "하나 넣기" });
			Root.Add(new Button(TakeOne) { text = "하나 꺼내기" });
		}

		public void OnActivate()
		{
			Refresh();
			if (refreshSchedule == null)
				refreshSchedule = Root.schedule.Execute(Refresh).Every(REFRESH_INTERVAL_MS);
			else
				refreshSchedule.Resume();
		}

		public void OnDeactivate() => refreshSchedule?.Pause();

		private void Refresh()
		{
			DrawChest();

			// 입력 중인 이름은 덮어쓰지 않음
			if (nameField.focusController?.focusedElement != nameField && WorldNameBridge.IsActive)
				nameField.SetValueWithoutNotify(WorldNameBridge.Channel.MyName);
		}

		private void Rename()
		{
			if (WorldNameBridge.IsActive)
				WorldNameBridge.Channel.Rename(nameField.value);
		}

		private void OpenNearbyChest()
		{
			if (WorldChestBridge.IsActive == false)
				return;

			if (WorldChestBridge.Channel.TryOpenNearby() == false)
				chestLabel.text = "가까운 상자 없음";
		}

		private void PutOne()
		{
			if (WorldChestBridge.IsActive && TryFirstCarried(out int itemId))
				WorldChestBridge.Channel.Put(itemId, 1);
		}

		private void TakeOne()
		{
			if (WorldChestBridge.IsActive == false)
				return;

			IReadOnlyList<ChestSlot> inside = WorldChestBridge.Channel.Contents;
			if (inside.Count > 0)
				WorldChestBridge.Channel.Take(inside[0].ItemId, 1);
		}

		private void DrawChest()
		{
			if (WorldChestBridge.IsActive == false)
			{
				chestLabel.text = "상자: 세계에 입장 전";
				return;
			}

			IReadOnlyList<ChestSlot> inside = WorldChestBridge.Channel.Contents;
			if (inside.Count == 0)
			{
				chestLabel.text = "상자: 비었거나 아직 안 열림";
				return;
			}

			StringBuilder line = new StringBuilder("상자:");
			for (int i = 0; i < inside.Count; i++)
			{
				ItemData item = SOHelper.Get<ItemData>(inside[i].ItemId);
				line.Append(' ')
					.Append(item == null ? "#" + inside[i].ItemId : item.Name)
					.Append('×')
					.Append(inside[i].Amount);
			}

			chestLabel.text = line.ToString();
		}

		// 가방에서 처음 찾은 아이템
		private static bool TryFirstCarried(out int itemId)
		{
			itemId = 0;
			if (SOManagerBridge.HasInstance == false)
				return false;

			if (SOManagerBridge.DataSOs.TryGetValue(typeof(ItemData), out Dictionary<int, DataSO> items) == false)
				return false;

			foreach (KeyValuePair<int, DataSO> entry in items)
			{
				if (SOManagerBridge.ItemInventory.CountByID(entry.Key) <= 0)
					continue;

				itemId = entry.Key;
				return true;
			}

			return false;
		}
	}
}
