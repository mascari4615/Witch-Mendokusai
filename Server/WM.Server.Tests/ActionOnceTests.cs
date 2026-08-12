using NUnit.Framework;
using WitchMendokusai.Net;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// <b>같은 일을 두 번 하지 않는다</b> (TASK-WM-305).
	///
	/// ★ 왜: 끊기는 순간 보낸 줍기가 조용히 사라지는 것을 고치려면 창이 <b>다시 보내야</b> 하고,
	///   그러면 세계는 같은 것을 두 번 하지 말아야 한다. 그 기억이 이 장부다.
	/// </summary>
	public sealed class ActionOnceTests
	{
		[Test]
		public void 처음_것은_하고_같은_번호는_안_한다()
		{
			ActionOnce book = new ActionOnce();

			Assert.That(book.FirstTime(7, 100), Is.True);
			Assert.That(book.FirstTime(7, 100), Is.False, "다시 보낸 것을 또 하면 두 번 주워진다");
			Assert.That(book.FirstTime(7, 101), Is.True);
		}

		[Test]
		public void 사람마다_따로_센다()
		{
			ActionOnce book = new ActionOnce();

			Assert.That(book.FirstTime(1, 5), Is.True);
			Assert.That(book.FirstTime(2, 5), Is.True, "남의 번호에 내 일이 막히면 안 된다");
		}

		[Test]
		public void 번호를_안_붙인_옛_창은_안_막는다()
		{
			ActionOnce book = new ActionOnce();

			Assert.That(book.FirstTime(3, 0), Is.True);
			Assert.That(book.FirstTime(3, 0), Is.True, "번호가 없으면 판별할 수 없다 — 하던 대로 한다");
			Assert.That(book.FirstTime(3, -1), Is.True);
		}

		[Test]
		public void 기억은_최근_것만_남는다()
		{
			ActionOnce book = new ActionOnce();
			for (long id = 1; id <= ActionOnce.REMEMBER + 10; id += 1)
				Assert.That(book.FirstTime(9, id), Is.True);

			// 한참 전 번호는 잊었다 — 그때쯤이면 창도 답을 받고 지운 뒤다.
			Assert.That(book.FirstTime(9, 1), Is.True);

			// 최근 것은 아직 기억한다.
			Assert.That(book.FirstTime(9, ActionOnce.REMEMBER + 10), Is.False);
		}

		[Test]
		public void 떠난_사람은_놓는다()
		{
			ActionOnce book = new ActionOnce();
			book.FirstTime(4, 20);
			Assert.That(book.Count, Is.EqualTo(1));

			book.Forget(4);
			Assert.That(book.Count, Is.EqualTo(0));
			Assert.That(book.FirstTime(4, 20), Is.True);
		}
	}
}
