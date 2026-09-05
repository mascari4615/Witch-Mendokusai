using UnityEngine;
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;
using VContainer;

namespace WitchMendokusai
{
	// TowerDefensePlacement 의 실제 놓기 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefensePlacement.cs 를 본다.
	public partial class TowerDefensePlacement : MonoBehaviour
	{
		/// <summary> 선택된 종류를 설치 — 핫바 문법의 단일 진입점(클릭 1회 = 1개). </summary>
		public void PlaceSelectedAt(Vector2 screenPointerPosition)
		{
			if (suppressNextPlacement)
			{
				suppressNextPlacement = false;
				return;
			}

			// ★ UI 위 클릭은 설치가 아니다 — 버튼을 눌렀는데 그 아래 지면에 건물이 서고 자원이 빠지면
			//   화면을 믿을 수 없게 된다(사용자 실증). 판정 정본은 UI 쪽 한 곳(UIPointer).
			if (UIPointer.IsOverInteractive(screenPointerPosition))
				return;

			// 무장이 안 됐으면 이 클릭은 설치가 아니라 **고르는** 클릭이다.
			// (연구·강화처럼 「이미 서 있는 것에 하는 일」이 여기서 열린다.)
			if (IsArmed == false)
			{
				// ★ 고르는 대상은 *누른 자리*가 정한다 — 떠다니던 hover 값이 아니라.
				//   둘은 어긋날 수 있고(커서가 막 움직인 프레임, 화면 밖에서 온 클릭), 그때 화면은
				//   「여기를 눌렀는데 저게 골렸다」가 된다. 누른 좌표로 다시 물으면 그 어긋남이 사라진다.
				UpdateHover(screenPointerPosition);
				SelectedBuilding = HoveredUnit;
				BuildingSelected(SelectedBuilding);
				return;
			}

			switch (SelectedKind)
			{
				case TowerDefensePlaceableKind.Harvester:
					PlaceHarvesterAt(screenPointerPosition);
					break;
				case TowerDefensePlaceableKind.Wall:
					PlaceWallAt(screenPointerPosition);
					break;
				case TowerDefensePlaceableKind.Trap:
					PlaceTrapAt(screenPointerPosition);
					break;
				case TowerDefensePlaceableKind.Outpost:
					PlaceOutpostAt(screenPointerPosition);
					break;
				case TowerDefensePlaceableKind.Generator:
					if (TryGetSnappedGroundPosition(screenPointerPosition, out Vector3 generatorPosition))
						match.TryPlaceGenerator(generatorPosition);
					break;
				case TowerDefensePlaceableKind.Hero:
					CommandHeroAt(screenPointerPosition);
					return; // 보내는 것은 반복이 자연스럽다 — 무장을 유지한다.
				default:
					PlaceTowerAt(screenPointerPosition);
					break;
			}

			// 한 번 지으면 무장 해제 — 다시 지으려면 칸을 다시 고른다.
			IsArmed = false;
		}

		/// <summary>
		/// 우클릭 = **취소**. 짓는 중이면 손에 든 것을 내려놓고, 아니면 영웅을 보낸다.
		///
		/// ★ 판매는 뺐다 (사용자 지시: "짓기할때 우클릭을 취소로 만들어주세요. 제거는 우선 기능 빼봐").
		///   되돌릴 수 없는 일(판매)이 무르는 손짓과 같은 자리에 있으면, 무르려다 건물을 잃는다.
		/// </summary>
		public void SellAt(Vector2 screenPointerPosition)
		{
			if (match == null || stage == null)
				return;
			if (UIPointer.IsOverInteractive(screenPointerPosition))
				return;

			if (IsArmed)
			{
				Disarm();
				return;
			}

			CommandHeroAt(screenPointerPosition);
		}

		/// <summary>
		/// 영웅을 그 자리로 보낸다 — 이 칸만 「짓기」가 아니라 「명령」이다. 셀 스냅을 안 쓰는 이유:
		/// 영웅은 격자 위에 서는 물건이 아니라 걸어가는 아이라, 칸에 맞춰 튀면 조작감이 건물처럼 굳는다.
		/// </summary>
		public void CommandHeroAt(Vector2 screenPointerPosition)
		{
			if (match == null)
				return;

			// 화면 위젯 위에서 누른 것은 땅을 가리킨 게 아니다 — 누르고 끄는 동안 손이 핫바를 스쳐도
			// 영웅이 화면 뒤 엉뚱한 곳으로 달려가지 않게 한다(누름-유지 명령이 생기며 실제로 잦아졌다).
			if (UIPointer.IsOverInteractive(screenPointerPosition))
				return;

			if (TryGetGroundPosition(screenPointerPosition, out Vector3 groundPosition) == false)
				return;

			match.CommandHero(groundPosition);
		}

		/// <summary> 전초기지 설치 — 새 목표이자 새 보급 원점. </summary>
		public void PlaceOutpostAt(Vector2 screenPointerPosition)
		{
			if (match == null)
				return;

			if (TryGetSnappedGroundPosition(screenPointerPosition, out Vector3 snappedWorldPosition) == false)
				return;

			match.TryPlaceOutpost(snappedWorldPosition);
		}

		/// <summary> 함정 설치 — 밟으면 터진다. </summary>
		public void PlaceTrapAt(Vector2 screenPointerPosition)
		{
			if (match == null)
				return;

			if (TryGetSnappedGroundPosition(screenPointerPosition, out Vector3 snappedWorldPosition) == false)
				return;

			match.TryPlaceTrap(snappedWorldPosition);
		}

		/// <summary> 벽 설치 — 마수의 길을 휘게 한다. 길을 완전히 막는 자리는 매치가 거절한다. </summary>
		public void PlaceWallAt(Vector2 screenPointerPosition)
		{
			if (match == null)
				return;

			if (TryGetSnappedGroundPosition(screenPointerPosition, out Vector3 snappedWorldPosition) == false)
				return;

			match.TryPlaceWall(snappedWorldPosition);
		}

		/// <summary> 우클릭 진입점 — 스냅 위치에 타워 배치 시도 + 성공/거절 사유 로그. </summary>
		public void PlaceTowerAt(Vector2 screenPointerPosition)
		{
			if (match == null)
				return;

			if (TryGetSnappedGroundPosition(screenPointerPosition, out Vector3 snappedWorldPosition) == false)
			{
				Debug.Log($"{nameof(TowerDefensePlacement)}: 타워 배치 실패 — 지면 레이캐스트 무효(허공/사거리 밖 클릭).");
				return;
			}

			if (match.IsCellOccupied(snappedWorldPosition))
			{
				Debug.Log($"{nameof(TowerDefensePlacement)}: 타워 배치 거절 — 셀 이미 점유 {snappedWorldPosition}.");
				return;
			}

			if (stage != null && match.Resource < stage.TowerCost)
			{
				Debug.Log($"{nameof(TowerDefensePlacement)}: 타워 배치 거절 — 자원 부족(필요 {stage.TowerCost}, 보유 {match.Resource}).");
				return;
			}

			bool placed = match.TryPlaceTower(snappedWorldPosition, SelectedTowerIndex);
			Debug.Log(placed
				? $"{nameof(TowerDefensePlacement)}: 타워 배치 성공 @ {snappedWorldPosition}."
				: $"{nameof(TowerDefensePlacement)}: 타워 배치 거절 — 알 수 없는 사유(매치 미시작/유닛데이터 미할당 등, 콘솔 상단 로그 확인).");
		}

		/// <summary> 좌클릭 진입점 — 스냅 위치 인근 미점유 자원 노드에 채집건물 배치 시도 + 성공/거절 사유 로그. </summary>
		public void PlaceHarvesterAt(Vector2 screenPointerPosition)
		{
			if (match == null)
				return;

			if (TryGetSnappedGroundPosition(screenPointerPosition, out Vector3 snappedWorldPosition) == false)
			{
				Debug.Log($"{nameof(TowerDefensePlacement)}: 채집건물 배치 실패 — 지면 레이캐스트 무효(허공/사거리 밖 클릭).");
				return;
			}

			if (match.TryFindPlaceableNode(snappedWorldPosition, out _, out Vector3 nodeWorldPosition) == false)
			{
				Debug.Log($"{nameof(TowerDefensePlacement)}: 채집건물 배치 거절 — 반경 내 미점유 자원 노드 없음 {snappedWorldPosition}.");
				return;
			}

			if (match.IsCellOccupied(nodeWorldPosition))
			{
				Debug.Log($"{nameof(TowerDefensePlacement)}: 채집건물 배치 거절 — 노드 셀 이미 점유 {nodeWorldPosition}.");
				return;
			}

			if (stage != null && match.Resource < stage.HarvesterCost)
			{
				Debug.Log($"{nameof(TowerDefensePlacement)}: 채집건물 배치 거절 — 자원 부족(필요 {stage.HarvesterCost}, 보유 {match.Resource}).");
				return;
			}

			bool placed = match.TryPlaceHarvester(snappedWorldPosition);
			Debug.Log(placed
				? $"{nameof(TowerDefensePlacement)}: 채집건물 배치 성공 @ {nodeWorldPosition}."
				: $"{nameof(TowerDefensePlacement)}: 채집건물 배치 거절 — 알 수 없는 사유(매치 미시작/유닛데이터 미할당 등, 콘솔 상단 로그 확인).");
		}

		// BuildManager.TryBuildRaycast 동형 — 모드 카메라 기준 레이캐스트 후 셀 중심 스냅.
		private bool TryGetSnappedGroundPosition(Vector2 screenPointerPosition, out Vector3 snappedWorldPosition)
		{
			snappedWorldPosition = default;
			if (TryGetGroundPosition(screenPointerPosition, out Vector3 groundPosition) == false)
				return false;

			snappedWorldPosition = SnapToCellCenter(groundPosition);
			return true;
		}

		/// <summary> 스냅 없는 원본 지면 좌표 — 격자에 서지 않는 것(영웅)이 쓴다. </summary>
		private bool TryGetGroundPosition(Vector2 screenPointerPosition, out Vector3 groundPosition)
		{
			groundPosition = default;

			Camera raycastCamera = RaycastCamera;
			if (raycastCamera == null)
				return false;

			Ray ray = raycastCamera.ScreenPointToRay(screenPointerPosition);
			if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, ~0, QueryTriggerInteraction.Ignore) == false)
				return false;

			groundPosition = hit.point.ToSim();
			return true;
		}

		// 스펙 고정식(Mathf.Floor(x)+0.5f) 을 cellSize 로 일반화 — cellSize=1 이면 스펙식과 완전히 동일.
		private Vector3 SnapToCellCenter(Vector3 worldPosition)
		{
			float halfCell = cellSize * 0.5f;
			float snappedX = Mathf.Floor(worldPosition.x / cellSize) * cellSize + halfCell;
			float snappedZ = Mathf.Floor(worldPosition.z / cellSize) * cellSize + halfCell;
			return new Vector3(snappedX, worldPosition.y, snappedZ);
		}
	}
}
