using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-167 마도 온실 Phase 1b — <see cref="Greenhouse"/> 인형 자동돌봄 안전망 회귀 잠금.
    /// 비전 "게으른 욘 대신 인형이 돌봐 살린다(안전망)"을 화면 없이 결정적으로 증명:
    /// 인형 충분 → 안 시듦 / 부족 → 가장 약한 것부터 시듦 / triage·분담 결정성. 순수 POCO.
    /// </summary>
    public sealed class GreenhouseTest
    {
        // 마도 작물: 생기 100, 분당 1 소모, 돌봄 50 회복.
        private static PlantGrowthParams WitchParams()
        {
            return new PlantGrowthParams(60, 3, 100f, 1f, 50f);
        }

        // 일반 작물: Drain 0 = 안 시듦(코지). 변이 기록 검증용(시듦 간섭 0).
        private static PlantGrowthParams LegacyParams()
        {
            return new PlantGrowthParams(60, 3, 100f, 0f, 50f);
        }

        private static Greenhouse PlantedHouse(int plotCount, float startVitality)
        {
            Greenhouse house = new();
            for (int id = 0; id < plotCount; id++)
            {
                house.AddPlot(id).Plant(plantDataId: 100 + id, WitchParams(), startVitality);
            }

            return house;
        }

        // ── 인형 안전망 ──

        [Test]
        public void EnoughCarers_NothingWithers()
        {
            Greenhouse house = PlantedHouse(plotCount: 2, startVitality: 100f);
            List<int> carers = new() { 1, 2 }; // 칸 수 == 인형 수

            // 매 틱 30분씩(소모 30 < 회복 50), 인형이 두 칸 다 돌봄.
            for (int tick = 0; tick < 10; tick++)
            {
                house.TickWithCarers(carers, 30);
            }

            Assert.That(house.LivingCount(), Is.EqualTo(2), "인형 충분 → 아무것도 안 시듦");
        }

        [Test]
        public void NoCarers_AllEventuallyWither()
        {
            Greenhouse house = PlantedHouse(plotCount: 2, startVitality: 100f);
            List<int> noCarers = new();

            house.TickWithCarers(noCarers, 120); // 생기 100 < 소모 120

            Assert.That(house.LivingCount(), Is.Zero, "돌봄 0 → 전부 시듦");
        }

        [Test]
        public void TooFewCarers_WeakestSavedFirst()
        {
            // 칸 3개, 인형 1개. 생기 다르게: 칸0=20(가장 약함), 칸1=60, 칸2=100.
            Greenhouse house = new();
            house.AddPlot(0).Plant(100, WitchParams(), 20f);
            house.AddPlot(1).Plant(101, WitchParams(), 60f);
            house.AddPlot(2).Plant(102, WitchParams(), 100f);
            List<int> oneCarer = new() { 1 };

            // 한 틱: 인형이 가장 약한 칸0(20)을 돌봄(+50=70) → 30분 경과(전부 -30).
            house.TickWithCarers(oneCarer, 30);

            // 칸0: 70-30=40(살아남음, 인형이 구함) / 칸1: 60-30=30 / 칸2: 100-30=70.
            Assert.That(house.GetPlot(0).Vitality, Is.EqualTo(40f), "가장 약했던 칸을 인형이 구함");
            Assert.That(house.GetPlot(1).Vitality, Is.EqualTo(30f));
            Assert.That(house.GetPlot(2).Vitality, Is.EqualTo(70f));
        }

        [Test]
        public void Triage_TieBreaksByLowestPlotId()
        {
            // 두 칸 생기 동일(20). 인형 1 → 낮은 plotId(3) 가 돌봄 받음(결정성).
            Greenhouse house = new();
            house.AddPlot(5).Plant(105, WitchParams(), 20f);
            house.AddPlot(3).Plant(103, WitchParams(), 20f);
            List<int> oneCarer = new() { 9 };

            house.TickWithCarers(oneCarer, 10);

            // 칸3 = 20+50-10 = 60(돌봄 받음) / 칸5 = 20-10 = 10(못 받음).
            Assert.That(house.GetPlot(3).Vitality, Is.EqualTo(60f), "동률은 낮은 plotId 우선");
            Assert.That(house.GetPlot(5).Vitality, Is.EqualTo(10f));
        }

        [Test]
        public void Carers_DoNotDoubleTendSamePlot()
        {
            // 칸 1개, 인형 2개. 한 칸을 둘 다 안 돌봄 — 하나만(분담, 중복 X).
            Greenhouse house = PlantedHouse(plotCount: 1, startVitality: 50f);
            List<int> twoCarers = new() { 1, 2 };

            house.TickWithCarers(twoCarers, 10);

            // 50 + 50(한 번만) - 10 = 90. (두 번이면 100 클램프였을 것 — 여기선 1회만이라 90.)
            Assert.That(house.GetPlot(0).Vitality, Is.EqualTo(90f), "한 칸은 인형 하나만 돌봄(중복 X)");
        }

        // ── 돌봄자 기록 = 변이 입력 정합 ──

        [Test]
        public void TickWithCarers_RecordsCarerForMutation()
        {
            // Legacy(안 시듦) 칸 1개를 인형 7이 매 틱 돌봐 개화 → 수확물의 DominantCarer = 7 확인.
            Greenhouse house = new();
            house.AddPlot(0).Plant(plantDataId: 100, LegacyParams(), startVitality: 100f);
            List<int> carer7 = new() { 7 };

            house.TickWithCarers(carer7, 60); // 60분 + carer7 기록
            house.TickWithCarers(carer7, 60); // 120
            house.TickWithCarers(carer7, 60); // 180 → 개화

            bool harvested = house.GetPlot(0).TryHarvest(out HarvestResult result);
            Assert.That(harvested, Is.True, "개화 후 수확 성공");
            Assert.That(result.HasDominantCarer, Is.True);
            Assert.That(result.DominantCarerId, Is.EqualTo(7), "온실 틱이 돌본 인형을 변이 입력으로 기록");
        }

        // ── 빈 온실 / 빈 칸 가드 ──

        [Test]
        public void EmptyHouse_TickNoThrow()
        {
            Greenhouse house = new();

            Assert.DoesNotThrow(() => house.TickWithCarers(new List<int> { 1 }, 30));
            Assert.That(house.LivingCount(), Is.Zero);
        }

        [Test]
        public void NullCarers_OnlyTimePasses()
        {
            Greenhouse house = PlantedHouse(plotCount: 1, startVitality: 100f);

            house.TickWithCarers(null, 30); // carer 없이 시간만

            Assert.That(house.GetPlot(0).Vitality, Is.EqualTo(70f), "돌봄 0, 시간만 경과");
        }
    }
}
