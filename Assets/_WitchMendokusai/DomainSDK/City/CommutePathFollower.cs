using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	// 통근 경로 추종 — 셀 경로(RoadGraph.FindPath 결과) 위를 progress(셀 단위 0..count-1)로 왕복(ping-pong).
	// 순수(상태 = progress/direction 만, Unity 의존 X 좌표 산술만). CityPaintManager 가 매 프레임 Advance →
	// CurrentSegment 로 두 셀 사이 보간 위치를 월드 좌표로 변환해 시민 큐브를 옮긴다.
	//
	// 비전-중립 — 시민이 무엇으로 보이는지(스킨)는 무관, 여기선 경로 위 진행도만. INC-7 mover 핵심.
	public sealed class CommutePathFollower
	{
		private readonly IReadOnlyList<Vector3Int> path;
		private float progress;   // 0 .. path.Count-1 (셀 인덱스 연속값)
		private int direction = 1; // +1 = 집→직장, -1 = 직장→집

		public CommutePathFollower(IReadOnlyList<Vector3Int> path)
		{
			this.path = path;
		}

		public bool HasPath => path != null && path.Count > 0;
		public float Progress => progress;
		public int Direction => direction;

		// step(셀 단위 거리, ≥0) 만큼 진행. 양 끝 도달 시 방향 반전(왕복). 단일/빈 경로면 정지.
		public void Advance(float step)
		{
			if (path == null || path.Count <= 1)
			{
				return;
			}

			float max = path.Count - 1;
			progress += step * direction;

			if (progress >= max)
			{
				progress = max;
				direction = -1;
			}
			else if (progress <= 0f)
			{
				progress = 0f;
				direction = 1;
			}
		}

		// 현재 위치 = path[lower]→path[lower+1] 사이 t(0..1) 보간. 단일 경로면 그 셀 고정.
		public void CurrentSegment(out Vector3Int fromCell, out Vector3Int toCell, out float t)
		{
			if (path == null || path.Count == 0)
			{
				fromCell = default;
				toCell = default;
				t = 0f;
				return;
			}

			if (path.Count == 1)
			{
				fromCell = path[0];
				toCell = path[0];
				t = 0f;
				return;
			}

			int lower = Mathf.Clamp(Mathf.FloorToInt(progress), 0, path.Count - 2);
			fromCell = path[lower];
			toCell = path[lower + 1];
			t = progress - lower;
		}
	}
}
