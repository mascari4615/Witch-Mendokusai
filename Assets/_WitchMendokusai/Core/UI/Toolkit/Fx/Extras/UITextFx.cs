using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 글자가 한 자씩 약하게 물결치고 빛이 지나감. UIFxProfile 의 textFx 칸, 대상은 wm-text-wave 클래스 Label
	/// - 패널이 열릴 때마다 지금 글로 다시 나눔. 글을 바꾼 뒤에는 Apply 를 다시 부를 것
	/// - 글자마다 Label 로 나눠 그리므로 제목과 고른 칸처럼 짧은 글에만. 서식 태그는 쓰지 않음
	/// </summary>
	[CreateAssetMenu(fileName = "Text_0_Wave", menuName = "WM/UI/Text Fx")]
	public sealed class UITextFx : ScriptableObject
	{
		public const string TARGET_CLASS = "wm-text-wave";
		// 줄바꿈 없는 빈칸 문자 번호
		private const char NO_BREAK_SPACE = (char)0x00A0;

		[Tooltip("위아래 흔들림, 픽셀")]
		public float amplitude = 4f;
		[Tooltip("한 번 출렁이는 시간, 초")]
		public float waveSeconds = 1.6f;
		[Tooltip("옆 글자와 박자 차이, 한 바퀴 비율")]
		public float phaseStep = 0.08f;
		[Tooltip("빛 색과 한 바퀴 시간")]
		public Color shimmerColor = new(1f, 0.96f, 0.78f, 1f);
		public float shimmerSeconds = 2.4f;
		[Tooltip("빛 폭, 글자 수")]
		public float shimmerWidth = 2.5f;

		private sealed class State
		{
			public string Text;
			public int Generation;
		}

		private static readonly ConditionalWeakTable<Label, State> states = new();

		/// <summary>root 아래 wm-text-wave Label 전부에</summary>
		public void ApplyAll(VisualElement root)
		{
			List<Label> labels = root.Query<Label>(className: TARGET_CLASS).ToList();
			foreach (Label label in labels)
				Apply(label);
		}

		/// <summary>label 을 글자 단위로 나눠 움직임. label.text 가 새로 들어왔으면 그 글, 아니면 처음 나눈 글로 다시</summary>
		public void Apply(Label label)
		{
			State state = states.GetValue(label, _ => new State());
			if (string.IsNullOrEmpty(label.text) == false || state.Text == null)
				state.Text = label.text;
			int generation = ++state.Generation;
			label.Clear();
			label.text = string.Empty;
			label.style.flexDirection = FlexDirection.Row;
			Color baseColor = label.resolvedStyle.color;
			List<Label> letters = new();
			TextElementEnumerator glyphs = StringInfo.GetTextElementEnumerator(state.Text);
			while (glyphs.MoveNext())
			{
				string glyph = glyphs.GetTextElement();
				// 빈칸 한 글자 Label 은 폭이 0 이 되므로 줄바꿈 없는 빈칸으로
				Label piece = new(glyph == " " ? NO_BREAK_SPACE.ToString() : glyph) { pickingMode = PickingMode.Ignore };
				piece.style.marginLeft = 0;
				piece.style.marginRight = 0;
				piece.style.paddingLeft = 0;
				piece.style.paddingRight = 0;
				label.Add(piece);
				letters.Add(piece);
			}
			float elapsed = 0f;
			label.schedule.Execute(timer =>
			{
				if (UIIdleFx.IsShown(label) == false)
					return;
				elapsed += UIFxTween.Seconds(timer);
				float sweep = Mathf.Repeat(elapsed / UIFxTween.Duration(shimmerSeconds), 1f) * (letters.Count + shimmerWidth * 2f) - shimmerWidth;
				for (int i = 0; i < letters.Count; i++)
				{
					float phase = (elapsed / UIFxTween.Duration(waveSeconds) + i * phaseStep) * Mathf.PI * 2f;
					letters[i].style.translate = new Translate(0f, Mathf.Sin(phase) * amplitude);
					float light = Mathf.Clamp01(1f - Mathf.Abs(i - sweep) / UIFxTween.Duration(shimmerWidth));
					letters[i].style.color = Color.Lerp(baseColor, shimmerColor, light);
				}
			}).Every(0).Until(() => label.panel == null || generation != state.Generation);
		}
	}
}
