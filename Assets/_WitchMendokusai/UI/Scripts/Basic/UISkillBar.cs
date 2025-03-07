using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class UISkillBar : MonoBehaviour, IUI
	{
		private UISkillSlot[] curSkillSlots;

		private void Start()
		{
			Init();
			TimeManager.Instance.RegisterCallback(UpdateUI);
		}

		public void Init()
		{
			curSkillSlots = GetComponentsInChildren<UISkillSlot>(true);
		}

		public void UpdateUI()
		{
			int skillCount = 0;

			var skills = Player.Instance.Object.SkillHandler.SkillDic.Values;
			foreach (Skill skill in skills)
			{
				curSkillSlots[skillCount].SetSlot(skill.Data);
				curSkillSlots[skillCount].UpdateCooltime(skill);

				skillCount++;
			}

			for (int i = 0; i < curSkillSlots.Length; i++)
				curSkillSlots[i].gameObject.SetActive(i < skillCount);
		}
	}
}