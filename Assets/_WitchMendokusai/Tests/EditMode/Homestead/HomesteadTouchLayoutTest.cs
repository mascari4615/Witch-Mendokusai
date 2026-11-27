using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 생활 손가락 버튼이 다른 화면 요소와 겹치지 않는지 수치로 잼. 화면 없이 Homestead.uss 원문을 읽음
	// 좌표는 패널 단위 (세로 800 기준, 위 왼쪽이 0). 폰 2400x1080 은 가로 1778, 데스크톱 1920x1080 은 가로 1422
	// 기본 HUD (uGUI, 씬 프리팹) 사각형은 USS 에 없어 C8 촬영 (2400x1080, 2026-11-27) 에서 잰 값. 모서리에 붙어 있다고 봄
	public class HomesteadTouchLayoutTest
	{
		private const float PANEL_HEIGHT = 800f;
		// 한글 한 자는 글자 크기만큼, 나머지 (숫자, 빗금, 칸) 는 0.6 배로 어림. 줄 높이는 1.45 배
		private const float HANGUL_WIDTH = 1f;
		private const float OTHER_WIDTH = 0.6f;
		private const float LINE_HEIGHT = 1.45f;
		// 가진 수는 끝이 없어서 세 자리로 어림
		private const int WIDE_HAVE = 999;

		private static readonly int[][] SCREENS = { new[] { 2400, 1080 }, new[] { 1920, 1080 } };

		private readonly struct Box
		{
			public readonly string Name;
			public readonly float Left;
			public readonly float Top;
			public readonly float Right;
			public readonly float Bottom;

			public Box(string name, float left, float top, float right, float bottom)
			{
				Name = name;
				Left = left;
				Top = top;
				Right = right;
				Bottom = bottom;
			}

			public bool Overlaps(Box other) => Left < other.Right && other.Left < Right && Top < other.Bottom && other.Top < Bottom;

			public override string ToString() => $"{Name} x[{Left:0},{Right:0}] y[{Top:0},{Bottom:0}]";
		}

		private static string currentUss;

		// 시험마다 다시 읽음. USS 만 고치면 다시 컴파일되지 않아 정적 값이 옛 글을 들고 있을 수 있음
		[SetUp]
		public void ReadStyleSheet() => currentUss = HomesteadTestKit.StyleSheetText();

		private static float PanelWidth(int[] screen) => screen[0] * PANEL_HEIGHT / screen[1];

		// 기본 HUD. C8 촬영 1200x540 축소본에서 잰 픽셀을 패널 단위로 (x2 / 1.35)
		private static List<Box> BaseHud(float width)
		{
			return new List<Box>
			{
				new("13/13 과 냥", width - 252f, PANEL_HEIGHT - 98f, width - 77f, PANEL_HEIGHT - 30f),
				new("왼쪽 아래 체력", 74f, 696f, 363f, 770f),
				new("가운데 아래 기술 셋", width / 2f - 30f, 711f, width / 2f + 178f, 778f),
				new("오른쪽 위 메뉴", width - 104f, 44f, width - 41f, 107f),
			};
		}

		// 낚시 눈금 (화면 가운데 아래). 높이는 단계마다 달라 넉넉히 160
		private static Box Duel(float width)
		{
			float bottom = Px(".wm-hs-hud__duel", "bottom");
			float half = Px(".wm-hs-hud__duel", "width") / 2f;
			return new Box("낚시 눈금", width / 2f - half, PANEL_HEIGHT - bottom - 160f, width / 2f + half, PANEL_HEIGHT - bottom);
		}

		private static (Box Reel, Box Stop) FishingButtons(float width, float columnBottom)
		{
			float right = Px(".wm-hs-fish__touch", "right");
			float reel = Px(".wm-hs-fish__reel", "height");
			float reelWidth = Px(".wm-hs-fish__reel", "width");
			float stopHeight = Px(".wm-hs-fish__stop", "height");
			float stopWidth = Px(".wm-hs-fish__stop", "min-width");
			float stopGap = Px(".wm-hs-fish__stop", "margin-bottom");
			float reelBottom = PANEL_HEIGHT - columnBottom;
			float stopBottom = reelBottom - reel - stopGap;
			return (new Box("채기 버튼", width - right - reelWidth, reelBottom - reel, width - right, reelBottom),
				new Box("그만두기", width - right - stopWidth, stopBottom - stopHeight, width - right, stopBottom));
		}

		[Test]
		public void Fishing_buttons_clear_base_hud_and_duel_on_both_screens()
		{
			float columnBottom = Px(".wm-hs-fish__touch", "bottom");
			foreach (int[] screen in SCREENS)
			{
				float width = PanelWidth(screen);
				(Box reel, Box stop) = FishingButtons(width, columnBottom);
				List<Box> others = BaseHud(width);
				others.Add(Duel(width));
				foreach (Box other in others)
				{
					Assert.IsFalse(reel.Overlaps(other), $"{screen[0]}x{screen[1]}: {reel} / {other}");
					Assert.IsFalse(stop.Overlaps(other), $"{screen[0]}x{screen[1]}: {stop} / {other}");
				}
				Assert.GreaterOrEqual(stop.Top, 0f);
			}
		}

		// 이 시험이 C8 의 겹침을 잡는지. 고치기 전 자리 (아래 48) 는 13/13 칸과 겹쳐야 함
		[Test]
		public void Old_fishing_spot_is_caught_as_overlap()
		{
			(Box reel, Box _) = FishingButtons(PanelWidth(SCREENS[0]), 48f);
			Assert.IsTrue(reel.Overlaps(BaseHud(PanelWidth(SCREENS[0]))[0]));
		}

		[Test]
		public void Fishing_buttons_hide_once_the_duel_is_over()
		{
			Assert.IsTrue(FishingTouch.ShowsButtons(FishingPhase.Waiting));
			Assert.IsTrue(FishingTouch.ShowsButtons(FishingPhase.Bite));
			Assert.IsTrue(FishingTouch.ShowsButtons(FishingPhase.Duel));
			Assert.IsFalse(FishingTouch.ShowsButtons(FishingPhase.Caught));
			Assert.IsFalse(FishingTouch.ShowsButtons(FishingPhase.Snapped));
			Assert.IsFalse(FishingTouch.ShowsButtons(FishingPhase.Escaped));
		}

		// 귀환 소식 판은 한 줄만 있어도 꾸미기 자리까지 내려옴. 그래서 판이 떠 있는 동안 꾸미기는 숨어야 함
		[Test]
		public void Digest_reaches_decorate_spot_so_decorate_hides_while_digest_shows()
		{
			Box decorate = DecorateBox();
			Box digest = DigestBox(1);
			Assert.IsTrue(digest.Overlaps(decorate), $"{digest} / {decorate}");
			Assert.IsFalse(DecorateButton.IsShown(true, true, true));
			Assert.IsTrue(DecorateButton.IsShown(true, true, false));
			Assert.IsFalse(DecorateButton.IsShown(false, true, false));
			Assert.IsFalse(DecorateButton.IsShown(true, false, false));
		}

		private static Box DecorateBox()
		{
			float left = Px(".wm-hs-hud__decorate", "left");
			float top = Percent(".wm-hs-hud__decorate", "top") * PANEL_HEIGHT;
			return new Box("꾸미기", left, top, left + Px(".wm-hs-hud__decorate", "min-width"), top + Px(".wm-hs-hud__decorate", "height"));
		}

		private static Box DigestBox(int rows)
		{
			float left = Px(".wm-hs-hud__digest", "left");
			float top = Px(".wm-hs-hud__digest", "top");
			float[] padding = Sides(".wm-hs-hud__digest-card", "padding");
			float border = Px(".wm-hs-hud__digest-card", "border-width");
			float title = Px(".wm-hs-hud__digest-title", "font-size") * LINE_HEIGHT + Px(".wm-hs-hud__digest-title", "margin-bottom");
			float height = border * 2f + padding[0] + padding[2] + title + rows * Px(".wm-hs-hud__digest-row", "height");
			return new Box("귀환 소식", left, top, left + Px(".wm-hs-hud__digest-card", "min-width"), top + height);
		}

		// 막대 칸 하나에 이름과 재료가 들어가는지. 재료 줄은 글자 넓이로, 줄 수는 그림이 줄어들 수 있는 만큼
		[Test]
		public void Build_slot_text_fits_inside_slot()
		{
			const string SLOT = ".wm-hs-build__slot";
			float[] padding = Sides(SLOT, "padding");
			float border = Px(SLOT, "border-width");
			float innerWidth = Px(SLOT, "width") - padding[1] - padding[3] - border * 2f;
			float innerHeight = Px(SLOT, "height") - padding[0] - padding[2] - border * 2f;
			float nameSize = Px(".wm-hs-build__name", "font-size");
			float costSize = Px(".wm-hs-build__cost", "font-size");
			float icon = Px(".wm-hs-build__icon", "min-height") + Px(".wm-hs-build__icon", "margin-bottom");

			HomesteadCatalog catalog = HomesteadTestKit.LoadCatalog();
			int checkedCount = 0;
			foreach (PlaceableDef placeable in catalog.Placeables)
			{
				if (placeable.Kind == PlaceableKind.Building)
					continue;
				List<(string Name, int Have, int Need)> parts = new();
				foreach (KeyValuePair<string, int> pair in placeable.Cost.Goods)
					parts.Add((catalog.Get<GoodsDef>(pair.Key).Name, WIDE_HAVE, pair.Value));
				string[] lines = BuildSlotText.Cost(parts).Split('\n');
				float used = icon + Lines(placeable.Name, nameSize, innerWidth) * nameSize * LINE_HEIGHT;
				foreach (string line in lines)
				{
					Assert.LessOrEqual(TextWidth(line, costSize), innerWidth, $"{placeable.Name}: '{line}'");
					used += costSize * LINE_HEIGHT;
				}
				Assert.LessOrEqual(used, innerHeight, $"{placeable.Name}: 글자 {lines.Length}줄");
				checkedCount++;
			}
			Assert.Greater(checkedCount, 0);
		}

		// 고치기 전 꼴 (한 줄에 두 칸 띄어 이음) 은 정원 나무에서 넘쳐야 함. 이 시험이 C8 의 넘침을 잡는지
		[Test]
		public void Old_single_line_cost_is_caught_as_overflow()
		{
			const string SLOT = ".wm-hs-build__slot";
			float[] padding = Sides(SLOT, "padding");
			float innerWidth = Px(SLOT, "width") - padding[1] - padding[3] - Px(SLOT, "border-width") * 2f;
			Assert.Greater(TextWidth($"나무 {WIDE_HAVE}/2  이파리 {WIDE_HAVE}/4", Px(".wm-hs-build__cost", "font-size")), innerWidth);
		}

		[Test]
		public void Cost_text_puts_one_good_per_line()
		{
			Assert.AreEqual("나무 0/2\n이파리 3/4", BuildSlotText.Cost(new List<(string, int, int)> { ("나무", 0, 2), ("이파리", 3, 4) }));
			Assert.AreEqual(string.Empty, BuildSlotText.Cost(new List<(string, int, int)>()));
		}

		private static int Lines(string text, float size, float width) => Math.Max(1, (int)Math.Ceiling(TextWidth(text, size) / width));

		private static float TextWidth(string text, float size)
		{
			float width = 0f;
			foreach (char letter in text)
				width += size * (letter >= '가' && letter <= '힣' ? HANGUL_WIDTH : OTHER_WIDTH);
			return width;
		}

		// ---- USS 읽기. 선택자 한 줄로 시작하는 덩어리의 속성 하나

		private static string Value(string selector, string property)
		{
			Match block = Regex.Match(currentUss, "^" + Regex.Escape(selector) + @"\s*\{([^}]*)\}", RegexOptions.Multiline);
			Assert.IsTrue(block.Success, $"USS 에 {selector} 없음");
			Match value = Regex.Match(block.Groups[1].Value, @"(?:^|[\s;])" + Regex.Escape(property) + @"\s*:\s*([^;]+);");
			Assert.IsTrue(value.Success, $"{selector} 에 {property} 없음");
			return value.Groups[1].Value.Trim();
		}

		private static float Px(string selector, string property) => Number(Value(selector, property), "px");

		private static float Percent(string selector, string property) => Number(Value(selector, property), "%") / 100f;

		// 위 오른쪽 아래 왼쪽 순서로 펼침 (CSS 줄임 규칙)
		private static float[] Sides(string selector, string property)
		{
			string[] parts = Value(selector, property).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			float[] numbers = Array.ConvertAll(parts, part => Number(part, "px"));
			switch (numbers.Length)
			{
				case 1:
					return new[] { numbers[0], numbers[0], numbers[0], numbers[0] };
				case 2:
					return new[] { numbers[0], numbers[1], numbers[0], numbers[1] };
				case 3:
					return new[] { numbers[0], numbers[1], numbers[2], numbers[1] };
				default:
					return numbers;
			}
		}

		private static float Number(string text, string unit)
		{
			string trimmed = text.EndsWith(unit, StringComparison.Ordinal) ? text.Substring(0, text.Length - unit.Length) : text;
			return float.Parse(trimmed, CultureInfo.InvariantCulture);
		}
	}
}
