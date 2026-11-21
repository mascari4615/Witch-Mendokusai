using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	/// <summary>
	/// 화면 연출 구동기의 카메라 손. 흔들림 (화면 평면 이동, 기울기), 시야각 펀치, 전진을 그리기 직전에만 얹고 그린 뒤 되돌림
	/// - 카메라 계산 (Cinemachine) 이 매 프레임 쓰는 값은 손대지 않음. 얹은 값이 다음 프레임으로 새지 않음
	/// - 시야각은 좁히는 쪽만 (넓히면 카메라 앞 화면 판 (배경 장악 판) 가장자리가 비어 보임). 물러남 (궁극기 물림 포함) 은 전진 음수로
	/// - 실제 시간 곡선을 구동기가 계산해 Set 으로 넣음
	/// - 여럿이 같은 카메라에 동시에 얹을 수 있음 (궁극기 구동기와 플레이어 피격 연출). 처음 얹는 손이 원래 값을 저장하고, 그린 뒤 처음 불린 손이 원래 값으로 되돌림. 손마다 따로 되돌리면 구독 순서에 따라 얹은 값이 다음 프레임으로 샘
	/// - 얹을 값이 전부 0 이면 아무것도 안 함
	/// </summary>
	public sealed class UltimateFeelCamera
	{
		/// <summary> 카메라마다 이번 그리기에 얹은 손과 원래 값 </summary>
		private sealed class SharedState
		{
			public Vector3 Position;
			public Quaternion Rotation;
			public float FieldOfView;
			public readonly List<UltimateFeelCamera> Appliers = new();
		}

		private static readonly Dictionary<Camera, SharedState> states = new();

		private readonly Camera camera;
		private readonly bool publishImpactCenter;
		private Vector3 target;
		private Vector2 offset;
		private float roll;
		private float zoom;
		private float dolly;
		private bool applied;
		private bool disposed;

		/// <param name="publishImpactCenter"> 그리기 직전 표적의 화면 위치를 화면 왜곡 중심으로 넣음 (궁극기 구동기만) </param>
		public UltimateFeelCamera(Camera camera, bool publishImpactCenter = true)
		{
			this.camera = camera;
			this.publishImpactCenter = publishImpactCenter;
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
			if (rendering != camera || camera == null || applied || IsIdle)
				return;
			Transform cameraTransform = camera.transform;
			if (states.TryGetValue(camera, out SharedState state) == false)
			{
				state = new SharedState();
				states[camera] = state;
			}
			if (state.Appliers.Count == 0)
			{
				state.Position = cameraTransform.position;
				state.Rotation = cameraTransform.rotation;
				state.FieldOfView = camera.fieldOfView;
			}
			state.Appliers.Add(this);
			applied = true;

			// 앞 손이 얹은 값 위에 더함
			Vector3 currentPosition = cameraTransform.position;
			Quaternion currentRotation = cameraTransform.rotation;
			float currentFieldOfView = camera.fieldOfView;
			float distance = Mathf.Max(0.5f, Vector3.Distance(currentPosition, target));
			float halfTangent = Mathf.Tan(currentFieldOfView * 0.5f * Mathf.Deg2Rad) / (1f + zoom);
			float baseFieldOfView = Mathf.Max(5f, 2f * Mathf.Atan(halfTangent) * Mathf.Rad2Deg);
			float screenWidth = 2f * distance * Mathf.Tan(baseFieldOfView * 0.5f * Mathf.Deg2Rad) * camera.aspect;
			Vector3 shift = cameraTransform.right * (offset.x * screenWidth) + cameraTransform.up * (offset.y * screenWidth) + cameraTransform.forward * (dolly * distance);
			cameraTransform.SetPositionAndRotation(currentPosition + shift, currentRotation * Quaternion.Euler(0f, 0f, roll));
			camera.fieldOfView = baseFieldOfView;
			if (publishImpactCenter == false)
				return;
			Vector3 viewport = camera.WorldToViewportPoint(target);
			UltimateImpactScreen.SetCenter(new Vector2(viewport.x, viewport.y), camera.aspect);
		}

		private bool IsIdle => offset == Vector2.zero && roll == 0f && zoom == 0f && dolly == 0f;

		private void OnEnd(ScriptableRenderContext context, Camera rendering)
		{
			if (rendering != camera)
				return;
			Restore();
		}

		/// <summary> 이 카메라에 얹은 손 전부를 원래 값으로. 얹은 손이 여럿이어도 한 번만 </summary>
		private void Restore()
		{
			if (applied == false)
				return;
			if (camera == null || states.TryGetValue(camera, out SharedState state) == false)
			{
				applied = false;
				return;
			}
			camera.transform.SetPositionAndRotation(state.Position, state.Rotation);
			camera.fieldOfView = state.FieldOfView;
			foreach (UltimateFeelCamera applier in state.Appliers)
				applier.applied = false;
			state.Appliers.Clear();
			states.Remove(camera);
		}
	}
}
