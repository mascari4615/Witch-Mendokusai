using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 여러 층이 시차를 두고 움직이는 전환. 덮기와 걷기가 같은 방향으로 이어짐 (되감기 아님)
	/// 색과 수치는 각 UIWipe 에셋 (ShutterUIWipe 등)
	/// </summary>
	internal static class LayeredWipeMath
	{
		// 길이 0 구간의 0 나누기 방지
		private const float MIN_SEGMENT_LENGTH = 1e-4f;

		public static float OutExpo(float value) => value >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * value);
		public static float InCubic(float value) => value * value * value;

		/// <summary>전체 진행 중 start 부터 length 동안의 구간 진행 0~1</summary>
		public static float Segment(float progress, float start, float length) => Mathf.Clamp01((progress - start) / Mathf.Max(MIN_SEGMENT_LENGTH, length));

		public static VisualElement MakeRect(string elementName, Color color)
		{
			VisualElement element = new() { name = elementName, pickingMode = PickingMode.Ignore };
			element.style.position = Position.Absolute;
			element.style.backgroundColor = color;
			return element;
		}

		public static void Place(VisualElement element, float left, float top, float width, float height)
		{
			element.style.left = left;
			element.style.top = top;
			element.style.width = width;
			element.style.height = height;
		}

		public static void Turn(VisualElement element, float degrees) =>
			element.style.rotate = new UnityEngine.UIElements.Rotate(new UnityEngine.UIElements.Angle(degrees, AngleUnit.Degree));
	}

	/// <summary>비스듬한 띠들이 차례로 밀려 들어와 덮고, 같은 방향으로 밀려 나가며 걷힘. 띠 앞머리에 강조색 선</summary>
	internal sealed class ShutterWipe : IWipeRenderer
	{
		private readonly ShutterUIWipe preset;

		public ShutterWipe(ShutterUIWipe preset) => this.preset = preset;

		private VisualElement host;
		private readonly List<VisualElement> bands = new();
		private Vector2 size;

		public VisualElement Root => host;

		public void Attach(VisualElement parent)
		{
			host = WipeRenderers.MakeFill("wipe-shutter");
			host.style.overflow = Overflow.Hidden;
			parent.Add(host);
		}

		public void Detach() => host?.RemoveFromHierarchy();

		public void Resize(Vector2 newSize)
		{
			size = newSize;
			host.Clear();
			bands.Clear();
			int count = Mathf.Max(2, preset.bands);
			float slot = size.x / count;
			float width = slot * preset.overlap;
			float radians = Mathf.Abs(preset.angle) * Mathf.Deg2Rad;
			// 기울인 띠가 화면 위아래 끝까지 닿는 높이
			float height = size.y / Mathf.Cos(radians) + width * Mathf.Sin(radians);
			// 기울어져 화면 위아래 모서리에서 옆으로 밀린 만큼 양 끝에 띠를 더함
			int extra = Mathf.CeilToInt(size.y * 0.5f * Mathf.Tan(radians) / slot) + 1;
			for (int i = -extra; i < count + extra; i++)
			{
				Color tone = Color.Lerp(preset.color, preset.accentColor, (i & 1) == 0 ? 0f : preset.tint);
				tone.a = preset.color.a;
				VisualElement band = LayeredWipeMath.MakeRect("shutter-band", tone);
				LayeredWipeMath.Place(band, slot * i - (width - slot) * 0.5f, (size.y - height) * 0.5f, width, height);
				LayeredWipeMath.Turn(band, preset.angle);
				VisualElement edge = LayeredWipeMath.MakeRect("shutter-edge", preset.accentColor);
				LayeredWipeMath.Place(edge, 0f, 0f, preset.accentLine, height);
				band.Add(edge);
				host.Add(band);
				bands.Add(band);
			}
		}

		public void Draw(float progress, bool cover)
		{
			if (bands.Count == 0)
				return;
			float stagger = preset.stagger;
			float each = 1f - stagger;
			for (int i = 0; i < bands.Count; i++)
			{
				float local = LayeredWipeMath.Segment(progress, stagger * i / (bands.Count - 1), each);
				// 덮기는 오른쪽에서 들어와 멈추고, 걷기는 멈춘 자리에서 왼쪽으로 나감
				float offset = cover
					? (1f - LayeredWipeMath.OutExpo(local)) * size.x * preset.travel
					: -LayeredWipeMath.InCubic(local) * size.x * preset.travel;
				bands[i].style.translate = new Translate(offset, 0f);
			}
		}
	}

	/// <summary>사선 한 줄이 그어지고 굵어져 화면을 덮음, 섬광. 걷기는 그 선을 따라 두 쪽으로 갈라져 벌어짐</summary>
	internal sealed class SlashWipe : IWipeRenderer
	{
		private readonly SlashUIWipe preset;

		public SlashWipe(SlashUIWipe preset) => this.preset = preset;

		// 선을 긋는 구간과 굵어지는 구간 비율
		private const float DRAW_PART = 0.35f;
		// 굵어지기 곡선 지수. 클수록 끝에서 한 번에 덮음
		private const float FLOOD_POWER = 4f;
		// 섬광이 켜지기 시작하는 덮기 진행, 걷기에서 꺼지는 구간
		private const float FLASH_START = 0.8f;
		private const float FLASH_FADE_PART = 0.3f;

		private VisualElement host;
		private VisualElement band;
		private VisualElement line;
		private VisualElement firstHalf;
		private VisualElement secondHalf;
		private VisualElement flash;
		private Vector2 size;
		private float diagonal;

		public VisualElement Root => host;

		public void Attach(VisualElement parent)
		{
			host = WipeRenderers.MakeFill("wipe-slash");
			host.style.overflow = Overflow.Hidden;
			parent.Add(host);
		}

		public void Detach() => host?.RemoveFromHierarchy();

		public void Resize(Vector2 newSize)
		{
			size = newSize;
			diagonal = newSize.magnitude;
			host.Clear();
			band = LayeredWipeMath.MakeRect("slash-band", preset.color);
			line = LayeredWipeMath.MakeRect("slash-line", preset.accentColor);
			firstHalf = LayeredWipeMath.MakeRect("slash-half", preset.color);
			secondHalf = LayeredWipeMath.MakeRect("slash-half", preset.color);
			flash = WipeRenderers.MakeFill("slash-flash");
			flash.style.backgroundColor = preset.flashColor;
			foreach (VisualElement element in new[] { band, firstHalf, secondHalf, line })
			{
				LayeredWipeMath.Turn(element, -preset.angle);
				host.Add(element);
			}
			// 두 쪽은 선을 경계로 맞닿은 반평면
			float halfHeight = diagonal * 0.5f;
			LayeredWipeMath.Place(firstHalf, (size.x - diagonal) * 0.5f, size.y * 0.5f - halfHeight, diagonal, halfHeight);
			LayeredWipeMath.Place(secondHalf, (size.x - diagonal) * 0.5f, size.y * 0.5f, diagonal, halfHeight);
			firstHalf.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100));
			secondHalf.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(0));
			host.Add(flash);
		}

		public void Draw(float progress, bool cover)
		{
			if (band == null)
				return;
			float accent = preset.accentLine;
			if (cover)
			{
				SetHalves(false, 0f);
				float drawn = LayeredWipeMath.OutExpo(LayeredWipeMath.Segment(progress, 0f, DRAW_PART));
				float flood = Mathf.Pow(LayeredWipeMath.Segment(progress, DRAW_PART, 1f - DRAW_PART), FLOOD_POWER);
				float thickness = Mathf.Lerp(accent, diagonal, flood);
				PlaceAcross(band, diagonal * drawn, thickness);
				PlaceAcross(line, diagonal * drawn, accent);
				band.style.display = DisplayStyle.Flex;
				line.style.display = flood >= 1f ? DisplayStyle.None : DisplayStyle.Flex;
				// 다 덮는 순간 한 번 번쩍. 덮인 상태에는 남지 않음
				flash.style.opacity = Mathf.Sin(LayeredWipeMath.Segment(progress, FLASH_START, 1f - FLASH_START) * Mathf.PI) * preset.flashAlpha;
				return;
			}
			band.style.display = DisplayStyle.None;
			float apart = LayeredWipeMath.InCubic(progress) * diagonal * 0.5f;
			SetHalves(true, apart);
			PlaceAcross(line, diagonal, accent);
			line.style.display = progress >= 1f ? DisplayStyle.None : DisplayStyle.Flex;
			// 덮기 끝에 꺼진 선과 섬광이 갈라지는 순간 다시 한 번 켜졌다 짐 (튀지 않게 0 에서 시작)
			float cut = Mathf.Sin(LayeredWipeMath.Segment(progress, 0f, FLASH_FADE_PART) * Mathf.PI);
			line.style.opacity = cut;
			flash.style.opacity = cut * preset.flashAlpha;
		}

		// 화면 가운데를 지나는 길이 length, 두께 thickness 의 띠
		private void PlaceAcross(VisualElement element, float length, float thickness) =>
			LayeredWipeMath.Place(element, (size.x - length) * 0.5f, (size.y - thickness) * 0.5f, length, thickness);

		private void SetHalves(bool visible, float apart)
		{
			DisplayStyle display = visible ? DisplayStyle.Flex : DisplayStyle.None;
			firstHalf.style.display = display;
			secondHalf.style.display = display;
			// 선의 수직 방향 (부모 좌표) 으로 벌어짐
			float radians = -preset.angle * Mathf.Deg2Rad;
			Vector2 normal = new(-Mathf.Sin(radians), Mathf.Cos(radians));
			firstHalf.style.translate = new Translate(-normal.x * apart, -normal.y * apart);
			secondHalf.style.translate = new Translate(normal.x * apart, normal.y * apart);
		}
	}

	/// <summary>가로 조각들이 어긋난 채 색이 갈라져 나타나 자리를 잡고, 걷을 때 다시 어긋나며 사라짐</summary>
	internal sealed class GlitchWipe : IWipeRenderer
	{
		private readonly GlitchUIWipe preset;

		public GlitchWipe(GlitchUIWipe preset) => this.preset = preset;

		// 조각마다 나타나는 시점이 흩어지는 비율
		private const float APPEAR_SPREAD = 0.6f;
		// 걷기에서 조각이 흐려지기 시작하는 조각 진행
		private const float GLITCH_FADE_START = 0.7f;
		// 색 갈라짐이 반대로 튈 때의 비율
		private const float JITTER_BACK_RATIO = -0.6f;

		private sealed class Slice
		{
			public VisualElement Root;
			public VisualElement Main;
			public VisualElement Warm;
			public VisualElement Cold;
			public float Delay;
			public float Direction;
		}

		private VisualElement host;
		private readonly List<Slice> slices = new();
		private Vector2 size;

		public VisualElement Root => host;

		public void Attach(VisualElement parent)
		{
			host = WipeRenderers.MakeFill("wipe-glitch");
			host.style.overflow = Overflow.Hidden;
			parent.Add(host);
		}

		public void Detach() => host?.RemoveFromHierarchy();

		public void Resize(Vector2 newSize)
		{
			size = newSize;
			host.Clear();
			slices.Clear();
			int count = Mathf.Max(2, preset.slices);
			// 높이가 들쭉날쭉한 조각. 합이 화면 높이
			float[] weights = new float[count];
			float total = 0f;
			for (int i = 0; i < count; i++)
			{
				weights[i] = Mathf.Lerp(preset.sliceWeight.x, preset.sliceWeight.y, WipePatterns.Hash01(i, 71));
				total += weights[i];
			}
			float top = 0f;
			for (int i = 0; i < count; i++)
			{
				float height = size.y * weights[i] / total;
				Slice slice = new()
				{
					Root = LayeredWipeMath.MakeRect("glitch-slice", Color.clear),
					Warm = LayeredWipeMath.MakeRect("glitch-warm", preset.warm),
					Cold = LayeredWipeMath.MakeRect("glitch-cold", preset.cold),
					Main = LayeredWipeMath.MakeRect("glitch-main", preset.color),
					Delay = WipePatterns.Hash01(i, 97) * APPEAR_SPREAD,
					Direction = WipePatterns.Hash01(i, 13) < 0.5f ? -1f : 1f,
				};
				LayeredWipeMath.Place(slice.Root, 0f, top, size.x, height + 1f);
				foreach (VisualElement layer in new[] { slice.Warm, slice.Cold, slice.Main })
				{
					LayeredWipeMath.Place(layer, 0f, 0f, size.x, height + 1f);
					slice.Root.Add(layer);
				}
				host.Add(slice.Root);
				slices.Add(slice);
				top += height;
			}
		}

		public void Draw(float progress, bool cover)
		{
			float shiftRange = size.x * preset.shift;
			foreach (Slice slice in slices)
			{
				float local = LayeredWipeMath.Segment(progress, slice.Delay, 1f - APPEAR_SPREAD);
				// 덮기: 0 에서 나타나 어긋남이 줄며 자리. 걷기: 어긋남이 커지며 사라짐
				float settle = cover ? 1f - local : local;
				bool shown = cover ? local > 0f : local < 1f;
				// 걷기 끝은 어긋난 채 끊기지 않고 흐려지며 사라짐
				slice.Root.style.opacity = cover ? 1f : 1f - LayeredWipeMath.Segment(local, GLITCH_FADE_START, 1f - GLITCH_FADE_START);
				slice.Root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
				float shift = slice.Direction * shiftRange * settle * settle;
				// 색 갈라짐은 어긋남 동안 계단식으로 튐
				float jitter = Mathf.Floor(progress * preset.steps) % 2f == 0f ? 1f : JITTER_BACK_RATIO;
				slice.Main.style.translate = new Translate(shift, 0f);
				slice.Warm.style.translate = new Translate(shift + preset.split * settle * jitter, 0f);
				slice.Cold.style.translate = new Translate(shift - preset.split * settle * jitter, 0f);
			}
		}
	}

	/// <summary>브라운관이 꺼지듯 위아래가 닫혀 가로 빛줄이 되고, 그 줄이 점으로 줄어 사라짐. 걷기는 반대 순서로 켜짐</summary>
	internal sealed class TubeWipe : IWipeRenderer
	{
		private readonly TubeUIWipe preset;

		public TubeWipe(TubeUIWipe preset) => this.preset = preset;

		// 위아래가 닫히는 구간 비율. 나머지는 빛줄이 점으로
		private const float CLOSE_PART = 0.6f;
		// 닫히기 시작할 때 빛줄 두께, 화면 높이 비율
		private const float TUBE_OPEN_LINE_RATIO = 0.12f;

		private VisualElement host;
		private VisualElement upper;
		private VisualElement lower;
		private VisualElement line;
		private Vector2 size;

		public VisualElement Root => host;

		public void Attach(VisualElement parent)
		{
			host = WipeRenderers.MakeFill("wipe-tube");
			host.style.overflow = Overflow.Hidden;
			parent.Add(host);
		}

		public void Detach() => host?.RemoveFromHierarchy();

		public void Resize(Vector2 newSize)
		{
			size = newSize;
			host.Clear();
			upper = LayeredWipeMath.MakeRect("tube-upper", preset.color);
			lower = LayeredWipeMath.MakeRect("tube-lower", preset.color);
			line = LayeredWipeMath.MakeRect("tube-line", preset.flashColor);
			line.style.borderTopLeftRadius = Length.Percent(50);
			line.style.borderTopRightRadius = Length.Percent(50);
			line.style.borderBottomLeftRadius = Length.Percent(50);
			line.style.borderBottomRightRadius = Length.Percent(50);
			host.Add(upper);
			host.Add(lower);
			host.Add(line);
		}

		public void Draw(float progress, bool cover)
		{
			if (upper == null)
				return;
			// 덮기와 걷기를 같은 시간축 (0 열림, 1 꺼짐) 으로
			float off = cover ? progress : 1f - progress;
			float close = LayeredWipeMath.Segment(off, 0f, CLOSE_PART);
			float shrink = LayeredWipeMath.Segment(off, CLOSE_PART, 1f - CLOSE_PART);
			float eased = close * close;
			// 다 닫히면 가운데 1px 겹침 (홀수 높이와 배율에서 틈 방지)
			float half = size.y * 0.5f * eased + eased;
			LayeredWipeMath.Place(upper, 0f, 0f, size.x, half);
			LayeredWipeMath.Place(lower, 0f, size.y - half, size.x, half);

			float thickness = Mathf.Lerp(size.y * TUBE_OPEN_LINE_RATIO, preset.line, eased);
			float width = size.x * (1f - LayeredWipeMath.OutExpo(shrink));
			bool lineShown = close > 0f && off < 1f;
			line.style.display = lineShown ? DisplayStyle.Flex : DisplayStyle.None;
			LayeredWipeMath.Place(line, (size.x - width) * 0.5f, (size.y - thickness) * 0.5f, width, thickness);
			// 닫힐수록 줄이 밝아지고, 점이 된 끝에서 꺼짐
			line.style.opacity = eased * eased * (1f - shrink * shrink);
		}
	}
}
