using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace WitchMendokusai
{
	/// <summary>
	/// 도감 디테일 일러스트 영역의 3D 미리보기. 별도 메뉴 카메라 + RenderTexture + 모델 mount.
	/// CodexDetailPanel 좌측이 RT.image 로 받아서 표시. 카드 swap 시 prefab 만 갈음 (카메라/RT 재사용).
	///
	/// 단계 A (현재): 임시 placeholder Cube 자동 생성 — 모든 entry 디테일 진입 시 같은 회전 cube.
	/// 단계 B (예정): entry.PreviewPrefab 로 swap. PreviewPrefab null 이면 Hide() — Detail Panel 이 정적 Icon 폴백.
	/// 단계 C (예정): World Space UIDocument 추가 (모델 옆 라벨 3D anchor).
	/// </summary>
	public class CodexPreviewController : MonoBehaviour
	{
		public static CodexPreviewController Instance { get; private set; }

		public static bool TryGetExistingInstance(out CodexPreviewController mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		private const int RT_RESOLUTION = 512;
		private const int CAMERA_LAYER = 30; // 도감 미리보기 전용 layer (다른 씬 객체 안 비춤)

		private Camera previewCamera;
		private GameObject modelMount;
		private GameObject currentModel;
		private RenderTexture renderTexture;
		private float autoRotateSpeed = 30f;
		private bool isDragging;

		public RenderTexture RenderTexture => renderTexture;

		private void Awake()
		{
			Instance = this;
			BuildStage();
			ShowPlaceholder();
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
		}

		private void BuildStage()
		{
			// 공간 격리 — 메인 씬 Local Volume / Reflection Probe / GI bounds 밖으로.
			transform.position = new Vector3(10000f, 10000f, 10000f);

			renderTexture = new RenderTexture(RT_RESOLUTION, RT_RESOLUTION, 24, RenderTextureFormat.ARGB32)
			{
				name = "CodexPreviewRT",
				antiAliasing = 2,
			};
			renderTexture.Create();

			GameObject cameraObject = new("CodexPreviewCamera");
			cameraObject.transform.SetParent(transform);
			cameraObject.transform.localPosition = new Vector3(0f, 1.2f, -2.5f);
			cameraObject.transform.LookAt(transform.position + Vector3.up * 0.5f);

			previewCamera = cameraObject.AddComponent<Camera>();
			previewCamera.targetTexture = renderTexture;
			previewCamera.clearFlags = CameraClearFlags.SolidColor;
			previewCamera.backgroundColor = new Color(0.12f, 0.13f, 0.16f, 1f);
			previewCamera.cullingMask = 1 << CAMERA_LAYER;
			previewCamera.fieldOfView = 35f;

			// URP — 메인 PP/Volume 영향 격리. 도감 미리보기는 깨끗한 렌더만.
			UniversalAdditionalCameraData cameraData = previewCamera.GetUniversalAdditionalCameraData();
			cameraData.renderType = CameraRenderType.Base;
			cameraData.renderPostProcessing = false;
			cameraData.volumeLayerMask = 0;
			cameraData.requiresColorOption = CameraOverrideOption.Off;
			cameraData.requiresDepthOption = CameraOverrideOption.Off;

			// 카메라 GameObject 에 자동 붙는 AudioListener 가 메인 AudioListener 와 충돌 — 즉시 제거.
			AudioListener audioListener = cameraObject.GetComponent<AudioListener>();
			if (audioListener != null)
				Destroy(audioListener);

			// Directional light 안 만듦 — URP forward+ light culling 에서 cullingMask 가 자주 무시되어
			// 메인 씬에 빛 누수. 대신 cube 를 Unlit material 로 박아 light 무관하게.

			// 평소에는 disable — 활성 시점에만 메인 PP/GI 등 평가에 끼어듦. CodexDetailPanel 이 Activate/Deactivate.
			previewCamera.enabled = false;

			modelMount = new GameObject("CodexPreviewMount");
			modelMount.transform.SetParent(transform);
			modelMount.transform.localPosition = Vector3.zero;
		}

		private void ShowPlaceholder()
		{
			GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
			cube.name = "CodexPreviewPlaceholder";
			cube.transform.SetParent(modelMount.transform);
			cube.transform.localPosition = Vector3.zero;
			cube.transform.localScale = Vector3.one * 0.8f;
			SetLayerRecursive(cube, CAMERA_LAYER);

			// Light 안 만들었으니 Unlit material 로 박아야 보임. URP/Unlit 셰이더.
			Renderer renderer = cube.GetComponent<Renderer>();
			Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
			if (unlitShader != null)
			{
				Material unlitMaterial = new(unlitShader)
				{
					color = new Color(0.6f, 0.65f, 0.75f, 1f),
				};
				renderer.material = unlitMaterial;
			}

			currentModel = cube;
		}

		public void Show(GameObject prefab)
		{
			ClearModel();
			if (prefab == null)
			{
				ShowPlaceholder();
				return;
			}

			currentModel = Instantiate(prefab, modelMount.transform);
			currentModel.SetActive(true); // 풀에 보관된 prefab 이 inactive 일 수 있음
			currentModel.transform.localPosition = Vector3.zero;
			currentModel.transform.localRotation = Quaternion.identity;
			SetLayerRecursive(currentModel, CAMERA_LAYER);
		}

		public void Hide()
		{
			ClearModel();
			ShowPlaceholder();
		}

		/// <summary>Detail 패널이 RT 사용 시작할 때 호출 — 카메라 활성. 평소 disable 유지.</summary>
		public void Activate()
		{
			if (previewCamera != null)
				previewCamera.enabled = true;
		}

		/// <summary>Detail 패널 detach / 다른 모드 진입 시 호출 — 카메라 비활성.</summary>
		public void Deactivate()
		{
			if (previewCamera != null)
				previewCamera.enabled = false;
		}

		private void ClearModel()
		{
			if (currentModel != null)
			{
				Destroy(currentModel);
				currentModel = null;
			}
		}

		private void Update()
		{
			if (modelMount != null && isDragging == false)
				modelMount.transform.Rotate(Vector3.up, autoRotateSpeed * Time.deltaTime);
		}

		public void BeginDrag() => isDragging = true;
		public void EndDrag() => isDragging = false;

		/// <summary>UI 마우스 X 변위 → yaw 회전. 양수 변위 = 시계방향 (오른쪽 드래그 시 모델 오른쪽 회전).</summary>
		public void DragYawDelta(float deltaPixelX)
		{
			if (modelMount == null)
				return;
			modelMount.transform.Rotate(Vector3.up, -deltaPixelX * 0.5f, Space.World);
		}

		private static void SetLayerRecursive(GameObject root, int layer)
		{
			root.layer = layer;
			for (int i = 0; i < root.transform.childCount; i++)
				SetLayerRecursive(root.transform.GetChild(i).gameObject, layer);
		}
	}
}
