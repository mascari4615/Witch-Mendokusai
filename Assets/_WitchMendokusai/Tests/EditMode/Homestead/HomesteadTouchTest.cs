using NUnit.Framework;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 생활 손가락 조작의 판정. 화면 없이 잼
	public class HomesteadTouchTest
	{
		[Test]
		public void Tap_without_held_takes()
		{
			BuildTapRule rule = new();
			Assert.AreEqual(BuildTap.Take, rule.Tap(3, 4, false));
		}

		[Test]
		public void First_tap_previews_and_same_cell_puts()
		{
			BuildTapRule rule = new();
			Assert.AreEqual(BuildTap.Preview, rule.Tap(3, 4, true));
			Assert.AreEqual(BuildTap.Put, rule.Tap(3, 4, true));
		}

		[Test]
		public void Other_cell_moves_preview_instead_of_putting()
		{
			BuildTapRule rule = new();
			rule.Tap(3, 4, true);
			Assert.AreEqual(BuildTap.Preview, rule.Tap(5, 4, true));
			Assert.AreEqual(BuildTap.Put, rule.Tap(5, 4, true));
		}

		[Test]
		public void After_put_next_tap_previews_again()
		{
			BuildTapRule rule = new();
			rule.Tap(1, 1, true);
			rule.Tap(1, 1, true);
			Assert.AreEqual(BuildTap.Preview, rule.Tap(1, 1, true));
		}

		[Test]
		public void Forget_drops_pending_cell()
		{
			BuildTapRule rule = new();
			rule.Tap(2, 2, true);
			rule.Forget();
			Assert.AreEqual(BuildTap.Preview, rule.Tap(2, 2, true));
		}

		[Test]
		public void Taking_drops_pending_cell()
		{
			BuildTapRule rule = new();
			rule.Tap(2, 2, true);
			rule.Tap(2, 2, false);
			Assert.AreEqual(BuildTap.Preview, rule.Tap(2, 2, true));
		}

		[Test]
		public void Fishing_button_text_follows_phase()
		{
			Assert.AreEqual("기다림", FishingTouch.ButtonText(FishingPhase.Waiting));
			Assert.AreEqual("채기!", FishingTouch.ButtonText(FishingPhase.Bite));
			Assert.AreEqual("감기", FishingTouch.ButtonText(FishingPhase.Duel));
			Assert.AreEqual(string.Empty, FishingTouch.ButtonText(FishingPhase.Caught));
		}
	}
}
