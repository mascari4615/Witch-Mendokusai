using System.Collections.Generic;

namespace WitchMendokusai
{
	// 스킬창 (RunEquipmentHud) 에 상태를 옮김. 던전은 이동 스킬과 장비 (Refresh), 마을은 이동 스킬만 (RefreshSkills)
	// 장비 레벨은 고른 카드 수나 장비가 바뀔 때만 다시 셈
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
			Attach();

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
			RefreshSkills(playerProvider);
		}

		// 켜진 스킬 칸만. 마을 스킬 바 (UISkillBar) 도 이것만 부름
		public void RefreshSkills(PlayerProvider playerProvider)
		{
			Attach();
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

		private void Attach()
		{
			if (hud.parent == null)
				UIRoot.Instance.HudLayer.Add(hud);
		}

		public void Hide()
		{
			hud.RemoveFromHierarchy();
			hud.ResetShown();
			levelCardCount = -1;
			levelEquipmentCount = -1;
		}
	}
}
