using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 전체 화면 창 한 벌. 생성은 UIRoot.CreateFullScreenFrame
	/// - Root: 화면 끝까지 (노치 쪽 포함). 배경, 어두운 막, 배경 그림, 테마 USS 클래스, 열고 닫기
	/// - Content: 안전 영역. 버튼, 목록, 글
	/// </summary>
	public readonly struct FullScreenFrame
	{
		public VisualElement Root { get; }
		public VisualElement Content { get; }

		public FullScreenFrame(VisualElement root, VisualElement content)
		{
			Root = root;
			Content = content;
		}
	}
}
