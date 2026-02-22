using System.Collections;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Animations;

namespace WitchMendokusai
{
	// 빛 설정 낮: 255 190 112
	// 빛 설정 밤: 114, 73, 137

	// TODO: 둘 중 하나만 보이도록 Editor 스크립트 작성
	// public enum CameraType
	// {
	// 	None = -1,

	// 	Content = 0,
	// 	UI = 1,
	// }

	public enum ContentCameraMode
	{
		None = -1,

		Normal = 0,
		Dungeon = 1,
	}

	public enum UICameraMode
	{
		None = -1,

		NPC = 0,

		Tab = 20,
	}

	public class CameraManager : Singleton<CameraManager>
	{
		[SerializeField] private CinemachineBrain cinemachineBrain;
		[SerializeField] private PositionConstraint[] posDelegates;
		[SerializeField] private CinemachineImpulseSource impulseSource;
		[SerializeField] private CinemachineTargetGroup chatTargetGroup;
		[SerializeField] private float xDiff = 3f;

		private CinemachinePositionComposer chatPositionTransposer;
		private Coroutine chatXCoroutine;
		private float targetChatX = 0f;

		private MCamera[] cameras;
		private MCamera curCamera;

		protected override void Awake()
		{
			base.Awake();

			// Init
			cameras = GetComponentsInChildren<MCamera>(false); // 활성화된 것만
			cinemachineBrain.UpdateMethod = CinemachineBrain.UpdateMethods.FixedUpdate;
			chatPositionTransposer = cameras.First(cam => cam.UICameraMode == UICameraMode.NPC).CinemachineCamera.GetCinemachineComponent(CinemachineCore.Stage.Body) as CinemachinePositionComposer;

			SetContentCameraMode(ContentCameraMode.Normal);
		}

		private void Start()
		{
			// Init
			posDelegates[0].SetSource(0, new ConstraintSource { sourceTransform = Player.Instance.Object.CameraPosition, weight = 1 });
			posDelegates[1].SetSource(0, new ConstraintSource { sourceTransform = Player.Instance.Object.SpritePosition, weight = 1 });
		}

		private void OnEnable()
		{
			InputManager.Instance.RegisterInputEvent(InputEventType.Scroll, InputEventResponseType.Performed, Zoom);
		}

		private void OnDisable()
		{
			InputManager.Instance.UnregisterInputEvent(InputEventType.Scroll, InputEventResponseType.Performed, Zoom);
		}

		public void SetContentCameraMode(ContentCameraMode mode)
		{
			// 카메라 설정
			int curCameraIndex = (int)mode;
			curCamera = cameras[curCameraIndex];

			// 카메라 블렌딩 설정 (던전일 경우 Cut, 그 외 EaseInOut)
			cinemachineBrain.DefaultBlend.Style = curCamera.BlendStyle;

			// 카메라 우선순위 설정 (사실상 카메라 변경)
			{
				for (int camIndex = 0; camIndex < cameras.Length; camIndex++)
				{
					bool isCurCamera = camIndex == curCameraIndex;
					cameras[camIndex].CinemachineCamera.Priority = isCurCamera ? 10 : 0;
				}
			}
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

		private void LateUpdate()
		{
			Vector3 direction = (Player.Instance.transform.position - cinemachineBrain.transform.position).normalized;
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

		public void Zoom()
		{
			curCamera.Zoom();
		}

		public void 뽀삐뽀삐뽀()
		{
			impulseSource.GenerateImpulse();
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