using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>캐릭터 그림이 위에서 떨어져 자리 잡고 둥실. 그림을 바꾸면 잠깐 비켰다가 새 그림이 다시 떨어짐. 탭, 메뉴 열기에서 Show</summary>
	[CreateAssetMenu(fileName = "Character_0_Drop", menuName = "WM/UI/Character Motion")]
	public sealed class UICharacterMotion : ScriptableObject
	{
		[Tooltip("떨어지기 시작 높이, 그림 높이 비율")]
		public float dropHeight = 0.28f;
		public float dropSeconds = 0.38f;
		[Tooltip("떨어지는 동안 불투명해지는 구간 비율")]
		[Range(0.05f, 1f)] public float fadeInPart = 0.33f;
		[Tooltip("착지 때 넘치는 깊이, 그림 높이 비율")]
		public float landDip = 0.03f;
		[Tooltip("이전 그림이 비키는 시간과 거리, 픽셀")]
		public float leaveSeconds = 0.09f;
		public float leaveDistance = 14f;
		[Tooltip("둥실 높이 (그림 높이 비율) 와 한 번 시간")]
		public float bobHeight = 0.015f;
		public float bobSeconds = 2.6f;

		// 떨어지기와 자리 잡기를 가르는 진행 지점 (곡선 모양 정의)
		private const float LAND_POINT = 0.7f;

		private sealed class State
		{
			public Action Cancel;
			public int Generation;
		}

		private static readonly ConditionalWeakTable<VisualElement, State> states = new();

		/// <summary>image 의 그림을 next 로 바꾸며 등장. next 가 비어 있으면 지금 그림으로 다시 등장</summary>
		public void Show(VisualElement image, Background next = default)
		{
			State state = states.GetValue(image, _ => new State());
			state.Cancel?.Invoke();
			int generation = ++state.Generation;
			bool swap = next.texture != null || next.sprite != null;
			Action land = () =>
			{
				if (generation != state.Generation)
					return;
				if (swap)
					image.style.backgroundImage = new StyleBackground(next);
				state.Cancel = UIFxTween.Run(image, dropSeconds, 0f, progress =>
				{
					float offset = progress < LAND_POINT
						? Mathf.Lerp(-dropHeight, landDip, UIFxTween.OutCubic(progress / LAND_POINT))
						: Mathf.Lerp(landDip, 0f, UIFxTween.Smooth((progress - LAND_POINT) / (1f - LAND_POINT)));
					// 높이 비율 이동이라 레이아웃 전에도 맞음
					image.style.translate = new Translate(Length.Percent(0f), Length.Percent(offset * 100f));
					image.style.opacity = Mathf.Clamp01(progress / fadeInPart);
				}, () => state.Cancel = Bob(image));
			};
			if (swap && image.resolvedStyle.opacity > UIFxTween.VISIBLE_OPACITY)
			{
				state.Cancel = UIFxTween.Run(image, leaveSeconds, 0f, progress =>
				{
					image.style.translate = new Translate(leaveDistance * progress, 0f);
					image.style.opacity = 1f - progress;
				}, land);
				return;
			}
			land();
		}

		private Action Bob(VisualElement image)
		{
			float elapsed = 0f;
			bool stopped = false;
			image.schedule.Execute(timer =>
			{
				if (UIIdleFx.IsShown(image) == false)
					return;
				elapsed += UIFxTween.Seconds(timer);
				float wave = 0.5f - 0.5f * Mathf.Cos(elapsed / UIFxTween.Duration(bobSeconds) * Mathf.PI * 2f);
				image.style.translate = new Translate(Length.Percent(0f), Length.Percent(-wave * bobHeight * 100f));
			}).Every(0).Until(() => stopped || image.panel == null);
			return () => stopped = true;
		}
	}
}
