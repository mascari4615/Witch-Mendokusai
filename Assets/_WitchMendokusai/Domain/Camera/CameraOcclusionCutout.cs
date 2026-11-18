using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	/// <summary>
	/// 캐릭터와 카메라 사이 물체를 캐릭터 둘레 원 안에서 점무늬로 비움.
	/// 값은 전역 셰이더 값, 받는 쪽은 `_WM_OCCLUDABLE` 키워드를 켠 재질 (Core/Shaders/WMOcclusionCutout.hlsl).
	/// 게임 카메라가 아닌 카메라 (씬 뷰, 미리보기) 는 반지름 0.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class CameraOcclusionCutout : MonoBehaviour
	{
		private static readonly int CENTER_ID = Shader.PropertyToID("_WM_CutoutCenter");
		private static readonly int RADIUS_ID = Shader.PropertyToID("_WM_CutoutRadius");
		private static readonly int SOFTNESS_ID = Shader.PropertyToID("_WM_CutoutSoftness");
		private static readonly int DEPTH_ID = Shader.PropertyToID("_WM_CutoutDepth");
		private static readonly int FLOOR_ID = Shader.PropertyToID("_WM_CutoutFloorY");
		private static readonly int MAX_COVERAGE_ID = Shader.PropertyToID("_WM_CutoutMaxCoverage");
		private static readonly int NEAR_DISTANCE_ID = Shader.PropertyToID("_WM_CutoutNearDistance");
		private static readonly int NEAR_SOFTNESS_ID = Shader.PropertyToID("_WM_CutoutNearSoftness");

		[Tooltip("원을 비울 화면. 게임 화면을 그리는 카메라")]
		[SerializeField] private Camera gameCamera;
		[Tooltip("원 반지름. 화면 높이에 대한 비율")]
		[SerializeField, Range(0f, 1f)] private float radius = 0.16f;
		[Tooltip("원 가장자리에서 점이 줄어드는 폭. 화면 높이에 대한 비율")]
		[SerializeField, Range(0f, 1f)] private float softness = 0.08f;
		[Tooltip("원 가운데에서 비우는 비율. 1 이면 가운데가 다 비고, 낮추면 가운데도 점무늬")]
		[SerializeField, Range(0f, 1f)] private float maxCoverage = 1f;
		[Tooltip("원의 중심 높이. 캐릭터 발에서 위로 (m)")]
		[SerializeField, Min(0f)] private float centerHeight = 0.6f;
		[Tooltip("캐릭터보다 이만큼 더 카메라 쪽에 있는 것만 비움 (m). 캐릭터 바로 옆 물체가 같이 비지 않게")]
		[SerializeField, Min(0f)] private float depthMargin = 0.6f;
		[Tooltip("캐릭터 발보다 이만큼 위의 면만 비움 (m). 발밑 바닥과 계단이 뚫리지 않게")]
		[SerializeField] private float floorMargin = 0.15f;
		[Tooltip("카메라에서 이 거리 (m) 안의 면은 원 밖도 비움. 카메라가 나뭇잎 속에 들어가 화면을 덮는 경우")]
		[SerializeField, Min(0f)] private float nearDistance = 5f;
		[Tooltip("위 거리에서 점이 줄어드는 폭 (m)")]
		[SerializeField, Min(0.01f)] private float nearSoftness = 2f;

		private CommandBuffer commandBuffer;

		private void OnEnable()
		{
			commandBuffer = new CommandBuffer { name = "WM Occlusion Cutout" };
			RenderPipelineManager.beginCameraRendering += ApplyForCamera;
		}

		private void OnDisable()
		{
			RenderPipelineManager.beginCameraRendering -= ApplyForCamera;
			Shader.SetGlobalFloat(RADIUS_ID, 0f);
			Shader.SetGlobalFloat(NEAR_DISTANCE_ID, 0f);
			commandBuffer.Release();
			commandBuffer = null;
		}

		// 카메라마다 그리기 직전, 그 카메라 명령 순서 안에서 전역 값 설정
		private void ApplyForCamera(ScriptableRenderContext context, Camera renderingCamera)
		{
			commandBuffer.Clear();
			Transform player = FindPlayer();
			bool active = renderingCamera == gameCamera && player != null;
			if (active)
			{
				Vector3 feet = player.position;
				Vector3 center = feet + Vector3.up * centerHeight;
				Vector3 viewport = renderingCamera.WorldToViewportPoint(center);
				Transform cameraTransform = renderingCamera.transform;
				float depth = Vector3.Dot(center - cameraTransform.position, cameraTransform.forward);
				active = viewport.z > 0f;
				commandBuffer.SetGlobalVector(CENTER_ID, new Vector4(viewport.x, viewport.y, 0f, 0f));
				commandBuffer.SetGlobalFloat(SOFTNESS_ID, softness);
				commandBuffer.SetGlobalFloat(MAX_COVERAGE_ID, maxCoverage);
				commandBuffer.SetGlobalFloat(DEPTH_ID, depth - depthMargin);
				commandBuffer.SetGlobalFloat(FLOOR_ID, feet.y + floorMargin);
			}
			commandBuffer.SetGlobalFloat(RADIUS_ID, active ? radius : 0f);
			commandBuffer.SetGlobalFloat(NEAR_DISTANCE_ID, active ? nearDistance : 0f);
			commandBuffer.SetGlobalFloat(NEAR_SOFTNESS_ID, nearSoftness);
			context.ExecuteCommandBuffer(commandBuffer);
		}

		// 매 프레임 조회. 스테이지 전환 뒤 새 플레이어
		private static Transform FindPlayer()
		{
			if (PlayerProvider.TryGetExistingInstance(out PlayerProvider provider) == false)
				return null;
			PlayerObject playerObject = provider.CurrentObject;
			return playerObject != null ? playerObject.transform : null;
		}
	}
}
