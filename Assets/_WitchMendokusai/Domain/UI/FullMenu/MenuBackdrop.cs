using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 전체 메뉴 결의 배경. 흐린 게임 화면 (BlurRequest 출력), UI 연출을 끄면 단색 막. 모양은 FullMenu.uss 의 wm-fullmenu__backdrop.
	/// 흐림 패스는 요청이 있을 때만 돌아서, 화면이 열려 있는 동안만 요청
	/// </summary>
	public sealed class MenuBackdrop
	{
		private const string USS_CLASS = "wm-fullmenu__backdrop";
		private const string USS_BLUR = "wm-fullmenu__backdrop--blur";

		private readonly VisualElement element;
		private bool isBlurRequested;

		/// <summary>화면 끝까지 덮는 칸 맨 뒤에 깔림</summary>
		public MenuBackdrop(VisualElement root, RenderTexture blurOutput)
		{
			element = new VisualElement { pickingMode = PickingMode.Ignore };
			element.AddToClassList(USS_CLASS);
			element.style.backgroundImage = Background.FromRenderTexture(blurOutput);
			root.Insert(0, element);
		}

		public void SetBlurred(bool isBlurred)
		{
			if (isBlurRequested == isBlurred)
			{
				return;
			}
			isBlurRequested = isBlurred;
			if (isBlurred)
			{
				BlurRequest.Add();
			}
			else
			{
				BlurRequest.Remove();
			}
			element.EnableInClassList(USS_BLUR, isBlurred);
		}

		/// <summary>흐림 출력은 매 프레임 바뀜. 열려 있는 동안 Update 에서 부름</summary>
		public void Repaint()
		{
			if (isBlurRequested)
			{
				element.MarkDirtyRepaint();
			}
		}
	}
}
