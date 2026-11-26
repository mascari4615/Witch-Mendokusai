using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// DialogueStageView 의 `@연출` 3D 장면 켜고 끄기. 3D 구현은 포트 뒤 (App/SequenceLibrary)
	public partial class DialogueStageView
	{
		private static bool IsSceneShowing => DialogueSceneStageBridge.Current != null && DialogueSceneStageBridge.Current.IsShowing;

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

		// 3D 연출 중에는 2D 입상 숨김. 3D 인물이 무대. 입상 상태는 그대로 두어 연출이 끝나면 다시 보임
		private void RefreshSceneLayer()
		{
			if (cast != null)
				cast.style.display = IsSceneShowing ? DisplayStyle.None : DisplayStyle.Flex;
		}

		private void HideScene()
		{
			if (IsSceneShowing == false)
				return;

			DialogueSceneStageBridge.Current.Hide();
			RefreshStageMood();
		}
	}
}
