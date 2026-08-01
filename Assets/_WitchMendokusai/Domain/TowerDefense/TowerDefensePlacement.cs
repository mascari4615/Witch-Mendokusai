using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 특수시공 개척(TD) 배치 입력 처리 (TASK-WM-194 증분3) — 포인터→월드→셀 수학 + TowerDefenseMatch 구동을
	/// 전담. BuildManager.TryBuildRaycast 동형(레이캐스트 자체 구동, 레이어마스크 우회) + 셀 스냅은 스펙 고정식
	/// (Mathf.Floor(x)+0.5f) 을 cellSize 로 일반화(수치 노출 룰: 하드코딩 0).
	/// 규칙 판단(자원/노드/점유)은 전부 TowerDefenseMatch 에 위임 — 본 컴포넌트는 실패 사유를 사용자에게
	/// 로그로 알리기 위해 동일 판정을 *읽기 전용*으로 미리 조회할 뿐(Match.TryPlaceX 재판정과 동일 소스,
	/// 이중 소스 아님 — IsCellOccupied/TryFindPlaceableNode 둘 다 Match 의 public 조회 API 재사용).
	/// </summary>
	public class TowerDefensePlacement : MonoBehaviour
	{
		[field: Header("_" + nameof(TowerDefensePlacement))]
		// 배치 레이캐스트는 **화면에 실제로 보이는 카메라**를 써야 한다 — 클릭한 픽셀의 의미가 곧 그 카메라
		// 기준이기 때문. 개척이 정식 content 카메라(vcam priority)로 바뀌면서 실제 렌더 카메라는
		// Cinemachine brain 이 물고 있는 단 하나이므로, 특정 Camera 를 인스펙터로 박아두면 모드 전환·
		// 블렌딩 중에 죽은 참조를 쓰게 된다. 매 호출 lazy 해석(ViewCameraResolver)이 단일 정본.
		private Camera RaycastCamera => ViewCameraResolver.Current;

		[Tooltip("배치 대상 매치 — 자원 차감/스폰/점유 판정 전부 여기 위임.")]
		[SerializeField] private TowerDefenseMatch match;

		[Tooltip("비용 표시용 스테이지 데이터(TowerCost/HarvesterCost 조회) — match 가 참조하는 것과 동일 SO.")]
		[SerializeField] private TowerDefenseStageSO stage;

		[Tooltip("배치 미리보기 마커 — 활성 중 매 프레임 스냅 위치로 추적, 비활성 시 숨김. 프리미티브/프리팹 아무거나.")]
		[SerializeField] private GameObject previewMarker;

		[Tooltip("셀 크기(월드 단위) — 스냅 격자 간격. 수치 노출 룰(하드코딩 금지).")]
		[SerializeField, Min(0.1f)] private float cellSize = 1f;

		[Tooltip("배치 레이캐스트 최대 도달 거리.")]
		[SerializeField, Min(1f)] private float raycastDistance = 200f;

		private InputManager inputManager;
		private bool isActive;

		// 설치 전에 「여기 지으면 어디까지 닿는지」를 보여주는 원. 설치 후에야 알 수 있으면 그건 판단이 아니라 도박이다.
		private TowerDefenseRing previewRing;

		/// <summary>
		/// 핫바에서 고른 설치 대상. 좌/우클릭으로 종류를 가르던 방식은 종류가 늘면 안 늘어난다
		/// (사용자 지시: "좌클릭/우클릭이 아니라 빌딩 핫바 좀 활용해야 할듯").
		/// 선택 = 핫바(숫자키/클릭), 설치 = 클릭 — 기존 건설 모드와 같은 조작 문법.
		/// </summary>
		public TowerDefensePlaceableKind SelectedKind { get; private set; } = TowerDefensePlaceableKind.Tower;

		public event System.Action<TowerDefensePlaceableKind> SelectionChanged = delegate { };

		public void SelectKind(TowerDefensePlaceableKind kind)
		{
			if (SelectedKind == kind)
				return;

			SelectedKind = kind;
			SelectionChanged(kind);
		}

		// HUD 버튼(다시 시작 등)을 누른 클릭이 그대로 배치로도 처리되는 것을 막는 1회용 소거.
		// UI Toolkit 위 클릭이 EventSystem 의 「UI 위인가」 판정에 항상 잡히지는 않기 때문에
		// 버튼 쪽에서 명시적으로 한 번 삼켜준다(안 그러면 버튼 아래 지면에 유닛이 서고 자원이 빠진다).
		private bool suppressNextPlacement;

		/// <summary> 다음 배치 클릭 1회를 무시 — HUD 버튼 핸들러가 호출. </summary>
		public void SuppressNextClick()
		{
			suppressNextPlacement = true;
		}

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

			if (SelectedKind == TowerDefensePlaceableKind.Harvester)
				PlaceHarvesterAt(screenPointerPosition);
			else
				PlaceTowerAt(screenPointerPosition);
		}

		[Inject]
		public void Construct(InputManager inputManager)
		{
			this.inputManager = inputManager;
		}

		/// <summary> TowerDefenseModeController 진입 시 호출 — 프리뷰 추적 시작. </summary>
		public void Activate()
		{
			isActive = true;
		}

		/// <summary> TowerDefenseModeController 이탈 시 호출 — 프리뷰 즉시 숨김. </summary>
		public void Deactivate()
		{
			isActive = false;
			if (previewMarker != null)
				previewMarker.SetActive(false);
			if (previewRing != null)
				previewRing.SetVisible(false);
		}

		private void Update()
		{
			if (isActive == false || previewMarker == null)
				return;

			// 프리뷰도 같은 판정을 따라야 한다 — UI 위에 초록 마커가 떠 있으면 "여기 설치된다"는 거짓말이 된다.
			if (inputManager == null
				|| UIPointer.IsOverInteractive(inputManager.MouseScreenPosition)
				|| TryGetSnappedGroundPosition(inputManager.MouseScreenPosition, out Vector3 snappedWorldPosition) == false)
			{
				previewMarker.SetActive(false);
				if (previewRing != null)
					previewRing.SetVisible(false);
				return;
			}

			previewMarker.SetActive(true);
			previewMarker.transform.position = snappedWorldPosition;
			UpdatePreviewRing(snappedWorldPosition);

			// 유효/무효 프리뷰 색 — match.IsCellOccupied 재사용(판정 이중화 X).
			if (match != null)
			{
				Renderer previewRenderer = previewMarker.GetComponentInChildren<Renderer>();
				if (previewRenderer != null)
					previewRenderer.material.color = match.IsCellOccupied(snappedWorldPosition) ? Color.red : Color.green;
			}
		}

		/// <summary>
		/// 미리보기 원 — 포탑이면 사거리, 채집이면 노드를 잡을 수 있는 거리. 둘 다 「이 자리의 의미」를 말한다.
		/// 반지름은 매치 정본(전술 사거리 / 노드 점유 반경)에서 읽는다 — 여기 숫자를 따로 박으면 거짓말이 된다.
		/// </summary>
		private void UpdatePreviewRing(Vector3 snappedWorldPosition)
		{
			if (match == null || stage == null)
				return;

			bool isHarvester = SelectedKind == TowerDefensePlaceableKind.Harvester;
			float radius = isHarvester ? stage.NodeCaptureRadius : match.TowerRange();
			if (radius <= 0f)
			{
				if (previewRing != null)
					previewRing.SetVisible(false);
				return;
			}

			if (previewRing == null)
				previewRing = TowerDefenseRing.Create(transform, "PlacementPreviewRing", Color.white, 0.12f, 0.06f);

			previewRing.transform.position = snappedWorldPosition + new Vector3(0f, 0.06f, 0f);
			previewRing.SetRadius(radius);
			previewRing.SetColor(isHarvester
				? new Color(0.42f, 0.92f, 0.68f, 0.9f)
				: new Color(0.45f, 0.78f, 1f, 0.9f));
			previewRing.SetVisible(true);
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

			bool placed = match.TryPlaceTower(snappedWorldPosition);
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

			Camera raycastCamera = RaycastCamera;
			if (raycastCamera == null)
				return false;

			Ray ray = raycastCamera.ScreenPointToRay(screenPointerPosition);
			if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, ~0, QueryTriggerInteraction.Ignore) == false)
				return false;

			snappedWorldPosition = SnapToCellCenter(hit.point);
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
