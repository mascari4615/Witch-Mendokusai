using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 대화의 카메라 어댑터. 샷 요청을 CameraManager 임대로 전달, 3D 모델 연출 없음
	public sealed class DialogueCameraDirector
	{
		private readonly CameraManager cameraManager;
		private readonly DialogueStageSettings settings;
		private readonly Func<string, Transform> resolveTarget;

		public DialogueCameraDirector(CameraManager cameraManager, DialogueStageSettings settings, Func<string, Transform> resolveTarget)
		{
			this.cameraManager = cameraManager;
			this.settings = settings;
			this.resolveTarget = resolveTarget;
		}

		public void Apply(string targetName, DialogueStageShot shot)
		{
			if (string.IsNullOrEmpty(targetName))
			{
				Release();
				return;
			}

			Transform target = resolveTarget(targetName);
			if (target == null)
			{
				Debug.LogWarning($"[{nameof(DialogueCameraDirector)}] 카메라 대상 없음: {targetName}");
				return;
			}
			cameraManager.FocusDialogue(target, settings.DistanceFor(shot), settings.CameraBlendSeconds, settings.CameraDistanceSmoothSeconds);
		}

		public void Release()
		{
			if (cameraManager != null)
				cameraManager.ReleaseDialogueFocus();
		}
	}
}
