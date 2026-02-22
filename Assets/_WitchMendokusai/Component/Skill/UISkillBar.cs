using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public class UISkillBar : UIBase
	{
		private List<UISkillSlot> curSkillSlots;

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

			var skills = Player.Instance.Object.SkillHandler.SkillDic.Values;
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
			TimeManager.Instance.RegisterCallback(UpdateUI);
		}

		protected override void OnClose()
		{
			TimeManager.Instance.RemoveCallback(UpdateUI);
		}
	}
}