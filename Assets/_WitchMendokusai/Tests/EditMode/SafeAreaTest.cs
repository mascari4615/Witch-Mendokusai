using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.Presentation;

namespace WitchMendokusai.Tests
{
	// 안전 영역 계산의 단일 출처 (SafeArea) 와 그 사용 규칙
	public class SafeAreaTest
	{
		private const float TOLERANCE = 0.0001f;
		// 이 파일 자체가 걸리지 않게 나눠 씀
		private const string SCREEN_SAFE_AREA = "Screen." + "safeArea";

		// 가로 2400x1080 폰, 왼쪽 노치 100px, 아래 제스처 바 30px
		private static readonly Rect NOTCH_PIXELS = Rect.MinMaxRect(100f, 30f, 2400f, 1080f);
		private static Rect NotchNormalized => SafeArea.Normalize(NOTCH_PIXELS, 2400f, 1080f);

		[Test]
		public void Normalize_turns_pixels_into_screen_ratio()
		{
			Rect normalized = NotchNormalized;
			Assert.AreEqual(100f / 2400f, normalized.xMin, TOLERANCE);
			Assert.AreEqual(30f / 1080f, normalized.yMin, TOLERANCE);
			Assert.AreEqual(1f, normalized.xMax, TOLERANCE);
			Assert.AreEqual(1f, normalized.yMax, TOLERANCE);
		}

		[Test]
		public void Full_screen_safe_area_leaves_anchors_untouched()
		{
			Rect full = SafeArea.Normalize(Rect.MinMaxRect(0f, 0f, 1920f, 1080f), 1920f, 1080f);
			Assert.AreEqual(new Vector2(0f, 1f), SafeArea.RemapAnchor(full, new Vector2(0f, 1f)));
			Assert.AreEqual(new Vector2(1f, 0f), SafeArea.RemapAnchor(full, new Vector2(1f, 0f)));
		}

		[Test]
		public void Corner_anchor_moves_to_safe_corner()
		{
			Vector2 topLeft = SafeArea.RemapAnchor(NotchNormalized, new Vector2(0f, 1f));
			Assert.AreEqual(100f / 2400f, topLeft.x, TOLERANCE);
			Assert.AreEqual(1f, topLeft.y, TOLERANCE);

			Vector2 bottomRight = SafeArea.RemapAnchor(NotchNormalized, new Vector2(1f, 0f));
			Assert.AreEqual(1f, bottomRight.x, TOLERANCE);
			Assert.AreEqual(30f / 1080f, bottomRight.y, TOLERANCE);
		}

		[Test]
		public void Insets_are_percent_of_full_screen_parent_with_top_flipped()
		{
			VisualElement element = new();
			SafeArea.ApplyInsets(element, NotchNormalized);
			Assert.AreEqual(LengthUnit.Percent, element.style.left.value.unit);
			Assert.AreEqual(100f / 2400f * 100f, element.style.left.value.value, TOLERANCE);
			Assert.AreEqual(0f, element.style.right.value.value, TOLERANCE);
			Assert.AreEqual(0f, element.style.top.value.value, TOLERANCE);
			Assert.AreEqual(30f / 1080f * 100f, element.style.bottom.value.value, TOLERANCE);
		}

		// 중복 금지: 화면 안전 영역은 SafeArea 만 읽음. 다른 자리에서 다시 계산하면 기기마다 어긋남
		[Test]
		public void Only_safe_area_reads_screen_safe_area()
		{
			string root = Path.Combine(Application.dataPath, "_WitchMendokusai");
			string[] offenders = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
				.Where(path => Path.GetFileName(path) != "SafeArea.cs")
				.Where(path => File.ReadAllText(path).Contains(SCREEN_SAFE_AREA))
				.Select(path => path.Substring(root.Length + 1))
				.ToArray();
			CollectionAssert.IsEmpty(offenders);
		}
	}
}
