using System;
using System.Collections;
using System.Linq;
using MessagePipe;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Animations;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	public class CameraManager : MonoBehaviour
	{
		public static CameraManager Instance { get; private set; }

		public static bool TryGetExistingInstance(out CameraManager mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		[SerializeField] private CinemachineBrain cinemachineBrain;
		[SerializeField] private PositionConstraint[] posDelegates;
		[SerializeField] private CinemachineImpulseSource impulseSource;
		[SerializeField] private CinemachineTargetGroup chatTargetGroup;
		[SerializeField] private float xDiff = 3f;

		[Header("카메라 모드 매트릭스 (TASK-WM-163)")]
		[Tooltip("전 vcam 부모 (Cameras holder) — pitch 적용 pivot. yaw 는 이 CameraManager 자신(yaw 루트)에 적용.")]
		[SerializeField] private Transform pitchPivot;
		[Tooltip("1인칭 vcam (Phase 2 셋업). null 이면 perspective 토글은 스프라이트 숨김만 수행.")]
		[SerializeField] private MCamera firstPersonCamera;
		[Tooltip("PointAndClick Q/E yaw 속도 (deg/sec).")]
		[SerializeField] private float yawKeySpeed = 150f;
		[Tooltip("PointAndClick 카메라 yaw 추종 부드러움 (클수록 빠름).")]
		[SerializeField] private float pointClickYawSmooth = 15f;
		[Tooltip("MouseLook 마우스 X 1픽셀당 yaw 변화량 (deg).")]
		[SerializeField] private float mouseYawSensitivity = 0.15f;
		[Tooltip("MouseLook 마우스 Y 1픽셀당 pitch 변화량 (deg).")]
		[SerializeField] private float mousePitchSensitivity = 0.12f;
		[Tooltip("pitch 추종 부드러움 (클수록 즉각적).")]
		[SerializeField] private float pitchSmooth = 30f;
		[Tooltip("pitch 하한 (위로 본 한계, 음수 = 위).")]
		[SerializeField] private float minPitch = -60f;
		[Tooltip("pitch 상한 (아래로 본 한계).")]
		[SerializeField] private float maxPitch = 70f;
		[Tooltip("1인칭 시 firstPersonCamera priority (Content vcam 10 보다 높게).")]
		[SerializeField] private int firstPersonPriority = 50;
		[Tooltip("1인칭 카메라 눈 높이 — HeadAnchor 위로 이만큼(월드 up). 머리 위치 조정. TASK-WM-163.")]
		[SerializeField] private float firstPersonEyeHeight = 0.45f;

		private CinemachinePositionComposer chatPositionTransposer;
		private Coroutine chatXCoroutine;
		private float targetChatX = 0f;

		private MCamera[] cameras;
		private MCamera curCamera;

		private Transform target;
		private Transform headAnchor; // 1인칭 카메라 직접 구동 대상 (보간된 머리 위치).
		private IDisposable playerSpawnedSub;
		private IDisposable playerDespawnedSub;

		// TASK-WM-163 — 카메라 모드 상태 (단일 권위자).
		private InputManager inputManager;
		public CameraControlMode ControlMode { get; private set; } = CameraControlMode.PointAndClick;
		public CameraPerspective Perspective { get; private set; } = CameraPerspective.ThirdPerson;
		public event Action<CameraControlMode> OnControlModeChanged = delegate { };
		public event Action<CameraPerspective> OnPerspectiveChanged = delegate { };

		private float yaw;   // 누적 yaw 목표 (deg). PlayerRotation 이 body 회전에 사용.
		private float pitch; // 누적 pitch 목표 (deg, MouseLook 한정). + = 아래.

		/// <summary>PlayerRotation body 회전용 — 평면 yaw (pitch 무시).</summary>
		public Quaternion FlatYawRotation => Quaternion.Euler(0f, yaw, 0f);

		/// <summary>현재 content 카메라 모드 — 자유 위치 카메라 컨트롤러(CityView/FreeFly)가 자기 활성 판단에 사용. TASK-WM-193.</summary>
		public ContentCameraMode CurrentContentMode => curCamera != null ? curCamera.ContentCameraMode : ContentCameraMode.None;

		/// <summary>
		/// 자유 위치 카메라(아바타 추종 궤도 밖) 모드인지. TASK-WM-193 / TASK-WM-194.
		/// 이 모드에서는 추종 리그의 yaw/pitch 구동을 멈춘다 — 안 멈추면 Cameras 홀더(=pitch pivot)가
		/// 돌면서 무대 고정 시점(투기장)이나 부감 시점이 플레이어 조작에 딸려 돌아간다.
		/// 게임 속 게임(투기장·개척)은 진입 순간 그 게임이 화면의 주체이므로 여기 포함된다.
		/// </summary>
		public bool IsFreePositionMode =>
			CurrentContentMode == ContentCameraMode.CityView
			|| CurrentContentMode == ContentCameraMode.FreeFly
			|| CurrentContentMode == ContentCameraMode.Arena
			|| CurrentContentMode == ContentCameraMode.TowerDefense;

		[Inject]
		public void Construct(ISubscriber<PlayerSpawnedEvent> spawnedSubscriber, ISubscriber<PlayerDespawnedEvent> despawnedSubscriber, InputManager inputManager)
		{
			this.inputManager = inputManager;
			playerSpawnedSub = spawnedSubscriber.Subscribe(OnPlayerSpawned);
			playerDespawnedSub = despawnedSubscriber.Subscribe(OnPlayerDespawned);
		}

		private void Awake()
		{
			if (Instance != null && Instance != this) { Destroy(gameObject); return; }
			Instance = this;

			// Init
			cameras = GetComponentsInChildren<MCamera>(false); // 활성화된 것만
			cinemachineBrain.UpdateMethod = CinemachineBrain.UpdateMethods.FixedUpdate;
			chatPositionTransposer = cameras.First(cam => cam.UICameraMode == UICameraMode.NPC).CinemachineCamera.GetCinemachineComponent(CinemachineCore.Stage.Body) as CinemachinePositionComposer;

			// TASK-WM-163 — yaw 누적 baseline = 현재 루트 yaw (prefab 0). pitch = 0 (PointAndClick baked).
			yaw = transform.eulerAngles.y;
			pitch = 0f;

			SetContentCameraMode(ContentCameraMode.Normal); // 내부에서 ApplyPerspective 호출
		}

		private void OnDestroy()
		{
			playerSpawnedSub?.Dispose();
			playerDespawnedSub?.Dispose();

			if (Instance == this)
				Instance = null;
		}

		private void OnPlayerSpawned(PlayerSpawnedEvent evt)
		{
			target = evt.Transform;
			headAnchor = evt.HeadAnchor;
			posDelegates[0].SetSource(0, new ConstraintSource { sourceTransform = evt.CameraPosition, weight = 1 });
			posDelegates[1].SetSource(0, new ConstraintSource { sourceTransform = evt.HeadAnchor, weight = 1 });
		}

		private void OnPlayerDespawned(PlayerDespawnedEvent evt)
		{
			target = null;
			headAnchor = null;
		}

		// === TASK-WM-163 — 카메라 모드 매트릭스 ===

		private void UpdateCameraOrientation()
		{
			if (inputManager == null)
				return;

			// 자유 위치 카메라(부감/자유비행)는 전용 컨트롤러가 vcam 을 직접구동 — 추종 리그 yaw/pitch·1인칭 구동 비활성. TASK-WM-193.
			if (IsFreePositionMode)
				return;

			if (ControlMode == CameraControlMode.MouseLook)
			{
				Vector2 look = inputManager.LookDelta;
				yaw += look.x * mouseYawSensitivity;
				pitch += -look.y * mousePitchSensitivity;
				pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

				// MouseLook = 마우스 델타 1:1 즉시 반영 (Lerp 지연 X — "딱딱 안 움직임" fix WM-163).
				// yaw = 루트 / pitch = pitchPivot(Cameras holder).
				transform.rotation = Quaternion.Euler(0f, yaw, 0f);
				if (pitchPivot != null)
					pitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
			}
			else
			{
				yaw += Time.deltaTime * yawKeySpeed * inputManager.CameraRotateInput;

				// PointAndClick = Q/E 부드럽게 (기존 느낌). pitch 는 0 으로 복귀(vcam baked 각도 유지).
				transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0f, yaw, 0f), Time.deltaTime * pointClickYawSmooth);
				if (pitchPivot != null)
					pitchPivot.localRotation = Quaternion.Lerp(pitchPivot.localRotation, Quaternion.identity, Time.deltaTime * pitchSmooth);
			}

			// 1인칭 = vcam 위치/회전을 직접 구동 (Cinemachine Follow/constraint 체인 우회 → jitter 근절).
			// 위치 = 보간된 머리(+eye height), 회전 = 같은 프레임 yaw/pitch → pos·rot 동일프레임 단일소스 정합.
			if (Perspective == CameraPerspective.FirstPerson && firstPersonCamera != null && headAnchor != null)
			{
				firstPersonCamera.transform.SetPositionAndRotation(
					headAnchor.position + Vector3.up * firstPersonEyeHeight,
					Quaternion.Euler(pitch, yaw, 0f));
			}
		}

		private void UpdateCursorState()
		{
			bool lockCursor = ControlMode == CameraControlMode.MouseLook
				&& GameConditionBridge.Get(GameConditionType.IsViewingUI) == false
				&& GameConditionBridge.Get(GameConditionType.IsPaused) == false
				&& GameConditionBridge.Get(GameConditionType.IsTyping) == false;

			Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
			Cursor.visible = lockCursor == false;
		}

		public void ToggleControlMode()
		{
			SetControlMode(ControlMode == CameraControlMode.PointAndClick
				? CameraControlMode.MouseLook
				: CameraControlMode.PointAndClick);
		}

		public void SetControlMode(CameraControlMode mode)
		{
			if (ControlMode == mode)
				return;

			ControlMode = mode;

			// PointAndClick 복귀 시 누적 pitch 리셋 (3인칭 baked 각도로 복원).
			if (mode == CameraControlMode.PointAndClick)
				pitch = 0f;

			OnControlModeChanged.Invoke(mode);
			EventBusBridge.Publish(new CameraControlModeChangedEvent { Mode = mode });
		}

		public void TogglePerspective()
		{
			SetPerspective(Perspective == CameraPerspective.ThirdPerson
				? CameraPerspective.FirstPerson
				: CameraPerspective.ThirdPerson);
		}

		public void SetPerspective(CameraPerspective perspective)
		{
			// 1인칭 vcam 미설정(Phase 2)이면 토글 무효 — 카메라는 3인칭인데 스프라이트만 사라지는
			// 반쪽 상태 방지. firstPersonCamera serialize ref 배선되면 활성.
			if (firstPersonCamera == null)
				return;

			if (Perspective == perspective)
				return;

			Perspective = perspective;
			ApplyPerspective();

			OnPerspectiveChanged.Invoke(perspective);
			EventBusBridge.Publish(new CameraPerspectiveChangedEvent
			{
				Perspective = perspective,
				IsFirstPerson = perspective == CameraPerspective.FirstPerson,
			});
		}

		private void ApplyPerspective()
		{
			bool isFirstPerson = Perspective == CameraPerspective.FirstPerson;

			// 1인칭 vcam priority boost (Content 10 보다 높게). null = Phase 2 미셋업.
			if (firstPersonCamera != null)
				firstPersonCamera.CinemachineCamera.Priority = isFirstPerson ? firstPersonPriority : 0;

			// 1인칭 = 매 렌더 프레임 갱신(LateUpdate)으로 마우스룩 stepping 제거.
			// 3인칭 = FixedUpdate (rigidbody 추종 캐릭터와 물리틱 동기 — 기존 OK 보존).
			cinemachineBrain.UpdateMethod = isFirstPerson
				? CinemachineBrain.UpdateMethods.LateUpdate
				: CinemachineBrain.UpdateMethods.FixedUpdate;
		}

		public void SetContentCameraMode(ContentCameraMode mode)
		{
			// content 카메라 선택 = enum 값(ContentCameraMode 필드) 기반 — 위치 인덱싱(cameras[(int)mode]) 폐기.
			// cameras[] = GetComponentsInChildren<MCamera> 라 content(Adventure/Dungeon) + UI(NPC/Tab) 혼합 배열 →
			// 위치 ≠ enum (CityView=2→Camera_NPC, Arena=3→Camera_Tab 오선택 잠복버그). 필드 기반은 계층 순서 무관 +
			// content 카메라 추가/재배치에 견고. Normal/Dungeon 은 필드로도 동일 카메라(동작 무변경). TASK-WM-165 item9.
			curCamera = cameras.First(cam => cam.ContentCameraMode == mode);

			// 카메라 블렌딩 설정 (던전일 경우 Cut, 그 외 EaseInOut)
			cinemachineBrain.DefaultBlend.Style = curCamera.BlendStyle;

			// 카메라 우선순위 설정 (사실상 카메라 변경) — 기존 동작 보존: current=10, 그 외 전부 0. 식별만 필드 기반.
			foreach (MCamera cam in cameras)
				cam.CinemachineCamera.Priority = cam == curCamera ? 10 : 0;

			// content 루프가 firstPersonCamera priority 도 0 으로 덮으므로 1인칭 상태 재확정.
			ApplyPerspective();
		}

		private int uiCameraPriorityStack = 0;
		public void SetUICameraMode(UICameraMode mode, bool isActive)
		{
			const int PriorityOffset = 1000;

			// UI 카메라 우선순위 설정
			{
				MCamera cam = cameras.FirstOrDefault(c => c.UICameraMode == mode);
				cam.CinemachineCamera.Priority = isActive ? (++uiCameraPriorityStack + PriorityOffset) : 0;
			}
		}

		public void SetNPC(Transform npcTransform)
		{
			chatTargetGroup.Targets[1].Object = npcTransform;
		}

		public void SetSelecting(bool isSelecting, bool shouldAnimate = true)
		{
			// 설정해야하는 경우, 기존 코루틴 중지
			if (chatXCoroutine != null)
				StopCoroutine(chatXCoroutine);

			float targetX = isSelecting ? xDiff : 0;

			// 이미 목표 위치가 같은 경우 처리하지 않음
			if (targetX == targetChatX)
			{
				if (shouldAnimate == false)
				{
					chatPositionTransposer.TargetOffset.x = targetX;
				}
				return;
			}
			targetChatX = targetX;

			// 이미 목표 위치에 도달한 경우 처리하지 않음
			if (Mathf.Approximately(targetX, chatPositionTransposer.TargetOffset.x))
			{
				chatPositionTransposer.TargetOffset.x = targetX;
				return;
			}

			// 코루틴 실행 또는 즉시 설정
			if (shouldAnimate)
				chatXCoroutine = StartCoroutine(ChatXCoroutine(targetX));
			else
				chatPositionTransposer.TargetOffset.x = targetX;
		}

		// 유닛이 말풍선을 띄울 때 카메라 이동 (2차원 기준 X축)
		private IEnumerator ChatXCoroutine(float targetX)
		{
			float startX = chatPositionTransposer.TargetOffset.x;
			float elapsed = 0f; // 경과 시간
			const float duration = 0.2f; // 이동에 걸리는 시간

			while (elapsed < duration)
			{
				yield return null;
				elapsed += Time.deltaTime;
				float t = Mathf.Clamp01(elapsed / duration);
				chatPositionTransposer.TargetOffset.x = Mathf.Lerp(startX, targetX, t);
			}

			chatPositionTransposer.TargetOffset.x = targetX;
			chatXCoroutine = null;
		}

		private void Update()
		{
			UpdateCursorState();
		}

		private void LateUpdate()
		{
			UpdateCameraOrientation();

			if (target == null)
				return;

			Vector3 direction = (target.position - cinemachineBrain.transform.position).normalized;
			// RaycastHit[] hits = Physics.RaycastAll(cinemachineBrain.transform.position, direction, Mathf.Infinity, 1 << LayerMask.NameToLayer("EnvironmentObject"));
			RaycastHit[] hits = Physics.RaycastAll(cinemachineBrain.transform.position, direction, Mathf.Infinity);

			for (int i = 0; i < hits.Length; i++)
			{
				TransparentObject[] obj = hits[i].transform.GetComponentsInChildren<TransparentObject>();

				for (int j = 0; j < obj.Length; j++)
				{
					obj[j]?.UpdateTransparent();
				}
			}
		}

		/// <summary>마을 경영 시점 순환 (F6): Normal → CityView(부감) → FreeFly(자유비행) → Normal. TASK-WM-193.
		/// Dungeon/Arena 등 특수 모드 중엔 Normal 로 복귀.</summary>
		public void CycleContentView()
		{
			ContentCameraMode next = CurrentContentMode switch
			{
				ContentCameraMode.Normal => ContentCameraMode.CityView,
				ContentCameraMode.CityView => ContentCameraMode.FreeFly,
				ContentCameraMode.FreeFly => ContentCameraMode.Normal,
				_ => ContentCameraMode.Normal,
			};
			SetContentCameraMode(next);
		}

		public void Zoom()
		{
			curCamera.Zoom();
		}

		public void 뽀삐뽀삐뽀()
		{
			impulseSource.GenerateImpulse();
		}

		public void GenerateCameraImpulse(float amplitude)
		{
			impulseSource.GenerateImpulse(Mathf.Max(0f, amplitude));
		}

#if UNITY_EDITOR
		[ContextMenu("SetCameraNormal")]
		private void SetCameraNormal_Editor() => SetContentCameraMode(ContentCameraMode.Normal);
		[ContextMenu("SetCameraDungeon")]
		private void SetCameraDungeon_Editor() => SetContentCameraMode(ContentCameraMode.Dungeon);
		[ContextMenu("SetCameraDialogue True")]
		private void SetCameraDialogue_Editor() => SetUICameraMode(UICameraMode.NPC, true);
		[ContextMenu("SetCameraDialogue False")]
		private void SetCameraDialogueFalse_Editor() => SetUICameraMode(UICameraMode.NPC, false);
		[ContextMenu("SetCameraTab True")]
		private void SetCameraTab_Editor() => SetUICameraMode(UICameraMode.Tab, true);
		[ContextMenu("SetCameraTab False")]
		private void SetCameraTabFalse_Editor() => SetUICameraMode(UICameraMode.Tab, false);
#endif
	}
}