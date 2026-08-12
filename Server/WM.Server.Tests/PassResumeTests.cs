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
