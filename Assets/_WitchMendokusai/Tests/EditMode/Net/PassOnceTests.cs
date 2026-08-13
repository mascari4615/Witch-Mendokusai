using NUnit.Framework;
using WitchMendokusai.Net;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 통행증 한 장으로 <b>짐은 한 번만</b> (TASK-WM-259 → 309).
	///
	/// ★ 도장은 「지어낸 것」만 막는다. 진짜 통행증 한 장을 <b>복사해</b> 두 번 들어오는 것은
	///   도장으로 못 막는다 — 그러면 가방이 두 벌 들어온다(전형적인 복사 버그).
	///
	/// ★ 그렇다고 「내밀면 곧 쓴 것」으로 세면 더 나쁜 일이 난다 (실측 2026-08-13):
	///   통행증을 내밀다 줄이 끊기면 짐은 아직 안 왔는데 통행증만 타 버려, 그 사람이 다시 붙었을 때
	///   옆 세계가 그를 <b>처음 보는 손님</b>으로 맞았다. 그래서 「맡아 두기 → 건넨 뒤 적기」로 나눴다.
	/// </summary>
	public class PassOnceTests
	{
		[Test]
		public void 처음_내밀면_짐을_줘야_한다()
		{
			PassOnce used = new PassOnce();

			Assert.IsTrue(used.TryClaim("통행증", 1000L, out bool needsLuggage));
			Assert.IsTrue(needsLuggage);
		}

		[Test]
		public void 짐을_건넨_뒤_다시_오면_받아_주되_짐은_안_준다()
		{
			PassOnce used = new PassOnce();
			used.TryClaim("통행증", 1000L, out bool _);
			used.MarkDelivered("통행증", 1000L);

			Assert.IsTrue(used.TryClaim("통행증", 3000L, out bool needsLuggage), "같은 사람이 다시 붙은 것이다");
			Assert.IsFalse(needsLuggage, "한 장으로 짐을 두 번 받으면 가방이 두 벌 온다");
		}

		[Test]
		public void 같은_순간에_둘이_같은_통행증을_내밀면_뒤엣것은_거절()
		{
			PassOnce used = new PassOnce();

			Assert.IsTrue(used.TryClaim("통행증", 1000L, out bool _));
			Assert.IsFalse(used.TryClaim("통행증", 1100L, out bool _), "복사한 통행증으로 동시에 들어오는 자리다");
		}

		[Test]
		public void 다른_통행증은_따로_센다()
		{
			PassOnce used = new PassOnce();

			Assert.IsTrue(used.TryClaim("가", 1000L, out bool _));
			Assert.IsTrue(used.TryClaim("나", 1000L, out bool _));
		}

		[Test]
		public void 빈_것은_안_받는다()
		{
			PassOnce used = new PassOnce();

			Assert.IsFalse(used.TryClaim(null, 1000L, out bool _));
			Assert.IsFalse(used.TryClaim(string.Empty, 1000L, out bool _));
			Assert.AreEqual(0, used.Count);
		}

		[Test]
		public void 기한_지난_것은_안_들고_있는다()
		{
			// 안 버리면 이 표가 세계의 기억을 먹는다 — 하루면 수십만 장이다.
			PassOnce used = new PassOnce();
			used.TryClaim("옛것", 1000L, out bool _);
			used.MarkDelivered("옛것", 1000L);

			Assert.AreEqual(1, used.Count);
			used.TryClaim("새것", 1000L + TravelPass.GOOD_FOR_MS + 1, out bool _);
			Assert.AreEqual(1, used.Count, "기한 지난 통행증은 어차피 거절된다 — 들고 있을 이유가 없다");
		}
	}
}
