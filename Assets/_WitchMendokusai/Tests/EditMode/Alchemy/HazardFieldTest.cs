using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-174 Phase 3 — 위험지대(저주 폭주 field) 경로 적분 부작용.
    /// "질러가면 빠르지만 부작용 / 돌아가면 안전" = 선분-원 교차 길이 × Severity 누적.
    /// 순수 기하(BrewEngine.SegmentInCircleLength) + Apply(hazards) + BrewSession 통합. EditMode.
    /// </summary>
    [TestFixture]
    public class HazardFieldTest
    {
        private static HazardZone Zone(float cx, float cy, float radius, float severity)
        {
            return new HazardZone
            {
                Id = 1,
                Name = "저주-폭주",
                Center = new BrewVector(cx, cy),
                Radius = radius,
                SeverityPerUnit = severity,
            };
        }

        // --- 선분-원 교차 길이 (순수 기하) ---

        [Test]
        public void SegmentInCircle_DiameterCrossing_EqualsChordLength()
        {
            // (-2,0)→(2,0) 가 중심(0,0) r=1 원을 지름으로 관통 = 길이 2.
            float length = BrewEngine.SegmentInCircleLength(
                new BrewVector(-2f, 0f), new BrewVector(2f, 0f), new BrewVector(0f, 0f), 1f);

            Assert.AreEqual(2f, length, 1e-4f);
        }

        [Test]
        public void SegmentInCircle_NoIntersection_Zero()
        {
            // (0,5)→(0,6) 은 원(0,0) r=1 에서 멀리 = 0.
            float length = BrewEngine.SegmentInCircleLength(
                new BrewVector(0f, 5f), new BrewVector(0f, 6f), new BrewVector(0f, 0f), 1f);

            Assert.AreEqual(0f, length, 1e-4f);
        }

        [Test]
        public void SegmentInCircle_PartialFromInside_ClampsToSegment()
        {
            // 중심(0,0) 에서 출발해 (2,0) 까지 = 반지름 1 만큼만 원 안 = 길이 1.
            float length = BrewEngine.SegmentInCircleLength(
                new BrewVector(0f, 0f), new BrewVector(2f, 0f), new BrewVector(0f, 0f), 1f);

            Assert.AreEqual(1f, length, 1e-4f);
        }

        [Test]
        public void SegmentInCircle_ZeroLength_Zero()
        {
            float length = BrewEngine.SegmentInCircleLength(
                new BrewVector(0f, 0f), new BrewVector(0f, 0f), new BrewVector(0f, 0f), 1f);

            Assert.AreEqual(0f, length, 1e-4f);
        }

        [Test]
        public void SegmentInCircle_BothOutsideSameSide_Zero()
        {
            // 둘 다 원 왼쪽 바깥(x<-1) = 통과 0.
            float length = BrewEngine.SegmentInCircleLength(
                new BrewVector(-3f, 0f), new BrewVector(-2f, 0f), new BrewVector(0f, 0f), 1f);

            Assert.AreEqual(0f, length, 1e-4f);
        }

        // --- Apply(hazards) 부작용 누적 ---

        [Test]
        public void Apply_ThroughHazard_AccruesSideEffect()
        {
            // (-2,0)→(2,0) 가 원(0,0) r=1 통과(길이2) × severity10 = 부작용 20.
            BrewState start = new BrewState { Position = new BrewVector(-2f, 0f), StepCount = 0, AccruedSideEffect = 0f };
            BrewStep step = new BrewStep { Direction = new BrewVector(1f, 0f), Grind = 4f }; // (-2,0)→(2,0)
            List<HazardZone> hazards = new List<HazardZone> { Zone(0f, 0f, 1f, 10f) };

            BrewState after = BrewEngine.Apply(start, step, hazards);

            Assert.AreEqual(20f, after.AccruedSideEffect, 1e-3f, "위험지대 질러가면 부작용 누적");
            Assert.AreEqual(2f, after.Position.X, 1e-4f);
        }

        [Test]
        public void Apply_DetourAroundHazard_NoSideEffect()
        {
            // (-2,0)→(-2,4) 는 원(0,0) r=1 에서 항상 x=-2 멀리 = 부작용 0(우회 = 안전).
            BrewState start = new BrewState { Position = new BrewVector(-2f, 0f), StepCount = 0, AccruedSideEffect = 0f };
            BrewStep step = new BrewStep { Direction = new BrewVector(0f, 1f), Grind = 4f };
            List<HazardZone> hazards = new List<HazardZone> { Zone(0f, 0f, 1f, 10f) };

            BrewState after = BrewEngine.Apply(start, step, hazards);

            Assert.AreEqual(0f, after.AccruedSideEffect, 1e-4f, "우회 = 부작용 0");
        }

        [Test]
        public void Apply_NullHazards_EquivalentToPlainApply()
        {
            BrewState start = BrewState.Start;
            BrewStep step = new BrewStep { Direction = new BrewVector(1f, 0f), Grind = 3f };

            BrewState withNull = BrewEngine.Apply(start, step, null);
            BrewState plain = BrewEngine.Apply(start, step);

            Assert.AreEqual(plain.Position.X, withNull.Position.X, 1e-4f);
            Assert.AreEqual(0f, withNull.AccruedSideEffect, 1e-4f);
        }

        // --- BrewSession 통합 ---

        [Test]
        public void Session_ThroughHazard_AccruesSideEffect_DetourDoesNot()
        {
            // 위험지대는 출발(원점)·목표 *사이* 옆에 — (2,0). 출발점(0,0)은 원 밖(거리2>1).
            BrewRecipe recipe = new BrewRecipe
            {
                Id = 1,
                EffectName = "더미",
                Target = new EffectTarget { Position = new BrewVector(4f, 0f), Radius = 0.5f },
            };
            List<HazardZone> hazards = new List<HazardZone> { Zone(2f, 0f, 1f, 10f) };

            // 질러가기: (0,0) → (4,0) 직선 = 원(2,0)r1 지름 관통(길이2) × 10 = 20.
            BrewSession through = new BrewSession();
            through.Start(recipe, hazards);
            through.AddStep(new BrewStep { Direction = new BrewVector(1f, 0f), Grind = 4f });
            Assert.Greater(through.AccruedSideEffect, 0f, "질러가면 세션 부작용 누적");
            Assert.IsTrue(through.IsComplete, "질러가도 목표 도달");

            // 우회: (0,0)→(0,2)→(4,2)→(4,0). 세 변 모두 원(2,0)r1 에서 거리2>1 = 미통과 = 부작용 0.
            BrewSession detour = new BrewSession();
            detour.Start(recipe, hazards);
            detour.AddStep(new BrewStep { Direction = new BrewVector(0f, 1f), Grind = 2f });   // (0,0)→(0,2)
            detour.AddStep(new BrewStep { Direction = new BrewVector(1f, 0f), Grind = 4f });   // (0,2)→(4,2)
            detour.AddStep(new BrewStep { Direction = new BrewVector(0f, -1f), Grind = 2f });  // (4,2)→(4,0)
            Assert.AreEqual(0f, detour.AccruedSideEffect, 1e-3f, "우회 경로 = 부작용 0");
            Assert.IsTrue(detour.IsComplete, "우회해도 목표 도달");
        }

        [Test]
        public void Session_NoHazards_SideEffectStaysZero()
        {
            BrewRecipe recipe = new BrewRecipe
            {
                Id = 1,
                EffectName = "더미",
                Target = new EffectTarget { Position = new BrewVector(2f, 0f), Radius = 0.5f },
            };

            BrewSession session = new BrewSession();
            session.Start(recipe); // 위험지대 없음
            session.AddStep(new BrewStep { Direction = new BrewVector(1f, 0f), Grind = 2f });

            Assert.AreEqual(0f, session.AccruedSideEffect, 1e-4f, "위험지대 미설정 = 부작용 0");
        }
    }
}
