using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// DialogueStageView 의 3D 화면 두 가지. `@연출` 은 저장된 연출 재생 (App/SequenceLibrary),
	// `@무대 3D|월드` 는 같은 변경분을 3D 인물과 카메라로 표현 (App/DialogueStage3D). 둘 다 포트 뒤
	public partial class DialogueStageView
	{
		private static bool IsSceneShowing => DialogueSceneStageBridge.Current != null && DialogueSceneStageBridge.Current.IsShowing;
		private static bool IsCastStageShowing => DialogueCastStageBridge.Current != null && DialogueCastStageBridge.Current.IsShowing;
		private static bool Is3DShowing => IsSceneShowing || IsCastStageShowing;

		private string currentLineText;

		private void ApplyScene(string sceneKey)
		{
			if (sceneKey == null)
			{
				HideScene();
				return;
			}

			IDialogueSceneStage sceneStage = DialogueSceneStageBridge.Current;
			if (sceneStage == null)
			{
				Debug.LogError($"[{nameof(DialogueStageView)}] @연출 {sceneKey}: 3D 연출 어댑터가 등록되지 않았다");
				return;
			}
			// 못 열면 이전 연출도 닫아 원고와 화면을 일치. 대사는 2D 위로 계속. 콘솔에 이유와 있는 이름 목록
			if (sceneStage.TryShow(sceneKey, out string problem) == false)
			{
				Debug.LogError($"[{nameof(DialogueStageView)}] @연출 {sceneKey} 를 열지 못했다: {problem}");
				HideScene();
				return;
			}
			RefreshStageMood();
		}

		private void HideScene()
		{
			if (IsSceneShowing == false)
				return;

			DialogueSceneStageBridge.Current.Hide();
			RefreshStageMood();
		}

		// 켜고 끄기는 변경분의 StageSpaceChanged, 켜진 동안은 줄마다 같은 변경분을 그대로 넘김
		private void ApplyCastStage(DialogueStageDelta delta)
		{
			IDialogueCastStage castStage = DialogueCastStageBridge.Current;
			if (delta.StageSpaceChanged)
			{
				HideCastStage();
				if (delta.StageSpace == DialogueStageSpace.Flat)
					return;
				if (castStage == null)
				{
					Debug.LogError($"[{nameof(DialogueStageView)}] @무대 {delta.StageSpace}: 3D 무대 어댑터가 등록되지 않았다");
					return;
				}
				if (castStage.TryShow(delta.StageSpace, out string problem) == false)
				{
					Debug.LogError($"[{nameof(DialogueStageView)}] @무대 {delta.StageSpace} 를 열지 못했다: {problem}");
					return;
				}
				RefreshStageMood();
			}
			if (IsCastStageShowing)
				castStage.Apply(delta, currentLineText);
		}

		private void HideCastStage()
		{
			if (IsCastStageShowing == false)
				return;

			DialogueCastStageBridge.Current.Hide();
			RefreshStageMood();
		}

		// 3D 화면 중에는 2D 입상 숨김. 입상 상태는 그대로 두어 3D 가 끝나면 다시 보임
		private void RefreshSceneLayer()
		{
			if (cast != null)
				cast.style.display = Is3DShowing ? DisplayStyle.None : DisplayStyle.Flex;
		}
	}
}
