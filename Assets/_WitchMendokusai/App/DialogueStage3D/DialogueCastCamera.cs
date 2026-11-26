using UnityEngine;

namespace WitchMendokusai.DialogueStage3D
{
	// 무대 카메라. 컷은 바로, 블렌드는 부드럽게, 머무는 동안 느리게 다가감. 실시간 시계
	public sealed class DialogueCastCamera : MonoBehaviour
	{
		private DialogueCastStageSettings settings;
		private Camera view;
		private DialogueShotPose from;
		private DialogueShotPose target;
		private float blendElapsed = -1f;
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
			view.nearClipPlane = 0.05f;
			view.farClipPlane = 60f;
		}

		public void Cut(DialogueShotPose pose)
		{
			target = pose;
			blendElapsed = -1f;
			holdElapsed = 0f;
			Apply(pose.Position, pose.Rotation, pose.FieldOfView);
		}

		public void Blend(DialogueShotPose pose)
		{
			from = new DialogueShotPose(transform.position, transform.position + transform.forward, view.fieldOfView);
			target = pose;
			blendElapsed = 0f;
			holdElapsed = 0f;
		}

		private void LateUpdate()
		{
			if (view == null)
				return;
			float deltaTime = Time.unscaledDeltaTime;
			if (blendElapsed >= 0f)
			{
				blendElapsed += deltaTime;
				float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blendElapsed / Mathf.Max(0.01f, settings.BlendSeconds)));
				Quaternion fromRotation = Quaternion.LookRotation(from.LookPoint - from.Position, Vector3.up);
				Apply(Vector3.Lerp(from.Position, target.Position, progress), Quaternion.Slerp(fromRotation, target.Rotation, progress), Mathf.Lerp(from.FieldOfView, target.FieldOfView, progress));
				if (progress >= 1f)
					blendElapsed = -1f;
				return;
			}
			holdElapsed += deltaTime;
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
