using UnityEngine;

namespace WitchMendokusai
{
	// 한 번 터질 때 조각마다 수평 각도 칸 하나씩 배분. 무작위 구면 방향은 적은 수에서 한쪽 쏠림
	public static class ShardSpread
	{
		// burstTurn: 이번 터짐 전체 회전 (0~1 바퀴), jitter: 칸 안 위치 (0~1), lift: 위쪽 성분 (0~1)
		public static Vector3 Direction(int index, int count, float burstTurn, float jitter, float lift)
		{
			int safeCount = Mathf.Max(1, count);
			float turn = burstTurn + (index + Mathf.Clamp01(jitter)) / safeCount;
			float radians = turn * Mathf.PI * 2f;
			Vector3 direction = new(Mathf.Cos(radians), Mathf.Clamp01(lift), Mathf.Sin(radians));
			return direction.normalized;
		}
	}
}
