using System;
using UnityEngine;

namespace WitchMendokusai.DialogueStage3D
{
	// 대화 카메라 움직임. 별도 무대는 자기 카메라, 월드는 게임 메인 카메라를 잠깐 빌려 씀.
	// 샷 바꿈은 컷, 들어오고 나갈 때만 블렌드, 머무는 동안 느리게 다가감. 실시간 시계
	public sealed class DialogueCastCamera : MonoBehaviour
	{
		private DialogueCastStageSettings settings;
		private Camera view;
		private DialogueShotPose from;
		private DialogueShotPose target;
		private float blendElapsed = -1f;
		private float blendSeconds;
		private float holdElapsed;
		private Action onBlendDone;

		public Camera View => view;

		// 무대용 새 카메라
		public void CreateOwnCamera(DialogueCastStageSettings stageSettings)
		{
			settings = stageSettings;
			view = gameObject.AddComponent<Camera>();
			view.clearFlags = CameraClearFlags.SolidColor;
			view.backgroundColor = settings.Background;
			view.cullingMask = 1 << settings.ContentLayer;
			view.depth = settings.CameraDepth;
			view.nearClipPlane = settings.CameraNearClip;
			view.farClipPlane = settings.CameraFarClip;
		}

		// 월드용. 이미 있는 카메라를 움직임 (주인 해제와 복구는 부르는 쪽)
		public void UseCamera(Camera existing, DialogueCastStageSettings stageSettings)
		{
			settings = stageSettings;
			view = existing;
		}

		public void Cut(DialogueShotPose pose)
		{
			target = pose;
			blendElapsed = -1f;
			holdElapsed = 0f;
			Apply(pose.Position, pose.Rotation, pose.FieldOfView);
		}

		public void Blend(DialogueShotPose pose, float seconds, Action done = null)
		{
			from = new DialogueShotPose(view.transform.position, view.transform.rotation, view.fieldOfView);
			target = pose;
			blendSeconds = seconds;
			blendElapsed = 0f;
			holdElapsed = 0f;
			onBlendDone = done;
		}

		private void LateUpdate()
		{
			if (view == null)
				return;
			float deltaTime = Time.unscaledDeltaTime;
			if (blendElapsed >= 0f)
			{
				blendElapsed += deltaTime;
				float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blendElapsed / Mathf.Max(0.01f, blendSeconds)));
				Apply(Vector3.Lerp(from.Position, target.Position, progress), Quaternion.Slerp(from.Rotation, target.Rotation, progress), Mathf.Lerp(from.FieldOfView, target.FieldOfView, progress));
				if (progress >= 1f)
				{
					blendElapsed = -1f;
					Action done = onBlendDone;
					onBlendDone = null;
					done?.Invoke();
				}
				return;
			}
			holdElapsed += deltaTime;
			Vector3 toward = target.LookPoint - target.Position;
			float push = Mathf.Min(settings.PushMax, holdElapsed * settings.PushSpeed);
			Apply(target.Position + toward.normalized * push, target.Rotation, target.FieldOfView);
		}

		private void Apply(Vector3 position, Quaternion rotation, float fieldOfView)
		{
			view.transform.SetPositionAndRotation(position, rotation);
			view.fieldOfView = fieldOfView;
		}
	}
}
