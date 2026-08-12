using NUnit.Framework;
using WitchMendokusai.Net;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 통행증은 <b>한 번만</b> (TASK-WM-259).
	///
	/// ★ 도장은 「지어낸 것」만 막는다. 진짜 통행증 한 장을 <b>복사해</b> 두 번 들어오는 것은
	///   도장으로 못 막는다 — 그러면 가방이 두 벌 들어온다(전형적인 복사 버그).
	/// </summary>
	public class PassOnceTests
	{
		[Test]
		public void 같은_통행증은_두_번_안_통한다()
		{
			PassOnce used = new PassOnce();

			Assert.IsTrue(used.TryUse("통행증", 1000L));
			Assert.IsFalse(used.TryUse("통행증", 1100L), "한 장으로 두 번 들어오면 가방이 두 벌 온다");
		}

		[Test]
		public void 다른_통행증은_따로_센다()
		{
			PassOnce used = new PassOnce();

			Assert.IsTrue(used.TryUse("가", 1000L));
			Assert.IsTrue(used.TryUse("나", 1000L));
		}

		[Test]
		public void 빈_것은_안_받는다()
		{
			PassOnce used = new PassOnce();

			Assert.IsFalse(used.TryUse(null, 1000L));
			Assert.IsFalse(used.TryUse(string.Empty, 1000L));
			Assert.AreEqual(0, used.Count);
		}

		[Test]
		public void 기한_지난_것은_안_들고_있는다()
		{
			// 안 버리면 이 표가 세계의 기억을 먹는다 — 하루면 수십만 장이다.
			PassOnce used = new PassOnce();
			used.TryUse("옛것", 1000L);

			Assert.AreEqual(1, used.Count);
			used.TryUse("새것", 1000L + TravelPass.GOOD_FOR_MS + 1);
			Assert.AreEqual(1, used.Count, "기한 지난 통행증은 어차피 거절된다 — 들고 있을 이유가 없다");
		}
	}
}
