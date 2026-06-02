using System;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-176 Phase 3 INC-6 — <see cref="HeatmapGradient"/> 값→색 매핑 회귀 잠금.
	///
	/// 양 끝 clamp · 스톱 정확 일치 · 구간 선형보간 · 커스텀 팔레트 · 스톱 부족 FastFail. 순수(new() + Assert).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class HeatmapGradientTest
	{
		// 흑→백 2스톱 — 보간 산수 검증용(선형 1:1).
		private static HeatmapGradient BlackToWhite()
		{
			HeatmapGradient.Stop[] stops =
			{
				new(0f, Color.black),
				new(1f, Color.white),
			};

			return new HeatmapGradient(stops);
		}

		[Test]
		public void Evaluate_BelowZero_ClampsToFirstStop()
		{
			HeatmapGradient gradient = BlackToWhite();
			Assert.That(gradient.Evaluate(-5f), Is.EqualTo(Color.black), "범위 밑 = 첫 스톱 색");
		}

		[Test]
		public void Evaluate_AboveOne_ClampsToLastStop()
		{
			HeatmapGradient gradient = BlackToWhite();
			Assert.That(gradient.Evaluate(5f), Is.EqualTo(Color.white), "범위 위 = 끝 스톱 색");
		}

		[Test]
		public void Evaluate_Midpoint_LinearInterpolates()
		{
			HeatmapGradient gradient = BlackToWhite();
			Color mid = gradient.Evaluate(0.5f);

			Assert.That(mid.r, Is.EqualTo(0.5f).Within(0.0001f), "흑→백 중간 = 회색 0.5");
			Assert.That(mid.g, Is.EqualTo(0.5f).Within(0.0001f));
			Assert.That(mid.b, Is.EqualTo(0.5f).Within(0.0001f));
		}

		[Test]
		public void Evaluate_QuarterPoint_LinearInterpolates()
		{
			HeatmapGradient gradient = BlackToWhite();
			Assert.That(gradient.Evaluate(0.25f).r, Is.EqualTo(0.25f).Within(0.0001f), "선형 1/4 지점");
		}

		[Test]
		public void Evaluate_ExactStopThreshold_ReturnsStopColor()
		{
			// 3스톱: 0=빨강 / 0.5=초록 / 1=파랑. 정확히 0.5 = 초록(보간 잔차 없이).
			HeatmapGradient.Stop[] stops =
			{
				new(0f, Color.red),
				new(0.5f, Color.green),
				new(1f, Color.blue),
			};
			HeatmapGradient gradient = new(stops);

			Color atHalf = gradient.Evaluate(0.5f);
			Assert.That(atHalf.g, Is.EqualTo(1f).Within(0.0001f), "스톱 정확 일치 = 그 스톱 색(초록)");
			Assert.That(atHalf.r, Is.EqualTo(0f).Within(0.0001f));
			Assert.That(atHalf.b, Is.EqualTo(0f).Within(0.0001f));
		}

		[Test]
		public void DefaultPalette_LowIsCold_HighIsHot()
		{
			// 기본 팔레트 = 차가움(파랑 우세) → 뜨거움(빨강 우세) 데이터뷰 계약.
			HeatmapGradient gradient = new();

			Color cold = gradient.Evaluate(0f);
			Color hot = gradient.Evaluate(1f);

			Assert.That(cold.b, Is.GreaterThan(cold.r), "낮은 값 = 파랑(차가움) 우세");
			Assert.That(hot.r, Is.GreaterThan(hot.b), "높은 값 = 빨강(뜨거움) 우세");
		}

		[Test]
		public void Constructor_FewerThanTwoStops_Throws()
		{
			HeatmapGradient.Stop[] one = { new(0f, Color.white) };
			Assert.That(() => new HeatmapGradient(one), Throws.TypeOf<ArgumentException>(), "스톱 1개 = 계약 위반");
			Assert.That(() => new HeatmapGradient(null), Throws.TypeOf<ArgumentException>(), "null = 계약 위반");
		}
	}
}
