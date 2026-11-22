using UnityEngine;

namespace WitchMendokusai
{
	// 버스 음량을 목표 dB 로 일정 시간에 걸쳐 옮기는 계산. 일시정지 동안 전투 소리 누르기 (사운드 조사 S06)
	public static class BusDuck
	{
		// 이번 프레임의 dB. fadeSeconds 동안 0 dB 와 duckDb 사이를 다 건너는 빠르기. fadeSeconds 가 0 이면 바로 목표
		public static float Step(float currentDb, float targetDb, float duckDb, float fadeSeconds, float deltaTime)
		{
			if (fadeSeconds <= 0f)
				return targetDb;
			float dbPerSecond = Mathf.Abs(duckDb) / fadeSeconds;
			return Mathf.MoveTowards(currentDb, targetDb, dbPerSecond * deltaTime);
		}

		public static float ToLinear(float decibels) => Mathf.Pow(10f, decibels / 20f);
	}
}
