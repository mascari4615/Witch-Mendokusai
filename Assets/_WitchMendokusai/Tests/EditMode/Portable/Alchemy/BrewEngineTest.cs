using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-174 Phase 0 — 솥 지도 항해 제조 순수 코어(BrewEngine) 결정성/누적/도달 판정.
    /// 위험지대/부작용 없는 순수 벡터 항해(Phase 0 슬라이스)만 검증.
    /// </summary>
    [TestFixture]
    public class BrewEngineTest
    {
        [Test]
        public void Apply_MovesMarker_ByDirectionTimesGrind()
        {
            BrewState start = BrewState.Start;
            BrewStep step = new BrewStep
            {
                Direction = new BrewVector(1f, 0f),
                Grind = 3f,
            };

            BrewState result = BrewEngine.Apply(start, step);

            Assert.AreEqual(3f, result.Position.X);
            Assert.AreEqual(0f, result.Position.Y);
            Assert.AreEqual(1, result.StepCount);
        }

        [Test]
        public void Brew_Accumulates_MultipleSteps_InOrder()
        {
            List<BrewStep> steps = new List<BrewStep>
            {
                new BrewStep { Direction = new BrewVector(1f, 0f), Grind = 2f },
                new BrewStep { Direction = new BrewVector(0f, 1f), Grind = 5f },
                new BrewStep { Direction = new BrewVector(-1f, 0f), Grind = 1f },
            };

            BrewState result = BrewEngine.Brew(BrewState.Start, steps);

            // (0,0) + (2,0) + (0,5) + (-1,0) = (1,5)
            Assert.AreEqual(1f, result.Position.X);
            Assert.AreEqual(5f, result.Position.Y);
            Assert.AreEqual(3, result.StepCount);
        }

        [Test]
        public void Brew_IsDeterministic()
        {
            List<BrewStep> steps = new List<BrewStep>
            {
                new BrewStep { Direction = new BrewVector(0.6f, 0.8f), Grind = 4f },
                new BrewStep { Direction = new BrewVector(-0.3f, 0.5f), Grind = 2f },
            };

            BrewState a = BrewEngine.Brew(BrewState.Start, steps);
            BrewState b = BrewEngine.Brew(BrewState.Start, steps);

            Assert.AreEqual(a.Position.X, b.Position.X);
            Assert.AreEqual(a.Position.Y, b.Position.Y);
            Assert.AreEqual(a.StepCount, b.StepCount);
        }

        [Test]
        public void Brew_NullSteps_ReturnsStart()
        {
            BrewState result = BrewEngine.Brew(BrewState.Start, null);

            Assert.AreEqual(0f, result.Position.X);
            Assert.AreEqual(0f, result.Position.Y);
            Assert.AreEqual(0, result.StepCount);
        }

        [Test]
        public void IsReached_True_WhenWithinRadius()
        {
            BrewState state = new BrewState { Position = new BrewVector(3f, 4f), StepCount = 1 };
            EffectTarget target = new EffectTarget
            {
                Position = new BrewVector(3.5f, 4f),
                Radius = 1f,
            };

            Assert.IsTrue(BrewEngine.IsReached(state, target));
        }

        [Test]
        public void IsReached_False_WhenOutsideRadius()
        {
            BrewState state = new BrewState { Position = new BrewVector(0f, 0f), StepCount = 0 };
            EffectTarget target = new EffectTarget
            {
                Position = new BrewVector(10f, 0f),
                Radius = 1f,
            };

            Assert.IsFalse(BrewEngine.IsReached(state, target));
        }

        [Test]
        public void IsReached_True_OnExactRadiusBoundary()
        {
            BrewState state = new BrewState { Position = new BrewVector(0f, 0f), StepCount = 0 };
            EffectTarget target = new EffectTarget
            {
                Position = new BrewVector(2f, 0f),
                Radius = 2f,
            };

            // 거리 == 반경 = 도달로 인정 (<=)
            Assert.IsTrue(BrewEngine.IsReached(state, target));
        }

        [Test]
        public void DistanceTo_ReturnsEuclideanDistance()
        {
            BrewState state = new BrewState { Position = new BrewVector(0f, 0f), StepCount = 0 };
            EffectTarget target = new EffectTarget
            {
                Position = new BrewVector(3f, 4f),
                Radius = 0f,
            };

            Assert.AreEqual(5f, BrewEngine.DistanceTo(state, target), 1e-4f);
        }

        [Test]
        public void DistanceTo_DecreasesAsMarkerApproaches()
        {
            EffectTarget target = new EffectTarget
            {
                Position = new BrewVector(10f, 0f),
                Radius = 0.5f,
            };

            BrewState before = new BrewState { Position = new BrewVector(2f, 0f), StepCount = 1 };
            BrewState after = BrewEngine.Apply(before, new BrewStep { Direction = new BrewVector(1f, 0f), Grind = 5f });

            Assert.Less(BrewEngine.DistanceTo(after, target), BrewEngine.DistanceTo(before, target));
        }
    }
}
