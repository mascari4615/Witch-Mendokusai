using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// `[Inject] Construct` 는 <b>두 번 불려도 같아야 한다</b> — 지금 실제로 두 번 불리는 곳이 있기 때문이다.
	///
	/// ★ 실측 경로 (2026-08-06):
	///   `SceneLifetimeScope` 가 `container.InjectGameObject(player.gameObject)` 로 Player 계층 전체를 주입하고,
	///   그 안에서 깨어난 `Player.Construct` 가 다시 `InjectGameObjectExcludingSelf(gameObject, this)` 로
	///   <b>자식들을 한 번 더</b> 주입한다. 즉 Player 의 자식 Construct 는 씬 로드마다 2회 실행된다.
	///
	///   오늘 전수 조사에서 Construct 115개 중 이벤트 구독(`+=`)을 하는 것은 <b>0개</b>였다 —
	///   그래서 이 중복은 지금 무해하다. `515f6fb9`(WM-194)의 「Construct 는 멱등」이라는 근거가 참이다.
	///   문제는 그게 <b>아무 데도 안 적혀 있고 아무도 안 지킨다</b>는 것이다. 누군가 Construct 안에서
	///   `something.OnChanged += Handler` 를 한 줄 쓰는 순간 그 핸들러는 <b>두 번 걸리고</b>,
	///   증상은 「가끔 두 번 발동한다」로 나타난다 — 원인 추적이 가장 어려운 부류다.
	///
	/// 그래서 지금 참인 불변식을 기계로 내린다. 일부러 하려면 줄 끝에
	/// `// construct-side-effect-ok` + 사유를 달면 된다(`// init-order-ok` 선례와 같은 모양).
	///
	/// 정본 = TASK-WM-175(중복 주입 우려) / `Witch-Mendokusai/CLAUDE.md` § 객체 참조 획득.
	/// </summary>
	public class ConstructIdempotencyTests
	{
		private const string ROOT = "Assets/_WitchMendokusai";
		private const string ESCAPE_HATCH = "construct-side-effect-ok";

		private static readonly Regex ConstructHeader = new Regex(@"public void Construct\([^)]*\)\s*\{");
		private static readonly Regex Subscription = new Regex(@"\+=");

		/// <summary> 여는 중괄호 위치에서 짝이 맞는 닫는 중괄호까지. 못 찾으면 -1. </summary>
		private static int FindBlockEnd(string text, int openBraceIndex)
		{
			int depth = 0;
			for (int i = openBraceIndex; i < text.Length; i++)
			{
				if (text[i] == '{')
				{
					depth++;
				}
				else if (text[i] == '}')
				{
					depth--;
					if (depth == 0)
						return i;
				}
			}
			return -1;
		}

		private static List<string> ScanForSubscriptions(out int constructCount)
		{
			List<string> offenders = new List<string>();
			constructCount = 0;

			string rootFull = Path.GetFullPath(ROOT);
			foreach (string file in Directory.GetFiles(rootFull, "*.cs", SearchOption.AllDirectories))
			{
				string normalized = file.Replace('\\', '/');
				if (normalized.Contains("/Tests/"))
					continue;

				string text = File.ReadAllText(file);
				foreach (Match header in ConstructHeader.Matches(text))
				{
					int open = header.Index + header.Length - 1;
					int close = FindBlockEnd(text, open);
					if (close < 0)
						continue;

					constructCount++;
					string body = text.Substring(open, close - open);
					int lineOffset = text.Substring(0, open).Split('\n').Length;

					string[] lines = body.Split('\n');
					for (int i = 0; i < lines.Length; i++)
					{
						if (Subscription.IsMatch(lines[i]) == false)
							continue;
						if (lines[i].Contains(ESCAPE_HATCH))
							continue;

						offenders.Add(normalized.Substring(normalized.IndexOf("_WitchMendokusai"))
							+ ":" + (lineOffset + i) + "  " + lines[i].Trim());
					}
				}
			}
			return offenders;
		}

		[Test]
		public void Construct_안에서_구독하지_않는다()
		{
			List<string> offenders = ScanForSubscriptions(out int constructCount);

			// ★ 「0건 통과」가 「아무것도 못 봤다」와 구별되게 — 오늘 하루 이 실패 모양을 여러 번 봤다.
			Assert.Greater(constructCount, 50,
				$"Construct 를 {constructCount} 개밖에 못 찾았다 — 경로({ROOT})가 옮겨졌는지 확인할 것. "
				+ "위반이 없는 게 아니라 아무것도 안 본 것이다.");

			Assert.IsEmpty(offenders,
				"Construct 안에서 `+=` 를 한다 — Player 계층은 Construct 가 **2회** 실행되므로 "
				+ "구독이 두 번 걸린다(증상 = 「가끔 두 번 발동」):\n  " + string.Join("\n  ", offenders)
				+ "\n의도한 것이면 그 줄 끝에 `// " + ESCAPE_HATCH + "` + 사유.");
		}

		// 위 시험이 기대는 전제(= Player 계층이 실제로 두 번 주입된다)가 사라지면 알려준다.
		// 전제가 없어지면 이 시험의 엄격함도 근거를 잃으므로, 조용히 남아 있지 않게 한다.
		[Test]
		public void 이중_주입_전제가_아직_유효하다()
		{
			string player = File.ReadAllText(Path.GetFullPath(ROOT + "/Domain/Doll/_Common/Scripts/Player.cs"));
			string scope = File.ReadAllText(Path.GetFullPath(ROOT + "/Domain/Application/Scripts/DI/SceneLifetimeScope.cs"));

			bool playerCascades = player.Contains("InjectGameObjectExcludingSelf");
			bool scopeInjectsPlayerTree = scope.Contains("InjectGameObject(player.gameObject)");

			Assert.IsTrue(playerCascades && scopeInjectsPlayerTree,
				"Player 계층 이중 주입 전제가 바뀌었다 (Player.cascade=" + playerCascades
				+ ", Scope.injectsTree=" + scopeInjectsPlayerTree + "). "
				+ "한쪽이 사라졌으면 중복이 없어진 것이니 위 시험의 근거 문구를 갱신할 것 — "
				+ "전제가 죽은 채로 남은 규칙이 오늘 하루의 주제였다.");
		}
	}
}
