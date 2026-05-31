using System;

namespace WitchMendokusai
{
	// 마력 신선도 감쇠 모델 — 순수 함수(상태 0). Phase 0 = 거리 기반 선형 감쇠만; 시간 기반은
	// Phase 2(노화 틱 + TimeManager 연동)에서 별 함수로 first-use 추가 (현재 추가 = 데드 인터페이스).
	//
	// 모델: remaining = max(0, 1 - distance × decayRate). 단순하지만 결정적이고 「길수록 줄어든다」
	// 직관과 정합. exp 모델(e^(-distance×rate))은 부드러우나 0 으로 점근만 해 「완전히 흩어짐」
	// 표현이 약함 — Phase 0 슬라이스 체감(짧은 직선 vs 긴 우회) 에는 선형이 더 또렷.
	//
	// 수치(decayRate) 는 호출자(추후 SO/Variable<float>)가 주입 — CLAUDE.md 「수치 노출 / 런타임
	// tweak」 룰: 모델은 산술만, 계수는 외부 정본.
	public static class Freshness
	{
		// 거리 distance 만큼 흐른 뒤의 신선도 비율 [0, 1]. 0 = 완전히 흩어짐, 1 = 그대로.
		// 결정적: 같은 (distance, decayRate) = 같은 출력.
		public static float DecayByDistance(float distance, float decayRate)
		{
			if (distance < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(distance), distance, "거리는 음수일 수 없다");
			}

			if (decayRate < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(decayRate), decayRate, "감쇠율은 음수일 수 없다");
			}

			float remaining = 1f - distance * decayRate;
			if (remaining < 0f)
			{
				return 0f;
			}

			return remaining;
		}
	}
}
