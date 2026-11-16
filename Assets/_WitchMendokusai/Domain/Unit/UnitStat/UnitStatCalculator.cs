using UnityEngine;

namespace WitchMendokusai
{
	public class UnitStatCalculator : MonoBehaviour
	{
		public static UnitStatCalculator Instance { get; private set; }

		public static bool TryGetExistingInstance(out UnitStatCalculator mgr)
		{
			mgr = Instance;
			return mgr != null;
		}


		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
		}

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
			else if (unitData is ResourceNode)
			{
				UpdateStatResourceNode(unitData, unitStat);
			}
		}

		private void UpdateStatDoll(Unit unitData, UnitStat unitStat)
		{
			// TODO: 인형 스탯 계산
		}

		private void UpdateStatMonster(Unit unitData, UnitStat unitStat)
		{
			// TODO: 몬스터 스탯 계산

			// 던전 Context 등을 계산. DungeonManager 가 spawn 안 된 상태 (Lobby / Loading / World
			// 안 DungeonManager.Awake 전 race) = 일반 monster default — Context 무관 스탯 그대로.
			if (DungeonManager.TryGetExistingInstance(out DungeonManager dungeonManager) == false)
				return;
			DungeonContext context = dungeonManager.Context;
			if (context != null)
			{
				double percentage = (double)unitStat[UnitStatType.HP_CUR] / unitStat[UnitStatType.HP_MAX];
				// 생성 시점 경과 분에 비례해 체력 성장 (SurvivalRunRules). 예전 3분 계단 0.7배는 위기가 없어 교체 (2026-11-16)
				// 판 제한 시간에서 멈춤. 살아 있는 보스가 시간이 지날수록 계속 단단해지지 않게
				double limitMinutes = context.InitialDungeonTime.TotalMinutes;
				double growthMinutes = limitMinutes > 0 ? System.Math.Min(context.ElapsedTime.TotalMinutes, limitMinutes) : context.ElapsedTime.TotalMinutes;
				double hpMultiplier = dungeonManager.RunRules.EnemyHpMultiplier(growthMinutes);
				unitStat[UnitStatType.HP_MAX] = (int)(unitStat[UnitStatType.HP_MAX_STAT] * hpMultiplier);
				unitStat[UnitStatType.HP_CUR] = (int)(unitStat[UnitStatType.HP_MAX] * percentage);
				// SetHp((int)(unitStat[UnitStatType.HP_MAX] * percentage));

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

		private void UpdateStatResourceNode(Unit unitData, UnitStat unitStat)
		{
			// ResourceNode는 InitStatInfos 기반 스탯을 그대로 사용
		}
	}
}
