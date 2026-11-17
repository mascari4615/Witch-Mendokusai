using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 큰 값 변화 (레벨, 날짜, 큰 재화) 는 옛 값을 잠깐, 짧게 바뀐 뒤, 새 값을 다른 색으로 잠깐. 일상 수치에는 쓰지 않음
	/// 진행 중에 다시 부르면 앞의 것은 그 자리에서 끝내고 새 값으로 다시 시작
	/// </summary>
	[CreateAssetMenu(fileName = "Value_0_HoldChange", menuName = "WM/UI/Value Change")]
	public sealed class UIValueChange : ScriptableObject
	{
		public float holdBeforeSeconds = 0.5f;
		public float outSeconds = 0.16f;
		public float inSeconds = 0.24f;
		[Tooltip("나갈 때와 들어올 때 움직이는 거리, 글자 높이 비율")]
		public float travel = 0.4f;
		[Tooltip("들어올 때 커졌다 줄어드는 배율과 넘침")]
		public float popScale = 1.3f;
		public float overshoot = 1.6f;
		[Tooltip("들어올 때 불투명해지는 구간 비율")]
		[Range(0.05f, 1f)] public float fadeInPart = 0.5f;
		public Color highlightColor = new(1f, 0.83f, 0.42f, 1f);
		public float holdAfterSeconds = 1.5f;

		private sealed class State
		{
			public int Generation;
			public Action Cancel;
			public StyleColor Color;
			public StyleTranslate Translate;
			public StyleScale Scale;
			public StyleFloat Opacity;
			public bool Running;
		}

		private static readonly ConditionalWeakTable<Label, State> states = new();

		public void Play(Label label, string newText, Action onDone = null)
		{
			State state = states.GetValue(label, _ => new State());
			state.Cancel?.Invoke();
			if (state.Running)
				Finish(label, state);
			state.Running = true;
			state.Color = label.style.color;
			state.Translate = label.style.translate;
			state.Scale = label.style.scale;
			state.Opacity = label.style.opacity;
			int generation = ++state.Generation;
			bool Alive() => generation == state.Generation;

			state.Cancel = UIFxTween.After(label, holdBeforeSeconds, () => state.Cancel = UIFxTween.Run(label, outSeconds, 0f, progress =>
			{
				label.style.translate = new Translate(Length.Percent(0f), Length.Percent(-travel * 100f * UIFxTween.InCubic(progress)));
				label.style.opacity = 1f - progress;
			}, () =>
			{
				label.text = newText;
				label.style.color = highlightColor;
				state.Cancel = UIFxTween.Run(label, inSeconds, 0f, progress =>
				{
					float eased = UIFxTween.OutBack(progress, overshoot);
					label.style.translate = new Translate(Length.Percent(0f), Length.Percent(travel * 100f * (1f - eased)));
					float scale = Mathf.LerpUnclamped(popScale, 1f, eased);
					label.style.scale = new Scale(new Vector2(scale, scale));
					label.style.opacity = Mathf.Clamp01(progress / fadeInPart);
				}, () => state.Cancel = UIFxTween.After(label, holdAfterSeconds, () =>
				{
					if (Alive() == false)
						return;
					Finish(label, state);
					onDone?.Invoke();
				}));
			}));
		}

		private static void Finish(Label label, State state)
		{
			label.style.color = state.Color;
			label.style.translate = state.Translate;
			label.style.scale = state.Scale;
			label.style.opacity = state.Opacity;
			state.Running = false;
		}
	}
}
