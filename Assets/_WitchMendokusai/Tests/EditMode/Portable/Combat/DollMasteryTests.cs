using System;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class DollMasteryTests
	{
		private const int LIGHT = 0;
		private const int SHADOW = 1;

		private static DollMastery Create() => new DollMastery(new[] { 3, 3 });

		[Test]
		public void 문턱에_닿는_순간_한_번만_준비를_알린다()
		{
			DollMastery mastery = Create();

			Assert.IsFalse(mastery.AddShard(LIGHT, 1));
			Assert.IsFalse(mastery.AddShard(LIGHT, 1));
			Assert.IsTrue(mastery.AddShard(LIGHT, 1));
			Assert.IsFalse(mastery.AddShard(LIGHT, 1), "찬 막대는 더 쌓지 않음");
			Assert.AreEqual(3, mastery.Progress(LIGHT));
		}

		[Test]
		public void 넘치는_조각은_문턱에서_멈춘다()
		{
			DollMastery mastery = Create();

			Assert.IsTrue(mastery.AddShard(SHADOW, 5));
			Assert.AreEqual(3, mastery.Progress(SHADOW));
		}

		[Test]
		public void 먼저_찬_쪽을_두고_다른_쪽도_채울_수_있다()
		{
			DollMastery mastery = Create();
			mastery.AddShard(LIGHT, 3);

			Assert.IsTrue(mastery.AddShard(SHADOW, 3));
			Assert.IsTrue(mastery.IsReady(LIGHT));
			Assert.IsTrue(mastery.IsReady(SHADOW));
		}

		[Test]
		public void 확정하면_조각을_더_받지_않는다()
		{
			DollMastery mastery = Create();
			mastery.AddShard(LIGHT, 3);
			mastery.AddShard(SHADOW, 1);

			mastery.Confirm(LIGHT);

			Assert.AreEqual(LIGHT, mastery.ConfirmedTrack);
			Assert.IsFalse(mastery.AcceptsShards);
			Assert.IsFalse(mastery.AddShard(SHADOW, 3));
			Assert.AreEqual(1, mastery.Progress(SHADOW));
		}

		[Test]
		public void 안_찬_갈래나_두_번째_확정은_실패한다()
		{
			DollMastery mastery = Create();
			mastery.AddShard(LIGHT, 3);
			mastery.AddShard(SHADOW, 3);

			Assert.Throws<InvalidOperationException>(() => Create().Confirm(LIGHT));
			mastery.Confirm(SHADOW);
			Assert.Throws<InvalidOperationException>(() => mastery.Confirm(LIGHT));
		}

		[Test]
		public void 조각_스탯은_상한까지만_준다()
		{
			Assert.AreEqual(2, DollMastery.PickupGrant(0, 2, 16));
			Assert.AreEqual(1, DollMastery.PickupGrant(15, 2, 16));
			Assert.AreEqual(0, DollMastery.PickupGrant(16, 2, 16));
			Assert.AreEqual(2, DollMastery.PickupGrant(100, 2, 0), "상한 0 은 무제한");
			Assert.AreEqual(0, DollMastery.PickupGrant(0, 0, 16));
		}

		[Test]
		public void 잘못된_문턱과_조각_양은_거부한다()
		{
			Assert.Throws<ArgumentException>(() => new DollMastery(new int[0]));
			Assert.Throws<ArgumentException>(() => new DollMastery(new[] { 3, 0 }));
			Assert.Throws<ArgumentException>(() => Create().AddShard(LIGHT, 0));
		}
	}
}
