using UnityEngine;

namespace WitchMendokusai.DialogueStage3D
{
	// 무대 카메라. 샷 바꿈은 컷, 머무는 동안 느리게 다가감. 실시간 시계
	public sealed class DialogueCastCamera : MonoBehaviour
	{
		private DialogueCastStageSettings settings;
		private Camera view;
		private DialogueShotPose target;
		private float holdElapsed;

		public Camera View => view;

		public void Initialize(DialogueCastStageSettings stageSettings)
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

		public void Cut(DialogueShotPose pose)
		{
			target = pose;
			holdElapsed = 0f;
			Apply(pose.Position, pose.Rotation, pose.FieldOfView);
		}

		private void LateUpdate()
		{
			if (view == null)
				return;
			holdElapsed += Time.unscaledDeltaTime;
			Vector3 toward = target.LookPoint - target.Position;
			float push = Mathf.Min(settings.PushMax, holdElapsed * settings.PushSpeed);
			Apply(target.Position + toward.normalized * push, target.Rotation, target.FieldOfView);
		}

		private void Apply(Vector3 position, Quaternion rotation, float fieldOfView)
		{
			transform.SetPositionAndRotation(position, rotation);
			view.fieldOfView = fieldOfView;
		}
	}
}
