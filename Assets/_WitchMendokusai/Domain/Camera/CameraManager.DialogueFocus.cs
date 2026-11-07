using System.Linq;
using Unity.Cinemachine;
using UnityEngine;

namespace WitchMendokusai
{
	// CameraManager 의 대화 샷 임대. NPC 카메라 하나만 빌리고 끝나면 이전 값으로 복귀
	public partial class CameraManager
	{
		private bool isDialogueFocused;
		private bool isDialogueRestoring;
		private MCamera dialogueCamera;
		private int previousDialoguePriority;
		private Transform previousDialogueTarget;
		private float previousAnchorWeight;
		private float previousDialogueDistance;
		private CinemachineBlendDefinition previousBlend;
		private float dialogueTargetDistance;
		private float dialogueDistanceSmoothSeconds;
		private float dialogueRestoreElapsed;
		private float dialogueBlendSeconds;

		public bool IsDialogueFocused => isDialogueFocused;

		// 자유 위치 카메라는 그 모드가 주인이라 임대 거절
		public bool FocusDialogue(Transform dialogueTarget, float distance, float blendSeconds, float distanceSmoothSeconds)
		{
			if (IsFreePositionMode)
				return false;

			if (isDialogueFocused == false)
				BeginDialogueLease(blendSeconds);

			dialogueTargetDistance = distance;
			dialogueDistanceSmoothSeconds = distanceSmoothSeconds;
			chatTargetGroup.Targets[1].Object = dialogueTarget;
			return true;
		}

		public void ReleaseDialogueFocus()
		{
			if (isDialogueFocused == false)
				return;

			isDialogueFocused = false;
			isDialogueRestoring = true;
			dialogueRestoreElapsed = 0f;
			dialogueCamera.CinemachineCamera.Priority = previousDialoguePriority;
			dialogueTargetDistance = previousDialogueDistance;
		}

		private void BeginDialogueLease(float blendSeconds)
		{
			if (isDialogueRestoring)
				FinishDialogueRestore();

			isDialogueFocused = true;
			dialogueBlendSeconds = blendSeconds;
			dialogueCamera = cameras.First(cam => cam.UICameraMode == UICameraMode.NPC);
			previousDialoguePriority = dialogueCamera.CinemachineCamera.Priority;
			previousDialogueTarget = chatTargetGroup.Targets[1].Object;
			previousAnchorWeight = chatTargetGroup.Targets[0].Weight;
			previousDialogueDistance = chatPositionTransposer.CameraDistance;
			previousBlend = cinemachineBrain.DefaultBlend;

			chatTargetGroup.Targets[0].Weight = 0f;
			cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blendSeconds);
			SetUICameraMode(UICameraMode.NPC, true);
		}

		private void TickDialogueFocus(float deltaTime)
		{
			if (isDialogueFocused == false && isDialogueRestoring == false)
				return;

			float blend = dialogueDistanceSmoothSeconds <= 0f ? 1f : 1f - Mathf.Exp(-deltaTime / dialogueDistanceSmoothSeconds);
			chatPositionTransposer.CameraDistance = Mathf.Lerp(chatPositionTransposer.CameraDistance, dialogueTargetDistance, blend);

			if (isDialogueRestoring == false)
				return;

			dialogueRestoreElapsed += deltaTime;
			if (dialogueRestoreElapsed >= dialogueBlendSeconds)
				FinishDialogueRestore();
		}

		private void FinishDialogueRestore()
		{
			isDialogueRestoring = false;
			chatPositionTransposer.CameraDistance = previousDialogueDistance;
			chatTargetGroup.Targets[1].Object = previousDialogueTarget;
			chatTargetGroup.Targets[0].Weight = previousAnchorWeight;
			cinemachineBrain.DefaultBlend = previousBlend;
		}
	}
}
