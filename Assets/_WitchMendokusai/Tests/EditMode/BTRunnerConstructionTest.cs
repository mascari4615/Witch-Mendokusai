using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class BTRunnerConstructionTest
	{
		private sealed class WaitingRunner : BTRunner
		{
			private readonly float duration;
			public int BuildCount { get; private set; }

			public WaitingRunner(float duration) : base(null)
			{
				this.duration = duration;
			}

			protected override Node MakeNode()
			{
				BuildCount++;
				return NodeHelper.Wait(duration);
			}
		}

		[TestCase(0.2f, 2)]
		[TestCase(0.5f, 5)]
		[TestCase(3f, 31)]
		public void ConstructorDuration_WaitsBeforeCompletion(float duration, int ticks)
		{
			WaitingRunner runner = new(duration);
			Assert.That(runner.CanChangeState, Is.False);
			for (int tick = 1; tick < ticks; tick++)
			{
				Assert.That(runner.UpdateBT(), Is.EqualTo(BTState.Running), $"tick {tick}");
				Assert.That(runner.CanChangeState, Is.False);
			}
			Assert.That(runner.UpdateBT(), Is.EqualTo(BTState.Success));
			Assert.That(runner.CanChangeState, Is.True);
			Assert.That(runner.BuildCount, Is.EqualTo(1));
		}

		[Test]
		public void UpdateWithoutStateQuery_UsesConstructorDurationAndReusesTree()
		{
			WaitingRunner runner = new(0.2f);
			Assert.That(runner.UpdateBT(), Is.EqualTo(BTState.Running));
			Assert.That(runner.UpdateBT(), Is.EqualTo(BTState.Success));
			Assert.That(runner.UpdateBT(), Is.EqualTo(BTState.Running));
			Assert.That(runner.UpdateBT(), Is.EqualTo(BTState.Success));
			Assert.That(runner.BuildCount, Is.EqualTo(1));
		}
	}
}
