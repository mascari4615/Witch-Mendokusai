using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 위협 단계 표의 한 줄. N번째 줄이 단계 N+1, 단계 0은 줄 없음
	// 체력, 피해, 속도, 물량, 정예는 아래 단계와 누적. 보상 배율만 그 단계 값
	[Serializable]
	public class DungeonThreatLevel
	{
		public float EnemyHealth = 1f;
		public float EnemyDamage = 1f;
		public int EliteExtraPerGap = 0;
		public float EnemySpeed = 1f;
		public float WaveRate = 1f;
		public float RewardMultiplier = 1f;
	}

	public readonly struct ExtraEliteSlot
	{
		public readonly float Time;
		public readonly int SourceIndex;

		public ExtraEliteSlot(float time, int sourceIndex)
		{
			Time = time;
			SourceIndex = sourceIndex;
		}
	}

	// 누적을 마친 한 판의 위협 효과
	public readonly struct DungeonThreatEffect
	{
		private const double ROUNDING_TOLERANCE = 0.0001d;

		public readonly int Level;
		public readonly float EnemyHealth;
		public readonly float EnemyDamage;
		public readonly int EliteExtraPerGap;
		public readonly float EnemySpeed;
		public readonly float WaveRate;
		public readonly float RewardMultiplier;

		public DungeonThreatEffect(int level, float enemyHealth, float enemyDamage, int eliteExtraPerGap, float enemySpeed, float waveRate, float rewardMultiplier)
		{
			Level = level;
			EnemyHealth = enemyHealth;
			EnemyDamage = enemyDamage;
			EliteExtraPerGap = eliteExtraPerGap;
			EnemySpeed = enemySpeed;
			WaveRate = waveRate;
			RewardMultiplier = rewardMultiplier;
		}

		public static DungeonThreatEffect Identity => new DungeonThreatEffect(0, 1f, 1f, 0, 1f, 1f, 1f);

		public int ScaleEnemyHealth(int health) => ScaleUp(health, EnemyHealth);

		// 경험치 등급용 원래 체력. 정예 배율이 곱해진 값에도 사용
		public int UnscaleEnemyHealth(int health) => (int)Math.Floor(health / (double)EnemyHealth + ROUNDING_TOLERANCE);

		public int ScaleEnemyDamage(int damage) => ScaleUp(damage, EnemyDamage);

		public int ScaleEnemySpeed(int speed) => (int)Math.Round(speed * (double)EnemySpeed, MidpointRounding.AwayFromZero);

		public float ScaleSpawnDelay(float delay) => delay / WaveRate;

		public int ScaleSwarmCount(int count) => ScaleUp(count, WaveRate);

		// 정수 스탯은 1 미만 배율 차이도 체감되도록 올림
		private static int ScaleUp(int value, float multiplier) => (int)Math.Ceiling(value * (double)multiplier - ROUNDING_TOLERANCE);
	}

	// 위협 단계 누적, 해금, 보상 규칙의 정본
	public static class DungeonThreatRules
	{
		public static int MaxLevel(IReadOnlyList<DungeonThreatLevel> levels) => levels == null ? 0 : levels.Count;

		public static DungeonThreatEffect Resolve(IReadOnlyList<DungeonThreatLevel> levels, int level)
		{
			int resolvedLevel = Clamp(level, 0, MaxLevel(levels));
			if (resolvedLevel == 0)
				return DungeonThreatEffect.Identity;

			float enemyHealth = 1f;
			float enemyDamage = 1f;
			int eliteExtraPerGap = 0;
			float enemySpeed = 1f;
			float waveRate = 1f;
			for (int i = 0; i < resolvedLevel; i++)
			{
				DungeonThreatLevel row = levels[i];
				enemyHealth *= Positive(row.EnemyHealth);
				enemyDamage *= Positive(row.EnemyDamage);
				eliteExtraPerGap += Math.Max(0, row.EliteExtraPerGap);
				enemySpeed *= Positive(row.EnemySpeed);
				waveRate *= Positive(row.WaveRate);
			}
			float rewardMultiplier = Positive(levels[resolvedLevel - 1].RewardMultiplier);
			return new DungeonThreatEffect(resolvedLevel, enemyHealth, enemyDamage, eliteExtraPerGap, enemySpeed, waveRate, rewardMultiplier);
		}

		// clearedCount: 아래부터 승리한 단계 수. 저장값 그대로라 범위 밖 가능
		public static int HighestUnlocked(int clearedCount, int maxLevel) => Clamp(clearedCount, 0, Math.Max(0, maxLevel));

		public static int StepSelection(int current, int direction, int clearedCount, int maxLevel)
			=> Clamp(current + direction, 0, HighestUnlocked(clearedCount, maxLevel));

		public static int AfterClear(int clearedCount, int clearedLevel) => Math.Max(Math.Max(0, clearedCount), clearedLevel + 1);

		// 이번 판 획득 냥에 더할 몫
		public static int BonusNyang(int earned, float rewardMultiplier)
		{
			if (earned <= 0)
				return 0;
			return Math.Max(0, (int)Math.Round(earned * (double)rewardMultiplier, MidpointRounding.AwayFromZero) - earned);
		}

		// eliteTimes: 오름차순. 이웃한 정예 사이를 균등 분할, 첫 정예 전과 마지막 정예 뒤는 제외
		public static List<ExtraEliteSlot> ExtraEliteSlots(IReadOnlyList<float> eliteTimes, int extraPerGap)
		{
			List<ExtraEliteSlot> slots = new List<ExtraEliteSlot>();
			if (extraPerGap <= 0)
				return slots;

			for (int i = 0; i + 1 < eliteTimes.Count; i++)
			{
				float gap = (eliteTimes[i + 1] - eliteTimes[i]) / (extraPerGap + 1);
				for (int j = 1; j <= extraPerGap; j++)
					slots.Add(new ExtraEliteSlot(eliteTimes[i] + gap * j, i));
			}
			return slots;
		}

		private static float Positive(float multiplier) => multiplier > 0f ? multiplier : 1f;

		private static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);
	}
}
