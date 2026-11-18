using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace WitchMendokusai
{
	// CameraManager 의 당김. 지금 content 카메라의 거리와 시야각에 잠깐 값을 얹었다가 뺌
	// - 얹은 값은 매 프레임 마지막 카메라 계산 직전에 더하고 다음 프레임 Update 에서 뺌
	//   -> 줌, 궤도 프로필, 카메라 전환이 읽고 쓰는 값은 늘 당김이 빠진 원래 값
	// - 대화 샷 (NPC 카메라) 과 자유 위치 카메라에는 얹지 않음
	// - 여러 당김이 겹치면 거리 배율은 곱, 시야각은 합
	public partial class CameraManager
	{
		private sealed class ZoomPunch
		{
			public float DistanceScale;
			public float FieldOfViewOffset;
			public float InSeconds;
			public float HoldSeconds;
			public float OutSeconds;
			public AnimationCurve Curve;
			public float Elapsed;
			public bool Releasing;
			public float ReleaseElapsed;
			public float ReleaseFrom;
			public float Weight;
			public bool Finished;
		}

		private readonly List<ZoomPunch> zoomPunches = new();
		private CameraPunchDriver punchDriver;
		private MCamera punchedCamera;
		private CinemachinePositionComposer punchedComposer;
		private float punchedDistance;
		private float punchedDistanceDelta;
		private float punchedFieldOfView;
		private float punchedFieldOfViewDelta;
		private bool isPunchApplied;

		/// <summary>
		/// 전투 카메라를 당겼다 풂. 실제 시간
		/// - distanceScale: 가장 당겼을 때 거리 배율 (1 보다 작으면 가까이)
		/// - fieldOfViewOffset: 가장 당겼을 때 시야각에 더하는 도
		/// - holdSeconds: 머무는 시간. float.PositiveInfinity 면 돌려받은 함수를 부를 때까지
		/// - 돌려받은 함수를 부르면 지금 세기에서 outSeconds 에 걸쳐 풂
		/// </summary>
		public Action PunchZoom(float distanceScale, float fieldOfViewOffset, float inSeconds, float holdSeconds, float outSeconds, AnimationCurve curve)
		{
			if (punchDriver == null)
			{
				punchDriver = gameObject.AddComponent<CameraPunchDriver>();
				punchDriver.Bind(this);
			}

			ZoomPunch punch = new()
			{
				DistanceScale = distanceScale,
				FieldOfViewOffset = fieldOfViewOffset,
				InSeconds = inSeconds,
				HoldSeconds = holdSeconds,
				OutSeconds = outSeconds,
				Curve = curve,
			};
			zoomPunches.Add(punch);
			return () => ReleaseZoomPunch(punch);
		}

		private static void ReleaseZoomPunch(ZoomPunch punch)
		{
			if (punch.Releasing || punch.Finished)
				return;
			punch.Releasing = true;
			punch.ReleaseElapsed = 0f;
			punch.ReleaseFrom = punch.Weight;
		}

		// CameraPunchDriver.LateUpdate: 각 카메라의 자기 갱신 뒤, 최종 카메라 계산 앞
		internal void ApplyZoomPunches(float deltaTime)
		{
			float distanceScale = 1f;
			float fieldOfViewOffset = 0f;
			for (int i = zoomPunches.Count - 1; i >= 0; i--)
			{
				ZoomPunch punch = zoomPunches[i];
				AdvanceZoomPunch(punch, deltaTime);
				if (punch.Finished)
				{
					zoomPunches.RemoveAt(i);
					continue;
				}
				distanceScale *= Mathf.LerpUnclamped(1f, punch.DistanceScale, punch.Weight);
				fieldOfViewOffset += punch.FieldOfViewOffset * punch.Weight;
			}

			if (zoomPunches.Count == 0 || curCamera == null || IsFreePositionMode)
				return;

			if (punchedCamera != curCamera)
			{
				punchedCamera = curCamera;
				punchedComposer = curCamera.CinemachineCamera.GetCinemachineComponent(CinemachineCore.Stage.Body) as CinemachinePositionComposer;
			}

			punchedDistanceDelta = 0f;
			if (punchedComposer != null)
			{
				punchedDistanceDelta = punchedComposer.CameraDistance * (distanceScale - 1f);
				punchedComposer.CameraDistance += punchedDistanceDelta;
				punchedDistance = punchedComposer.CameraDistance;
			}

			punchedFieldOfViewDelta = fieldOfViewOffset;
			punchedCamera.CinemachineCamera.Lens.FieldOfView += punchedFieldOfViewDelta;
			punchedFieldOfView = punchedCamera.CinemachineCamera.Lens.FieldOfView;
			isPunchApplied = true;
		}

		// CameraPunchDriver.Update: 지난 프레임에 얹은 값 회수
		// 그 사이 다른 쪽이 값을 새로 썼으면 (프로필 해제, 거리 초기화) 그 값이 정본이라 건드리지 않음
		internal void RemoveZoomPunches()
		{
			if (isPunchApplied == false)
				return;
			isPunchApplied = false;

			if (punchedCamera == null)
				return;

			if (punchedComposer != null && Mathf.Approximately(punchedComposer.CameraDistance, punchedDistance))
				punchedComposer.CameraDistance -= punchedDistanceDelta;

			if (Mathf.Approximately(punchedCamera.CinemachineCamera.Lens.FieldOfView, punchedFieldOfView))
				punchedCamera.CinemachineCamera.Lens.FieldOfView -= punchedFieldOfViewDelta;
		}

		private static void AdvanceZoomPunch(ZoomPunch punch, float deltaTime)
		{
			if (punch.Releasing == false)
			{
				punch.Elapsed += deltaTime;
				if (punch.Elapsed < punch.InSeconds)
				{
					punch.Weight = punch.Curve.Evaluate(punch.Elapsed / punch.InSeconds);
					return;
				}
				if (punch.Elapsed < punch.InSeconds + punch.HoldSeconds)
				{
					punch.Weight = 1f;
					return;
				}
				punch.Releasing = true;
				punch.ReleaseElapsed = 0f;
				punch.ReleaseFrom = 1f;
			}
			else
			{
				punch.ReleaseElapsed += deltaTime;
			}

			if (punch.ReleaseElapsed >= punch.OutSeconds)
			{
				punch.Weight = 0f;
				punch.Finished = true;
				return;
			}
			punch.Weight = punch.ReleaseFrom * (1f - punch.Curve.Evaluate(punch.ReleaseElapsed / punch.OutSeconds));
		}
	}
}
