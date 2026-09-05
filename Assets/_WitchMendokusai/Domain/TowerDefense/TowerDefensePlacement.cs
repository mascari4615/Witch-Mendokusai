using UnityEngine;
// ★ 좌표는 판정 쪽 (TASK-WM-214) — 엔진으로 나갈 땐 자동, 엔진에서 받을 땐 캐스트.
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;
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
	public partial class TowerDefensePlacement : MonoBehaviour
	{
		// 배치 레이캐스트는 **화면에 실제로 보이는 카메라**를 써야 한다 — 클릭한 픽셀의 의미가 곧 그 카메라
		// 기준이기 때문. 개척이 정식 content 카메라(vcam priority)로 바뀌면서 실제 렌더 카메라는
		// Cinemachine brain 이 물고 있는 단 하나이므로, 특정 Camera 를 인스펙터로 박아두면 모드 전환·
		// 블렌딩 중에 죽은 참조를 쓰게 된다. 매 호출 lazy 해석(ViewCameraResolver)이 단일 정본.
		private Camera RaycastCamera => ViewCameraResolver.Current;

		// 이 제목은 *직렬화되는 것* 바로 위에 있어야 뜬다. 예전엔 계산으로만 나오는 값(저장되지 않는다)
		// 위에 붙어 있어서 컴파일러가 통째로 버렸고, 인스펙터엔 아무 제목도 안 떴다.
		[Header("_" + nameof(TowerDefensePlacement))]
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

		/// <summary>
		/// 지금 고른 칸이 무엇을 세우는가 — **매치가 준 목록**을 그대로 읽는다.
		///
		/// ★ 예전엔 여기서 칸 번호를 직접 계산했다(포탑 수 + 1 = 채집, + 2 = 벽 …). 화면도 같은 계산을
		///   따로 갖고 있었고, 연구로 해금이 생기는 순간 둘이 어긋난다 — 「함정을 골랐는데 전초기지가
		///   지어진다」. 목록의 주인은 규칙층 하나뿐이다.
		/// </summary>
		public TowerDefensePlaceableKind SelectedKind =>
			SelectedSlot >= 0 && SelectedSlot < slots.Count
				? slots[SelectedSlot].Kind
				: TowerDefensePlaceableKind.Harvester;

		private readonly System.Collections.Generic.List<TowerDefenseSlot> slots = new();

		/// <summary> 이번 판에 쓸 수 있는 칸 — 매치가 해금 상태대로 만들어 넘긴다. </summary>
		public void SetSlots(System.Collections.Generic.IReadOnlyList<TowerDefenseSlot> available)
		{
			slots.Clear();
			if (available != null)
				slots.AddRange(available);
			if (SelectedSlot >= slots.Count)
				SelectedSlot = 0; // 열려 있던 칸이 사라졌으면 첫 칸으로 — 없는 칸을 든 채로 두지 않는다.
		}

		/// <summary>
		/// 핫바 슬롯 — 목록의 index 그대로다. 「몇 번째가 무엇인가」는 규칙층이 정한다.
		/// </summary>
		public int SelectedSlot { get; private set; }

		/// <summary> 지금 고른 포탑 종류 번호(포탑 칸이 아니면 뜻이 없다). </summary>
		public int SelectedTowerIndex =>
			SelectedSlot >= 0 && SelectedSlot < slots.Count ? slots[SelectedSlot].TowerIndex : 0;

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
			hasPendingTouchTarget = false;
		}

		// TASK-WM-200 — 손가락으로 지을 때 「한 번 더 눌러 확인」을 기다리는 자리.
		private bool hasPendingTouchTarget;
		private Vector3 pendingTouchTarget;

		/// <summary>
		/// 손가락이 이번에 고른 자리 — 「여기 지을까?」 하고 기다리는 중이면 true (화면이 안내 문구를 바꾼다).
		/// </summary>
		public bool IsWaitingTouchConfirm => hasPendingTouchTarget;

		public event System.Action<int> SelectionChanged = delegate { };

		/// <summary> 슬롯 선택 — 범위를 벗어나면 무시(없는 칸을 누른 것). </summary>
		public void SelectSlot(int slot)
		{
			// 열려 있는 칸만 고를 수 있다 — 없는 칸을 누른 것은 아무 일도 아니다.
			if (slot < 0 || slot >= slots.Count)
				return;
			if (SelectedSlot == slot)
			{
				IsArmed = true; // 같은 칸을 다시 누르면 「또 짓겠다」는 뜻이다.
				return;
			}

			SelectedSlot = slot;
			IsArmed = true; // 칸을 고르는 것 = 설치 대기. 한 번 지으면 다시 꺼진다.
			hasPendingTouchTarget = false; // 다른 것을 고르면 기다리던 자리는 뜻을 잃는다.
			SelectionChanged(slot);
		}

		/// <summary> 종류로 고르기 — 재시작 등이 쓰는 경로. 안 열린 종류면 아무 일도 안 한다. </summary>
		public void SelectKind(TowerDefensePlaceableKind kind)
		{
			for (int index = 0; index < slots.Count; index++)
			{
				if (slots[index].Kind != kind)
					continue;
				SelectSlot(index);
				return;
			}
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
			// 「여기 지을까?」 하고 기다리던 자리는 판을 나가면 뜻을 잃는다 — 남겨 두면 다음 판에서
			// 그 근처를 한 번 톡 하는 순간 확인 없이 지어진다(옛 대답으로 새 질문에 답하는 꼴).
			hasPendingTouchTarget = false;
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
		public MatchCombatant HoveredUnit { get; private set; }

		/// <summary>
		/// 지금 고른 건물(없으면 null) — 「이미 서 있는 것에 하는 일」(연구·강화)이 여기에 붙는다.
		/// 무장하지 않은 클릭이 곧 선택이라, 짓는 손동작과 고르는 손동작이 섞이지 않는다.
		/// </summary>
		public MatchCombatant SelectedBuilding { get; private set; }

		/// <summary> 밖에서 건물을 골라 준다 — 「연구」 버튼처럼 화면이 여는 문. </summary>
		public void SelectBuilding(MatchCombatant building)
		{
			SelectedBuilding = building;
			IsArmed = false; // 고르는 중엔 설치 대기가 아니다(다음 클릭이 건물을 세우면 안 된다).
			BuildingSelected(SelectedBuilding);
		}
		public event System.Action<MatchCombatant> BuildingSelected = delegate { };
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

			MatchCombatant nearest = null;
			float nearestDistance = float.MaxValue;
			for (int index = 0; index < hitCount; index++)
			{
				MatchCombatant combatant = hoverHits[index].collider.GetComponentInParent<MatchCombatant>();
				if (combatant == null || hoverHits[index].distance >= nearestDistance)
					continue;

				nearest = combatant;
				nearestDistance = hoverHits[index].distance;
			}

			HoveredUnit = nearest;
		}

		/// <summary>
		/// 입력관리자를 늦게라도 확보한다 — 주입이 안 온 경우의 자가 복구.
		/// ★ 이것이 null 이면 미리보기·hover 가 통째로 죽는데, 아무 소리도 안 난다.
		/// </summary>
		private void EnsureInputManager()
		{
			if (inputManager != null)
				return;

			if (InputManager.TryGetExistingInstance(out InputManager found))
				inputManager = found;
		}

		// 커서 아래 유닛 탐색 버퍼 — 매 프레임 새 배열을 만들지 않는다.
		private readonly RaycastHit[] hoverHits = new RaycastHit[16];

		/// <summary>
		/// 손가락 조작 (TASK-WM-200). 마우스와 규칙이 다른데, 그 차이가 *손가락에만 있는 사정*에서 나온다.
		///
		/// ★ 왜 「톡」이고 「누름」이 아닌가: 손가락 하나가 시점 끌기도 겸한다. 누르는 순간 짓게 하면
		///   판을 훑어보려고 끌 때마다 건물이 서고 자원이 빠진다. 그래서 *끌지 않고 뗀 것*만 짓는 손짓이다.
		/// ★ 왜 두 번 눌러야 하나: 마우스는 커서를 올려두고 「여기 서겠구나」를 *보고 나서* 누른다.
		///   손가락엔 그 「올려두기」가 없다 — 첫 톡이 그 자리를 맡고, 둘째 톡이 짓는다. 미리보기의
		///   뜻(짓기 전에 본다)을 없애지 않고 손가락으로 옮긴 것이다.
		/// ★ 왜 빈 땅 톡이 영웅인가: 손가락엔 오른쪽 단추가 없다. 없는 단추를 흉내 내는 대신,
		///   *가리킨 곳에 아무것도 없다*는 사실에 뜻을 준다 — 건물 위 톡은 그대로 「살펴보기」다.
		/// </summary>
		private void HandleTouchTap()
		{
			if (inputManager == null || inputManager.IsTouchMode == false)
				return;
			if (inputManager.PointerTappedThisFrame == false)
				return;

			Vector2 tapPosition = inputManager.PointerTapPosition;
			if (UIPointer.IsOverInteractive(tapPosition))
				return;

			if (IsArmed == false)
			{
				UpdateHover(tapPosition);
				if (HoveredUnit != null)
				{
					SelectedBuilding = HoveredUnit;
					BuildingSelected(SelectedBuilding);
				}
				else
				{
					CommandHeroAt(tapPosition);
				}
				return;
			}

			if (TryGetSnappedGroundPosition(tapPosition, out Vector3 tappedCell) == false)
				return;

			// 같은 칸을 다시 톡 = 「그래, 여기」. 칸 반쪽 안이면 같은 칸으로 본다(손가락은 뭉툭하다).
			float sameCellRadius = cellSize * 0.5f;
			if (hasPendingTouchTarget
				&& (pendingTouchTarget - tappedCell).sqrMagnitude <= sameCellRadius * sameCellRadius)
			{
				hasPendingTouchTarget = false;
				PlaceSelectedAt(tapPosition);
				return;
			}

			// 첫 톡 — 아직 짓지 않는다. 미리보기가 그 자리로 옮겨가 「여기 서면 이렇다」를 보여준다.
			hasPendingTouchTarget = true;
			pendingTouchTarget = tappedCell;
		}

		private void Update()
		{
			if (isActive == false)
				return;

			// ★ 손가락 입력을 *미리보기 유령*보다 먼저 처리한다 (2026-08-07 실기: 개척에서 확대·축소
			//   말고는 아무것도 안 먹었다). 예전엔 미리보기를 못 만들면 여기서 통째로 돌아가서,
			//   미리보기가 필요 없는 것들 — 영웅에게 「저기로」, 건물 살펴보기 — 까지 같이 죽었다.
			//   보여주는 장치가 없다고 조작이 사라지면 안 된다. 그 둘은 원래 상관이 없다.
			EnsureInputManager();
			HandleTouchTap();

			EnsurePreviewMarker();
			if (previewMarker == null)
				return;

			// ★ 입력관리자가 없으면 아래 판정이 통째로 「못 그림」으로 떨어져 *미리보기가 조용히 사라진다*
			//   (실측: inputManager=null 이라 매 프레임 스스로 꺼지고 있었다 — 사용자 실증 "설치
			//   미리보기 동작 안하는 것 같은데"). 주입이 안 왔으면 스스로 찾는다. 없는 것보다 낫고,
			//   무엇보다 *조용히 없어지는 것*보다 낫다. (위에서 이미 확보한다 — 여기서 또 부르면
			//   같은 일을 두 번 하고, 한 프레임에 톡이 두 번 먹혀 두 칸이 지어진다.)

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
			// ★ 안개판보다 *위에* 올린다 (사용자 실측 4회: 마커가 계속 안개에 가려짐).
			//   앞선 세 번은 그리는 순서·깊이로만 풀려 했는데 안 됐다 — 안개는 바닥 위 얇게 떠 있는
			//   판이고, 마커가 그보다 아래 있으면 무슨 수를 써도 아래에 깔린다. 높이로 푸는 게 확실하다.
			float markerHeight = (stage != null ? stage.FogHeight : 0.06f) + 0.04f;
			previewMarker.transform.position = (snappedWorldPosition + new Vector3(0f, markerHeight, 0f)).ToUnity();
			UpdatePreviewRing(snappedWorldPosition);
			UpdateGhostBuilding(snappedWorldPosition);

			// 유효/무효 프리뷰 색 — 판정은 전부 match 재사용(이중화 X).
			// 「지을 수 있나」는 이제 셋이다: 칸이 비었나 / 암반이 아닌가 / *보급이 닿나*.
			if (match != null)
			{
				// ★ 판정을 여기서 다시 조립하지 않는다 — 예전엔 그러다 판 끝 검사를 빠뜨려
				//   가장자리에서 초록불이 켜지는데 실제로는 거절됐다. 규칙에게 그대로 묻는다.
				bool canBuild = match.IsCellOccupied(snappedWorldPosition) == false
					&& match.CanBuildAt(snappedWorldPosition);

				Renderer previewRenderer = previewMarker.GetComponentInChildren<Renderer>();
				if (previewRenderer != null)
					previewRenderer.material.color = canBuild ? Color.green : Color.red;

				TintGhost(canBuild);
			}
		}
	}
}
