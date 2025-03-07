using UnityEngine;

namespace WitchMendokusai
{
	public class UnitStatCalculator : Singleton<UnitStatCalculator>
	{
		private const double DIFFICULTY_HP_BONUS_FACTOR = .7f;

		public void CalcStat(Unit unitData, UnitStat unitStat)
		{
			if (unitData == null)
			{
				return;
			}

			if (unitData is Doll)
			{
				UpdateStatDoll(unitData, unitStat);
			}
			else if (unitData is Monster)
			{
				UpdateStatMonster(unitData, unitStat);
			}
		}

		private void UpdateStatDoll(Unit unitData, UnitStat unitStat)
		{
			// TODO: 인형 스탯 계산
		}

		private void UpdateStatMonster(Unit unitData, UnitStat unitStat)
		{
			// TODO: 몬스터 스탯 계산

			// 던전 Context 등을 계산
			// 예를 들어, 던전 난이도에 따른 스탯 계산
			DungeonContext context = DungeonManager.Instance.Context;
			{
				double persentage = (double)unitStat[UnitStatType.HP_CUR] / (double)unitStat[UnitStatType.HP_MAX];
				unitStat[UnitStatType.HP_MAX] = (int)((double)unitStat[UnitStatType.HP_MAX_STAT] * (1 + DIFFICULTY_HP_BONUS_FACTOR * (double)context.CurDifficulty));
				unitStat[UnitStatType.HP_CUR] = (int)((double)unitStat[UnitStatType.HP_MAX] * persentage);
				// SetHp((int)(unitStat[UnitStatType.HP_MAX] * persentage));

				// 기반 스탯을 기반으로 런타임 스탯 계산
				// 예를 들어, HP 스탯과 HP % 스탯을 기반으로 HP_MAX, HP_CUR 계산

				foreach (DungeonConstraint constraint in context.Constraints)
				{
					foreach (DungeonConstraintEffectInfo effect in constraint.Effects)
					{
						if (effect.Affiliation != unitData.Affiliation)
							continue;

						// Debug.Log($"{unit.Name} {effect.StatType} {effect.Value}");
						unitStat[effect.StatType] += effect.Value;
					}
				}
				// SetHp(UnitStat[UnitStatType.HP_MAX]);
			}
		}
	}
}