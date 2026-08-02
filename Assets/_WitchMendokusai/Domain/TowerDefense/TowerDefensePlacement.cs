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
		public TowerDefensePlaceableKind SelectedKind
		{
			get
			{
				if (SelectedSlot < TowerSlotCount)
					return TowerDefensePlaceableKind.Tower;
				if (SelectedSlot == TowerSlotCount)
					return TowerDefensePlaceableKind.Harvester;
				if (SelectedSlot == TowerSlotCount + 1)
					return TowerDefensePlaceableKind.Lab;
				if (SelectedSlot == TowerSlotCount + 2)
					return TowerDefensePlaceableKind.Wall;
				if (SelectedSlot == TowerSlotCount + 3)
					return TowerDefensePlaceableKind.Trap;
				return SelectedSlot == TowerSlotCount + 4
					? TowerDefensePlaceableKind.Outpost
					: TowerDefensePlaceableKind.Hero;
			}
		}

		/// <summary>
		/// 핫바 슬롯 — 0..포탑종류수-1 = 포탑, 마지막 = 채집. 포탑이 여러 종류가 되면서 「종류」가 아니라
		/// 「슬롯」이 선택의 단위가 된다(종류를 늘릴 때 입력·화면을 고칠 필요가 없다).
		/// </summary>
		public int SelectedSlot { get; private set; }

		/// <summary>
		/// 지금 「설치 대기」인가 — 칸을 고른 순간 켜지고, **한 번 설치하면 꺼진다**(사용자 지시:
		/// "기본적으로 계속 설치 모드인게 아니라 ... 최소한 1회 클릭 설치로").
		///
		/// ★ 왜 꺼야 하나: 계속 무장돼 있으면 화면을 클릭하는 *모든* 행위가 설치가 된다 — 건물을 보려고
		///   눌러도, 시점을 잡으려고 눌러도 자원이 빠진다. 무장이 1회용이면 클릭의 기본 의미가
		///   「고른다·본다」로 돌아오고, 짓는 것은 *의도한 한 번*이 된다.
		/// ★ 영웅 칸만 예외 — 그건 짓는 게 아니라 보내는 것이라 반복이 자연스럽다.
		/// </summary>
		public bool IsArmed { get; private set; }

		/// <summary> 설치 대기 해제(설치 완료·취소). </summary>
		public void Disarm()
		{
			IsArmed = false;
		}

		// 화면에 실제로 보이는 포탑 칸 → 진짜 포탑 종류 번호. 잠긴 인형을 칸에서 빼면 둘이 어긋나므로
		// (3번 칸이 3번 포탑이 아닐 수 있다) 매치가 알려준 목록을 그대로 따른다.
		private readonly System.Collections.Generic.List<int> availableTowers = new();

		/// <summary> 이번 판에 쓸 수 있는 포탑 종류 목록(핫바 순서 그대로). </summary>
		public void SetAvailableTowers(System.Collections.Generic.IReadOnlyList<int> towerIndices)
		{
			availableTowers.Clear();
			if (towerIndices != null)
				availableTowers.AddRange(towerIndices);
			SelectedSlot = 0;
		}

		/// <summary> 지금 고른 포탑 종류 인덱스(채집을 고른 상태면 첫 포탑). </summary>
		public int SelectedTowerIndex
		{
			get
			{
				if (availableTowers.Count == 0)
					return 0;
				int slot = SelectedSlot < availableTowers.Count ? SelectedSlot : 0;
				return availableTowers[slot];
			}
		}

		private int TowerSlotCount => availableTowers.Count > 0 ? availableTowers.Count : 1;

		public event System.Action<int> SelectionChanged = delegate { };

		/// <summary> 슬롯 선택 — 범위를 벗어나면 무시(없는 칸을 누른 것). </summary>
		public void SelectSlot(int slot)
		{
			// 칸 = 포탑들 + 채집 + 연구 + 벽 + 함정 + 전초기지 + 영웅. 범위 밖은 없는 칸을 누른 것.
			if (slot < 0 || slot > TowerSlotCount + 5)
				return;
			if (SelectedSlot == slot)
			{
				IsArmed = true; // 같은 칸을 다시 누르면 「또 짓겠다」는 뜻이다.
				return;
			}

			SelectedSlot = slot;
			IsArmed = true; // 칸을 고르는 것 = 설치 대기. 한 번 지으면 다시 꺼진다.
			SelectionChanged(slot);
		}

		/// <summary> 하위 호환 진입점(종류 지정) — 재시작 등이 쓰는 경로. </summary>
		public void SelectKind(TowerDefensePlaceableKind kind)
		{
			SelectSlot(kind == TowerDefensePlaceableKind.Harvester ? TowerSlotCount : 0);
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

			// 무장이 안 됐으면 이 클릭은 설치가 아니다 — 보거나 고르는 클릭이다.
			if (IsArmed == false)
				return;

			switch (SelectedKind)
			{
				case TowerDefensePlaceableKind.Harvester:
					PlaceHarvesterAt(screenPointerPosition);
					break;
				case TowerDefensePlaceableKind.Lab:
					PlaceLabAt(screenPointerPosition);
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
			if (ghostBuilding != null)
				ghostBuilding.SetActive(false);
			if (previewMarker != null)
				previewMarker.SetActive(false);
			if (previewRing != null)
				previewRing.SetVisible(false);
		}

		/// <summary>
		/// 지금 커서가 얹힌 유닛(없으면 null) — 툴팁이 읽는다.
		/// 배치 레이캐스트와 *같은 레이*를 쓴다: 화면이 「여기」라고 말하는 곳과 툴팁이 말하는 대상이
		/// 갈라지면 둘 중 하나는 거짓말이 된다.
		/// </summary>
		public ArenaCombatant HoveredUnit { get; private set; }
		public Vector2 HoverScreenPosition { get; private set; }

		private void UpdateHover(Vector2 screenPointerPosition)
		{
			HoverScreenPosition = screenPointerPosition;

			Camera raycastCamera = RaycastCamera;
			if (raycastCamera == null || UIPointer.IsOverInteractive(screenPointerPosition))
			{
				HoveredUnit = null;
				return;
			}

			// ★ 단순 Raycast 는 *미리보기 마커*를 맞는다 — 그건 커서를 따라다니므로 항상 커서 밑에 있고,
			//   그래서 툴팁 대상이 영원히 「아무것도 아님」이 됐다(사용자 실증: "유닛 툴팁 어딨는데").
			//   맞은 것을 전부 훑어 *유닛인 것*만 고른다.
			Ray ray = raycastCamera.ScreenPointToRay(screenPointerPosition);
			int hitCount = Physics.RaycastNonAlloc(ray, hoverHits, raycastDistance, ~0, QueryTriggerInteraction.Ignore);

			ArenaCombatant nearest = null;
			float nearestDistance = float.MaxValue;
			for (int index = 0; index < hitCount; index++)
			{
				ArenaCombatant combatant = hoverHits[index].collider.GetComponentInParent<ArenaCombatant>();
				if (combatant == null || hoverHits[index].distance >= nearestDistance)
					continue;

				nearest = combatant;
				nearestDistance = hoverHits[index].distance;
			}

			HoveredUnit = nearest;
		}

		// 커서 아래 유닛 탐색 버퍼 — 매 프레임 새 배열을 만들지 않는다.
		private readonly RaycastHit[] hoverHits = new RaycastHit[16];

		private void Update()
		{
			if (isActive == false || previewMarker == null)
				return;

			if (inputManager != null)
				UpdateHover(inputManager.MouseScreenPosition);

			// 무장하지 않았으면 미리보기 자체가 없다 — 커서에 늘 유령이 붙어 있으면 「지금 짓는 중」이
			// 거짓말이 된다.
			if (IsArmed == false)
			{
				previewMarker.SetActive(false);
				if (ghostBuilding != null)
					ghostBuilding.SetActive(false);
				if (previewRing != null)
					previewRing.SetVisible(false);
				return;
			}

			// 프리뷰도 같은 판정을 따라야 한다 — UI 위에 초록 마커가 떠 있으면 "여기 설치된다"는 거짓말이 된다.
			if (inputManager == null
				|| UIPointer.IsOverInteractive(inputManager.MouseScreenPosition)
				|| TryGetSnappedGroundPosition(inputManager.MouseScreenPosition, out Vector3 snappedWorldPosition) == false)
			{
				previewMarker.SetActive(false);
				if (ghostBuilding != null)
					ghostBuilding.SetActive(false);
				if (previewRing != null)
					previewRing.SetVisible(false);
				return;
			}

			previewMarker.SetActive(true);
			previewMarker.transform.position = snappedWorldPosition;
			UpdatePreviewRing(snappedWorldPosition);
			UpdateGhostBuilding(snappedWorldPosition);

			// 유효/무효 프리뷰 색 — 판정은 전부 match 재사용(이중화 X).
			// 「지을 수 있나」는 이제 셋이다: 칸이 비었나 / 암반이 아닌가 / *보급이 닿나*.
			if (match != null)
			{
				bool canBuild = match.IsCellOccupied(snappedWorldPosition) == false
					&& match.IsObstacleAt(snappedWorldPosition) == false
					&& match.IsInBuildableRange(snappedWorldPosition);

				Renderer previewRenderer = previewMarker.GetComponentInChildren<Renderer>();
				if (previewRenderer != null)
					previewRenderer.material.color = canBuild ? Color.green : Color.red;

				TintGhost(canBuild);
			}
		}

		// ── 유령 건물(설치 미리보기) ──────────────────────────────────────────────
		// ★ 왜 필요한가 (사용자 지시: "설치 미리보기에서 건물 모습이랑 사거리가 보여야겠죠"):
		//   네모 마커 하나로는 *무엇을* 짓는지 알 수 없다. 종류가 일곱이 넘는데 커서에 뜨는 그림이 늘 같으면
		//   핫바를 잘못 고른 것을 설치한 뒤에야 안다. 실제로 세울 그 프리팹을 반투명으로 미리 세워 보여준다.
		private GameObject ghostBuilding;
		private GameObject ghostSourcePrefab;
		private readonly System.Collections.Generic.List<Renderer> ghostRenderers = new();

		private void UpdateGhostBuilding(Vector3 snappedWorldPosition)
		{
			GameObject wanted = GhostPrefabForSelection();
			if (wanted == null)
			{
				if (ghostBuilding != null)
					ghostBuilding.SetActive(false);
				return;
			}

			// 고른 종류가 바뀌면 유령도 갈아끼운다.
			if (ghostSourcePrefab != wanted)
			{
				if (ghostBuilding != null)
					Destroy(ghostBuilding);

				ghostSourcePrefab = wanted;
				ghostBuilding = Instantiate(wanted, transform);
				ghostBuilding.name = "PlacementGhost";
				StripGhost(ghostBuilding);

				ghostRenderers.Clear();
				ghostBuilding.GetComponentsInChildren(true, ghostRenderers);
			}

			ghostBuilding.SetActive(true);
			ghostBuilding.transform.position = snappedWorldPosition;
		}

		/// <summary> 유령은 *보이기만* 한다 — 충돌·전투·이동이 살아 있으면 미리보기가 게임에 개입한다. </summary>
		private static void StripGhost(GameObject ghost)
		{
			foreach (Collider collider in ghost.GetComponentsInChildren<Collider>(true))
				collider.enabled = false;
			foreach (Rigidbody body in ghost.GetComponentsInChildren<Rigidbody>(true))
				body.isKinematic = true;
			foreach (MonoBehaviour behaviour in ghost.GetComponentsInChildren<MonoBehaviour>(true))
				behaviour.enabled = false;
		}

		private void TintGhost(bool canBuild)
		{
			if (ghostBuilding == null)
				return;

			Color tint = canBuild ? new Color(0.5f, 1f, 0.6f, 0.55f) : new Color(1f, 0.45f, 0.45f, 0.55f);
			foreach (Renderer renderer in ghostRenderers)
			{
				if (renderer == null)
					continue;
				if (renderer is SpriteRenderer sprite)
					sprite.color = tint;
				else if (renderer.material.HasProperty("_BaseColor"))
					renderer.material.SetColor("_BaseColor", tint);
				else
					renderer.material.color = tint;
			}
		}

		private GameObject GhostPrefabForSelection()
		{
			if (stage == null)
				return null;

			return SelectedKind switch
			{
				TowerDefensePlaceableKind.Harvester => stage.HarvesterUnit != null ? stage.HarvesterUnit.Prefab : null,
				TowerDefensePlaceableKind.Lab => stage.HarvesterUnit != null ? stage.HarvesterUnit.Prefab : null,
				TowerDefensePlaceableKind.Tower => stage.TowerUnit != null ? stage.TowerUnit.Prefab : null,
				// 벽·함정·전초기지는 프리팹이 아니라 코드가 그리는 도형이라 유령이 없다(마커가 그 자리를 대신한다).
				_ => null,
			};
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
			bool isLab = SelectedKind == TowerDefensePlaceableKind.Lab;
			if (SelectedKind == TowerDefensePlaceableKind.Trap)
			{
				if (previewRing == null)
					previewRing = TowerDefenseRing.Create(transform, "PlacementPreviewRing", Color.white, 0.12f, 0.06f);
				previewRing.transform.position = snappedWorldPosition + new Vector3(0f, 0.06f, 0f);
				previewRing.SetRadius(stage.TrapRadius);
				previewRing.SetColor(new Color(1f, 0.45f, 0.32f, 0.9f));
				previewRing.SetVisible(true);
				return;
			}

			if (SelectedKind == TowerDefensePlaceableKind.Wall)
			{
				if (previewRing != null)
					previewRing.SetVisible(false);
				return;
			}

			// 영웅은 짓는 게 아니라 보내는 것 — 원은 「거기 서면 어디까지 닿나」를 말한다.
			if (SelectedKind == TowerDefensePlaceableKind.Hero)
			{
				if (stage.HeroArchetype == null || match.HasHero == false)
				{
					if (previewRing != null)
						previewRing.SetVisible(false);
					return;
				}

				if (previewRing == null)
					previewRing = TowerDefenseRing.Create(transform, "PlacementPreviewRing", Color.white, 0.12f, 0.06f);
				previewRing.transform.position = snappedWorldPosition + new Vector3(0f, 0.06f, 0f);
				previewRing.SetRadius(stage.HeroArchetype.Range);
				previewRing.SetColor(new Color(1f, 0.62f, 0.9f, 0.9f));
				previewRing.SetVisible(true);
				return;
			}
			float radius = isLab ? stage.LabVisionRadius
				: isHarvester ? stage.NodeCaptureRadius
				: match.TowerRange(SelectedTowerIndex);
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
			previewRing.SetColor(isLab
				? new Color(0.86f, 0.62f, 1f, 0.9f)
				: isHarvester
					? new Color(0.42f, 0.92f, 0.68f, 0.9f)
					: new Color(0.45f, 0.78f, 1f, 0.9f));
			previewRing.SetVisible(true);
		}

		/// <summary>
		/// 우클릭 = 그 칸의 것을 판다. 「짓기=좌클릭 / 팔기=우클릭」은 건설류의 관용이라 배울 게 없다.
		/// UI 위 클릭은 설치와 마찬가지로 판매도 아니다.
		/// </summary>
		public void SellAt(Vector2 screenPointerPosition)
		{
			if (match == null || stage == null)
				return;
			if (UIPointer.IsOverInteractive(screenPointerPosition))
				return;

			if (TryGetSnappedGroundPosition(screenPointerPosition, out Vector3 snappedWorldPosition) == false)
				return;

			if (match.TrySell(snappedWorldPosition, stage.SellRefundRatio) == false)
				Debug.Log($"{nameof(TowerDefensePlacement)}: 판매 거절 — 빈 칸이거나 코어.");
		}

		/// <summary>
		/// 영웅을 그 자리로 보낸다 — 이 칸만 「짓기」가 아니라 「명령」이다. 셀 스냅을 안 쓰는 이유:
		/// 영웅은 격자 위에 서는 물건이 아니라 걸어가는 아이라, 칸에 맞춰 튀면 조작감이 건물처럼 굳는다.
		/// </summary>
		public void CommandHeroAt(Vector2 screenPointerPosition)
		{
			if (match == null)
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

		/// <summary> 연구 인형 설치 — 빈 칸이면 어디든. 지어지는 순간 모든 포탑이 강해진다. </summary>
		public void PlaceLabAt(Vector2 screenPointerPosition)
		{
			if (match == null)
				return;

			if (TryGetSnappedGroundPosition(screenPointerPosition, out Vector3 snappedWorldPosition) == false)
			{
				Debug.Log($"{nameof(TowerDefensePlacement)}: 연구 인형 배치 실패 — 지면 레이캐스트 무효.");
				return;
			}

			if (match.TryPlaceLab(snappedWorldPosition) == false)
				Debug.Log($"{nameof(TowerDefensePlacement)}: 연구 인형 배치 거절 — 자원 부족 또는 이미 점유된 칸.");
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

			groundPosition = hit.point;
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
