using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.Net;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 옆 세계로 들고 가는 <b>통행증</b> (TASK-WM-253).
	///
	/// ★ 이 통행증은 <b>창이 들고 간다</b>. 그러니 창이 그 안을 고칠 수 있으면
	///   「걸어서 국경을 넘으면 가방이 가득 차는」 세계가 된다. 여기서 그걸 막는다.
	/// </summary>
	public class TravelPassTests
	{
		private const string SECRET = "두 세계만 아는 말";

		private static TravelPass.Bundle Sample(long madeAtMs)
		{
			return new TravelPass.Bundle(42, 12.5f, -3.25f,
				new List<(int, int)> { (7, 3), (9, 1) }, madeAtMs);
		}

		[Test]
		public void 만든_것을_그대로_읽는다()
		{
			string pass = TravelPass.Write(Sample(1000L), SECRET);

			Assert.IsTrue(TravelPass.TryRead(pass, SECRET, 2000L, out TravelPass.Bundle came, out TravelPass.Refusal why));
			Assert.AreEqual(TravelPass.Refusal.None, why);
			Assert.AreEqual(42, came.IdentityId);
			Assert.AreEqual(12.5f, came.X, 0.01f);
			Assert.AreEqual(-3.25f, came.Z, 0.01f);
			Assert.AreEqual(2, came.Bag.Count);
			Assert.AreEqual(7, came.Bag[0].ItemId);
			Assert.AreEqual(3, came.Bag[0].Amount);
		}

		[Test]
		public void 가방을_고쳐_오면_안_받는다()
		{
			string pass = TravelPass.Write(Sample(1000L), SECRET);
			string greedy = pass.Replace(";7,3", ";7,999");

			Assert.IsFalse(TravelPass.TryRead(greedy, SECRET, 2000L, out _, out TravelPass.Refusal why));
			Assert.AreEqual(TravelPass.Refusal.BadSeal, why,
				"국경을 넘으며 가방을 고칠 수 있으면 그건 세계가 아니라 편집기다");
		}

		[Test]
		public void 남의_이름으로_들어오지_못한다()
		{
			string pass = TravelPass.Write(Sample(1000L), SECRET);
			string stolen = pass.Replace("42;", "43;");

			Assert.IsFalse(TravelPass.TryRead(stolen, SECRET, 2000L, out _, out TravelPass.Refusal why));
			Assert.AreEqual(TravelPass.Refusal.BadSeal, why);
		}

		[Test]
		public void 다른_비밀로_만든_것은_안_받는다()
		{
			string pass = TravelPass.Write(Sample(1000L), "남의 말");

			Assert.IsFalse(TravelPass.TryRead(pass, SECRET, 2000L, out _, out TravelPass.Refusal why));
			Assert.AreEqual(TravelPass.Refusal.BadSeal, why, "창이 도장을 지어내면 아무나 들어온다");
		}

		[Test]
		public void 기한이_지나면_안_받는다()
		{
			string pass = TravelPass.Write(Sample(1000L), SECRET);

			Assert.IsFalse(TravelPass.TryRead(pass, SECRET, 1000L + TravelPass.GOOD_FOR_MS + 1, out _, out TravelPass.Refusal why));
			Assert.AreEqual(TravelPass.Refusal.TooOld, why,
				"도장이 영원하면 오늘 받은 통행증으로 내일 또 들어온다 — 가방이 복사된다");
		}

		[Test]
		public void 앞당겨_만든_것도_안_받는다()
		{
			string pass = TravelPass.Write(Sample(100000L), SECRET);

			Assert.IsFalse(TravelPass.TryRead(pass, SECRET, 1000L, out _, out TravelPass.Refusal why));
			Assert.AreEqual(TravelPass.Refusal.FromTheFuture, why);
		}

		[Test]
		public void 모양이_아니면_안_받는다()
		{
			Assert.IsFalse(TravelPass.TryRead(null, SECRET, 1000L, out _, out TravelPass.Refusal a));
			Assert.AreEqual(TravelPass.Refusal.Garbled, a);

			Assert.IsFalse(TravelPass.TryRead("도장없음", SECRET, 1000L, out _, out TravelPass.Refusal b));
			Assert.AreEqual(TravelPass.Refusal.Garbled, b);

			Assert.IsFalse(TravelPass.TryRead("|", SECRET, 1000L, out _, out TravelPass.Refusal c));
			Assert.AreEqual(TravelPass.Refusal.Garbled, c);
		}

		[Test]
		public void 빈_가방도_그대로_간다()
		{
			TravelPass.Bundle empty = new TravelPass.Bundle(5, 0f, 0f, null, 1000L);
			string pass = TravelPass.Write(empty, SECRET);

			Assert.IsTrue(TravelPass.TryRead(pass, SECRET, 1500L, out TravelPass.Bundle came, out _));
			Assert.AreEqual(0, came.Bag.Count);
			Assert.AreEqual(5, came.IdentityId);
		}
	}
}
