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

		private const string MARK = "9f2c1abe";

		private static TravelPass.Bundle Sample(long madeAtMs)
		{
			return new TravelPass.Bundle(MARK, "마스카", 12.5f, -3.25f,
				new List<(int, int)> { (7, 3), (9, 1) }, madeAtMs, 70);
		}

		[Test]
		public void 만든_것을_그대로_읽는다()
		{
			string pass = TravelPass.Write(Sample(1000L), SECRET);

			Assert.IsTrue(TravelPass.TryRead(pass, SECRET, 2000L, out TravelPass.Bundle came, out TravelPass.Refusal why));
			Assert.AreEqual(TravelPass.Refusal.None, why);
			Assert.AreEqual(MARK, came.Mark, "번호가 아니라 세계 공통 이름표가 건너간다");
			Assert.AreEqual("마스카", came.Name, "이름이 안 가면 국경을 넘는 순간 친구가 「손님 7」이 된다");
			Assert.AreEqual(12.5f, came.X, 0.01f);
			Assert.AreEqual(-3.25f, came.Z, 0.01f);
			Assert.AreEqual(70, came.Health, "몸을 안 들고 가면 국경이 회복 장소가 된다");
			Assert.AreEqual(2, came.Bag.Count);
			Assert.AreEqual(7, came.Bag[0].ItemId);
			Assert.AreEqual(3, came.Bag[0].Amount);
		}

		[Test]
		public void 몸을_고쳐_오면_안_받는다()
		{
			string pass = TravelPass.Write(Sample(1000L), SECRET);
			string healed = pass.Replace(";70;", ";100;");

			Assert.IsFalse(TravelPass.TryRead(healed, SECRET, 2000L, out _, out TravelPass.Refusal why));
			Assert.AreEqual(TravelPass.Refusal.BadSeal, why, "국경을 넘으며 몸을 채울 수 있으면 싸움이 뜻이 없다");
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
			string stolen = pass.Replace(MARK + ";", "9f2c1abf;");

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
			TravelPass.Bundle empty = new TravelPass.Bundle(MARK, string.Empty, 0f, 0f, null, 1000L, 100);
			string pass = TravelPass.Write(empty, SECRET);

			Assert.IsTrue(TravelPass.TryRead(pass, SECRET, 1500L, out TravelPass.Bundle came, out _));
			Assert.AreEqual(0, came.Bag.Count);
			Assert.AreEqual(MARK, came.Mark);
		}

		[Test]
		public void 이름에_칸_나누는_글자가_있어도_안_어긋난다()
		{
			// 이름은 사람이 짓는다 — 「;」 하나로 가방 칸이 밀리면 남의 물건이 들어온다.
			TravelPass.Bundle odd = new TravelPass.Bundle(MARK, "가;나|다%라", 1f, 2f,
				new List<(int, int)> { (4, 2) }, 1000L, 55);
			string pass = TravelPass.Write(odd, SECRET);

			Assert.IsTrue(TravelPass.TryRead(pass, SECRET, 1500L, out TravelPass.Bundle came, out _));
			Assert.AreEqual("가;나|다%라", came.Name);
			Assert.AreEqual(1, came.Bag.Count);
			Assert.AreEqual(4, came.Bag[0].ItemId);
			Assert.AreEqual(2, came.Bag[0].Amount);
			Assert.AreEqual(55, came.Health);
		}

		[Test]
		public void 이름표가_없는_통행증은_안_받는다()
		{
			// 이름표가 비면 「누구인지 모르는 사람」이 남의 가방을 들고 서 있게 된다.
			TravelPass.Bundle nameless = new TravelPass.Bundle(string.Empty, "가", 0f, 0f, null, 1000L, 100);

			Assert.IsFalse(TravelPass.TryRead(TravelPass.Write(nameless, SECRET), SECRET, 1500L, out _, out _));
		}
	}
}
