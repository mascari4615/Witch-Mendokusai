using UnityEngine;
using VContainer;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	[DefaultExecutionOrder(-100)]
	public class Player : MonoBehaviour
	{
		[SerializeField] private bool dontDestroyOnLoad = false;
		[SerializeField] private float aimMaxDistance = 100f;
		[SerializeField, Tooltip("EXP_COLLIDER_SCALE 1당 획득 범위 증가 배율")] private float expColliderScalePerStack = 0.5f;

		[Header("Double Tap Sprint")]
		[SerializeField, Min(0f), Tooltip("이동 키 두 번 누름으로 달리기. 첫 누름과 두 번째 누름 사이 최대 시간 (초)")]
		private float doubleTapWindowSeconds = 0.3f;
		[SerializeField, Range(-1f, 1f), Tooltip("두 번 누른 방향이 같다고 볼 기준. 1 이면 완전히 같은 방향만")]
		private float doubleTapSameDirectionMinDot = 0.7f;
		[SerializeField, Range(0.01f, 1f), Tooltip("이동 입력이 이 세기 이상이면 누른 것으로 봄")]
		private float doubleTapPressThreshold = 0.5f;

		public PlayerObject Object { get; private set; }
		public PlayerRotation Rotation { get; private set; }
		[field: SerializeField] public GameObject ExpCollider { get; private set; }

		private PlayerInteraction interaction;
		private PlayerAim aim;
		// 달리기 키 누름 여부. 실제 달리기는 ApplySprint 가 던전 여부와 합쳐 정함
		private bool isSprintRequested;
		private readonly MoveDoubleTapSprint doubleTapSprint = new();

		public Vector3 AimDirection { get; private set; }
		public Vector3 AimPos { get; private set; }
		public bool IsAutoAim { get; private set; }
		public Transform NearestTarget { get; private set; }

		public UnitStat UnitStat => Object.UnitStat;

		private PlayerProvider playerProvider;
		private DataManager dataManager;
		private InputManager inputManager;

		[Inject]
		public void Construct(PlayerProvider playerProvider, DataManager dataManager, InputManager inputManager, IObjectResolver container)
		{
			this.playerProvider = playerProvider;
			this.dataManager = dataManager;
			this.inputManager = inputManager;
			// TASK-WM-115 R3a — Object/Rotation 을 SetCurrent *전* 확정.
			// injected Player 는 Construct 가 Awake 보다 먼저 (데이터 입증) → Awake 에서 set 하면
			// SetCurrent 시 Object null. 자식 cascade(아래) 도 Construct 시작이라 여기가 최早 단일 정본.
			Object = GetComponent<PlayerObject>();
			Rotation = GetComponent<PlayerRotation>();
			playerProvider.SetCurrent(this);
			aim = new(transform, inputManager, aimMaxDistance, ObjectBufferManager.GetObjects(ObjectType.Monster), ObjectBufferManager.GetObjects(ObjectType.ResourceNode));

			// Player.prefab 자식/형제 컴포넌트 cascade Inject (PlayerObject/PlayerRotation/DollAnimator/
			// InteractiveMarker/AutoAimMarker/UnitMovement 등). RegisterComponentInHierarchy<Player> 는 Player
			// 컴포넌트만 inject — 자식은 컨테이너가 모름. 컨테이너 주입 root 가 비등록 자식 cascade =
			// UIRoot.Construct / ObjectPoolManager.InjectGameObject 와 동일 canonical 패턴 (TASK-WM-078, 2026-05-16).
			//
			// raw InjectGameObject 대신 self-exclude 정본 헬퍼 사용 — 자기 Construct 재진입으로 인한
			// 무한 재귀(StackOverflow) 차단. TASK-WM-109-B / ObjectResolverHierarchyExtensions.
			container.InjectGameObjectExcludingSelf(gameObject, this);
		}

		private void Awake()
		{
			interaction = new(transform);
			// Object/Rotation = Construct 단일 정본 (TASK-WM-115 R3a — Awake<Construct 순서).

			if (dontDestroyOnLoad == true)
				DontDestroyOnLoad(gameObject);

			EventBusBridge.Subscribe<PlayerJumpRequestedEvent>(OnJumpRequested);
			EventBusBridge.Subscribe<PlayerJumpReleasedEvent>(OnJumpReleased);
			EventBusBridge.Subscribe<PlayerSkillUseRequestedEvent>(OnSkillUseRequested);
			EventBusBridge.Subscribe<PlayerSprintChangedEvent>(OnSprintChanged);
			EventBusBridge.Subscribe<PlayerCrouchChangedEvent>(OnCrouchChanged);
			EventBusBridge.Subscribe<PlayerAutoAimToggledEvent>(OnAutoAimToggled);
			EventBusBridge.Subscribe<PlayerInteractRequestedEvent>(OnInteractRequested);
			EventBusBridge.Subscribe<CameraPerspectiveChangedEvent>(OnCameraPerspectiveChanged);
		}

		private void Start()
		{
			Object.Init(GetDoll(dataManager.CurDollID));

			EventBusBridge.Publish(new PlayerSpawnedEvent
			{
				Transform = transform,
				CameraPosition = Object.CameraPosition,
				HeadAnchor = Object.HeadAnchor,
			});
		}

		private void OnDestroy()
		{
			if (playerProvider != null)
				playerProvider.Clear();

			EventBusBridge.Publish(new PlayerDespawnedEvent());
			EventBusBridge.Unsubscribe<PlayerJumpRequestedEvent>(OnJumpRequested);
			EventBusBridge.Unsubscribe<PlayerJumpReleasedEvent>(OnJumpReleased);
			EventBusBridge.Unsubscribe<PlayerSkillUseRequestedEvent>(OnSkillUseRequested);
			EventBusBridge.Unsubscribe<PlayerSprintChangedEvent>(OnSprintChanged);
			EventBusBridge.Unsubscribe<PlayerCrouchChangedEvent>(OnCrouchChanged);
			EventBusBridge.Unsubscribe<PlayerAutoAimToggledEvent>(OnAutoAimToggled);
			EventBusBridge.Unsubscribe<PlayerInteractRequestedEvent>(OnInteractRequested);
			EventBusBridge.Unsubscribe<CameraPerspectiveChangedEvent>(OnCameraPerspectiveChanged);
		}

		private void OnApplicationFocus(bool hasFocus)
		{
			if (hasFocus == false && Object != null)
			{
				isSprintRequested = false;
				doubleTapSprint.Cancel();
				Object.UnitMovement.CancelMovementInput();
			}
		}

		private void OnDisable()
		{
			isSprintRequested = false;
			doubleTapSprint.Cancel();
			if (Object != null)
				Object.UnitMovement.CancelMovementInput();
		}

		private void OnJumpRequested(PlayerJumpRequestedEvent evt) => TryJump();
		private void OnJumpReleased(PlayerJumpReleasedEvent evt) => StopJump();
		private void OnSkillUseRequested(PlayerSkillUseRequestedEvent evt) => TryUseSkill(evt.SkillIndex);
		private void OnSprintChanged(PlayerSprintChangedEvent evt) => SetSprinting(evt.IsSprinting);
		private void OnCrouchChanged(PlayerCrouchChangedEvent evt) => SetCrouching(evt.IsCrouching);
		private void OnAutoAimToggled(PlayerAutoAimToggledEvent evt) => SetAutoAim(IsAutoAim == false);
		private void OnInteractRequested(PlayerInteractRequestedEvent evt) => TryInteract();
		private void OnCameraPerspectiveChanged(CameraPerspectiveChangedEvent evt) => Object.SetSelfVisible(evt.IsFirstPerson == false);

		private void Update()
		{
			// 최근접 대상 탐색은 프레임당 한 번, 조준 위치와 방향은 그 결과로 계산
			GameObject nearestTarget = aim.GetNearestTarget();
			// 마우스 조준, 조준 모드 (Y) 를 켜면 가장 가까운 적. 클릭 공격 복구와 함께 되살림 (사용자 2026-11-21)
			AimPos = aim.CalcAim(nearestTarget, IsAutoAim);
			AimDirection = aim.CalcAimDirection(AimPos);
			NearestTarget = nearestTarget != null ? nearestTarget.transform : null;

			CalcMoveDirection();
			// 손가락 조작은 뛰기 버튼이 따로 있고, 스틱을 놓았다 다시 미는 일이 잦아서 두 번 누름 판정 끔
			doubleTapSprint.Tick(inputManager.MoveInput, Time.unscaledTime, inputManager.IsTouchMode == false,
				doubleTapWindowSeconds, doubleTapSameDirectionMinDot, doubleTapPressThreshold);
			ApplySprint();
		}

		public void UpdatePickupRange()
		{
			float stackScale = 1f + UnitStat[UnitStatType.EXP_COLLIDER_SCALE] * expColliderScalePerStack;
			ExpCollider.transform.localScale = Vector3.one * (stackScale * UnitStat.PercentMultiplier(UnitStatType.PICKUP_RADIUS_BONUS));
		}

		public void TryInteract()
		{
			interaction.TryInteraction();
		}

		public void TryUseSkill(int skillIndex)
		{
			if (Object.UnitStat[UnitStatType.CASTING_SKILL] > 0)
				return;

			Object.UseSkill(skillIndex);
		}

		public void TryJump()
		{
			if (Object.UnitStat[UnitStatType.DEAD] > 0)
				return;

			if (Object.UnitStat[UnitStatType.CASTING_SKILL] > 0)
				return;

			Object.UnitMovement.TryJump();
		}

		public void StopJump()
		{
			Object.UnitMovement.StopJump();
		}

		public void SetAutoAim(bool isAutoAim)
		{
			Debug.Log($"SetAutoAim: {isAutoAim}");
			IsAutoAim = isAutoAim;
		}

		private void CalcMoveDirection()
		{
			Object.UnitMovement.SetAnalogMoveDirection(inputManager.MoveInput);
		}

		public void SetSprinting(bool isSprinting)
		{
			isSprintRequested = isSprinting;
			ApplySprint();
		}

		/// <summary>
		/// 달리기 키 또는 이동 두 번 누름이면 달림. 생존 던전 안에서는 둘 다 금지 (사용자 2026-11-18 "던전에서는 못달리게").
		/// 매 프레임 다시 맞춤. 마을에서 누른 채 던전에 들어가면 멈추고, 누른 채 나오면 다시 달림
		/// </summary>
		private void ApplySprint()
		{
			bool isRequested = isSprintRequested || doubleTapSprint.IsSprinting;
			int wanted = isRequested && DungeonManagerBridge.IsDungeon == false ? 1 : 0;
			if (UnitStat[UnitStatType.IS_SPRINTING] != wanted)
				UnitStat[UnitStatType.IS_SPRINTING] = wanted;
		}

		public void SetCrouching(bool isCrouching)
		{
			Object.UnitMovement.SetCrouching(isCrouching);
		}
	}
}
