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
		public void 완성은_한_사람에게만_간다()
		{
			WorldSim world = new WorldSim();
			world.Cauldron.AddStep(Step(1f, 0f));
			world.Cauldron.AddStep(Step(0f, 1f));

			bool first = world.Cauldron.TryComplete(out BrewState taken);
			bool second = world.Cauldron.TryComplete(out BrewState empty);

			// 같은 순간에 둘이 눌러도 뒤엣사람은 빈 솥을 받는다 = 이중지급이 구조적으로 불가능.
			Assert.That(first, Is.True);
			Assert.That(taken.StepCount, Is.EqualTo(2));
			Assert.That(second, Is.False);
			Assert.That(empty.StepCount, Is.EqualTo(0));
		}

		[Test]
		public void 완성_뒤에는_솥이_비어_있다()
		{
			WorldSim world = new WorldSim();
			world.Cauldron.AddStep(Step(1f, 0f));

			world.Cauldron.TryComplete(out BrewState _);

			Assert.That(world.Cauldron.State.StepCount, Is.EqualTo(0));
			List<BrewStep> buffer = new List<BrewStep>();
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
