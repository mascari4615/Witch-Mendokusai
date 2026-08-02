using UnityEngine;

namespace WitchMendokusai
{
	/// <summary> 마수가 무엇에 적응했나 — 내가 많이 쓴 수단에만 붙는다. </summary>
	public readonly struct TowerDefenseAdaptationState
	{
		/// <summary> 둔화 저항(0~1). 1 이면 둔화가 통째로 안 걸린다(상한은 규칙이 막는다). </summary>
		public readonly float SlowResist;

		/// <summary> 광역 저항 — 광역 피해가 이 비율만큼 깎인다. </summary>
		public readonly float SplashResist;

		/// <summary> 관통 저항 — 꿰뚫린 두 번째 이후의 피해가 깎인다. </summary>
		public readonly float PierceResist;

		public TowerDefenseAdaptationState(float slowResist, float splashResist, float pierceResist)
		{
			SlowResist = slowResist;
			SplashResist = splashResist;
			PierceResist = pierceResist;
		}

		public bool HasAny => SlowResist > 0f || SplashResist > 0f || PierceResist > 0f;
	}

	/// <summary>
	/// 적응(TASK-WM-194) — 「한 번 찾은 정답」이 영원히 통하지 않게 한다.
	///
	/// ★ 무작위가 아니라 *내 행동의 결과*다: 둔화로만 이겨왔으면 둔화에 익숙해진 놈이 오고,
	///   광역으로만 쓸어왔으면 흩어져 오는 놈이 온다. 그래서 「부당하다」가 아니라 「내가 그렇게 만들었다」가 된다.
	/// ★ 상한이 반드시 있다: 저항이 1 에 닿으면 그 전략은 *못 쓰는 것*이 되고, 그건 적응이 아니라 봉인이다.
	///   최대 절반(0.5)까지만 — 여전히 통하되 예전만큼은 아니게.
	/// ★ 화면에 보여야 한다: 예고에 「둔화에 익숙함」이 뜨지 않으면 플레이어는 자기 포탑이 고장 났다고 여긴다.
	///
	/// 순수 정적 — 씬·RNG 0.
	/// </summary>
	public static class TowerDefenseAdaptation
	{
		/// <summary> 저항 상한 — 이 위로는 절대 안 올라간다(전략 봉인 방지). </summary>
		public const float MAX_RESIST = 0.5f;

		/// <summary>
		/// 지금까지 내가 쓴 수단의 누적치로부터 다음 파도의 적응을 계산한다.
		/// 비율로 본다 — 총량이 아니라 *편중*이 적응을 만든다(오래 하면 다 저항이 붙는 건 벌칙이다).
		/// </summary>
		public static TowerDefenseAdaptationState From(int slowUses, int splashHits, int pierceHits, float sensitivity)
		{
			int total = slowUses + splashHits + pierceHits;
			if (total <= 0 || sensitivity <= 0f)
				return new TowerDefenseAdaptationState(0f, 0f, 0f);

			return new TowerDefenseAdaptationState(
				Resist(slowUses, total, sensitivity),
				Resist(splashHits, total, sensitivity),
				Resist(pierceHits, total, sensitivity));
		}

		// 편중도(그 수단이 차지한 비율)가 1/3(균등)을 넘는 만큼만 저항이 붙는다.
		// 골고루 쓰면 아무 저항도 안 생긴다 — 「한 수단에만 기대지 마라」가 규칙의 전부다.
		private static float Resist(int uses, int total, float sensitivity)
		{
			float share = (float)uses / total;
			float excess = share - (1f / 3f);
			if (excess <= 0f)
				return 0f;

			return Mathf.Clamp(excess * 1.5f * sensitivity, 0f, MAX_RESIST);
		}

		/// <summary> 화면에 띄울 한 줄 — 무엇에 익숙해졌는지 말해줘야 대응할 수 있다. </summary>
		public static string Describe(TowerDefenseAdaptationState state)
		{
			if (state.HasAny == false)
				return string.Empty;

			float strongest = Mathf.Max(state.SlowResist, Mathf.Max(state.SplashResist, state.PierceResist));
			if (Mathf.Approximately(strongest, state.SlowResist))
				return "둔화에 익숙함";
			if (Mathf.Approximately(strongest, state.SplashResist))
				return "광역에 익숙함";
			return "관통에 익숙함";
		}
	}
}
