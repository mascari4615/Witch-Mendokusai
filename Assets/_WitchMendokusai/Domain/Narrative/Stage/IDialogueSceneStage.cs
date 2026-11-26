using System.Collections.Generic;

namespace WitchMendokusai
{
	// 3D 연출 어댑터 포트. 대화 코어는 연출 이름만 알고 3D 패키지는 모름. 구현은 App/SequenceLibrary
	public interface IDialogueSceneStage
	{
		// 대본의 `@연출 <이름>` 에 쓸 수 있는 이름. 사람과 AI 가 고를 목록
		IReadOnlyList<string> SceneKeys { get; }

		// 지금 3D 연출이 화면을 쓰는지. 씬 전환처럼 대화 밖에서도 꺼지므로 대화 쪽은 이 값만 기준
		bool IsShowing { get; }

		// 화면을 3D 연출로 전환. 못 열면 지금 화면 유지, 이유 반환
		bool TryShow(string sceneKey, out string problem);

		// 넘겨받은 카메라와 화면을 원래대로. 켜진 연출이 없으면 무동작
		void Hide();
	}

	public static class DialogueSceneStageBridge
	{
		public static IDialogueSceneStage Current { get; private set; }

		public static void Register(IDialogueSceneStage stage) => Current = stage;

		public static void Clear(IDialogueSceneStage stage)
		{
			if (Current == stage)
			{
				Current = null;
			}
		}
	}
}
