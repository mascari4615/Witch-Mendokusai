using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary> 슬라임 왕 패턴의 계산만. 오브젝트와 시간에 안 묶여서 EditMode 시험 대상 </summary>
	public static class SlimeKingPatternMath
	{
		private const float CENTER_EPSILON = 0.05f;

		/// <summary> 순서 목록에서 다음 칸. 목록이 비면 0 </summary>
		public static int NextIndex(int current, int count) => count <= 0 ? 0 : (current + 1) % count;

		/// <summary> 순서 목록. 비면 투사체와 돌진 </summary>
		public static SlimeKingPattern PatternAt(IReadOnlyList<SlimeKingPattern> order, int index)
		{
			if (order == null || order.Count == 0)
				return index % 2 == 0 ? SlimeKingPattern.Projectile : SlimeKingPattern.Dash;
			return order[index % order.Count];
		}

		public static int OrderCount(IReadOnlyList<SlimeKingPattern> order) => order == null || order.Count == 0 ? 2 : order.Count;

		/// <summary> 끌어당김 속도. 플레이어 걷기 속도 x 비율 (비율 1 미만이면 반대로 걸어 벗어남) </summary>
		public static float PullSpeed(float playerWalkSpeed, float ratio) => Mathf.Max(0f, playerWalkSpeed) * Mathf.Clamp(ratio, 0f, 0.99f);

		/// <summary> target 을 center 쪽으로 끄는 수평 속도. 범위 밖이거나 거의 중심이면 0 </summary>
		public static Vector3 PullVelocity(Vector3 center, Vector3 target, float radius, float speed)
		{
			Vector3 toCenter = center - target;
			toCenter.y = 0f;
			float distance = toCenter.magnitude;
			if (distance > radius || distance < CENTER_EPSILON)
				return Vector3.zero;
			return toCenter / distance * speed;
		}

		/// <summary> 두 자리의 수평 거리가 radius 이하 </summary>
		public static bool IsWithin(Vector3 from, Vector3 to, float radius)
		{
			Vector3 delta = to - from;
			delta.y = 0f;
			return delta.sqrMagnitude <= radius * radius;
		}

		/// <summary>
		/// 착지 자리. around 둘레에 count 개를 고르게 나눈 각도 + 흔들림, 거리는 min~max
		/// angleJitter01, distance01 은 0~1 난수 (시험에서 고정값). jitterRatio 는 칸 폭에 대한 흔들림 비율
		/// </summary>
		public static Vector3 LandingPoint(Vector3 around, int index, int count, float startAngle, float angleJitter01, float distance01, float minDistance, float maxDistance, float jitterRatio)
		{
			int safeCount = Mathf.Max(1, count);
			float slice = 360f / safeCount;
			float angle = startAngle + slice * index + (angleJitter01 - 0.5f) * slice * Mathf.Clamp01(jitterRatio);
			float distance = Mathf.Lerp(Mathf.Min(minDistance, maxDistance), Mathf.Max(minDistance, maxDistance), Mathf.Clamp01(distance01));
			Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
			return around + direction * distance;
		}

		/// <summary> 포물선 자리. progress 0~1, 가운데에서 height 만큼 높음 </summary>
		public static Vector3 ArcPoint(Vector3 from, Vector3 to, float height, float progress)
		{
			float clamped = Mathf.Clamp01(progress);
			Vector3 point = Vector3.Lerp(from, to, clamped);
			point.y += height * 4f * clamped * (1f - clamped);
			return point;
		}
	}
}
