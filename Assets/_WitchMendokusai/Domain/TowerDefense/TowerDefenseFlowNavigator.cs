using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 흐름장을 읽어 마수에게 방향을 알려주는 안내자(TASK-WM-194).
	/// 무대 로컬 좌표계 ↔ 판 셀 좌표계 환산까지 여기서 책임진다 — 흐름장은 좌표 원점을 모른다.
	///
	/// 목표가 코어가 아닐 때(예: 눈앞의 포탑과 교전)는 안내하지 않는다: 흐름장은 *코어행* 안내판이고,
	/// 가까운 목표는 어차피 직선으로 닿는다. 잘못 안내하면 마수가 눈앞의 적을 두고 코어로 걸어간다.
	/// </summary>
	public sealed class TowerDefenseFlowNavigator : ITacticNavigator
	{
		private readonly TowerDefenseMapLayout layout;
		private readonly TowerDefenseFlowField flowField;
		private readonly Transform stageRoot;
		private readonly float directGoalDistance;

		/// <summary>
		/// 모서리를 얼마나 둥글게 도나 — 0 이면 다음 칸 *중심*을 딱 밟고, 1 이면 그 너머 칸까지 내다본다.
		///
		/// ★ 사용자 실증: "몬스터들이 복셀 블럭 단위로 움직인다 … 뚝뚝 끊겨 움직이는 것처럼 보이지
		///   않았으면." 원인은 안내가 *칸 중심만* 가리킨 것 — 칸마다 방향이 꺾이니 걸음이 각졌다.
		///   한 칸 더 내다보고 그 사이를 섞으면 같은 길을 곡선으로 걷는다(길 자체는 안 바뀐다).
		/// </summary>
		private readonly float cornerSmoothing;


		public TowerDefenseFlowNavigator(
			TowerDefenseMapLayout layout,
			TowerDefenseFlowField flowField,
			Transform stageRoot,
			float directGoalDistance,
			float cornerSmoothing = 0f)
		{
			this.cornerSmoothing = Mathf.Clamp01(cornerSmoothing);
			this.layout = layout;
			this.flowField = flowField;
			this.stageRoot = stageRoot;
			this.directGoalDistance = directGoalDistance;
		}

		public bool TryGetSteering(Vector3 from, Vector3 to, float lane, out Vector3 direction)
		{
			direction = Vector3.zero;
			if (layout == null || flowField == null || stageRoot == null)
				return false;

			// 코어 말고 다른 걸 노리는 중이면 안내 대상이 아니다(흐름장은 코어행 전용).
			Vector3 goalLocal = stageRoot.InverseTransformPoint(to);
			Vector2Int goalCell = layout.WorldToCell(goalLocal);
			if (goalCell != flowField.GoalCell)
				return false;

			Vector3 fromLocal = stageRoot.InverseTransformPoint(from);
			Vector2Int fromCell = layout.WorldToCell(fromLocal);

			// 코어가 코앞이면 화살표(칸 단위)보다 직선이 정확하다 — 마지막 한 칸에서 덜덜 떠는 것 방지.
			if ((goalLocal - fromLocal).sqrMagnitude <= directGoalDistance * directGoalDistance)
				return false;

			// ★ 「여러 최단 경로 중 *내 것*」을 고른다 — 길이는 그대로, 밟는 칸만 달라진다.
			//   값은 개체가 태어날 때 정해져 넘어온다(여기서 위치로 뽑으면 걸을 때마다 바뀌어 덜덜 떤다).
			if (flowField.TryGetNextCell(fromCell, lane, out Vector2Int nextCell) == false)
				return false;

			Vector3 nextLocal = layout.CellToWorld(nextCell);

			// 한 칸 더 내다본다 — 다음 칸과 그 다음 칸 사이를 섞으면 모서리가 둥글어진다.
			// 못 내다보면(막다른 길·목표 직전) 그냥 다음 칸을 본다 — 길은 그대로다.
			//
			// ★ 단, *벽 모서리에서는 섞지 않는다.* 「길은 그대로」라고 적어 뒀지만 벽을 낀 모서리에서는
			//   사실이 아니었다: 흐름장이 안전한 ㄱ자를 줘도 이 섞기가 그걸 대각선 지름길로 되돌려
			//   마수를 바위 모서리에 처박았다. 몸 지름이 칸과 같아서(반경 0.50 · 칸 1.00) 그 틈으로는
			//   못 들어간다 — 라이브 실측에서 「갈 수 있다는 칸인데 암반에 막힘」이 73줄이었고,
			//   흐름장 쪽 대각선을 막은 뒤에도 그대로 남아 있었다(원인이 여기였다는 증거).
			//   트인 곳에서는 그대로 둥글게 돈다 — 각진 걸음은 벽 옆에서만 생긴다.
			if (cornerSmoothing > 0f
				&& flowField.TryGetNextCell(nextCell, lane, out Vector2Int afterCell)
				&& IsShortcutClear(fromCell, afterCell))
			{
				nextLocal = Vector3.Lerp(nextLocal, layout.CellToWorld(afterCell), cornerSmoothing);
			}

			Vector3 localDirection = nextLocal - fromLocal;
			localDirection.y = 0f;
			if (localDirection.sqrMagnitude <= Mathf.Epsilon)
				return false;

			Vector3 worldDirection = stageRoot.TransformDirection(localDirection);
			worldDirection.y = 0f;

			direction = worldDirection.normalized;
			return true;
		}

		/// <summary>
		/// 지금 칸에서 두 칸 뒤를 향해 질러가도 벽에 안 닿는가 — 둘러싼 네 칸 중 하나라도 벽이면 안 된다.
		///
		/// ★ 「두 칸 사이를 다 훑는」 정밀 검사가 아니라, 몸이 칸만큼 굵다는 사실에 맞춘 보수적 판정이다.
		///   지름길은 벽이 하나도 없을 때만 허용한다 — 애매하면 안 질러가는 쪽이 옳다(끼면 판이 안 끝난다).
		/// </summary>
		private bool IsShortcutClear(Vector2Int fromCell, Vector2Int afterCell)
		{
			int minX = Mathf.Min(fromCell.x, afterCell.x);
			int maxX = Mathf.Max(fromCell.x, afterCell.x);
			int minY = Mathf.Min(fromCell.y, afterCell.y);
			int maxY = Mathf.Max(fromCell.y, afterCell.y);

			for (int x = minX; x <= maxX; x++)
			{
				for (int y = minY; y <= maxY; y++)
				{
					if (layout.IsBlocked(new Vector2Int(x, y)))
						return false;
				}
			}
			return true;
		}
	}
}
