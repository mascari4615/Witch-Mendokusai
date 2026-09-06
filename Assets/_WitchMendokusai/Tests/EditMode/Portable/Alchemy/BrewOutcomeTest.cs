using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-174 Phase 5a — 제조 결과 등급(BrewOutcome) 채점.
    /// "경로가 결과를 바꾼다"의 보상축: 질러가기(부작용↑)=조악품 / 안전 우회(부작용 0)=명품.
    /// 미도달=Failed / 도달 시 강도(중심 근접)−부작용 페널티 → 품질 → 등급. 순수 결정성. EditMode.
    /// </summary>
    [TestFixture]
    public class BrewOutcomeTest
    {
        private static EffectTarget Target(float x, float y, float radius)
        {
            return new EffectTarget { Position = new BrewVector(x, y), Radius = radius };
        }

        private static BrewState At(float x, float y, float sideEffect)
        {
            return new BrewState
            {
                Position = new BrewVector(x, y),
                StepCount = 1,
                AccruedSideEffect = sideEffect,
            };
        }

        // --- 도달/미도달 ---

        [Test]
        public void Evaluate_NotReached_Failed()
        {
            // (10,0) 은 목표(0,0) r0.5 에서 멀리 = 미도달.
            BrewOutcome outcome = BrewEngine.Evaluate(At(10f, 0f, 0f), Target(0f, 0f, 0.5f), BrewOutcomeRules.Default);

            Assert.IsFalse(outcome.Reached, "멀리 = 미도달");
            Assert.AreEqual(BrewGrade.Failed, outcome.Grade);
            Assert.AreEqual(0f, outcome.Quality, 1e-4f, "미도달 = 품질 0");
        }

        [Test]
        public void Evaluate_CenterNoSideEffect_Masterwork()
        {
            // 정확히 중심 + 부작용 0 = 강도1·품질1 = 명품.
            BrewOutcome outcome = BrewEngine.Evaluate(At(0f, 0f, 0f), Target(0f, 0f, 1f), BrewOutcomeRules.Default);

            Assert.IsTrue(outcome.Reached);
            Assert.AreEqual(1f, outcome.Potency, 1e-4f, "중심 = 강도 1");
            Assert.AreEqual(1f, outcome.Quality, 1e-4f, "부작용 0 = 품질 1");
            Assert.AreEqual(BrewGrade.Masterwork, outcome.Grade);
        }

        [Test]
        public void Evaluate_AtEdge_Crude()
        {
            // 목표(0,0) r2, 마커 (2,0) = 가장자리 도달 = 강도 0 = 조악품.
            BrewOutcome outcome = BrewEngine.Evaluate(At(2f, 0f, 0f), Target(0f, 0f, 2f), BrewOutcomeRules.Default);

            Assert.IsTrue(outcome.Reached, "가장자리도 도달");
            Assert.AreEqual(0f, outcome.Potency, 1e-4f, "가장자리 = 강도 0");
            Assert.AreEqual(BrewGrade.Crude, outcome.Grade);
        }

        // --- 부작용 → 품질/등급 ---

        [Test]
        public void Evaluate_HigherSideEffect_LowersQualityAndDemotesGrade()
        {
            EffectTarget target = Target(0f, 0f, 1f);
            BrewOutcome clean = BrewEngine.Evaluate(At(0f, 0f, 0f), target, BrewOutcomeRules.Default);
            // 부작용 8 × weight 0.05 = 페널티 0.4 → 품질 1-0.4=0.6 = Fine(Masterwork 에서 강등).
            BrewOutcome dirty = BrewEngine.Evaluate(At(0f, 0f, 8f), target, BrewOutcomeRules.Default);

            Assert.Less(dirty.Quality, clean.Quality, "부작용↑ = 품질↓");
            Assert.AreEqual(BrewGrade.Masterwork, clean.Grade);
            Assert.AreEqual(BrewGrade.Fine, dirty.Grade, "부작용으로 등급 강등");
        }

        [Test]
        public void Evaluate_PotencyProportionalToDistance()
        {
            // 목표(0,0) r2, 마커 (1,0) = 거리 1 = 강도 1 - 1/2 = 0.5.
            BrewOutcome outcome = BrewEngine.Evaluate(At(1f, 0f, 0f), Target(0f, 0f, 2f), BrewOutcomeRules.Default);

            Assert.AreEqual(0.5f, outcome.Potency, 1e-4f, "강도 = 거리 비례");
        }

        [Test]
        public void Evaluate_QualityClampsAboveZero()
        {
            // 부작용 100 × 0.05 = 페널티 5 → 1-5=-4 → clamp 0(음수 X).
            BrewOutcome outcome = BrewEngine.Evaluate(At(0f, 0f, 100f), Target(0f, 0f, 1f), BrewOutcomeRules.Default);

            Assert.AreEqual(0f, outcome.Quality, 1e-4f, "품질 음수 clamp");
            Assert.AreEqual(BrewGrade.Crude, outcome.Grade, "도달이라 Failed 아님 — Crude");
        }

        [Test]
        public void Evaluate_PointTarget_NoDivByZero()
        {
            // 반경 0 점목표 + 정확히 도달 = div0 없이 강도 1.
            BrewOutcome outcome = BrewEngine.Evaluate(At(0f, 0f, 0f), Target(0f, 0f, 0f), BrewOutcomeRules.Default);

            Assert.IsTrue(outcome.Reached, "점목표 정확 도달");
            Assert.AreEqual(1f, outcome.Potency, 1e-4f, "반경 0 도달 = 강도 1(div0 없음)");
            Assert.IsFalse(float.IsNaN(outcome.Quality), "NaN 없음");
            Assert.AreEqual(BrewGrade.Masterwork, outcome.Grade);
        }

        // --- BrewSession 통합: 질러가기 vs 우회 = 등급 차 ---

        [Test]
        public void Session_ThroughHazard_Crude_vs_Detour_Masterwork()
        {
            // Phase 3 HazardFieldTest 와 동일 기하: 목표(4,0)r0.5, 위험지대(2,0)r1 sev10.
            BrewRecipe recipe = new BrewRecipe
            {
                Id = 1,
                EffectName = "더미",
                Target = new EffectTarget { Position = new BrewVector(4f, 0f), Radius = 0.5f },
            };
            List<HazardZone> hazards = new List<HazardZone>
            {
                new HazardZone { Id = 1, Name = "저주-폭주", Center = new BrewVector(2f, 0f), Radius = 1f, SeverityPerUnit = 10f },
            };

            // 질러가기: (0,0)→(4,0) 직선 = 위험지대 지름 관통(부작용 20) → 페널티 1.0 → 품질 0 = Crude.
            BrewSession through = new BrewSession();
            through.Start(recipe, hazards);
            through.AddStep(new BrewStep { Direction = new BrewVector(1f, 0f), Grind = 4f });
            BrewOutcome throughOutcome = through.Evaluate(BrewOutcomeRules.Default);

            // 우회: (0,0)→(0,2)→(4,2)→(4,0) = 부작용 0 → 품질 1 = Masterwork.
            BrewSession detour = new BrewSession();
            detour.Start(recipe, hazards);
            detour.AddStep(new BrewStep { Direction = new BrewVector(0f, 1f), Grind = 2f });
            detour.AddStep(new BrewStep { Direction = new BrewVector(1f, 0f), Grind = 4f });
            detour.AddStep(new BrewStep { Direction = new BrewVector(0f, -1f), Grind = 2f });
            BrewOutcome detourOutcome = detour.Evaluate(BrewOutcomeRules.Default);

            Assert.AreEqual(BrewGrade.Crude, throughOutcome.Grade, "질러가기 = 부작용으로 조악품");
            Assert.AreEqual(BrewGrade.Masterwork, detourOutcome.Grade, "안전 우회 = 명품");
            Assert.Less(throughOutcome.Quality, detourOutcome.Quality, "경로가 등급을 가른다");
        }
    }
}
