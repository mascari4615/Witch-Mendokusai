using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	// 글자 표시, 넘김 2단계, 자동 진행, 움직임 곡선
	public sealed class DialogueLineAdvancerTest
	{
		private static DialogueLineAdvancer New(float charactersPerSecond = 10f) =>
			new() { CharactersPerSecond = charactersPerSecond, AutoDelaySeconds = 1f, AutoSecondsPerCharacter = 0f };

		[Test]
		public void TickRevealsCharactersByTime()
		{
			DialogueLineAdvancer advancer = New();
			advancer.Begin("가나다라마바사아자차");

			advancer.Tick(0.35f);
			Assert.That(advancer.VisibleCharacters, Is.EqualTo(3));
			Assert.That(advancer.IsTyping, Is.True);
		}

		[Test]
		public void FirstPressCompletes_SecondPressAdvances()
		{
			DialogueLineAdvancer advancer = New();
			advancer.Begin("가나다");

			Assert.That(advancer.Press(), Is.False);
			Assert.That(advancer.IsTyping, Is.False);
			Assert.That(advancer.VisibleCharacters, Is.EqualTo(3));
			Assert.That(advancer.Press(), Is.True);
		}

		[Test]
		public void EmptyLine_AdvancesAtFirstPress()
		{
			DialogueLineAdvancer advancer = New();
			advancer.Begin(null);
			Assert.That(advancer.Press(), Is.True);
		}

		[Test]
		public void AutoFires_OnlyAfterTextAndDelay()
		{
			DialogueLineAdvancer advancer = New();
			advancer.AutoEnabled = true;
			advancer.Begin("가나");

			Assert.That(advancer.Tick(0.5f), Is.False);
			Assert.That(advancer.IsTyping, Is.False);
			Assert.That(advancer.Tick(0.9f), Is.False);
			Assert.That(advancer.Tick(0.2f), Is.True);
		}

		[Test]
		public void AutoOff_NeverFires()
		{
			DialogueLineAdvancer advancer = New();
			advancer.Begin("가");
			advancer.Complete();
			Assert.That(advancer.Tick(100f), Is.False);
		}

		[Test]
		public void Reveal_DoesNotSplitSurrogatePair()
		{
			DialogueLineAdvancer advancer = New(1f);
			advancer.Begin("가\U0001F600나");

			advancer.Tick(2.1f);
			Assert.That(advancer.VisibleCharacters, Is.EqualTo(3));
		}

		[Test]
		public void MotionSampler_InterpolatesBetweenKeys()
		{
			DialogueMotionKey[] keys =
			{
				new(0f, 0f, 0f),
				new(0.5f, 10f, -20f, 1.5f),
				new(1f, 0f, 0f),
			};

			DialogueMotionKey quarter = DialogueMotionSampler.Sample(keys, 0.25f);
			Assert.That(quarter.X, Is.EqualTo(5f));
			Assert.That(quarter.Y, Is.EqualTo(-10f));
			Assert.That(quarter.Scale, Is.EqualTo(1.25f));

			Assert.That(DialogueMotionSampler.Sample(keys, 2f).Y, Is.EqualTo(0f));
			Assert.That(DialogueMotionSampler.Sample(null, 0.5f).Scale, Is.EqualTo(1f));
		}
	}
}
