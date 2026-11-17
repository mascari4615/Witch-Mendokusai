using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>기존 USS 움직임 (Pop, Rise 등 Slot.uss) 을 에셋으로. 길이와 곡선은 USS 정본</summary>
	[CreateAssetMenu(fileName = "Motion_0_Uss", menuName = "WM/UI/Panel Motion/USS Class")]
	public sealed class UssPanelMotion : UIPanelMotion
	{
		public UIMotionStyle style = UIMotionStyle.Pop;

		protected override bool HandlesDisplay => true;

		protected override Action Open(VisualElement panel, VisualElement source, Action onDone)
		{
			// 다른 에셋이 막아 둔 transition 을 USS 값으로. 닫힌 채로 종류를 바꿔야 시작 값이 맞음
			panel.style.transitionDuration = StyleKeyword.Null;
			if (panel.resolvedStyle.display == DisplayStyle.None)
				UIMotion.UseStyle(panel, style);
			UIMotion.Show(panel);
			onDone();
			return () => { };
		}

		protected override Action Close(VisualElement panel, VisualElement source, Action onDone)
		{
			panel.style.transitionDuration = StyleKeyword.Null;
			UIMotion.Hide(panel);
			onDone();
			return () => { };
		}
	}
}
