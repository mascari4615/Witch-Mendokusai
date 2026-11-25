using System;

namespace WitchMendokusai
{
	// 터질 때 나오는 폭발 하나. 첫 폭발 뒤 DelaySeconds 에
	public readonly struct CrossBlast
	{
		public readonly float DelaySeconds;
		public readonly int DamagePercent;
		public readonly float RadiusMultiplier;

		public CrossBlast(float delaySeconds, int damagePercent, float radiusMultiplier)
		{
			DelaySeconds = delaySeconds;
			DamagePercent = damagePercent;
			RadiusMultiplier = radiusMultiplier;
		}
	}

	// 알리사 대형 십자 수리검 (닌자 최종) 의 정본. 던지는 간격, 폭발 배율, 두 번째 폭발, n 번째 셈
	public static class CrossShurikenRules
	{
		// 던지는 간격. 뇌격은 카드 한 장이 간격을 길게 바꿈 (득실). 쿨타임 감소는 다른 무기와 같은 식
		public static float ThrowInterval(float baseSeconds, float heavySeconds, bool heavy, int sharedSpeedStacks, float reductionPerStack, float minRatio, int cooldownPercent)
			=> AttackCadence.Interval(heavy ? heavySeconds : baseSeconds, sharedSpeedStacks, reductionPerStack, minRatio, cooldownPercent);

		// 폭발 피해 배율. 뇌격과 반복 카드를 곱함
		public static int BlastPercent(bool heavy, int heavyPercent, int repeatPercent)
		{
			float percent = heavy ? heavyPercent : 100f;
			return (int)Math.Round(percent * (100 + Math.Max(0, repeatPercent)) / 100f);
		}

		// 폭발 index (0 이 첫 폭발). 두 번째는 작게
		public static CrossBlast Blast(int index, int blastPercent, float gapSeconds, int echoPercent, float echoRadius)
		{
			if (index == 0)
				return new CrossBlast(0f, blastPercent, 1f);
			return new CrossBlast(gapSeconds * index, (int)Math.Round(blastPercent * echoPercent / 100f), echoRadius);
		}

		public static int BlastCount(bool echo) => echo ? 2 : 1;

		// 전기장이 때리는 횟수. 놓이는 순간은 폭발이 때렸으므로 세지 않음
		public static int FieldTicks(float seconds, float intervalSeconds) => intervalSeconds <= 0f ? 0 : (int)Math.Floor(Math.Max(0f, seconds) / intervalSeconds + 0.0001f);

		// n 번째마다 (투척 표식 4, 단도 회전 5). count 는 이번 것까지 센 수
		public static bool IsNth(int count, int everyNth) => everyNth > 0 && count > 0 && count % everyNth == 0;
	}
}
