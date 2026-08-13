using NUnit.Framework;
using WitchMendokusai.Net;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 통행증 한 장으로 <b>짐은 한 번만</b>, 그러나 <b>들어오기는 다시</b> (TASK-WM-309).
	///
	/// ★ 왜: 통행증을 내밀다 줄이 끊기면 예전에는 그 통행증이 타 버렸다 —
	///   다시 붙은 사람은 옆 세계에서 <b>처음 보는 손님</b>이 됐다(장부에 신원이 하나 더 쌓였다).
	/// </summary>
	public sealed class PassResumeTests
	{
		[Test]
		public void 처음_내밀면_짐을_줘야_한다()
		{
			PassOnce book = new PassOnce();

			Assert.That(book.TryClaim("통행증-가", 1000, out bool needsLuggage), Is.True);
			Assert.That(needsLuggage, Is.True);
		}

		[Test]
		public void 짐을_건넨_뒤_다시_오면_받아_주되_짐은_안_준다()
		{
			PassOnce book = new PassOnce();
			book.TryClaim("통행증-나", 1000, out bool _);
			book.MarkDelivered("통행증-나", 1000);

			Assert.That(book.TryClaim("통행증-나", 5000, out bool needsLuggage), Is.True, "같은 사람이 다시 붙은 것이다");
			Assert.That(needsLuggage, Is.False, "또 주면 그게 가방 복사다");
		}

		[Test]
		public void 같은_순간에_둘이_같은_통행증을_내밀면_뒤엣것은_거절()
		{
			PassOnce book = new PassOnce();

			Assert.That(book.TryClaim("통행증-다", 1000, out bool _), Is.True);
			Assert.That(book.TryClaim("통행증-다", 1200, out bool _), Is.False, "복사한 통행증으로 동시에 들어오는 자리다");
		}

		/// <summary>
		/// 맡아 둔 통행증은 <b>주인에게는 열려 있어야 한다</b> (TASK-WM-337).
		///
		/// ★ 실측 2026-08-14: 집은 뒤 짐을 받기 전에 끊긴 사람이 곧바로 다시 붙으면 거절당해
		///   <b>가방 없는 손님</b>이 됐다(관문에서 가방 3 → 0). 10초를 기다려야 제 짐을 찾았다.
		///   막으려던 것은 <b>남의</b> 동시 복사이지 그 사람의 재시도가 아니다.
		/// </summary>
		[Test]
		public void 집은_뒤_끊긴_사람은_곧바로_다시_맡을_수_있다()
		{
			PassOnce book = new PassOnce();

			Assert.That(book.TryClaim("통행증-라", 1000, "나그네", out bool first), Is.True);
			Assert.That(first, Is.True);

			// 짐을 못 받고 끊겼다 — 곧바로 다시 붙는다(10초를 못 기다린다).
			Assert.That(book.TryClaim("통행증-라", 1500, "나그네", out bool again), Is.True, "제 통행증에서 쫓겨나면 안 된다");
			Assert.That(again, Is.True, "아직 짐을 못 받았으니 이번엔 줘야 한다");
		}

		[Test]
		public void 남이_같은_종이를_들고_오면_여전히_거절한다()
		{
			PassOnce book = new PassOnce();

			Assert.That(book.TryClaim("통행증-마", 1000, "나그네", out bool _), Is.True);
			Assert.That(book.TryClaim("통행증-마", 1200, "낯선이", out bool _), Is.False, "이게 진짜 복사 시도다");
		}

		[Test]
		public void 주인을_안_밝히면_예전처럼_거절한다()
		{
			PassOnce book = new PassOnce();

			Assert.That(book.TryClaim("통행증-바", 1000, out bool _), Is.True);
			Assert.That(book.TryClaim("통행증-바", 1200, out bool _), Is.False, "누구인지 모르면 막는 쪽이 안전하다");
		}

		[Test]
		public void 반쪽으로_죽은_시도는_통행증을_영영_묶지_않는다()
		{
			PassOnce book = new PassOnce();
			book.TryClaim("통행증-라", 1000, out bool _);

			// 도착이 끝내 안 됐다 — 맡아 둔 것이 풀린 뒤에는 다시 시도할 수 있어야 한다.
			Assert.That(book.TryClaim("통행증-라", 1000 + PassOnce.CLAIM_GOOD_FOR_MS + 1, out bool needsLuggage), Is.True);
			Assert.That(needsLuggage, Is.True, "짐을 아직 못 받았으니 이번엔 줘야 한다");
		}

		[Test]
		public void 기한이_지난_기억은_버린다()
		{
			PassOnce book = new PassOnce();
			book.TryClaim("통행증-마", 1000, out bool _);
			book.MarkDelivered("통행증-마", 1000);
			Assert.That(book.Count, Is.EqualTo(1));

			book.TryClaim("통행증-바", 1000 + TravelPass.GOOD_FOR_MS + 1, out bool _);
			Assert.That(book.Count, Is.EqualTo(1), "옛 통행증은 어차피 도장이 거절한다 — 여기 쌓아 둘 이유가 없다");
		}
	}
}
