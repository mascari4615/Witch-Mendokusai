using System;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-173 Phase 0 — <see cref="Freshness"/> 거리 기반 선형 감쇠 모델 회귀 잠금.
	///
	/// 결정성·경계(거리 0/큰 거리)·음수 입력 boundary 검증. 순수 함수 → EditMode 우선.
	/// </summary>
	public sealed class FreshnessTest
	{
		[Test]
		public void DecayByDistance_ZeroDistance_FullFresh()
		{
			Assert.That(Freshness.DecayByDistance(0f, 0.1f), Is.EqualTo(1f).Within(0.0001f), "거리 0 = 전혀 흐르지 않음 = 신선도 그대로");
		}

		[Test]
		public void DecayByDistance_ZeroRate_NoDecay()
		{
			// 노화율 0 = 어떤 거리도 무손 (예: 텔레포트 마법진 톤의 차후 첫 사용 표현).
			Assert.That(Freshness.DecayByDistance(100f, 0f), Is.EqualTo(1f).Within(0.0001f));
		}

		[Test]
		public void DecayByDistance_LinearMidpoint()
		{
			// 1 - 5 × 0.1 = 0.5.
			Assert.That(Freshness.DecayByDistance(5f, 0.1f), Is.EqualTo(0.5f).Within(0.0001f));
		}

		[Test]
		public void DecayByDistance_BeyondFullDecay_ClampsToZero()
		{
			// 1 - 20 × 0.1 = -1 → 0 (음수 신선도 없음 — 완전히 흩어짐).
			Assert.That(Freshness.DecayByDistance(20f, 0.1f), Is.Zero);
		}

		[Test]
		public void DecayByDistance_Monotonic_LongerDecaysMore()
		{
			// 길이 ↑ → 신선도 ↓ (단조 감소). Phase 0 핵심 직관.
			float shortFresh = Freshness.DecayByDistance(2f, 0.1f);
			float longFresh = Freshness.DecayByDistance(8f, 0.1f);

			Assert.That(shortFresh, Is.GreaterThan(longFresh), "짧은 거리가 더 신선");
		}

		[Test]
		public void DecayByDistance_DeterministicAcrossCalls()
		{
			// 6 동기 「퀄리티」 — 같은 입력 = 같은 출력 (1000회).
			float first = Freshness.DecayByDistance(3.7f, 0.13f);
			for (int i = 0; i < 1000; i++)
			{
				Assert.That(Freshness.DecayByDistance(3.7f, 0.13f), Is.EqualTo(first), "결정성 — 부동소수 같은 입력 = 같은 비트");
			}
		}

		[Test]
		public void DecayByDistance_NegativeDistance_Throws()
		{
			// boundary — 음수 거리는 외부 입력 위반(FastFail).
			Assert.Throws<ArgumentOutOfRangeException>(() => Freshness.DecayByDistance(-1f, 0.1f));
		}

		[Test]
		public void DecayByDistance_NegativeRate_Throws()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => Freshness.DecayByDistance(1f, -0.1f));
		}
	}
}
