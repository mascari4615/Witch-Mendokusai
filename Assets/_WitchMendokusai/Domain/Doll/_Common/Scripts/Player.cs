using UnityEngine;
using VContainer;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	[DefaultExecutionOrder(-100)]
	public class Player : MonoBehaviour
	{
		[SerializeField] private bool dontDestroyOnLoad = false;

		public PlayerObject Object { get; private set; }
		public PlayerRotation Rotation { get; private set; }
		[field: SerializeField] public GameObject ExpCollider { get; private set; }

		private PlayerInteraction interaction;
		private PlayerAim aim;

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
			aim = new(transform, inputManager, ObjectBufferManager.GetObjects(ObjectType.Monster), ObjectBufferManager.GetObjects(ObjectType.ResourceNode));

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
			AimPos = aim.CalcAim(useAutoAim: IsAutoAim);
			AimDirection = aim.CalcAimDirection(useAutoAim: IsAutoAim);
			NearestTarget = aim.GetNearestTarget()?.transform;

			CalcMoveDirection();
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
			Object.UnitMovement.SetMoveDirection(inputManager.MoveInput);
		}

		public void SetSprinting(bool isSprinting)
		{
			UnitStat[UnitStatType.IS_SPRINTING] = isSprinting ? 1 : 0;
		}

		public void SetCrouching(bool isCrouching)
		{
			UnitStat[UnitStatType.IS_CROUCHING] = isCrouching ? 1 : 0;
		}
	}
}
