using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class UISkillBar : UIBase
	{
		private List<UISkillSlot> curSkillSlots;
		// VisualElement 는 MonoBehaviour 생성 때 못 만들어서 처음 쓸 때 만듦
		private RunEquipmentHudBinder villageHudBinder;
		private RunEquipmentHudBinder VillageHud => villageHudBinder ??= new RunEquipmentHudBinder();

		private PlayerProvider playerProvider;
		private TimeManager timeManager;

		[Inject]
		public void Construct(PlayerProvider playerProvider, TimeManager timeManager)
		{
			this.playerProvider = playerProvider;
			this.timeManager = timeManager;
		}

		private void Start()
		{
			Init();
			SetActive(true);
		}

		public override void Init()
		{
			curSkillSlots = GetComponentsInChildren<UISkillSlot>(true).ToList();

			foreach (UISkillSlot skillSlot in curSkillSlots)
				skillSlot.Init();
		}

		// 마을 스킬 바. 던전 스킬창과 같은 UI Toolkit 부품 (RunEquipmentHud) 의 이동 스킬 줄만 씀 (사용자 2026-11-18 "기존 마름모 디자인 처럼")
		// 던전에서는 UIDungeonRuntime 이 띄우는 스킬창이 대신하므로 숨김
		// 예전 uGUI 칸 ([Block] SkillSlot) 은 씬에 남아 있지만 늘 꺼 둠. 씬에서 지우는 일은 남음
		public override void UpdateUI()
		{
			foreach (UISkillSlot skillSlot in curSkillSlots)
			{
				if (skillSlot.gameObject.activeSelf)
					skillSlot.gameObject.SetActive(false);
			}

			if (DungeonManagerBridge.IsDungeon)
			{
				villageHudBinder?.Hud.RemoveFromHierarchy();
				return;
			}

			VillageHud.RefreshSkills(playerProvider);
		}

		protected override void OnOpen()
		{
			timeManager.RegisterCallback(UpdateUI);
		}

		protected override void OnClose()
		{
			timeManager.RemoveCallback(UpdateUI);
			villageHudBinder?.Hud.RemoveFromHierarchy();
		}
	}
}
