using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class SkillHandler
	{
		/// <summary>
		/// Key : SkillIndex
		/// </summary>
		public Dictionary<int, Skill> SkillDic { get; private set; } = new();
		private readonly UnitObject unitObject;

		public SkillHandler(UnitObject unitObject)
		{
			this.unitObject = unitObject;

			for (int i = 0; i < unitObject.UnitData.DefaultSkills.Length; i++)
				SetSkill(i, unitObject.UnitData.DefaultSkills[i]);
			unitObject.UnitStat.AddListener(UnitStatType.COOLTIME_BONUS, UpdateCooltimeBonus);
		}

		public void SetSkill(int skillIndex, SkillData skill)
		{
			SkillDic[skillIndex] = new Skill(skill);
			SkillDic[skillIndex].UpdateCooltime(coolTimeBonus: unitObject.UnitStat[UnitStatType.COOLTIME_BONUS]);
		}

		public bool UseSkill(int skillButtonIndex)
		{
			if (SkillDic.TryGetValue(skillButtonIndex, out Skill skill))
			{
				if (skill.IsReady)
				{
					skill.Use(unitObject);
					return true;
				}
			}

			return false;
		}

		public void UpdateCooltimeBonus()
		{
			foreach (Skill skill in SkillDic.Values)
				skill.UpdateCooltime(coolTimeBonus: unitObject.UnitStat[UnitStatType.COOLTIME_BONUS]);
		}

		public void Tick()
		{
			foreach (Skill skill in SkillDic.Values)
			{
				skill.Tick();

				if (skill.IsReady && skill.Data.AutoUse)
					skill.Use(unitObject);
			}
		}
	}
}