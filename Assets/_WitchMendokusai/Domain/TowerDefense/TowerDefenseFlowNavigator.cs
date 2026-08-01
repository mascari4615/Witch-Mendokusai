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

		public TowerDefenseFlowNavigator(
			TowerDefenseMapLayout layout,
			TowerDefenseFlowField flowField,
			Transform stageRoot,
			float directGoalDistance)
		{
			this.layout = layout;
			this.flowField = flowField;
			this.stageRoot = stageRoot;
			this.directGoalDistance = directGoalDistance;
		}

		public bool TryGetSteering(Vector3 from, Vector3 to, out Vector3 direction)
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

			if (flowField.TryGetNextCell(fromCell, out Vector2Int nextCell) == false)
				return false;

			Vector3 nextLocal = layout.CellToWorld(nextCell);
			Vector3 localDirection = nextLocal - fromLocal;
			localDirection.y = 0f;
			if (localDirection.sqrMagnitude <= Mathf.Epsilon)
				return false;

			Vector3 worldDirection = stageRoot.TransformDirection(localDirection);
			worldDirection.y = 0f;
			direction = worldDirection.normalized;
			return true;
		}
	}
}
