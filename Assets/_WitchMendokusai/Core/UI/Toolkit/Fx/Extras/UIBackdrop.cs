using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 패널 뒤 살아 있는 배경. 열기 직전 게임 화면을 찍어 작게 줄이고 흐리게, 밝기 몇 단계를 두 색 사이로 칠해 깔고 천천히 물결
	/// UIFxProfile 의 backdrop 칸. 패널이 열릴 때 Show (콜백 뒤 패널을 열면 찍힌 화면에 패널이 안 들어감), 닫힐 때 Hide
	/// 층은 패널 바로 뒤 형제로 깔려 패널 움직임에 같이 줄지 않음
	/// </summary>
	[CreateAssetMenu(fileName = "Backdrop_0_Live", menuName = "WM/UI/Backdrop")]
	public sealed class UIBackdrop : ScriptableObject
	{
		[Tooltip("줄인 가로 크기, 픽셀. 작을수록 흐리고 가벼움")]
		[Min(16)] public int width = 192;
		[Tooltip("흐림 반경과 반복 횟수")]
		[Min(0)] public int blurRadius = 2;
		[Min(0)] public int blurPasses = 3;
		[Tooltip("밝기 단계 수. 0 이면 부드럽게")]
		[Min(0)] public int levels = 5;
		[Tooltip("가장 어두운 곳과 가장 밝은 곳 색. 밝기만 남기고 이 두 색 사이로 칠함")]
		public Color shadowColor = new(0.07f, 0.05f, 0.14f, 1f);
		public Color highlightColor = new(0.62f, 0.54f, 0.86f, 1f);
		[Tooltip("원래 색을 섞는 정도. 0 이면 두 색만")]
		[Range(0f, 1f)] public float originalAmount = 0.12f;
		public float fadeSeconds = 0.2f;
		[Tooltip("물결 크기 (배경이 화면보다 큰 비율) 와 한 번 도는 시간")]
		public float rippleScale = 0.04f;
		public float rippleSeconds = 6f;

		private const string LAYER_NAME = "fx-backdrop";

		private sealed class State
		{
			public VisualElement Layer;
			public Texture2D Texture;
			public Action Cancel;
			public int Generation;
		}

		private static readonly ConditionalWeakTable<VisualElement, State> states = new();

		/// <summary>panelRoot 바로 뒤에 배경을 깔고 onReady</summary>
		public void Show(VisualElement panelRoot, Action onReady = null)
		{
			State state = states.GetValue(panelRoot, _ => new State());
			int generation = ++state.Generation;
			UIFxRunner.CaptureScreen(captured =>
			{
				if (generation != state.Generation)
				{
					Destroy(captured);
					return;
				}
				Texture2D processed = Process(captured);
				Destroy(captured);
				if (state.Texture != null)
					Destroy(state.Texture);
				state.Texture = processed;
				EnsureLayer(state, panelRoot);
				state.Layer.style.backgroundImage = new StyleBackground(processed);
				Animate(state);
				onReady?.Invoke();
			});
		}

		public void Hide(VisualElement panelRoot)
		{
			if (states.TryGetValue(panelRoot, out State state) == false || state.Layer == null)
				return;
			state.Generation++;
			state.Cancel?.Invoke();
			VisualElement layer = state.Layer;
			float from = layer.resolvedStyle.opacity;
			state.Cancel = UIFxTween.Run(layer, fadeSeconds, 0f, progress => layer.style.opacity = from * (1f - progress), () =>
			{
				layer.style.display = DisplayStyle.None;
				layer.style.backgroundImage = StyleKeyword.Null;
				if (state.Texture != null)
					Destroy(state.Texture);
				state.Texture = null;
			});
		}

		private void EnsureLayer(State state, VisualElement panelRoot)
		{
			if (state.Layer == null)
			{
				state.Layer = UIFxTween.MakeLayer(LAYER_NAME);
				state.Layer.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
			}
			VisualElement host = panelRoot.parent;
			if (host == null)
				throw new InvalidOperationException($"{name}: 패널이 아직 화면에 붙지 않음");
			state.Layer.RemoveFromHierarchy();
			host.Insert(host.IndexOf(panelRoot), state.Layer);
			state.Layer.style.display = DisplayStyle.Flex;
		}

		private void Animate(State state)
		{
			state.Cancel?.Invoke();
			VisualElement layer = state.Layer;
			layer.style.opacity = 0f;
			float elapsed = 0f;
			bool stopped = false;
			layer.schedule.Execute(timer =>
			{
				elapsed += UIFxTween.Seconds(timer);
				layer.style.opacity = Mathf.Clamp01(elapsed / UIFxTween.Duration(fadeSeconds));
				float phase = elapsed / UIFxTween.Duration(rippleSeconds) * Mathf.PI * 2f;
				// 가로세로가 다른 박자로 출렁이는 물결 (모양 정의, 크기와 빠르기는 rippleScale, rippleSeconds)
				float grow = 1f + rippleScale * (1f + 0.5f * Mathf.Sin(phase * 0.7f));
				layer.style.scale = new Scale(new Vector2(grow + rippleScale * 0.3f * Mathf.Sin(phase), grow + rippleScale * 0.3f * Mathf.Cos(phase * 1.3f)));
			}).Every(0).Until(() => stopped);
			state.Cancel = () => stopped = true;
		}

		private Texture2D Process(Texture2D source)
		{
			int outWidth = Mathf.Min(width, source.width);
			int outHeight = Mathf.Max(1, Mathf.RoundToInt(outWidth * source.height / (float)source.width));
			Color[] pixels = new Color[outWidth * outHeight];
			for (int y = 0; y < outHeight; y++)
			{
				for (int x = 0; x < outWidth; x++)
					pixels[y * outWidth + x] = source.GetPixelBilinear((x + 0.5f) / outWidth, (y + 0.5f) / outHeight);
			}
			for (int pass = 0; pass < blurPasses; pass++)
				pixels = BoxBlur(pixels, outWidth, outHeight, blurRadius);
			for (int i = 0; i < pixels.Length; i++)
			{
				float light = pixels[i].grayscale;
				if (levels > 1)
				{
					float steps = levels - 1;
					light = Mathf.Round(light * steps) / steps;
				}
				Color color = Color.Lerp(Color.Lerp(shadowColor, highlightColor, light), pixels[i], originalAmount);
				color.a = 1f;
				pixels[i] = color;
			}
			Texture2D result = new(outWidth, outHeight, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
			result.SetPixels(pixels);
			result.Apply(false);
			return result;
		}

		private static Color[] BoxBlur(Color[] source, int columns, int rows, int radius)
		{
			if (radius <= 0)
				return source;
			Color[] horizontal = new Color[source.Length];
			Color[] result = new Color[source.Length];
			for (int y = 0; y < rows; y++)
			{
				for (int x = 0; x < columns; x++)
				{
					Color sum = Color.clear;
					int count = 0;
					for (int k = -radius; k <= radius; k++)
					{
						int sampleX = Mathf.Clamp(x + k, 0, columns - 1);
						sum += source[y * columns + sampleX];
						count++;
					}
					horizontal[y * columns + x] = sum / count;
				}
			}
			for (int y = 0; y < rows; y++)
			{
				for (int x = 0; x < columns; x++)
				{
					Color sum = Color.clear;
					int count = 0;
					for (int k = -radius; k <= radius; k++)
					{
						int sampleY = Mathf.Clamp(y + k, 0, rows - 1);
						sum += horizontal[sampleY * columns + x];
						count++;
					}
					result[y * columns + x] = sum / count;
				}
			}
			return result;
		}
	}
}
