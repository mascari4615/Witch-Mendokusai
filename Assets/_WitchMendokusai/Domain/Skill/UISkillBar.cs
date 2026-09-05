using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class UISkillBar : UIBase
	{
		private List<UISkillSlot> curSkillSlots;

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

		public override void UpdateUI()
		{
			int skillCount = 0;

			IEnumerable<Skill> skills = playerProvider.CurrentObject.SkillHandler.SkillDic.Values;
			foreach (Skill skill in skills)
			{
				curSkillSlots[skillCount].SetSlot(skill.Data);
				curSkillSlots[skillCount].UpdateCooltime(skill);

				skillCount++;
			}

			for (int i = 0; i < curSkillSlots.Count; i++)
				curSkillSlots[i].gameObject.SetActive(i < skillCount);
		}

		protected override void OnOpen()
		{
			timeManager.RegisterCallback(UpdateUI);
		}

		protected override void OnClose()
		{
			timeManager.RemoveCallback(UpdateUI);
		}
	}
}
