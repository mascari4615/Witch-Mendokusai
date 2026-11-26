using System.Collections.Generic;

namespace WitchMendokusai
{
	// 3D 배우 무대 포트. 2D 입상과 같은 변경분 (등장, 이동, 화자, 몸짓, 샷) 을 받아 3D 인물과 카메라로 표현.
	// 같은 대본이 2D 와 3D 양쪽에서 돈다. 구현은 App/DialogueStage3D
	public interface IDialogueCastStage
	{
		bool IsShowing { get; }

		// `@동작 <인물> <이름>` 에 쓸 수 있는 이름. 사람과 AI 가 고를 목록
		IReadOnlyList<string> GestureNames { get; }

		// Stage 는 별도 무대, World 는 플레이하던 월드의 인물과 자리. 못 열면 지금 화면 유지, 이유 반환
		bool TryShow(DialogueStageSpace space, out string problem);

		// 한 줄의 변경분 적용. lineText 는 말하기 동작 길이에 씀, 대사 없는 자리면 null
		void Apply(DialogueStageDelta delta, string lineText);

		// 넘겨받은 카메라와 화면을 원래대로. 켜진 무대가 없으면 무동작
		void Hide();
	}

	public static class DialogueCastStageBridge
	{
		public static IDialogueCastStage Current { get; private set; }

		public static void Register(IDialogueCastStage stage) => Current = stage;

		public static void Clear(IDialogueCastStage stage)
		{
			if (Current == stage)
			{
				Current = null;
			}
		}
	}
}
