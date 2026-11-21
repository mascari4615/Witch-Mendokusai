using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	/// <summary>
	/// 화면 연출 구동기의 카메라 손. 흔들림 (화면 평면 이동, 기울기), 시야각 펀치, 전진을 그리기 직전에만 얹고 그린 뒤 되돌림
	/// - 카메라 계산 (Cinemachine) 이 매 프레임 쓰는 값은 손대지 않음. 얹은 값이 다음 프레임으로 새지 않음
	/// - 시야각은 좁히는 쪽만 (넓히면 카메라 앞 화면 판 (배경 장악 판) 가장자리가 비어 보임). 물러남 (궁극기 물림 포함) 은 전진 음수로
	/// - 실제 시간 곡선을 구동기가 계산해 Set 으로 넣음
	/// </summary>
	public sealed class UltimateFeelCamera
	{
		private readonly Camera camera;
		private Vector3 target;
		private Vector2 offset;
		private float roll;
		private float zoom;
		private float dolly;
		private bool applied;
		private Vector3 savedPosition;
		private Quaternion savedRotation;
		private float savedFieldOfView;
		private bool disposed;

		public UltimateFeelCamera(Camera camera)
		{
			this.camera = camera;
			if (camera == null)
				return;
			RenderPipelineManager.beginCameraRendering += OnBegin;
			RenderPipelineManager.endCameraRendering += OnEnd;
		}

		/// <param name="screenOffset"> 화면 폭 비율 이동 </param>
		/// <param name="rollDegrees"> 화면 기울기 </param>
		/// <param name="zoomRatio"> 화면 배율 증가 비율 (시야각을 좁힘), 음수는 무시 </param>
		/// <param name="dollyRatio"> 표적까지 거리 비율 전진 </param>
		public void Set(Vector3 focus, Vector2 screenOffset, float rollDegrees, float zoomRatio, float dollyRatio)
		{
			target = focus;
			offset = screenOffset;
			roll = rollDegrees;
			zoom = Mathf.Max(0f, zoomRatio);
			dolly = dollyRatio;
		}

		public void Dispose()
		{
			if (disposed)
				return;
			disposed = true;
			RenderPipelineManager.beginCameraRendering -= OnBegin;
			RenderPipelineManager.endCameraRendering -= OnEnd;
			Restore();
		}

		private void OnBegin(ScriptableRenderContext context, Camera rendering)
		{
			if (rendering != camera || camera == null || applied)
				return;
			Transform cameraTransform = camera.transform;
			savedPosition = cameraTransform.position;
			savedRotation = cameraTransform.rotation;
			savedFieldOfView = camera.fieldOfView;
			applied = true;

			float distance = Mathf.Max(0.5f, Vector3.Distance(savedPosition, target));
			float halfTangent = Mathf.Tan(savedFieldOfView * 0.5f * Mathf.Deg2Rad) / (1f + zoom);
			float baseFieldOfView = Mathf.Max(5f, 2f * Mathf.Atan(halfTangent) * Mathf.Rad2Deg);
			float screenWidth = 2f * distance * Mathf.Tan(baseFieldOfView * 0.5f * Mathf.Deg2Rad) * camera.aspect;
			Vector3 shift = cameraTransform.right * (offset.x * screenWidth) + cameraTransform.up * (offset.y * screenWidth) + cameraTransform.forward * (dolly * distance);
			cameraTransform.SetPositionAndRotation(savedPosition + shift, savedRotation * Quaternion.Euler(0f, 0f, roll));
			camera.fieldOfView = baseFieldOfView;
			Vector3 viewport = camera.WorldToViewportPoint(target);
			UltimateImpactScreen.SetCenter(new Vector2(viewport.x, viewport.y), camera.aspect);
		}

		private void OnEnd(ScriptableRenderContext context, Camera rendering)
		{
			if (rendering != camera)
				return;
			Restore();
		}

		private void Restore()
		{
			if (applied == false || camera == null)
				return;
			applied = false;
			camera.transform.SetPositionAndRotation(savedPosition, savedRotation);
			camera.fieldOfView = savedFieldOfView;
		}
	}
}
