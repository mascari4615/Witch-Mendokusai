using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class ShadowCloneRulesTests
	{
		[Test]
		public void CountNeedsRootAndStopsAtCap()
		{
			Assert.AreEqual(0, ShadowCloneRules.Count(0, 2, 3), "뿌리 없이는 수 카드만으로 안 나옴");
			Assert.AreEqual(1, ShadowCloneRules.Count(1, 0, 3));
			Assert.AreEqual(3, ShadowCloneRules.Count(1, 2, 3));
			Assert.AreEqual(3, ShadowCloneRules.Count(1, 5, 3));
		}

		// 갈림길은 수 (셋 x 40) 와 세기 (폭풍 그림자까지 둘 x 65) 라 더해지는 투척이 비슷해야 함
		[Test]
		public void ForkSidesAddSimilarThrows()
		{
			int many = ShadowCloneRules.Count(1, 2, 3) * ShadowCloneRules.SharePercent(false, 40, 65);
			int strong = ShadowCloneRules.Count(1, 1, 3) * ShadowCloneRules.SharePercent(true, 40, 65);
			Assert.AreEqual(120, many);
			Assert.AreEqual(130, strong);
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
			ShadowCloneCycle cycle = new(10f, 2f);
			Assert.AreEqual(ShadowCloneEvent.None, cycle.Tick(100f, false));
			Assert.IsTrue(cycle.IsPresent);
			Assert.AreEqual(ShadowCloneEvent.None, cycle.Tick(9.9f, true), "수명은 마무리를 받은 뒤부터");
		}

		[Test]
		public void CloneVanishesAndReforms()
		{
			ShadowCloneCycle cycle = new(10f, 2f);
			Assert.AreEqual(ShadowCloneEvent.Vanished, cycle.Tick(10f, true));
			Assert.IsFalse(cycle.IsPresent);
			Assert.AreEqual(ShadowCloneEvent.None, cycle.Tick(1.9f, true));
			Assert.AreEqual(ShadowCloneEvent.Formed, cycle.Tick(0.2f, true));
			Assert.AreEqual(ShadowCloneEvent.None, cycle.Tick(9.8f, true));
			Assert.AreEqual(ShadowCloneEvent.Vanished, cycle.Tick(0.2f, true));
		}

		[Test]
		public void LongFrameGivesOneEventAtATime()
		{
			ShadowCloneCycle cycle = new(10f, 2f);
			Assert.AreEqual(ShadowCloneEvent.Vanished, cycle.Tick(12.5f, true));
			Assert.AreEqual(ShadowCloneEvent.Formed, cycle.Tick(0f, true));
			Assert.AreEqual(ShadowCloneEvent.None, cycle.Tick(0f, true));
		}

		// 사라진 동안 마무리가 꺼져도 (형태 교체) 다시 나타남
		[Test]
		public void HiddenCloneAlwaysComesBack()
		{
			ShadowCloneCycle cycle = new(10f, 2f);
			cycle.Tick(10f, true);
			Assert.AreEqual(ShadowCloneEvent.Formed, cycle.Tick(2f, false));
		}
	}
}
