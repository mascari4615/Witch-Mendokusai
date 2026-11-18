using System.Collections.Generic;

namespace WitchMendokusai
{
	// 판 상태를 스킬창 (RunEquipmentHud) 에 옮김. 장비 레벨은 고른 카드 수나 장비가 바뀔 때만 다시 셈
	public class RunEquipmentHudBinder
	{
		private readonly RunEquipmentHud hud = new();
		private readonly List<Skill> skills = new();
		private readonly List<int> levels = new();
		private int levelCardCount = -1;
		private int levelEquipmentCount = -1;

		public RunEquipmentHud Hud => hud;

		public void Refresh(DungeonManager dungeonManager, PlayerProvider playerProvider, CardManager cardManager)
		{
			if (hud.parent == null)
				UIRoot.Instance.HudLayer.Add(hud);

			RunLoadout loadout = dungeonManager.RunLoadout;
			List<EquipmentData> equipments = loadout.Equipments();
			// 랜덤 모드는 SO 칸 수 + 고유 장비. 장착 모드는 인형 장비 칸 그대로
			int slotCount = loadout.UsesRunSlots ? dungeonManager.RunEquipmentSlotCount + 1 : equipments.Count;

			int acquiredCount = loadout.Acquired.Count;
			if (levelCardCount != cardManager.SelectedCardCount || levelEquipmentCount != acquiredCount || levels.Count != equipments.Count)
			{
				levelCardCount = cardManager.SelectedCardCount;
				levelEquipmentCount = acquiredCount;
				levels.Clear();
				foreach (EquipmentData equipment in equipments)
					levels.Add(equipment != null ? cardManager.EquipmentLevel(equipment) : 0);
			}
			hud.RefreshEquipments(equipments, levels, slotCount);

			skills.Clear();
			SkillHandler skillHandler = playerProvider.CurrentObject.SkillHandler;
			foreach (KeyValuePair<int, Skill> pair in skillHandler.SkillDic)
			{
				if (skillHandler.IsSlotEnabled(pair.Key))
					skills.Add(pair.Value);
			}
			hud.RefreshSkills(skills);
		}

		public void SetVisible(bool isVisible) => hud.SetVisible(isVisible);

		public void Hide()
		{
			hud.RemoveFromHierarchy();
			hud.ResetShown();
			levelCardCount = -1;
			levelEquipmentCount = -1;
		}
	}
}
