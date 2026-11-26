using System;

namespace WitchMendokusai
{
	// 알리사 표창 폭풍 (닌자 최종) 의 정본. 주위를 도는 표창의 수, 각도, 반지름, 흩어짐
	// 같은 적 재타격은 표창마다 따로 (기존 RepeatingDamagingObject). 폭풍 하나가 같이 세면 4자루나 12자루나 초당 두 번이라 표창을 늘리는 카드가 빈 카드가 됨
	public static class ShurikenStormRules
	{
		private const float FULL_TURN = 360f;

		// 도는 표창 수. 기본 4 + 소용돌이 뿌리 2 + 레벨 성장 (그림자 마무리). 상한은 화면이 표창으로 덮이지 않게
		public static int Count(int baseCount, int bonusCount, int growthCount, int maxCount)
			=> Math.Max(0, Math.Min(maxCount, baseCount + Math.Max(0, bonusCount) + Math.Max(0, growthCount)));

		// 분신의 폭풍은 본체보다 적은 수로. 분신 셋이 본체 수를 그대로 따라 하면 물체가 네 배
		public static int CloneCount(int ownerCount, int maxPerClone) => Math.Max(0, Math.Min(ownerCount, maxPerClone));

		// 표창 index 의 각도 (도, 0 이상 360 미만). 0 은 월드 앞 (+z), 위에서 볼 때 시계 방향 (Quaternion.Euler y 와 같음)
		public static float Angle(int index, int count, float spinDegrees)
		{
			if (count <= 0)
				return 0f;
			float angle = (spinDegrees + FULL_TURN * index / count) % FULL_TURN;
			if (angle < 0f)
				angle += FULL_TURN;
			return angle >= FULL_TURN ? 0f : angle;
		}

		// 흩어졌다 돌아오는 데 걸리는 시간. scatterDistance 는 주인에게서 잰 반지름. 도는 반지름 이하면 0
		public static float ScatterSeconds(float stormRadius, float scatterDistance, float outSeconds, float returnSpeed, float hoverSeconds = 0f)
		{
			if (scatterDistance <= stormRadius || returnSpeed <= 0f)
				return 0f;
			return Math.Max(0f, outSeconds) + Math.Max(0f, hoverSeconds) + (scatterDistance - stormRadius) / returnSpeed;
		}

		// 흩어지기 시작하고 elapsed 초 뒤의 반지름. 나갈 때는 끝에서 느려지고, 끝에서 hoverSeconds 멈췄다, 돌아올 때는 고른 속도. 끝나면 도는 반지름
		public static float ScatterRadius(float elapsed, float stormRadius, float scatterDistance, float outSeconds, float returnSpeed, float hoverSeconds = 0f)
		{
			if (elapsed <= 0f || elapsed >= ScatterSeconds(stormRadius, scatterDistance, outSeconds, returnSpeed, hoverSeconds))
				return stormRadius;
			float outbound = Math.Max(0f, outSeconds);
			if (elapsed < outbound)
			{
				float left = 1f - elapsed / outbound;
				return scatterDistance - (scatterDistance - stormRadius) * left * left;
			}
			float returning = Math.Max(0f, elapsed - outbound - Math.Max(0f, hoverSeconds));
			return Math.Max(stormRadius, scatterDistance - returning * returnSpeed);
		}

		// 가장 멀리 나가 멈춘 동안 (달아오름)
		public static bool IsHovering(float elapsed, float stormRadius, float scatterDistance, float outSeconds, float hoverSeconds)
			=> scatterDistance > stormRadius && elapsed >= Math.Max(0f, outSeconds) && elapsed < Math.Max(0f, outSeconds) + Math.Max(0f, hoverSeconds);

		// 나가는 중이거나 멈춘 동안
		public static bool IsOutgoing(float elapsed, float stormRadius, float scatterDistance, float outSeconds, float hoverSeconds)
			=> scatterDistance > stormRadius && elapsed > 0f && elapsed < Math.Max(0f, outSeconds) + Math.Max(0f, hoverSeconds);
	}
}
