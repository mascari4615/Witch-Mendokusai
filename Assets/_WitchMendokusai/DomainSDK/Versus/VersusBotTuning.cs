using System;

namespace WitchMendokusai
{
	/// <summary> 연습 상대(봇)의 성격 수치 (TASK-WM-411). 전부 인스펙터 노브 — 「너무 세다/약하다」를 그 자리에서 돌린다. </summary>
	[Serializable]
	public struct VersusBotTuning
	{
		public float PreferredDistance;  // 유지하려는 거리.
		public float DistanceTolerance;  // 이 안이면 다가가지도 물러나지도 않는다.
		public float StrafeFlipSeconds;  // 옆돌기 방향을 뒤집는 주기 — 읽히지 않게 한다.
		public float FireAimDot;         // 이동 방향이 상대 쪽에 얼마나 맞아야 쏘나(1 = 정확히 정면).
		public float MaxFireDistance;    // 이보다 멀면 안 쏜다.
		public float WallMargin;         // 벽에서 이만큼 떨어지면 안쪽으로 민다.
		public float DodgeRadius;        // 탄이 이 안에 들어오면 대시로 피한다.

		public static VersusBotTuning Default()
		{
			return new VersusBotTuning
			{
				PreferredDistance = 7f,
				DistanceTolerance = 1.5f,
				StrafeFlipSeconds = 1.4f,
				FireAimDot = 0.55f,
				MaxFireDistance = 16f,
				WallMargin = 1.5f,
				DodgeRadius = 2.2f,
			};
		}
	}
}
