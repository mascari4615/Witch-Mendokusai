using System;

namespace WitchMendokusai
{
	// 알리사 표창 폭풍 (닌자 최종) 의 정본. 주위를 도는 표창의 수, 각도, 반지름, 흩어짐
	// 같은 적 재타격 간격은 표창마다가 아니라 폭풍 하나가 같이 셈 (HitLedger 하나를 나눠 씀). 표창마다 세면 수가 늘 때 피해가 그대로 곱해짐
	public static class ShurikenStormRules
	{
		// 도는 표창 수. 기본 4 + 소용돌이 뿌리 2 + 레벨 성장 (그림자 마무리). 상한은 화면이 표창으로 덮이지 않게
		public static int Count(int baseCount, int bonusCount, int growthCount, int maxCount)
			=> Math.Max(0, Math.Min(maxCount, baseCount + Math.Max(0, bonusCount) + Math.Max(0, growthCount)));

		// 분신의 폭풍은 본체보다 적은 수로. 분신 셋이 본체 수를 그대로 따라 하면 물체가 네 배
		public static int CloneCount(int ownerCount, int maxPerClone) => Math.Max(0, Math.Min(ownerCount, maxPerClone));

		// 표창 index 의 각도 (도, 0 이상 360 미만). 고르게 나누고 같이 돎
		public static float Angle(int index, int count, float spinDegrees)
		{
			if (count <= 0)
				return 0f;
			float angle = (spinDegrees + 360f * index / count) % 360f;
			return angle < 0f ? angle + 360f : angle;
		}

		// 흩어졌다 돌아오는 데 걸리는 시간. 흩어질 거리가 도는 반지름 이하면 0
		public static float ScatterSeconds(float stormRadius, float scatterDistance, float outSeconds, float returnSpeed)
		{
			if (scatterDistance <= stormRadius || returnSpeed <= 0f)
				return 0f;
			return Math.Max(0f, outSeconds) + (scatterDistance - stormRadius) / returnSpeed;
		}

		// 흩어지기 시작하고 elapsed 초 뒤의 반지름. 나갈 때는 끝에서 느려지고, 돌아올 때는 고른 속도. 끝나면 도는 반지름
		public static float ScatterRadius(float elapsed, float stormRadius, float scatterDistance, float outSeconds, float returnSpeed)
		{
			if (elapsed <= 0f || elapsed >= ScatterSeconds(stormRadius, scatterDistance, outSeconds, returnSpeed))
				return stormRadius;
			if (elapsed < outSeconds)
			{
				float left = 1f - elapsed / outSeconds;
				return scatterDistance - (scatterDistance - stormRadius) * left * left;
			}
			return Math.Max(stormRadius, scatterDistance - (elapsed - outSeconds) * returnSpeed);
		}
	}
}
