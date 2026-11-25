using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class ShadowCloneRulesTests
	{
		private const float LIFE = 10f;
		private const float REFORM = 2f;

		private static ShadowCloneEvent Tick(ShadowCloneCycle cycle, float seconds, bool bursts) => cycle.Tick(seconds, bursts, LIFE, REFORM);

		[Test]
		public void CountNeedsRootAndStopsAtCap()
		{
			Assert.AreEqual(0, ShadowCloneRules.Count(0, 2, 3), "뿌리 없이는 수 카드만으로 안 나옴");
			Assert.AreEqual(1, ShadowCloneRules.Count(1, 0, 3));
			Assert.AreEqual(1, ShadowCloneRules.Count(1, -4, 3));
			Assert.AreEqual(3, ShadowCloneRules.Count(1, 2, 3));
			Assert.AreEqual(3, ShadowCloneRules.Count(1, 5, 3));
			Assert.AreEqual(0, ShadowCloneRules.Count(1, 0, 0), "상한이 먼저");
		}

		[Test]
		public void ShareFollowsFork()
		{
			Assert.AreEqual(40, ShadowCloneRules.SharePercent(false, 40, 65));
			Assert.AreEqual(65, ShadowCloneRules.SharePercent(true, 40, 65));
		}

		[Test]
		public void SlotsFanBehindTheOwner()
		{
			Assert.AreEqual(180f, ShadowCloneRules.SlotAngle(0, 1, 100f), 0.0001f);
			Assert.AreEqual(130f, ShadowCloneRules.SlotAngle(0, 3, 100f), 0.0001f);
			Assert.AreEqual(180f, ShadowCloneRules.SlotAngle(1, 3, 100f), 0.0001f);
			Assert.AreEqual(230f, ShadowCloneRules.SlotAngle(2, 3, 100f), 0.0001f);
		}

		[Test]
		public void CloneStaysWithoutFinisher()
		{
			ShadowCloneCycle cycle = new();
			Assert.AreEqual(ShadowCloneEvent.None, Tick(cycle, 100f, false));
			Assert.IsTrue(cycle.IsPresent);
			Assert.AreEqual(ShadowCloneEvent.None, Tick(cycle, 9.9f, true), "수명은 마무리를 받은 뒤부터");
		}

		[Test]
		public void CloneVanishesAndReforms()
		{
			ShadowCloneCycle cycle = new();
			Assert.AreEqual(ShadowCloneEvent.Vanished, Tick(cycle, 10f, true));
			Assert.IsFalse(cycle.IsPresent);
			Assert.AreEqual(ShadowCloneEvent.None, Tick(cycle, 1.9f, true));
			Assert.AreEqual(ShadowCloneEvent.Formed, Tick(cycle, 0.2f, true));
			Assert.AreEqual(ShadowCloneEvent.None, Tick(cycle, 9.8f, true));
			Assert.AreEqual(ShadowCloneEvent.Vanished, Tick(cycle, 0.2f, true));
		}

		[Test]
		public void LongFrameGivesOneEventAtATime()
		{
			ShadowCloneCycle cycle = new();
			Assert.AreEqual(ShadowCloneEvent.Vanished, Tick(cycle, 12.5f, true));
			Assert.AreEqual(ShadowCloneEvent.Formed, Tick(cycle, 0f, true));
			Assert.AreEqual(ShadowCloneEvent.None, Tick(cycle, 0f, true));
			Assert.AreEqual(ShadowCloneEvent.None, Tick(cycle, -3f, true));
		}

		[Test]
		public void LosingFinisherResetsLifeButHiddenCloneComesBack()
		{
			ShadowCloneCycle cycle = new();
			Tick(cycle, 6f, true);
			Tick(cycle, 100f, false);
			Assert.AreEqual(ShadowCloneEvent.None, Tick(cycle, 4f, true), "꺼졌다 켜지면 수명을 처음부터");
			Assert.AreEqual(ShadowCloneEvent.Vanished, Tick(cycle, 6f, true));
			Assert.AreEqual(ShadowCloneEvent.Formed, Tick(cycle, 2f, false));
		}

		[Test]
		public void StaggeredClonesVanishApart()
		{
			Assert.AreEqual(0f, ShadowCloneRules.Stagger(0, 1, LIFE), 0.0001f);
			ShadowCloneCycle first = new();
			ShadowCloneCycle second = new();
			first.Begin(ShadowCloneRules.Stagger(0, 2, LIFE));
			second.Begin(ShadowCloneRules.Stagger(1, 2, LIFE));
			Assert.AreEqual(ShadowCloneEvent.None, Tick(first, 5f, true));
			Assert.AreEqual(ShadowCloneEvent.Vanished, Tick(second, 5f, true));
		}
	}
}
