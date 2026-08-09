using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 같이 젓는 솥이 세계의 것인지 (TASK-WM-217) — FishNet 호스트가 굴리던 그것.
	/// </summary>
	public sealed class WorldCauldronTests
	{
		private static BrewStep Step(float x, float y) => new BrewStep
		{
			Direction = new BrewVector { X = x, Y = y },
			Grind = 1f,
		};

		[Test]
		public void 둘이_넣어도_같은_솥에_쌓인다()
		{
			WorldSim world = new WorldSim();

			world.Cauldron.AddStep(Step(1f, 0f));
			world.Cauldron.AddStep(Step(0f, 1f));

			Assert.That(world.Cauldron.State.StepCount, Is.EqualTo(2));
		}

		[Test]
		public void 저을_때마다_수가_오른다()
		{
			WorldSim world = new WorldSim();

			world.Cauldron.AddStep(Step(1f, 0f));
			int afterOne = world.Cauldron.Version;
			world.Cauldron.ResetBrew();

			Assert.That(afterOne, Is.EqualTo(1));
			Assert.That(world.Cauldron.Version, Is.EqualTo(2));
		}

		[Test]
		public void 비우면_처음부터()
		{
			WorldSim world = new WorldSim();
			world.Cauldron.AddStep(Step(1f, 0f));

			world.Cauldron.ResetBrew();

			Assert.That(world.Cauldron.State.StepCount, Is.EqualTo(0));

			List<BrewStep> buffer = new List<BrewStep> { Step(9f, 9f) };
			world.Cauldron.ReadSteps(buffer);
			Assert.That(buffer, Is.Empty);
		}

		[Test]
		public void 저은_길을_읽어_갈_수_있다()
		{
			WorldSim world = new WorldSim();
			world.Cauldron.AddStep(Step(1f, 0f));
			world.Cauldron.AddStep(Step(0f, 2f));

			List<BrewStep> buffer = new List<BrewStep>();
			world.Cauldron.ReadSteps(buffer);

			Assert.That(buffer.Count, Is.EqualTo(2));
			Assert.That(buffer[1].Direction.Y, Is.EqualTo(2f));
		}

		[Test]
		public void 젓는_규칙은_게임과_같은_것을_쓴다()
		{
			WorldSim world = new WorldSim();
			BrewStep step = Step(1f, 0f);

			world.Cauldron.AddStep(step);
			BrewState direct = BrewEngine.Apply(BrewState.Start, step);

			Assert.That(world.Cauldron.State.Position.X, Is.EqualTo(direct.Position.X));
			Assert.That(world.Cauldron.State.AccruedSideEffect, Is.EqualTo(direct.AccruedSideEffect));
		}
	}
}
