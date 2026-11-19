using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace WitchMendokusai.Tests
{
	// 크레딧 고지가 빌드에 실리는 경로: UIRoot 프리팹 -> CreditsInfo -> 라이선스 TextAsset
	// 항목 필수 필드, origin 과 라이선스 파일 대조는 CreditOriginCheck 와 같은 규칙
	public class CreditsInfoTest
	{
		private const string UI_ROOT_PATH = "Assets/_WitchMendokusai/Core/Assets/Singletons/UIRoot.prefab";
		private static readonly string[] FONT_NAMES = { "Gowun Batang", "Black Han Sans", "Noto Sans KR", "Galmuri", "Barlow Condensed", "Liberation Sans" };

		private static CreditsInfo Credits => AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(UI_ROOT_PATH).GetComponent<UIRoot>().Credits;

		[Test]
		public void Ui_root_carries_credits() => Assert.IsNotNull(Credits);

		[Test]
		public void Ui_root_credits_is_the_only_credits_asset()
			=> Assert.AreSame(Credits, CreditOriginCheck.FindCredits());

		[Test]
		public void Every_bundled_font_is_credited()
			=> CollectionAssert.AreEquivalent(FONT_NAMES, Credits.InCategory(CreditCategory.Font).Select(entry => entry.Name).ToArray());

		[Test]
		public void Every_font_credit_has_ofl_text_and_copyright()
		{
			foreach (CreditEntry entry in Credits.InCategory(CreditCategory.Font))
			{
				Assert.IsNotNull(entry.License, entry.Name);
				StringAssert.Contains("Open Font License", entry.License.text, entry.Name);
				StringAssert.Contains("1.1", entry.LicenseName, entry.Name);
				Assert.IsNotEmpty(entry.CopyrightLine, entry.Name);
			}
		}

		[Test]
		public void At_least_one_credit_has_full_license_text()
			=> Assert.IsTrue(Credits.Entries.Any(entry => entry.License != null && entry.License.text.Contains("PERMISSION & CONDITIONS")));

		[Test]
		public void Every_credit_fills_required_fields()
		{
			List<string> problems = Credits.Validate();
			Assert.IsEmpty(problems, string.Join("\n", problems));
		}

		[Test]
		public void Attribution_required_credits_name_author_and_license_url()
		{
			foreach (CreditEntry entry in Credits.Entries.Where(entry => entry.AttributionRequired))
			{
				Assert.IsNotEmpty(entry.Author, entry.Id);
				Assert.IsTrue(entry.License != null || string.IsNullOrWhiteSpace(entry.LicenseUrl) == false, entry.Id);
				Assert.IsNotEmpty(entry.CopyrightLine, entry.Id);
			}
		}

		[Test]
		public void Cc_by_credits_are_attribution_required()
		{
			foreach (CreditEntry entry in Credits.Entries.Where(entry => entry.LicenseName.StartsWith("CC BY", System.StringComparison.Ordinal)))
				Assert.IsTrue(entry.AttributionRequired, entry.Id);
		}

		// 비영리, 변경 금지 조건은 상업 출시와 충돌. 사용자 결정 대기 목록에 있는 것만 허용 (memo wm/design/credits-20261120/inventory.md 위험 절)
		private static readonly string[] RESTRICTED_LICENSE_PENDING_DECISION = { "protodome-bluenoise" };

		[Test]
		public void Non_commercial_or_no_derivative_licenses_are_pending_decision()
		{
			string[] restricted = Credits.Entries
				.Where(entry => entry.LicenseName.Contains("-NC", System.StringComparison.Ordinal) || entry.LicenseName.Contains("-ND", System.StringComparison.Ordinal))
				.Select(entry => entry.Id)
				.ToArray();
			CollectionAssert.AreEquivalent(RESTRICTED_LICENSE_PENDING_DECISION, restricted);
		}

		[Test]
		public void Every_category_is_in_display_order()
			=> CollectionAssert.AreEquivalent(System.Enum.GetValues(typeof(CreditCategory)), CreditCategoryText.Order);

		[Test]
		public void Repository_origins_and_license_files_match_credits()
		{
			List<string> problems = CreditOriginCheck.Run(Credits, CreditOriginCheck.ProjectRoot);
			Assert.IsEmpty(problems, string.Join("\n", problems));
		}

		[TestCase("\n\n  Copyright 2021 A  \nrest", "Copyright 2021 A")]
		[TestCase("", "")]
		public void First_line_skips_blank_lines(string text, string expected)
			=> Assert.AreEqual(expected, CreditEntry.FirstLine(text));

		[Test]
		public void Origin_file_parses_credit_and_license()
		{
			CreditOrigin origin = CreditOrigin.Parse("﻿credit: quaternius-claymore\r\n# 메모\nlicense: CC0 1.0\n");
			Assert.AreEqual("quaternius-claymore", origin.CreditId);
			Assert.AreEqual("CC0 1.0", origin.LicenseName);
		}

		[Test]
		public void Origin_with_unknown_credit_or_other_license_is_reported()
		{
			Assert.IsNotEmpty(new CreditOrigin("no-such-credit", "CC0 1.0").Check(Credits, "x.origin"));
			Assert.IsNotEmpty(new CreditOrigin("quaternius-claymore", "CC BY 4.0").Check(Credits, "x.origin"));
			Assert.IsNotEmpty(new CreditOrigin(string.Empty, string.Empty).Check(Credits, "x.origin"));
			Assert.IsEmpty(new CreditOrigin("quaternius-claymore", "CC0 1.0").Check(Credits, "x.origin"));
		}
	}
}
