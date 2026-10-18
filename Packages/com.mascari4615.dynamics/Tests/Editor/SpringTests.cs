using System;
using System.Numerics;
using Dynamics;
using NUnit.Framework;

namespace DynamicsTests
{
    public sealed class SpringTests
    {
        [TestCase(.4f)]
        [TestCase(1f)]
        [TestCase(2.5f)]
        public void MovingTargetProducesLagThenSettlesWithoutChangingLength(float damping)
        {
            Vector3 goal = new Vector3(.3f, -.4f, 0);
            SpringState state = new SpringState { Position = new Vector3(0, -.5f, 0) };
            SpringSettings settings = new SpringSettings { FrequencyHz = 2, DampingRatio = damping };
            state = SpringParticle.Step(state, goal, Vector3.Zero, .5f, settings, new CapsuleObstacle[0], 1f / 120);
            Assert.That(Vector3.Distance(state.Position, goal), Is.GreaterThan(.1f));
            for (int step = 0; step < 480; step++)
            {
                state = SpringParticle.Step(state, goal, Vector3.Zero, .5f, settings, new CapsuleObstacle[0], 1f / 120);
                Assert.That(state.Position.Length(), Is.EqualTo(.5f).Within(.000001));
            }
            Assert.That(Vector3.Distance(state.Position, goal), Is.LessThan(.001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CollisionAtTheTargetPreservesBothLengthAndClearance(bool capsule)
        {
            Vector3 goal = new Vector3(0, -.5f, 0);
            CapsuleObstacle obstacle = new CapsuleObstacle { Start = goal, End = goal, Radius = .1f };
            if (capsule) { obstacle.Start.X -= .2f; obstacle.End.X += .2f; }
            SpringSettings settings = new SpringSettings { FrequencyHz = 2, DampingRatio = .5f, Radius = .02f };
            SpringState state = new SpringState { Position = goal };
            for (int step = 0; step < 240; step++)
            {
                state = SpringParticle.Step(state, goal, Vector3.Zero, .5f, settings, new[] { obstacle }, 1f / 120);
                Vector3 center = capsule ? new Vector3(Math.Max(-.2f, Math.Min(.2f, state.Position.X)), -.5f, 0) : goal;
                Assert.That(Vector3.Distance(state.Position, center), Is.GreaterThanOrEqualTo(.11999f));
                Assert.That(state.Position.Length(), Is.EqualTo(.5f).Within(.000001));
            }
        }

        [Test]
        public void InvalidValuesAndImpossibleCollisionAreExplicitFailures()
        {
            SpringState state = new SpringState { Position = -Vector3.UnitY };
            SpringSettings settings = new SpringSettings { FrequencyHz = 2, DampingRatio = .5f };
            Assert.Throws<ArgumentException>(() => SpringParticle.Step(state, -Vector3.UnitY, Vector3.Zero, 1, settings,
                new[] { new CapsuleObstacle { Radius = 2 } }, 1f / 120));
            Assert.Throws<ArgumentException>(() => SpringParticle.Step(state, -Vector3.UnitY, Vector3.Zero, 1, settings, new CapsuleObstacle[0], float.NaN));
            settings.FrequencyHz = float.PositiveInfinity;
            Assert.Throws<ArgumentException>(() => SpringParticle.Step(state, -Vector3.UnitY, Vector3.Zero, 1, settings, new CapsuleObstacle[0], 1f / 120));
            Assert.That(state.Position, Is.EqualTo(-Vector3.UnitY));
        }
    }
}
