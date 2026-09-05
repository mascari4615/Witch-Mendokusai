using System;

namespace WitchMendokusai
{
	/// <summary>
	/// 대결 축 v0 의 「몸」 수치 (TASK-WM-411). 카드가 흔드는 <see cref="VersusFighterStats"/> 와 달리
	/// 이쪽은 판 전체에 한 벌만 있는 값 — 대시가 얼마나 멀리 가나, 탄이 몇 초 사나 같은 것.
	/// 하드코딩 금지 룰(WitchMendokusai/CLAUDE.md § 수치 노출)에 따라 감독의 인스펙터로 나온다.
	/// </summary>
	[Serializable]
	public struct VersusTuning
	{
		public float FighterRadius;         // 기본 몸 반지름(카드가 곱한다).
		public float DashDistance;          // 대시 1회 이동 거리.
		public float DashCooldownSeconds;   // 대시 연타 방지.
		public float ProjectileRadius;      // 기본 탄 반지름(카드가 곱한다).
		public float ProjectileLifeSeconds; // 이 시간이 지나면 탄이 사라진다 — 튕김 카드가 쌓여도 화면이 안 메워진다.
		public float IntermissionSeconds;   // 라운드 사이 숨 고르기.
		public float ProjectileSpreadDegrees; // 여러 발 나갈 때 부채꼴 각도.

		public static VersusTuning Default()
		{
			return new VersusTuning
			{
				FighterRadius = 0.5f,
				DashDistance = 4.5f,
				DashCooldownSeconds = 0.6f,
				ProjectileRadius = 0.25f,
				ProjectileLifeSeconds = 6f,
				IntermissionSeconds = 1.2f,
				ProjectileSpreadDegrees = 8f,
			};
		}
	}
}
