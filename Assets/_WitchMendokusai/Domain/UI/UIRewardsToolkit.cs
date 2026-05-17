using System.Collections.Generic;
using UnityEngine.UIElements;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	/// <summary>
	/// 보상 표시 뷰 — uGUI UIRewards(68L) 의 Toolkit 병렬 신설 (TASK-WM-113 S3-C).
	/// 구 UIRewards 는 UIDungeonEntrance/UIQuestToolTip 가 여전히 실사용 → 빅뱅 X,
	/// 본 뷰는 *신규 병렬* (first-use = 던전엔트런스 S3-E `UIDungeonEntranceToolkit`).
	/// 구 UIRewards 의 RewardType switch 로직 1:1 보존. CanvasGroup → style.display,
	/// prefab 고정 N UISlot → lazy ToolkitSlot 풀(GC 회피, uGUI hide-excess 시맨틱 등가).
	/// 구 UIRewards deletion = 형제 사용처 전부 이행 후 최후 E.
	/// </summary>
	public class UIRewardsToolkit : VisualElement
	{
		public const string USS_CLASS = "wm-rewards";

		private readonly List<ToolkitSlot> slots = new();

		public UIRewardsToolkit()
		{
			AddToClassList(USS_CLASS);
			style.display = DisplayStyle.None;
		}

		public void UpdateUI(List<RewardInfo> infos) =>
			UpdateUI(infos.ConvertAll(x => x.ToInfoData()));

		public void UpdateUI(List<RewardInfoData> data)
		{
			bool hasData = data != null && data.Count > 0;
			style.display = hasData ? DisplayStyle.Flex : DisplayStyle.None;

			if (hasData == false)
				return;

			EnsureSlotCount(data.Count);

			for (int i = 0; i < slots.Count; i++)
			{
				if (i < data.Count)
				{
					slots[i].style.display = DisplayStyle.Flex;

					switch (data[i].Type)
					{
						case RewardType.Item:
							ItemData itemData = GetItemData(data[i].DataSOID);
							slots[i].SetSlot(itemData);
							break;
						case RewardType.Gold:
							slots[i].SetSlot(GetGameStatData(GameStatType.NYANG), data[i].Amount);
							break;
						case RewardType.Exp:
							slots[i].SetSlot(GetGameStatData(GameStatType.VILLAGE_QUEST_EXP), data[i].Amount);
							break;
					}
				}
				else
				{
					slots[i].style.display = DisplayStyle.None;
				}
			}
		}

		private void EnsureSlotCount(int count)
		{
			while (slots.Count < count)
			{
				ToolkitSlot slot = new ToolkitSlot();
				slot.SetSlotIndex(slots.Count);
				slots.Add(slot);
				Add(slot);
			}
		}
	}
}
