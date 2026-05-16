using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// skillIndex는 unitObject의 skill 슬롯 번호(0, 1, 2, ...)를 의미함.
	// skillIndex는 skillID가 아님에 주의
	public class SkillHandler
	{
		/// <summary> Key : SkillIndex </summary>
		public readonly Dictionary<int, Skill> skillDic = new();
		public IReadOnlyDictionary<int, Skill> SkillDic => skillDic;

		private readonly UnitObject unitObject;
		// TASK-WM-107 Slice 4 — SkillContext 가 운반할 app 서비스 (UnitObject base-deps 채널 경유 push).
		private readonly PlayerProvider playerProvider;
		private readonly ObjectPoolManager objectPoolManager;

		public SkillHandler(UnitObject unitObject, PlayerProvider playerProvider, ObjectPoolManager objectPoolManager)
		{
			this.unitObject = unitObject;
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;

			for (int i = 0; i < unitObject.UnitData.DefaultSkills.Length; i++)
				SetSkill(i, unitObject.UnitData.DefaultSkills[i]);
			unitObject.UnitStat.AddListener(UnitStatType.COOLTIME_BONUS, UpdateCooltimeBonus);
		}

		public void SetSkill(int skillIndex, SkillData skill)
		{
			skillDic[skillIndex] = new Skill(skill);
			skillDic[skillIndex].UpdateCooltime(coolTimeBonus: unitObject.UnitStat[UnitStatType.COOLTIME_BONUS]);
		}

		public bool UseSkill(int skillIndex)
		{
			if (skillDic.TryGetValue(skillIndex, out Skill skill))
			{
				if (skill.IsReady)
				{
					skill.Use(new SkillContext(unitObject, playerProvider, objectPoolManager));
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

				bool isAutoUse = skill.Data.PlayMode switch
				{
					SkillPlayMode.Auto => true,
					SkillPlayMode.AutoWhenDungeon when DungeonManager.Instance.IsDungeon => skill.IsReady,
					_ => false,
				};

				if (isAutoUse && skill.IsReady)
					skill.Use(new SkillContext(unitObject, playerProvider, objectPoolManager));
			}
		}
	}
}