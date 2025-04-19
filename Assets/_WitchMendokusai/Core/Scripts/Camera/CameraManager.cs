using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Animations;

namespace WitchMendokusai
{
	public enum CameraType
	{
		Normal,
		Dungeon,
		Dialogue,
	}

	public class CameraManager : Singleton<CameraManager>
	{
		[SerializeField] private CinemachineBrain cinemachineBrain;
		[SerializeField] private MCamera[] cameras;
		[SerializeField] private PositionConstraint[] posDelegates;
		[SerializeField] private CinemachineImpulseSource impulseSource;
		[SerializeField] private float xDiff = .3f;

		private Coroutine chatXCoroutine;
		private CinemachinePositionComposer chatPositionTransposer;

		private MCamera curCamera;

		protected override void Awake()
		{
			base.Awake();

			// Init
			cinemachineBrain.UpdateMethod = CinemachineBrain.UpdateMethods.FixedUpdate;
			chatPositionTransposer = cameras[2].CinemachineCamera.GetCinemachineComponent(CinemachineCore.Stage.Body) as CinemachinePositionComposer;
			curCamera = cameras[0];
		}

		private void Start()
		{
			// Init
			posDelegates[0].SetSource(0, new ConstraintSource { sourceTransform = Player.Instance.Object.CameraPosition, weight = 1 });
			posDelegates[1].SetSource(0, new ConstraintSource { sourceTransform = Player.Instance.Object.SpritePosition, weight = 1 });

			InputManager.Instance.RegisterInputEvent(InputEventType.Scroll, InputEventResponseType.Performed, Zoom);
		}

		public void SetCamera(CameraType cameraType)
		{
			// 카메라 설정
			int curCameraIndex = (int)cameraType;
			curCamera = cameras[curCameraIndex];

			// 카메라 블렌딩 설정 (던전일 경우 Cut, 그 외 EaseInOut)
			{
				bool isDungeon = cameraType == CameraType.Dungeon;
				if (isDungeon)
				{
					CinemachineBlendDefinition.Styles styles = CinemachineBlendDefinition.Styles.Cut;
					cinemachineBrain.DefaultBlend.Style = styles;
				}
				else
				{
					CinemachineBlendDefinition.Styles styles = CinemachineBlendDefinition.Styles.EaseInOut;
					cinemachineBrain.DefaultBlend.Style = styles;
				}
			}

			// 카메라 우선순위 설정 (사실상 카메라 변경)
			{
				for (int camIndex = 0; camIndex < cameras.Length; camIndex++)
				{
					bool isCurCamera = camIndex == curCameraIndex;
					cameras[camIndex].CinemachineCamera.Priority = isCurCamera ? 10 : 0;
				}
			}

			// 유닛이 말풍선을 띄울 때 카메라 이동 (2차원 기준 X축)
			{
				if (chatXCoroutine != null)
					StopCoroutine(chatXCoroutine);
				chatXCoroutine = StartCoroutine(ChatXCoroutine(0));
			}
		}

		public void SetChatCamera()
		{
			if (chatXCoroutine != null)
				StopCoroutine(chatXCoroutine);
			chatXCoroutine = StartCoroutine(ChatXCoroutine(-xDiff));
		}

		// 유닛이 말풍선을 띄울 때 카메라 이동 (2차원 기준 X축)
		private IEnumerator ChatXCoroutine(float diff)
		{
			const float lerpSpeed = 0.03f;
			while (true)
			{
				chatPositionTransposer.Composition.ScreenPosition.x = Mathf.Lerp(chatPositionTransposer.Composition.ScreenPosition.x, diff, lerpSpeed);
				if (Mathf.Abs(chatPositionTransposer.Composition.ScreenPosition.x - diff) < 0.01f)
					break;
				yield return null;
			}
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
	}
}