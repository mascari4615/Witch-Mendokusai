using System;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	/// <summary>
	/// 대결 축(TASK-WM-411) 한 명의 싸움 수치. 카드가 바꾸는 것은 *전부 여기* — 엔진/뷰가 아니라 이 구조체다.
	/// 순수 데이터(DomainSDK). 즉사제라 체력 수치가 없다: 맞으면 그 라운드가 끝난다.
	/// </summary>
	[Serializable]
	public struct VersusFighterStats
	{
		public float MoveSpeed;         // m/s.
		public float FireInterval;      // 발사 간격(초). 작을수록 연사.
		public float ProjectileSpeed;   // m/s.
		public float ProjectileScale;   // 탄 반지름 배수 — 맞추기 쉬워지는 대신 자기도 커진 탄에 맞는다(양날).
		public int ProjectileCount;     // 한 번 쏠 때 나가는 탄 수(부채꼴).
		public int BounceCount;         // 벽 반사 횟수. 0 = 벽에 닿으면 소멸.
		public float ProjectileGravity; // 0 = 직선. > 0 = 포물선(곡사).
		public int DashCharges;         // 라운드당 대시 횟수.
		public int ShieldCharges;       // 라운드당 무효화 횟수 — 즉사제의 유일한 「한 번 봐줌」.
		public float BodyScale;         // 내 몸 크기 배수. 커지면 맞기 쉽다 = 카드의 대가를 몸으로 치른다.

		/// <summary>
		/// 카드 0장 상태. 이 값이 v0 밸런스의 원점 — 여기서 카드가 얼마나 크게 흔드는지로 재미를 잰다.
		/// </summary>
		public static VersusFighterStats Default()
		{
			return new VersusFighterStats
			{
				MoveSpeed = 7f,
				FireInterval = 0.45f,
				ProjectileSpeed = 18f,
				ProjectileScale = 1f,
				ProjectileCount = 1,
				BounceCount = 0,
				ProjectileGravity = 0f,
				DashCharges = 0,
				ShieldCharges = 0,
				BodyScale = 1f,
			};
		}

		/// <summary>
		/// 카드가 곱셈으로 쌓이면 값이 0 또는 무한으로 새어 나가 라운드가 성립하지 않는다(연사 0초 = 프레임당 발사).
		/// 어떤 조합을 뽑아도 「판이 돈다」를 보장하는 하한/상한. 카드 효과 적용 뒤 항상 통과시킨다.
		/// </summary>
		public VersusFighterStats Clamped()
		{
			VersusFighterStats clamped = this;
			clamped.MoveSpeed = Mathf.Clamp(clamped.MoveSpeed, 2f, 30f);
			clamped.FireInterval = Mathf.Clamp(clamped.FireInterval, 0.06f, 3f);
			clamped.ProjectileSpeed = Mathf.Clamp(clamped.ProjectileSpeed, 4f, 80f);
			clamped.ProjectileScale = Mathf.Clamp(clamped.ProjectileScale, 0.3f, 6f);
			clamped.ProjectileCount = Mathf.Clamp(clamped.ProjectileCount, 1, 24);
			clamped.BounceCount = Mathf.Clamp(clamped.BounceCount, 0, 12);
			clamped.ProjectileGravity = Mathf.Clamp(clamped.ProjectileGravity, 0f, 60f);
			clamped.DashCharges = Mathf.Clamp(clamped.DashCharges, 0, 8);
			clamped.ShieldCharges = Mathf.Clamp(clamped.ShieldCharges, 0, 8);
			clamped.BodyScale = Mathf.Clamp(clamped.BodyScale, 0.5f, 3f);
			return clamped;
		}
	}
}
