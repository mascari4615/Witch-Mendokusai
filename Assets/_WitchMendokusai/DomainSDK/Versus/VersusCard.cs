namespace WitchMendokusai
{
	/// <summary>
	/// v0 카드 8종. 카드 = 「스탯 한 줄이 아니라 *싸우는 방식*이 바뀌는 것」이 기준 —
	/// ROUNDS 가 재미있는 이유는 수치가 올라서가 아니라 화면에서 벌어지는 일이 달라져서다.
	/// </summary>
	public enum VersusCardKind
	{
		Swift = 0,      // 발이 빨라진다.
		RapidFire = 1,  // 연사.
		Bounce = 2,     // 탄이 벽에 튄다 — 사각이 사라진다.
		Huge = 3,       // 탄이 커진다(대신 내 몸도 커진다).
		Split = 4,      // 한 번에 두 발, 부채꼴.
		Dash = 5,       // 순간 이동 회피 1회.
		Shield = 6,     // 즉사 1회 무효.
		Heavy = 7,      // 곡사 — 느리게 떨어지는 대신 탄이 빨라진다.
	}

	/// <summary>
	/// 카드 → 스탯 적용 규칙(순수 함수). 뷰/엔진이 아니라 여기 하나만 보면 빌드가 왜 그렇게 됐는지 전부 설명된다.
	/// </summary>
	public static class VersusCards
	{
		/// <summary> v0 에 존재하는 카드 전부. 드래프트·시험이 이 배열 하나만 본다(중복 정의 0). </summary>
		public static readonly VersusCardKind[] All =
		{
			VersusCardKind.Swift,
			VersusCardKind.RapidFire,
			VersusCardKind.Bounce,
			VersusCardKind.Huge,
			VersusCardKind.Split,
			VersusCardKind.Dash,
			VersusCardKind.Shield,
			VersusCardKind.Heavy,
		};

		/// <summary>
		/// 카드 1장을 스탯에 얹는다. 같은 카드를 또 뽑으면 또 쌓인다(중복 허용) — 막판 난장판이 이 축의 재미다.
		/// 항상 <see cref="VersusFighterStats.Clamped"/> 를 통과시켜 판이 깨지는 조합을 원천 차단한다.
		/// </summary>
		public static VersusFighterStats Apply(VersusFighterStats stats, VersusCardKind card)
		{
			switch (card)
			{
				case VersusCardKind.Swift:
					stats.MoveSpeed *= 1.25f;
					break;

				case VersusCardKind.RapidFire:
					stats.FireInterval *= 0.7f;
					break;

				case VersusCardKind.Bounce:
					stats.BounceCount += 2;
					break;

				case VersusCardKind.Huge:
					// 탄만 커지면 그냥 상위호환이라 고민이 안 된다. 몸이 같이 커져야 「뽑을까 말까」가 생긴다.
					stats.ProjectileScale *= 1.6f;
					stats.BodyScale *= 1.15f;
					break;

				case VersusCardKind.Split:
					stats.ProjectileCount += 1;
					// 탄이 늘면 화면이 금방 메워지므로 한 발의 지속을 줄여 균형을 맞춘다.
					stats.ProjectileSpeed *= 1.1f;
					break;

				case VersusCardKind.Dash:
					stats.DashCharges += 1;
					break;

				case VersusCardKind.Shield:
					stats.ShieldCharges += 1;
					break;

				case VersusCardKind.Heavy:
					stats.ProjectileGravity += 12f;
					stats.ProjectileSpeed *= 1.35f;
					break;
			}

			return stats.Clamped();
		}

		/// <summary> 뽑기 화면에 그대로 뜨는 한 줄. 수치가 아니라 *무슨 일이 벌어지는가* 로 쓴다. </summary>
		public static string Describe(VersusCardKind card)
		{
			switch (card)
			{
				case VersusCardKind.Swift: return "빨라진다";
				case VersusCardKind.RapidFire: return "더 자주 쏜다";
				case VersusCardKind.Bounce: return "벽에 튄다";
				case VersusCardKind.Huge: return "탄이 커진다 — 몸도 커진다";
				case VersusCardKind.Split: return "한 번에 한 발 더";
				case VersusCardKind.Dash: return "대시 1회";
				case VersusCardKind.Shield: return "한 대 막는다";
				case VersusCardKind.Heavy: return "탄이 빨라지고 아래로 휜다";
				default: return string.Empty;
			}
		}
	}
}
