using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>하위 창이 열리면 부모가 살짝 커지고 흐려지며 뒤로, 닫히면 돌아오며 맨 앞에. UIFxProfile 의 stackPush 칸</summary>
	[CreateAssetMenu(fileName = "Stack_0_Push", menuName = "WM/UI/Stack Push")]
	public sealed class UIStackPush : ScriptableObject
	{
		public float seconds = 0.18f;
		[Tooltip("뒤로 밀릴 때 크기")]
		public float pushedScale = 1.05f;
		[Tooltip("뒤로 밀릴 때 불투명도")]
		[Range(0f, 1f)] public float pushedOpacity = 0.45f;

		private sealed class State
		{
			public float Amount;
			public Action Cancel;
		}

		private static readonly ConditionalWeakTable<VisualElement, State> states = new();

		/// <summary>pushed 면 뒤로, 아니면 원래대로</summary>
		public void Apply(VisualElement parent, bool pushed)
		{
			State state = states.GetValue(parent, _ => new State());
			state.Cancel?.Invoke();
			float from = state.Amount;
			float to = pushed ? 1f : 0f;
			state.Cancel = UIFxTween.Run(parent, seconds, 0f, progress =>
			{
				state.Amount = Mathf.Lerp(from, to, UIFxTween.OutCubic(progress));
				float scale = Mathf.Lerp(1f, pushedScale, state.Amount);
				parent.style.scale = new Scale(new Vector2(scale, scale));
				parent.style.opacity = Mathf.Lerp(1f, pushedOpacity, state.Amount);
			}, () =>
			{
				state.Cancel = null;
				if (pushed)
					return;
				parent.style.scale = StyleKeyword.Null;
				parent.style.opacity = StyleKeyword.Null;
				parent.BringToFront();
			});
		}
	}
}
