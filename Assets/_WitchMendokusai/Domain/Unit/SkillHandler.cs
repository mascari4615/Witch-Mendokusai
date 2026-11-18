using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// skillIndex는 unitObject의 skill 슬롯 번호(0, 1, 2, ...)를 의미함.
	// skillIndex는 skillID가 아님에 주의
	public class SkillHandler : IDisposable
	{
		/// <summary> Key : SkillIndex </summary>
		public readonly Dictionary<int, Skill> skillDic = new();
		public IReadOnlyDictionary<int, Skill> SkillDic => skillDic;

		private readonly UnitObject unitObject;
		// TASK-WM-107 Slice 4 — SkillContext 가 운반할 app 서비스 (UnitObject base-deps 채널 경유 push).
		private readonly PlayerProvider playerProvider;
		private readonly ObjectPoolManager objectPoolManager;

		// WM-165 — 아레나 출전 유닛은 전술 코어가 시전 권위자 → 자동시전(Auto/AutoWhenDungeon) 억제.
		// 기본 true = 레거시(던전 Auto) 무변경. 아레나 셋업이 false 로 설정.
		public bool AutoCastEnabled { get; set; } = true;

		// 생존 던전 안에서 쓰는 스킬 칸 수. 앞 칸부터. 조작 캐릭터는 Shift 이동 스킬만 남기고 클릭과 우클릭 스킬을 끔 (사용자 2026-11-18)
		// 기본은 제한 없음. 값은 PlayerObject 의 SerializeField
		public int DungeonSlotLimit { get; set; } = int.MaxValue;

		private static bool IsInDungeon => DungeonManager.Instance != null && DungeonManager.Instance.IsDungeon;

		public bool IsSlotEnabled(int skillIndex) => IsInDungeon == false || skillIndex < DungeonSlotLimit;

		public SkillHandler(UnitObject unitObject, PlayerProvider playerProvider, ObjectPoolManager objectPoolManager)
		{
			this.unitObject = unitObject;
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;

			for (int i = 0; i < unitObject.UnitData.DefaultSkills.Length; i++)
				SetSkill(i, unitObject.UnitData.DefaultSkills[i]);
			unitObject.UnitStat.AddListener(UnitStatType.COOLTIME_BONUS, UpdateCooltimeBonus);
		}

		public void Dispose()
		{
			unitObject.UnitStat.RemoveListener(UnitStatType.COOLTIME_BONUS, UpdateCooltimeBonus);
		}

		public void SetSkill(int skillIndex, SkillData skill)
		{
			skillDic[skillIndex] = new Skill(skill);
			skillDic[skillIndex].UpdateCooltime(coolTimeBonus: unitObject.UnitStat[UnitStatType.COOLTIME_BONUS]);
		}

		public bool UseSkill(int skillIndex)
		{
			return UseSkill(skillIndex, null);
		}

		// WM-165 — 아레나 전술 코어가 타겟 지정 시전. target=null 이면 레거시(던전/플레이어) 동작.
		public bool UseSkill(int skillIndex, UnitObject target)
		{
			if (IsSlotEnabled(skillIndex) == false)
				return false;

			if (skillDic.TryGetValue(skillIndex, out Skill skill))
			{
				if (skill.IsReady)
				{
					skill.Use(new SkillContext(unitObject, playerProvider, objectPoolManager, target: target));
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
			if (unitObject.IsActionSuspended)
				return;

			foreach (KeyValuePair<int, Skill> pair in SkillDic)
			{
				Skill skill = pair.Value;
				skill.Tick();

				if (AutoCastEnabled == false || IsSlotEnabled(pair.Key) == false)
					continue;

				bool isAutoUse = skill.Data.PlayMode switch
				{
					SkillPlayMode.Auto => true,
					SkillPlayMode.AutoWhenDungeon when IsInDungeon => skill.IsReady,
					_ => false,
				};

				if (isAutoUse && skill.IsReady)
					skill.Use(new SkillContext(unitObject, playerProvider, objectPoolManager));
			}
		}
	}
}
