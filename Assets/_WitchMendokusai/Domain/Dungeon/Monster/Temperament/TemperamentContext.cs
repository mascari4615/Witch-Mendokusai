using UnityEngine;

namespace WitchMendokusai
{
	// 성격 계산에 주는 한 순간의 사실. 높이 (y) 는 0 으로 맞춰 줌
	public struct TemperamentContext
	{
		// 이보다 가까우면 같은 자리로 봄 (m 제곱)
		private const float SAME_SPOT_SQUARED = 0.0001f;

		public Vector3 Self;
		public Vector3 Player;
		// 주인공이 실제로 움직이는 속도 (멈추면 0)
		public Vector3 PlayerVelocity;
		// 주인공이 바라보는 쪽 (단위 벡터)
		public Vector3 PlayerLook;
		// 게임 시간 (초)
		public float Now;
		// 남은 체력 비율 0~1
		public float HealthRatio;
		// 마지막으로 맞은 뒤 흐른 초. 안 맞았으면 무한대
		public float SinceHit;

		public Vector3 ToPlayer => Flat(Player - Self);
		public float Distance => ToPlayer.magnitude;

		public static Vector3 Flat(Vector3 vector)
		{
			vector.y = 0f;
			return vector;
		}

		public static Vector3 Toward(Vector3 from, Vector3 to)
		{
			Vector3 offset = Flat(to - from);
			return offset.sqrMagnitude < SAME_SPOT_SQUARED ? Vector3.zero : offset.normalized;
		}
	}
}
