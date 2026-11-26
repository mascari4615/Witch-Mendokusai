using UnityEngine;

namespace WitchMendokusai
{
	// 등 뒤에서 좌우로 훑는 시점 (직접 누르는 궁극기). 시점 조작을 잠깐 빌리고 끝나면 되돌림
	// - 마우스 좌우 (손가락은 훑기) 로 돌고 위아래는 잠금. 끝나면 조작 방식, 궤도 설정, 바라보던 방향이 처음 값으로
	// - 되돌리기는 시작한 쪽의 책임 (EndSweepView). 두 번 불러도 됨
	public partial class CameraManager
	{
		[Header("등 뒤 훑기 시점")]
		[SerializeField, Min(0f), Tooltip("들어갈 때 시점이 돌아가는 길이 (초)")] private float sweepBlendSeconds = 0.3f;

		private OrbitCameraProfile sweepReturnProfile;
		private CameraControlMode sweepReturnMode;
		private float sweepReturnYaw;
		private float sweepReturnZoom;
		private MCamera sweepCamera;
		private float sweepFromYaw;
		private float sweepStartTime;

		/// <summary> 훑기 시점인 동안. 위아래 시점 이동을 막고 손가락 훑기를 허용 </summary>
		public bool IsSweepView { get; private set; }

		/// <summary> 훑기 시점이 바라보는 평면 방향 </summary>
		public Vector3 SweepForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

		public void BeginSweepView(OrbitCameraProfile profile, Vector3 forward)
		{
			if (IsSweepView)
				return;
			sweepReturnProfile = OrbitProfile;
			sweepReturnMode = ControlMode;
			sweepReturnYaw = yaw;
			sweepCamera = curCamera;
			sweepReturnZoom = curCamera.ZoomTarget;
			sweepFromYaw = pitchPivot != null ? pitchPivot.eulerAngles.y : yaw;
			sweepStartTime = Time.time;
			IsSweepView = true;
			SetControlMode(CameraControlMode.MouseLook);
			SetOrbitProfile(profile);
			pitch = 0f;
			yaw = Quaternion.LookRotation(forward, Vector3.up).eulerAngles.y;
			suppressLookFrame = true;
		}

		public void EndSweepView()
		{
			if (IsSweepView == false)
				return;
			IsSweepView = false;
			SetControlMode(sweepReturnMode);
			SetOrbitProfile(sweepReturnProfile);
			// 궤도 설정을 거두면 거리가 기본값이 됨. 손으로 맞춘 거리로
			if (sweepCamera == curCamera)
				curCamera.ZoomTarget = sweepReturnZoom;
			// 시점 조작 방식이 마우스 시점이면 되돌리지 않음. 그쪽은 지금 보는 방향이 곧 조작
			if (ControlMode == CameraControlMode.PointAndClick)
				yaw = sweepReturnYaw;
			suppressLookFrame = true;
		}

		// 들어갈 때는 부드럽게 돌아감. 그 뒤는 마우스가 쥔 방향 그대로
		private Quaternion LookRotation()
		{
			if (IsSweepView == false || sweepBlendSeconds <= 0f)
				return Quaternion.Euler(pitch, yaw, 0f);
			float progress = Mathf.SmoothStep(0f, 1f, (Time.time - sweepStartTime) / sweepBlendSeconds);
			return Quaternion.Euler(pitch, Mathf.LerpAngle(sweepFromYaw, yaw, progress), 0f);
		}
	}
}
