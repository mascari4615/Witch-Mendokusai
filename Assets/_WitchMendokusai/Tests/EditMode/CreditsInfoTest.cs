using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace WitchMendokusai.Tests
{
	// 글꼴 라이선스 (SIL OFL 1.1) 고지가 빌드에 실리는 경로: UIRoot 프리팹 -> CreditsInfo -> 라이선스 TextAsset
	public class CreditsInfoTest
	{
		private const string UI_ROOT_PATH = "Assets/_WitchMendokusai/Core/Assets/Singletons/UIRoot.prefab";
		private static readonly string[] FONT_NAMES = { "Gowun Batang", "Black Han Sans", "Noto Sans KR" };

		private static CreditsInfo Credits => AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(UI_ROOT_PATH).GetComponent<UIRoot>().Credits;

		[Test]
		public void Ui_root_carries_credits() => Assert.IsNotNull(Credits);

		[Test]
		public void Every_bundled_font_is_credited()
			=> CollectionAssert.AreEquivalent(FONT_NAMES, Credits.Fonts.Select(entry => entry.Name).ToArray());

		[Test]
		public void Every_credit_has_ofl_text_and_copyright()
		{
			foreach (CreditEntry entry in Credits.Fonts)
			{
				Assert.IsNotNull(entry.License, entry.Name);
				StringAssert.Contains("Open Font License", entry.License.text, entry.Name);
				StringAssert.Contains("1.1", entry.LicenseName, entry.Name);
				Assert.IsNotEmpty(entry.CopyrightLine, entry.Name);
			}
		}

		[Test]
		public void At_least_one_credit_has_full_license_text()
			=> Assert.IsTrue(Credits.Fonts.Any(entry => entry.License.text.Contains("PERMISSION & CONDITIONS")));

		[TestCase("\n\n  Copyright 2021 A  \nrest", "Copyright 2021 A")]
		[TestCase("", "")]
		public void First_line_skips_blank_lines(string text, string expected)
			=> Assert.AreEqual(expected, CreditEntry.FirstLine(text));
	}
}
