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

		/// <summary>
		/// 안내를 얼마나 안 믿고 코어 쪽으로 곧장 갈 것인가(0 = 예전처럼 한 줄, 1 = 안내 무시).
		/// 이게 「떼거지로 몰려온다」의 실체다 — 자세한 이유는 TryGetSteering 의 ★★ 주석.
		/// </summary>
		private readonly float directBlend;

		public TowerDefenseFlowNavigator(
			TowerDefenseMapLayout layout,
			TowerDefenseFlowField flowField,
			Transform stageRoot,
			float directGoalDistance,
			float cornerSmoothing = 0f,
			float directBlend = 0f)
		{
			this.cornerSmoothing = Mathf.Clamp01(cornerSmoothing);
			this.directBlend = Mathf.Clamp01(directBlend);
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

			// 한 칸 더 내다본다 — 다음 칸과 그 다음 칸 사이를 섞으면 모서리가 둥글어진다.
			// 못 내다보면(막다른 길·목표 직전) 그냥 다음 칸을 본다 — 길은 그대로다.
			if (cornerSmoothing > 0f && flowField.TryGetNextCell(nextCell, out Vector2Int afterCell))
				nextLocal = Vector3.Lerp(nextLocal, layout.CellToWorld(afterCell), cornerSmoothing);

			Vector3 localDirection = nextLocal - fromLocal;
			localDirection.y = 0f;
			if (localDirection.sqrMagnitude <= Mathf.Epsilon)
				return false;

			Vector3 worldDirection = stageRoot.TransformDirection(localDirection);
			worldDirection.y = 0f;

			// ★★ 「길 딱 정해서 오는 게 아니라 떼거지로」 (사용자 지시 — They Are Billions).
			//   흐름장은 모두에게 *똑같은 제일 싼 길*을 준다. 그래서 사방 여덟 곳에서 나와도 몇 걸음 만에
			//   한 줄로 합류하고, 화면에는 「정해진 길」이 생긴다 — 풍선타워디펜스의 그림이다.
			//   고치는 법은 길을 여러 개 파는 게 아니라, **길을 덜 믿게 하는 것**이다:
			//   각자 「코어 쪽으로 곧장」과 「안내가 시킨 대로」를 자기 비율로 섞는다. 뚫린 벌판에서는
			//   서로 다른 직선을 그어 넓은 면이 되고, 바위를 만나면 안내가 이겨서 알아서 돌아 나간다.
			//   비율은 *개체마다 고정*이다(위치로 뽑는 게 아니라 출발점이 정한다) — 매 프레임 흔들면
			//   덜덜 떨며 지그재그로 걷는다.
			if (directBlend > 0f)
			{
				Vector3 straight = to - from;
				straight.y = 0f;
				if (straight.sqrMagnitude > Mathf.Epsilon)
				{
					// 개체 고유값 — 같은 자리에서 출발한 둘은 같게, 다른 자리는 다르게. 0.5~1.5배로 흩어진다.
					float lane = Mathf.Abs(Mathf.Sin(from.x * 12.9898f + from.z * 78.233f) * 43758.5453f) % 1f;
					float blend = Mathf.Clamp01(directBlend * (0.5f + lane));
					worldDirection = Vector3.Lerp(worldDirection.normalized, straight.normalized, blend);
					worldDirection.y = 0f;
				}
			}

			direction = worldDirection.normalized;
			return true;
		}
	}
}
