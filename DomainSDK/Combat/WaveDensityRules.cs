using System;

namespace WitchMendokusai
{
	// 생성 종류, 상한 적용 범위 구분
	public enum SpawnClass
	{
		// 웨이브 주기 생성과 최소 적 수 보충. 소프트 상한에서 멈춤
		Periodic,
		// 무리 사건. 절대 상한에서 멈춤
		Event,
		// 보스와 정예. 상한 무시
		Priority,
	}

	// 웨이브 밀도 판정의 정본. 엔진 무관
	public static class WaveDensityRules
	{
		// 최소 적 수 아래로 부족한 마릿수. 최소 0 이하면 기능 꺼짐
		public static int Deficit(int current, int minOnScreen)
		{
			if (minOnScreen <= 0)
				return 0;
			return Math.Max(0, minOnScreen - Math.Max(0, current));
		}

		// 상한 값 0 이하는 해당 상한 없음
		public static int AllowedCount(SpawnClass kind, int current, int requested, int softCap, int hardCap)
		{
			if (requested <= 0)
				return 0;
			if (kind == SpawnClass.Priority)
				return requested;

			int limit = kind == SpawnClass.Periodic ? EffectivePeriodicLimit(softCap, hardCap) : hardCap;
			if (limit <= 0)
				return requested;
			return Math.Max(0, Math.Min(requested, limit - Math.Max(0, current)));
		}

		// 이번 생성 기회에서 만들 마릿수. 최소 적 수 아래면 부족분, 아니면 1
		// burstMax 0 이하면 부족분 전부
		public static int PeriodicCount(int current, int minOnScreen, int burstMax, int softCap, int hardCap)
		{
			int requested = Math.Max(1, Deficit(current, minOnScreen));
			if (burstMax > 0)
				requested = Math.Min(requested, burstMax);
			return AllowedCount(SpawnClass.Periodic, current, requested, softCap, hardCap);
		}

		private static int EffectivePeriodicLimit(int softCap, int hardCap)
		{
			if (softCap <= 0)
				return hardCap;
			if (hardCap <= 0)
				return softCap;
			return Math.Min(softCap, hardCap);
		}
	}
}
