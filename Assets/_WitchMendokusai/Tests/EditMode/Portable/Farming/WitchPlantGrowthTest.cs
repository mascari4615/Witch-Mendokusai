using NUnit.Framework;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-167 마도 온실 Phase 0 — <see cref="WitchPlantGrowth"/> 순수 성장·돌봄·시듦 코어 회귀 잠금.
    /// 레거시(Drain=0) 동등성 + 마도 진화(시듦·돌봄·관찰표본·변이) 결정 검증. 순수 POCO.
    /// </summary>
    public sealed class WitchPlantGrowthTest
    {
        // 마도 작물 디폴트: 60분/단계, 3단계, 생기 10, 분당 1 소모, 돌봄 1회 5 회복.
        private static PlantGrowthParams WitchParams()
        {
            return new PlantGrowthParams(60, 3, 10f, 1f, 5f);
        }

        // 레거시 작물: 시듦 0 (Drain=0) = 기존 단조 시간성장.
        private static PlantGrowthParams LegacyParams()
        {
            return new PlantGrowthParams(60, 3, 10f, 0f, 0f);
        }

        // ── 레거시 동등성 (회귀 잠금) ──

        [Test]
        public void LegacyCrop_NoDrain_NeverWithers_StageMatchesTime()
        {
            PlantGrowthState state = new(10f);
            PlantGrowthParams parameters = LegacyParams();

            WitchPlantGrowth.Step(state, parameters, 60);
            Assert.That(WitchPlantGrowth.StageOf(state, parameters), Is.EqualTo(1), "60분 = 1단계 (단조)");

            WitchPlantGrowth.Step(state, parameters, 120);
            Assert.That(WitchPlantGrowth.StageOf(state, parameters), Is.EqualTo(3), "총 180분 = 최종 단계");
            Assert.That(state.Withered, Is.False, "Drain 0 = 절대 안 시듦");
            Assert.That(WitchPlantGrowth.IsHarvestable(state, parameters), Is.True);
        }

        [Test]
        public void StageOf_CapsAtMaxStage()
        {
            PlantGrowthState state = new(10f);
            PlantGrowthParams parameters = LegacyParams();

            WitchPlantGrowth.Step(state, parameters, 600); // 10단계분이지만 상한 3

            Assert.That(WitchPlantGrowth.StageOf(state, parameters), Is.EqualTo(3));
        }

        // ── 시듦 (방치) ──

        [Test]
        public void Step_VitalityHitsZero_Withers_AndHaltsGrowth()
        {
            PlantGrowthState state = new(10f);
            PlantGrowthParams parameters = WitchParams();

            WitchPlantGrowth.Step(state, parameters, 5); // 생기 5, 성장 5분
            Assert.That(state.Withered, Is.False);
            Assert.That(state.GrowthMinutes, Is.EqualTo(5));

            WitchPlantGrowth.Step(state, parameters, 5); // 생기 0 → 시듦, 성장 적립 X
            Assert.That(state.Withered, Is.True);
            Assert.That(state.Vitality, Is.EqualTo(0f));
            Assert.That(state.GrowthMinutes, Is.EqualTo(5), "시든 틱은 성장 안 함");
        }

        [Test]
        public void Step_OnWithered_IsNoOp()
        {
            PlantGrowthState state = new(1f);
            PlantGrowthParams parameters = WitchParams();

            WitchPlantGrowth.Step(state, parameters, 5); // 시듦
            Assert.That(state.Withered, Is.True);

            WitchPlantGrowth.Step(state, parameters, 100); // 무효
            Assert.That(state.GrowthMinutes, Is.EqualTo(0));
        }

        [Test]
        public void Withered_IsNotHarvestable()
        {
            PlantGrowthState state = new(1f);
            PlantGrowthParams parameters = WitchParams();

            WitchPlantGrowth.Step(state, parameters, 5);

            Assert.That(WitchPlantGrowth.IsHarvestable(state, parameters), Is.False);
        }

        // ── 돌봄 (생기 회복) ──

        [Test]
        public void Tend_RestoresVitality_PreventsWither()
        {
            PlantGrowthState state = new(10f);
            PlantGrowthParams parameters = WitchParams();

            WitchPlantGrowth.Step(state, parameters, 5);          // 생기 5
            WitchPlantGrowth.Tend(state, parameters, carerId: 1); // +5 → 10 (상한)
            WitchPlantGrowth.Step(state, parameters, 5);          // 생기 5 (시들지 않음)

            Assert.That(state.Withered, Is.False);
            Assert.That(state.Vitality, Is.EqualTo(5f));
            Assert.That(state.GrowthMinutes, Is.EqualTo(10));
        }

        [Test]
        public void Tend_ClampsToMaxVitality()
        {
            PlantGrowthState state = new(9f);
            PlantGrowthParams parameters = WitchParams();

            WitchPlantGrowth.Tend(state, parameters, carerId: 1); // 9 + 5 = 14 → 상한 10

            Assert.That(state.Vitality, Is.EqualTo(10f));
        }

        [Test]
        public void Tend_OnWithered_IsNoOp()
        {
            PlantGrowthState state = new(1f);
            PlantGrowthParams parameters = WitchParams();

            WitchPlantGrowth.Step(state, parameters, 5); // 시듦
            WitchPlantGrowth.Tend(state, parameters, carerId: 1);

            Assert.That(state.Vitality, Is.EqualTo(0f), "시든 식물은 돌봄 무효");
            Assert.That(state.TendCounts.Count, Is.EqualTo(0));
        }

        // ── 관찰 → 진짜화 (영구 표본) ──

        [Test]
        public void IsSpecimen_ObservedAndBloomed_True()
        {
            PlantGrowthState state = new(10f);
            PlantGrowthParams parameters = LegacyParams();
            state.Observed = true;

            WitchPlantGrowth.Step(state, parameters, 180); // 개화

            Assert.That(WitchPlantGrowth.IsSpecimen(state, parameters), Is.True);
        }

        [Test]
        public void IsSpecimen_BloomedButNotObserved_False()
        {
            PlantGrowthState state = new(10f);
            PlantGrowthParams parameters = LegacyParams();

            WitchPlantGrowth.Step(state, parameters, 180); // 개화했지만 관찰 X

            Assert.That(WitchPlantGrowth.IsHarvestable(state, parameters), Is.True);
            Assert.That(WitchPlantGrowth.IsSpecimen(state, parameters), Is.False, "관찰 안 하면 진짜 안 됨");
        }

        // ── 변이 (누가 돌봤나) ──

        [Test]
        public void TryGetDominantCarer_MostTendsWins()
        {
            PlantGrowthState state = new(10f);
            PlantGrowthParams parameters = WitchParams();

            WitchPlantGrowth.Tend(state, parameters, carerId: 5);
            WitchPlantGrowth.Tend(state, parameters, carerId: 3);
            WitchPlantGrowth.Tend(state, parameters, carerId: 3);

            Assert.That(WitchPlantGrowth.TryGetDominantCarer(state, out int carer), Is.True);
            Assert.That(carer, Is.EqualTo(3), "3이 2회로 최다");
        }

        [Test]
        public void TryGetDominantCarer_Tie_LowestIdWins()
        {
            PlantGrowthState state = new(10f);
            PlantGrowthParams parameters = WitchParams();

            WitchPlantGrowth.Tend(state, parameters, carerId: 5);
            WitchPlantGrowth.Tend(state, parameters, carerId: 3); // 둘 다 1회 → 최저 id

            Assert.That(WitchPlantGrowth.TryGetDominantCarer(state, out int carer), Is.True);
            Assert.That(carer, Is.EqualTo(3));
        }

        [Test]
        public void TryGetDominantCarer_NoTend_False()
        {
            PlantGrowthState state = new(10f);

            Assert.That(WitchPlantGrowth.TryGetDominantCarer(state, out int carer), Is.False);
            Assert.That(carer, Is.EqualTo(-1));
        }
    }
}
