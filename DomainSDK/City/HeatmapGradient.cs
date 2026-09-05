using System;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	// 값[0,1] → 색 — 진단 히트맵(SimCity 데이터뷰)의 순수 색 함수. 상태 0(new() 후 Evaluate 만).
	// 정렬된 스톱(threshold, color) 사이 선형보간. CityMetricField(값 산출) 와 분리 — 값의 의미 무관, 숫자만 받음.
	//
	// 기본 팔레트 = 고전 데이터뷰(차가운 파랑 → 청록 → 초록 → 노랑 → 뜨거운 빨강). 0=낮음, 1=높음.
	// 커스텀 스톱 주입(생성자) = 모딩/수치 노출 벡터(팔레트 외부화). 비전-중립 — 색 의미는 호출자가 부여.
	public sealed class HeatmapGradient
	{
		// 색 스톱 — 정규화 위치(threshold)와 그 위치의 색. 오름차순 정렬 전제(생성자 계약).
		public readonly struct Stop
		{
			public readonly float Threshold;
			public readonly Color Color;

			public Stop(float threshold, Color color)
			{
				Threshold = threshold;
				Color = color;
			}
		}

		// 고전 데이터뷰 팔레트 (차가움 → 뜨거움). 타입 범위 상수(튜닝 수치 아님 — 모딩은 커스텀 스톱으로).
		private static readonly Stop[] DEFAULT_STOPS =
		{
			new(0.00f, new Color(0.15f, 0.30f, 0.85f)), // 파랑 (낮음)
			new(0.25f, new Color(0.20f, 0.75f, 0.85f)), // 청록
			new(0.50f, new Color(0.30f, 0.80f, 0.30f)), // 초록
			new(0.75f, new Color(0.95f, 0.85f, 0.25f)), // 노랑
			new(1.00f, new Color(0.90f, 0.25f, 0.20f)), // 빨강 (높음)
		};

		private readonly Stop[] stops;

		public HeatmapGradient() : this(DEFAULT_STOPS) { }

		public HeatmapGradient(Stop[] stops)
		{
			// 보간 구간 성립에 최소 2 스톱(양 끝) 필요 — 미만 = 계약 위반(FastFail, 데이터뷰 빈 그래디언트 무의미).
			if (stops == null || stops.Length < 2)
				throw new ArgumentException("HeatmapGradient 는 스톱 2개 이상 필요(보간 구간 성립).", nameof(stops));

			this.stops = stops;
		}

		// 값 t[0,1] → 색. 범위 밖은 clamp(양 끝 색). 값을 감싸는 인접 스톱 쌍을 선형보간.
		public Color Evaluate(float t)
		{
			float clamped = Mathf.Clamp01(t);

			if (clamped <= stops[0].Threshold)
				return stops[0].Color;
			if (clamped >= stops[stops.Length - 1].Threshold)
				return stops[stops.Length - 1].Color;

			for (int i = 0; i < stops.Length - 1; i++)
			{
				Stop low = stops[i];
				Stop high = stops[i + 1];
				if (clamped >= low.Threshold && clamped <= high.Threshold)
				{
					float span = high.Threshold - low.Threshold;
					float localT = span <= Mathf.Epsilon ? 0f : (clamped - low.Threshold) / span;
					return Color.Lerp(low.Color, high.Color, localT);
				}
			}

			return stops[stops.Length - 1].Color; // 위 게이트로 도달 X — 컴파일러 만족용.
		}
	}
}
