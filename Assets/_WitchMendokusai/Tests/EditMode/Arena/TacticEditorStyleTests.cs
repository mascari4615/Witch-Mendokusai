using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 전술 편집기가 쓰는 USS 클래스 이름이 <b>실제로 시트에 있는지</b> 기계로 대조한다.
	///
	/// ★ 왜 이 시험이 필요한가: USS 클래스 이름 오타는 <b>예외를 안 낸다.</b> 규칙이 그냥 안 걸리고
	///   요소는 스타일 없이 뜬다 — 즉 실패가 「예외」가 아니라 「좀 못생김」으로 나타난다.
	///   그런데 이 패널은 아직 아무도 렌더된 걸 본 적이 없다(WM-165 item 10 = PlayMode 미확인).
	///   눈으로 못 잡는 실패를 눈에 맡기면 영영 안 잡힌다. 그래서 텍스트로 대조한다.
	///
	/// 이 시험은 유니티 UI 를 안 띄운다 — 두 파일을 읽어 이름 집합을 비교할 뿐이라 빠르고 결정적이다.
	/// 정본 = CLAUDE.md § 코드로 짓는 UIToolkit 은 USS 로 (TASK-WM-206 / WM-179).
	/// </summary>
	public class TacticEditorStyleTests
	{
		private const string VIEW_PATH = "Assets/_WitchMendokusai/Domain/Arena/UI/TacticEditorView.cs";
		private const string SHEET_PATH = "Assets/_WitchMendokusai/Domain/UI/Slot.uss";

		// 이 패널의 클래스는 전부 이 접두사를 쓴다 — 시트 안 남의 규칙과 섞이지 않게.
		private const string CLASS_PREFIX = "wm-tactic-";

		private static string ReadRepoFile(string relativePath)
		{
			string full = Path.GetFullPath(relativePath);
			Assert.IsTrue(File.Exists(full), relativePath + " 가 없다 (파일이 옮겨졌으면 이 시험의 경로도 같이 고칠 것)");
			return File.ReadAllText(full);
		}

		/// <summary> .cs 의 `private const string CLASS_X = "wm-tactic-y";` 에서 실제 이름만 뽑는다. </summary>
		private static HashSet<string> ClassNamesUsedByView()
		{
			HashSet<string> used = new HashSet<string>();
			foreach (Match match in Regex.Matches(ReadRepoFile(VIEW_PATH), "\"(" + CLASS_PREFIX + "[a-z0-9-]+)\""))
				used.Add(match.Groups[1].Value);
			return used;
		}

		/// <summary> .uss 의 `.wm-tactic-y { ... }` 선택자에서 이름만 뽑는다. </summary>
		private static HashSet<string> ClassNamesDefinedInSheet()
		{
			HashSet<string> defined = new HashSet<string>();
			foreach (Match match in Regex.Matches(ReadRepoFile(SHEET_PATH), @"\.(" + CLASS_PREFIX + @"[a-z0-9-]+)\s*[,{]"))
				defined.Add(match.Groups[1].Value);
			return defined;
		}

		[Test]
		public void 뷰가_쓰는_클래스는_전부_시트에_있다()
		{
			HashSet<string> used = ClassNamesUsedByView();
			Assert.Greater(used.Count, 0, "뷰에서 클래스 이름을 하나도 못 찾았다 — 상수 형태가 바뀌었으면 이 시험의 정규식도 같이 고칠 것.");

			List<string> missing = new List<string>();
			HashSet<string> defined = ClassNamesDefinedInSheet();
			foreach (string name in used)
			{
				if (defined.Contains(name) == false)
					missing.Add(name);
			}

			Assert.IsEmpty(missing,
				"뷰가 쓰는데 " + SHEET_PATH + " 에 없는 클래스: " + string.Join(", ", missing)
				+ " — 오타면 그냥 스타일이 안 걸린 채 뜬다(예외 안 남).");
		}

		[Test]
		public void 시트의_전술_클래스는_전부_뷰가_쓴다()
		{
			// 반대 방향 — 안 쓰는 규칙이 남으면 다음 사람이 「이건 뭐지」로 시간을 쓴다.
			// 이름을 바꾸면 양쪽이 같이 깨지므로 한쪽만 고치고 넘어가는 일이 안 생긴다.
			HashSet<string> defined = ClassNamesDefinedInSheet();
			Assert.Greater(defined.Count, 0, "시트에서 " + CLASS_PREFIX + " 규칙을 하나도 못 찾았다.");

			List<string> unused = new List<string>();
			HashSet<string> used = ClassNamesUsedByView();
			foreach (string name in defined)
			{
				if (used.Contains(name) == false)
					unused.Add(name);
			}

			Assert.IsEmpty(unused,
				SHEET_PATH + " 에 있는데 아무도 안 쓰는 클래스: " + string.Join(", ", unused));
		}

		[Test]
		public void 뷰에_인라인_수치_스타일이_남아있지_않다()
		{
			// 「USS 로 옮겼다」가 반만 참인 상태를 막는다 — 폭·색·여백이 코드에 하나라도 남으면
			// 그 값만 재컴파일해야 고쳐지고, 그게 이 룰이 없애려던 바로 그 비용이다.
			// display 는 예외: 그건 생김새가 아니라 **런타임 상태**(그 종류가 안 쓰는 칸 감추기)다.
			List<string> offenders = new List<string>();
			foreach (Match match in Regex.Matches(ReadRepoFile(VIEW_PATH), @"\.style\.(\w+)"))
			{
				if (match.Groups[1].Value != "display")
					offenders.Add(match.Groups[1].Value);
			}

			Assert.IsEmpty(offenders,
				"TacticEditorView 에 인라인 스타일이 남았다: " + string.Join(", ", offenders)
				+ " — 생김새는 " + SHEET_PATH + " 로. (display 만 예외 = 런타임 상태)");
		}
	}
}
