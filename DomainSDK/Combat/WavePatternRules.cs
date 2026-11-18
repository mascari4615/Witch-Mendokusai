using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 웨이브 패턴 계산의 정본. 엔진 무관
	public static class WavePatternRules
	{
		// 초당 생성 수를 누적. 정수만큼 꺼내고 나머지는 다음 틱으로
		public static int TakeSpawns(ref float debt, float perSecond, float deltaSeconds)
		{
			if (perSecond <= 0f || deltaSeconds <= 0f)
				return 0;
			debt += perSecond * deltaSeconds;
			int count = (int)Math.Floor(debt);
			debt -= count;
			return count;
		}

		// 타원 둘레 index 번째 자리. 0번은 x 축 양의 방향, 반시계
		public static void EllipsePoint(int index, int count, float radiusX, float radiusZ, float startAngleRadians, out float x, out float z)
		{
			if (count <= 0)
				throw new ArgumentOutOfRangeException(nameof(count), count, "타원 자리 수는 1 이상");
			double angle = startAngleRadians + Math.PI * 2d * index / count;
			x = (float)(Math.Cos(angle) * radiusX);
			z = (float)(Math.Sin(angle) * radiusZ);
		}

		// 조이는 타원의 반지름 비율. 1 에서 시작해 minScale 에서 멈춤
		public static float ContractedScale(float ageSeconds, float shrinkPerSecond, float minScale)
		{
			float clampedMin = Math.Max(0f, Math.Min(1f, minScale));
			float scale = 1f - Math.Max(0f, shrinkPerSecond) * Math.Max(0f, ageSeconds);
			return Math.Max(clampedMin, scale);
		}

		// 벽 한 파의 빈 줄 시작 위치. 빈 줄 묶음이 겹치지 않게 가운데 근처
		public static int InitialGapStart(int laneCount, int gapCount)
		{
			ValidateLanes(laneCount, gapCount);
			return (laneCount - gapCount) / 2;
		}

		// 빈 줄 묶음을 -maxStep~+maxStep 줄 이동. randomStep 은 그 범위의 값을 돌려줌. 끝에 닿으면 안쪽으로 고정
		public static int StepGapStart(int gapStart, int laneCount, int gapCount, int maxStep, Func<int, int, int> randomStep)
		{
			ValidateLanes(laneCount, gapCount);
			int step = maxStep <= 0 ? 0 : randomStep(-maxStep, maxStep);
			int maxStart = laneCount - gapCount;
			return Math.Max(0, Math.Min(maxStart, gapStart + step));
		}

		public static bool IsGapLane(int lane, int gapStart, int gapCount) => lane >= gapStart && lane < gapStart + gapCount;

		// 줄 번호를 가운데 기준 오프셋 칸으로. 줄 수가 짝수면 반 칸 어긋남
		public static float LaneOffset(int lane, int laneCount, float laneSpacing) => (lane - (laneCount - 1) * 0.5f) * laneSpacing;

		// 생성 배율에 맞춘 경험치 요구 배율. follow 1 이면 같은 비율, 0 이면 1 고정
		public static float ExpRequirementMultiplier(float spawnRate, float follow)
		{
			if (spawnRate <= 0f)
				throw new ArgumentOutOfRangeException(nameof(spawnRate), spawnRate, "생성 배율은 0 초과");
			return Math.Max(0f, 1f + (spawnRate - 1f) * Math.Max(0f, follow));
		}

		// 여러 패턴의 생성률 배율을 곱으로 합침. 음수는 0
		public static float CombineRates(IEnumerable<float> rates)
		{
			float result = 1f;
			foreach (float rate in rates)
				result *= Math.Max(0f, rate);
			return result;
		}

		private static void ValidateLanes(int laneCount, int gapCount)
		{
			if (laneCount <= 0)
				throw new ArgumentOutOfRangeException(nameof(laneCount), laneCount, "줄 수는 1 이상");
			if (gapCount < 0 || gapCount > laneCount)
				throw new ArgumentOutOfRangeException(nameof(gapCount), gapCount, "빈 줄 수는 0 이상, 줄 수 이하");
		}
	}
}
