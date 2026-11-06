using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace WitchMendokusai
{
	// 연출 공통. 켜고 끄는 값은 PlayerPrefs, 꺼지면 최종 상태로 즉시 이동
	public static class UIEffects
	{
		private const string PREF_KEY = "WM.UIEffects";
		private static readonly int UNSCALED_TIME_ID = Shader.PropertyToID("_WMUIUnscaledTime");

		public static bool Enabled
		{
			get => PlayerPrefs.GetInt(PREF_KEY, 1) == 1;
			set => PlayerPrefs.SetInt(PREF_KEY, value ? 1 : 0);
		}

		public static void TickShaderTime() => Shader.SetGlobalFloat(UNSCALED_TIME_ID, Time.unscaledTime);

		public static void PopIn(Transform target, float duration, float delay = 0f, float startScale = 0.6f)
		{
			target.DOKill();
			if (Enabled == false)
			{
				target.localScale = Vector3.one;
				return;
			}
			target.localScale = Vector3.one * startScale;
			target.DOScale(Vector3.one, duration).SetDelay(delay).SetEase(Ease.OutBack).SetUpdate(true);
		}

		public static void FadeIn(CanvasGroup group, float duration, float delay = 0f)
		{
			group.DOKill();
			if (Enabled == false)
			{
				group.alpha = 1f;
				return;
			}
			group.alpha = 0f;
			group.DOFade(1f, duration).SetDelay(delay).SetUpdate(true);
		}

		public static void Punch(Transform target, float amount, float duration)
		{
			if (Enabled == false)
				return;
			target.DOKill(true);
			target.DOPunchScale(Vector3.one * amount, duration, 1, 0f).SetUpdate(true);
		}

		public static void CountUp(TMP_Text text, int to, float duration, float delay, Func<int, string> format)
		{
			text.DOKill();
			if (Enabled == false)
			{
				text.text = format(to);
				return;
			}
			text.text = format(0);
			int current = 0;
			DOTween.To(() => current, value => { current = value; text.text = format(value); }, to, duration)
				.SetDelay(delay).SetEase(Ease.OutQuad).SetUpdate(true).SetTarget(text);
		}

		// 마지막 줄 한 글자 방지. 마지막 공백에서 줄바꿈, 공백 없으면 글자 축소
		public static void AvoidOrphan(TMP_Text text, float minFontSize)
		{
			text.ForceMeshUpdate();
			TMP_TextInfo info = text.textInfo;
			if (info.lineCount < 2)
				return;
			TMP_LineInfo lastLine = info.lineInfo[info.lineCount - 1];
			if (lastLine.visibleCharacterCount > 1)
				return;
			int lastSpace = text.text.LastIndexOf(' ');
			if (lastSpace > 0)
			{
				text.text = text.text.Remove(lastSpace, 1).Insert(lastSpace, "\n");
				return;
			}
			while (text.fontSize > minFontSize && text.textInfo.lineCount > 1)
			{
				text.fontSize -= 1f;
				text.ForceMeshUpdate();
			}
		}
	}
}
