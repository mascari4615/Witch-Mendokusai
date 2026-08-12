using NUnit.Framework;
using WitchMendokusai.Net;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// <b>되감아 판정하기</b>의 밑감 두 개 (TASK-WM-303) — 지나간 자리 기억 · 회선 시간 재기.
	///
	/// ★ 왜 여기부터: 회선이 나쁜 사람은 같은 싸움에 손짓이 더 든다(실측: 곧은 46 · 100ms 58 · 250ms 70).
	///   고치려면 세계가 ① 그 사람이 <b>보고 있던 순간</b>을 알고 ② 그 순간의 <b>남의 자리</b>를 알아야 한다.
	/// </summary>
	public sealed class RewindJudgeTests
	{
		[Test]
		public void 두_점_사이는_이어서_읽는다()
		{
			PastPlaces places = new PastPlaces();
			places.Remember(1, 1000, new Vector3(0f, 0f, 0f));
			places.Remember(1, 1100, new Vector3(1f, 0f, 0f));

			Assert.That(places.Where(1, 1050, out Vector3 middle), Is.True);
			Assert.That(middle.x, Is.EqualTo(0.5f).Within(0.001f), "판 사이를 곧게 이어 읽어야 계단이 안 생긴다");
		}

		[Test]
		public void 기억_밖은_지어내지_않고_끝값을_준다()
		{
			PastPlaces places = new PastPlaces();
			places.Remember(2, 1000, new Vector3(3f, 0f, 0f));
			places.Remember(2, 1100, new Vector3(4f, 0f, 0f));

			Assert.That(places.Where(2, 500, out Vector3 tooOld), Is.True);
			Assert.That(tooOld.x, Is.EqualTo(3f).Within(0.001f));

			Assert.That(places.Where(2, 9000, out Vector3 tooNew), Is.True);
			Assert.That(tooNew.x, Is.EqualTo(4f).Within(0.001f));
		}

		[Test]
		public void 모르는_사람은_모른다고_한다()
		{
			PastPlaces places = new PastPlaces();
			Assert.That(places.Where(99, 1000, out Vector3 _), Is.False);
		}

		[Test]
		public void 오래된_발자국은_버린다()
		{
			PastPlaces places = new PastPlaces();
			for (long at = 0; at <= 5000; at += 50)
				places.Remember(3, at, new Vector3(at / 1000f, 0f, 0f));

			// 기억 밖(4초 전)을 물으면 <b>남아 있는 가장 옛 것</b>이 나온다 — 무한히 안 쌓인다는 뜻이다.
			Assert.That(places.Where(3, 1000, out Vector3 old), Is.True);
			Assert.That(old.x, Is.GreaterThanOrEqualTo((5000 - PastPlaces.KEEP_MS) / 1000f - 0.001f));
		}

		[Test]
		public void 세계가_왕복을_재고_절반만_되감는다()
		{
			LineTime line = new LineTime();
			Assert.That(line.HeardStamp(7, 1000, 1200), Is.True, "세계가 찍은 도장을 되받으면 왕복이 나온다");
			Assert.That(line.RewindMsFor(7), Is.EqualTo(100), "왕복 200ms → 화면은 100ms 옛것");
		}

		[Test]
		public void 말이_안_되는_도장은_안_받는다()
		{
			LineTime line = new LineTime();
			Assert.That(line.HeardStamp(7, 5000, 1200), Is.False, "미래에서 온 도장");
			Assert.That(line.HeardStamp(7, 1000, 100000), Is.False, "한참 전 도장으로 회선을 늘릴 수 없다");
			Assert.That(line.RewindMsFor(7), Is.EqualTo(0));
		}

		[Test]
		public void 아무리_멀어도_한도까지만_되감는다()
		{
			LineTime line = new LineTime();
			for (int i = 1; i <= 40; i += 1)
				line.HeardStamp(8, i * 10000, i * 10000 + 3000);

			Assert.That(line.RewindMsFor(8), Is.EqualTo(LineTime.MOST_REWIND_MS));
		}

		[Test]
		public void 나간_사람은_회선도_놓는다()
		{
			LineTime line = new LineTime();
			line.HeardStamp(9, 1000, 1300);
			Assert.That(line.RewindMsFor(9), Is.GreaterThan(0));

			line.Forget(9);
			Assert.That(line.RewindMsFor(9), Is.EqualTo(0));
		}
	}
}
