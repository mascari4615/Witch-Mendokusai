using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	/// <summary>
	/// 파도가 「어느 방향에서」 오는가 (TASK-WM-194).
	///
	/// ★ 왜 필요한가: 마수가 고정된 둥지 몇 곳에서만 나오면 그 몇 줄기가 사실상 *길*이 된다.
	///   한 번 막아두면 다음 판도 같은 자리를 막으면 되므로 매 파도의 판단이 똑같아진다.
	///   대신 **판 테두리의 한 토막**을 파도마다 다르게 정하고 그 줄 전체에서 밀려오게 하면
	///   출구가 고정되지 않는다 = 길 개념이 사라지고, 매번 「이번엔 어느 쪽을 막나」가 새로 생긴다.
	///
	/// ★ 왜 결정론인가: 방향을 *미리 알려줘야* 준비가 성립한다(데아빌의 재미 엔진).
	///   무작위면 예고가 거짓말이 되고 준비가 운에 무효화된다 — 구성/이벤트 계산과 같은 원칙.
	///   다음 파도 방향 = <see cref="AngleDegrees"/>(waveIndex + 1, seed) 한 줄로 얻는다.
	///
	/// 각도 규약: XZ 평면, 0° = +Z(북), 시계 방향 증가 → 90° = +X(동). 씬·RNG 0 (EditMode 전량 검증).
	/// </summary>
	public static class TowerDefenseWaveOrigin
	{
		// 황금각 기반 전진 — 연속한 파도가 최대한 멀리 떨어진 방향을 고르면서도 완전히 결정적이다.
		// (0.381966 = 2 - 황금비. 무작위 없이 「골고루 · 안 겹치게」를 얻는 표준 수열.)
		private const float GOLDEN_STEP = 0.381966f;
		private const float SEED_STEP = 0.618034f;

		/// <summary> waveIndex(0-based) 파가 들어오는 방향(도). 같은 판·같은 파도면 항상 같다. </summary>
		public static float AngleDegrees(int waveIndex, int seed)
		{
			float turns = seed * SEED_STEP + waveIndex * GOLDEN_STEP;
			return Wrap360((turns - Mathf.Floor(turns)) * 360f);
		}

		/// <summary>
		/// 그 방향의 테두리 토막에 출현 지점을 고르게 뿌린다(무대 로컬 좌표, y = 0).
		/// arcDegrees 는 토막의 폭 — 넓을수록 「전선」이 길어져 한 곳만 막아선 못 버틴다.
		/// count 가 1 이면 정확히 중앙 각, 2 이상이면 양 끝을 포함해 균등 분할.
		/// </summary>
		public static void Sample(
			int waveIndex,
			int seed,
			float arcDegrees,
			float halfWidth,
			float halfLength,
			float inset,
			int count,
			List<Vector3> into)
		{
			if (into == null)
				return;

			into.Clear();
			if (count <= 0)
				return;

			SampleAt(AngleDegrees(waveIndex, seed), arcDegrees, halfWidth, halfLength, inset, count, into);
		}

		/// <summary>
		/// 중심 각을 *직접 줘서* 뽑는다 — 뚫린 자리가 파도를 끌어당길 때처럼, 각이 씨앗이 아니라
		/// 판 상태에서 나오는 경우가 있다. 예고와 스폰이 같은 각을 봐야 하므로 문은 하나여야 한다.
		/// </summary>
		public static void SampleAt(
			float centerDegrees,
			float arcDegrees,
			float halfWidth,
			float halfLength,
			float inset,
			int count,
			List<Vector3> into)
		{
			if (into == null)
				return;

			into.Clear();
			if (count <= 0)
				return;

			float center = Wrap360(centerDegrees);
			float arc = Mathf.Max(0f, arcDegrees);

			for (int index = 0; index < count; index++)
			{
				// 균등 분할 — 한 마리씩 다른 각으로 나오되 순서는 고정(결정론).
				float ratio = count == 1 ? 0.5f : (float)index / (count - 1);
				float angle = center + (ratio - 0.5f) * arc;
				into.Add(BorderPoint(angle, halfWidth, halfLength, inset));
			}
		}

		/// <summary>
		/// 중심에서 angle 방향으로 쏜 반직선이 판 테두리와 만나는 지점(무대 로컬). inset 만큼 안쪽으로 당긴다.
		/// 원형 링이 아니라 *직사각 테두리*를 쓴다 — 판이 정사각이 아닐 때 원으로 뿌리면 짧은 축에서
		/// 판 밖으로 나가고 긴 축에서는 한참 안쪽에 뜬다(= 어떤 방향은 코어에 훨씬 가깝게 시작).
		/// </summary>
		public static Vector3 BorderPoint(float angleDegrees, float halfWidth, float halfLength, float inset)
		{
			float radians = Wrap360(angleDegrees) * Mathf.Deg2Rad;
			float directionX = Mathf.Sin(radians);
			float directionZ = Mathf.Cos(radians);

			float usableWidth = Mathf.Max(0f, halfWidth - inset);
			float usableLength = Mathf.Max(0f, halfLength - inset);

			// 테두리까지의 거리 = 두 축 중 먼저 닿는 쪽. (축에 딱 붙은 방향은 그 축을 무시한다.)
			float scaleX = Mathf.Abs(directionX) > 1e-5f ? usableWidth / Mathf.Abs(directionX) : float.MaxValue;
			float scaleZ = Mathf.Abs(directionZ) > 1e-5f ? usableLength / Mathf.Abs(directionZ) : float.MaxValue;
			float scale = Mathf.Min(scaleX, scaleZ);
			if (scale >= float.MaxValue)
				return Vector3.zero;

			return new Vector3(directionX * scale, 0f, directionZ * scale);
		}

		/// <summary>
		/// 8방위 이름 — 예고가 「숫자」가 아니라 **말**로 서게 한다
		/// (사용자 지시: 판이 도는 동안 웨이브 번호·시간 같은 숫자를 늘어놓지 않는다).
		/// </summary>
		public static string DirectionName(float angleDegrees)
		{
			int sector = Mathf.RoundToInt(Wrap360(angleDegrees) / 45f) % 8;
			return sector switch
			{
				0 => "북",
				1 => "북동",
				2 => "동",
				3 => "남동",
				4 => "남",
				5 => "남서",
				6 => "서",
				_ => "북서",
			};
		}

		private static float Wrap360(float degrees)
		{
			float wrapped = degrees % 360f;
			return wrapped < 0f ? wrapped + 360f : wrapped;
		}

		/// <summary>
		/// 두 각 사이를 *짧은 쪽으로* 섞는다. 350도와 10도의 중간은 180도가 아니라 0도다 —
		/// 그냥 보간하면 파도가 정반대에서 온다.
		/// </summary>
		public static float Blend(float fromDegrees, float toDegrees, float t)
		{
			float delta = Mathf.Repeat(toDegrees - fromDegrees + 180f, 360f) - 180f;
			return Wrap360(fromDegrees + delta * Mathf.Clamp01(t));
		}

	}
}
