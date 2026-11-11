namespace WitchMendokusai
{
	// 하늘만 다른 시각으로 그리는 요청. 월드 시계는 계속 흐름
	// 걸기: 생존 판 진행, 풀기: 마을 귀환
	public static class SkyTimeOverride
	{
		public static bool IsActive { get; private set; }
		public static float Hour { get; private set; }

		public static void Set(float hour)
		{
			IsActive = true;
			Hour = hour;
		}

		public static void Clear() => IsActive = false;
	}
}
