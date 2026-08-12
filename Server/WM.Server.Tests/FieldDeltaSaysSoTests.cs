using System.Linq;
using NUnit.Framework;
using WitchMendokusai;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 「바뀐 자리만」 보낼 때는 <b>그렇다고 말해야 한다</b> (TASK-WM-230).
	///
	/// ★ 왜 이 시험이 있나: 세계는 이 값을 <b>인자로 받아 놓고 쓰지 않고 있었다</b>. 컴파일도 되고
	///   시험도 초록이고 화면도 멀쩡했다 — 다만 창이 부분 목록을 전체로 알고 통째로 갈아 끼워
	///   들판 67자리가 한 번에 사라졌다(실측 2026-08-12). 「받아 놓고 안 쓰는 인자」는
	///   말이 안 나가는 것과 같고, 그건 아무도 안 본다. 그래서 글자로 못박는다.
	/// </summary>
	public class FieldDeltaSaysSoTests
	{
		private static GatherableNode[] OneNode()
		{
			return new[] { new GatherableNode { Id = 7, X = 1f, Z = 2f, ItemId = 3, Amount = 4 } };
		}

		[Test]
		public void 바뀐_자리만_보낼_때는_바뀌었다고_말한다()
		{
			string said = Protocol.WorldSnapshot(Enumerable.Empty<WorldDoll>(), null, null, null,
				OneNode(), null, null, 1, null, true, null, true, null);

			StringAssert.Contains("\"fieldChanged\":true", said,
				"부분 목록을 전체처럼 보내면 창이 나머지를 통째로 지운다");
		}

		[Test]
		public void 사라진_자리도_번호로_알려_준다()
		{
			string said = Protocol.WorldSnapshot(Enumerable.Empty<WorldDoll>(), null, null, null,
				OneNode(), null, null, 1, null, true, null, true, new[] { 11, 12 });

			StringAssert.Contains("\"fieldGone\":[11,12]", said,
				"뽑아 간 자리를 안 알려 주면 창에는 없는 것이 계속 보인다");
		}

		[Test]
		public void 전부_보낼_때는_바뀌었다는_말을_안_붙인다()
		{
			string said = Protocol.WorldSnapshot(Enumerable.Empty<WorldDoll>(), null, null, null,
				OneNode(), null, null, 1, null, true, null, false, null);

			StringAssert.DoesNotContain("fieldChanged", said, "전부인데 「바뀐 것만」이라 하면 창이 옛것을 안 지운다");
		}
	}
}
