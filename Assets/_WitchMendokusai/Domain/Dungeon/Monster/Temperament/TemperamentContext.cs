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

		// 아래는 감각 (MonsterTemperament.Senses) 을 켠 성격에만 채움. 모두 SenseRadius 안의 것
		// 같은 성격 자산을 쓰는 다른 몬스터 수와 그들의 무게중심 (Kin)
		public int KinCount;
		public Vector3 KinCenter;
		// 가장 가까운 다른 성격 (또는 성격 없는) 몬스터 (Ally)
		public bool HasAlly;
		public Vector3 Ally;
		// 가장 가까운 바닥 전리품 (Loot)
		public bool HasLoot;
		public Vector3 Loot;
		// 근처에서 몬스터가 쓰러진 뒤 흐른 초와 그 자리 (Fallen). 없었으면 무한대
		public float SinceFallen;
		public Vector3 FallenAt;

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
