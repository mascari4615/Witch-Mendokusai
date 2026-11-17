using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>UI 연출 에셋 공용 시간 진행. 실제 시간 기준 (시간 정지 중에도 동작), 요소가 패널에서 떨어지면 일정도 멈춤</summary>
	public static class UIFxTween
	{
		private const float MILLISECONDS_PER_SECOND = 1000f;
		// 멈칫 뒤 한 프레임에 끝까지 가지 않게 하는 상한
		private const float MAX_STEP_SECONDS = 0.1f;

		/// <summary>delay 뒤 seconds 동안 진행 0~1 을 step 에 넘기고 끝나면 onDone. 돌려준 함수를 부르면 중단 (onDone 안 부름)</summary>
		public static Action Run(VisualElement owner, float seconds, float delay, Action<float> step, Action onDone = null)
		{
			float elapsed = -delay;
			bool done = false;
			owner.schedule.Execute(timer =>
			{
				if (done)
					return;
				elapsed += Mathf.Min(timer.deltaTime / MILLISECONDS_PER_SECOND, MAX_STEP_SECONDS);
				if (elapsed < 0f)
					return;
				float progress = seconds <= 0f ? 1f : Mathf.Clamp01(elapsed / seconds);
				step(progress);
				if (progress < 1f)
					return;
				done = true;
				onDone?.Invoke();
			}).Every(0).Until(() => done);
			return () => done = true;
		}

		/// <summary>seconds 뒤 한 번</summary>
		public static Action After(VisualElement owner, float seconds, Action action) => Run(owner, 0f, seconds, _ => { }, action);

		/// <summary>0 나누기를 막는 최소 길이, 초</summary>
		public const float MIN_DURATION = 0.01f;
		/// <summary>이보다 흐리면 안 보이는 것으로 봄</summary>
		public const float VISIBLE_OPACITY = 0.01f;

		/// <summary>이번 프레임 실제 시간, 초 (상한 있음)</summary>
		public static float Seconds(TimerState timer) => Mathf.Min(timer.deltaTime / MILLISECONDS_PER_SECOND, MAX_STEP_SECONDS);

		/// <summary>0 이하 길이를 막은 길이</summary>
		public static float Duration(float seconds) => Mathf.Max(MIN_DURATION, seconds);

		public static float OutCubic(float value) => 1f - Mathf.Pow(1f - value, 3f);
		public static float InCubic(float value) => value * value * value;
		public static float Smooth(float value) => value * value * (3f - 2f * value);

		/// <summary>끝에서 살짝 넘쳤다 돌아옴. overshoot 0 이면 OutCubic 과 비슷</summary>
		public static float OutBack(float value, float overshoot)
		{
			float shifted = value - 1f;
			return 1f + (overshoot + 1f) * shifted * shifted * shifted + overshoot * shifted * shifted;
		}

		/// <summary>요소 하나를 덮개 층 위 같은 자리에. 루트 좌표 기준 절대 배치</summary>
		public static void PlaceOver(VisualElement layer, VisualElement overlay, Rect worldRect)
		{
			Vector2 topLeft = overlay.WorldToLocal(worldRect.min);
			Vector2 bottomRight = overlay.WorldToLocal(worldRect.max);
			layer.style.position = Position.Absolute;
			layer.style.left = topLeft.x;
			layer.style.top = topLeft.y;
			layer.style.width = bottomRight.x - topLeft.x;
			layer.style.height = bottomRight.y - topLeft.y;
		}

		public static VisualElement MakeLayer(string layerName)
		{
			VisualElement layer = new() { name = layerName, pickingMode = PickingMode.Ignore };
			layer.style.position = Position.Absolute;
			layer.style.left = 0;
			layer.style.top = 0;
			layer.style.right = 0;
			layer.style.bottom = 0;
			layer.AddToClassList(UIIdleFx.LAYER_CLASS);
			layer.AddToClassList(UIIdleFx.SKIP_CLASS);
			return layer;
		}

		public static void SetBorder(VisualElement element, float width, Color color)
		{
			element.style.borderTopWidth = width;
			element.style.borderBottomWidth = width;
			element.style.borderLeftWidth = width;
			element.style.borderRightWidth = width;
			element.style.borderTopColor = color;
			element.style.borderBottomColor = color;
			element.style.borderLeftColor = color;
			element.style.borderRightColor = color;
		}

		public static void SetRadius(VisualElement element, Length radius)
		{
			element.style.borderTopLeftRadius = radius;
			element.style.borderTopRightRadius = radius;
			element.style.borderBottomLeftRadius = radius;
			element.style.borderBottomRightRadius = radius;
		}

		public static UnityEngine.UIElements.Rotate Degrees(float degrees) => new(new UnityEngine.UIElements.Angle(degrees, AngleUnit.Degree));
	}
}
