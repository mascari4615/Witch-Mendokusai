using NUnit.Framework;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-167 마도 온실 Phase 1a — <see cref="GreenhousePlot"/> 생애 상태머신 회귀 잠금.
    /// 마도 작물 한 칸의 한 생애("심고→방치하면 시듦 / 돌보면 생존→개화 / 봐주면 진짜→수확")를
    /// 화면 없이 결정적으로 증명. 순수 POCO — PlayMode/씬 무관. (패턴: WitchPlantGrowthTest)
    /// </summary>
    public sealed class GreenhousePlotTest
    {
        // 마도 작물: 60분/단계, 3단계, 생기 100, 분당 1 소모, 돌봄 50 회복.
        // (정수 산수 — float 오차 0. 돌봄 1회 회복 50 ≫ step 30분 소모 30 = 주기 돌봄이면 생존.)
        private static PlantGrowthParams WitchParams()
        {
            return new PlantGrowthParams(60, 3, 100f, 1f, 50f);
        }

        // 일반 작물: Drain 0 = 코지(절대 안 시듦).
        private static PlantGrowthParams LegacyParams()
        {
            return new PlantGrowthParams(60, 3, 10f, 0f, 0f);
        }

        // ── 심기 ──

        [Test]
        public void NewPlot_IsEmpty()
        {
            GreenhousePlot plot = new();

            Assert.That(plot.Phase, Is.EqualTo(PlotPhase.Empty));
            Assert.That(plot.IsPlanted, Is.False);
        }

        [Test]
        public void Plant_ThenGrowing()
        {
            GreenhousePlot plot = new();

            bool planted = plot.Plant(plantDataId: 42, WitchParams(), startVitality: 10f);

            Assert.That(planted, Is.True);
            Assert.That(plot.Phase, Is.EqualTo(PlotPhase.Growing));
            Assert.That(plot.PlantDataId, Is.EqualTo(42));
        }

        [Test]
        public void Plant_OnOccupied_Rejected()
        {
            GreenhousePlot plot = new();
            plot.Plant(1, WitchParams(), 10f);

            bool second = plot.Plant(2, WitchParams(), 10f);

            Assert.That(second, Is.False, "이미 심긴 칸엔 다시 못 심음");
            Assert.That(plot.PlantDataId, Is.EqualTo(1), "원래 작물 유지");
        }

        // ── 방치 → 시듦 / 돌봄 → 생존 ──

        [Test]
        public void Witch_Neglected_Withers()
        {
            GreenhousePlot plot = new();
            plot.Plant(1, WitchParams(), 100f);

            plot.Step(120); // 돌봄 없이 120분 → 생기 100 소진(분당 1)

            Assert.That(plot.Phase, Is.EqualTo(PlotPhase.Withered));
        }

        [Test]
        public void Witch_Tended_Survives_AndBlooms()
        {
            GreenhousePlot plot = new();
            plot.Plant(1, WitchParams(), 100f);

            // 180분 생장 필요(3단계). 30분 step 6회, 매번 먼저 돌봐(+50) 생기 유지(-30).
            for (int cycle = 0; cycle < 6; cycle++)
            {
                plot.Tend(carerId: 1); // +50 (상한 100 클램프)
                plot.Step(30);          // -30, +30분 생장
            }

            Assert.That(plot.Phase, Is.EqualTo(PlotPhase.Bloomed));
        }

        [Test]
        public void Legacy_NeverWithers_InPlot()
        {
            GreenhousePlot plot = new();
            plot.Plant(1, LegacyParams(), 10f);

            plot.Step(1000); // 한참 방치

            Assert.That(plot.Phase, Is.EqualTo(PlotPhase.Bloomed), "일반작물=코지, 안 시들고 개화");
        }

        // ── 관찰 → 진짜화 → 수확 ──

        [Test]
        public void Observe_ThenHarvest_IsSpecimen()
        {
            GreenhousePlot plot = new();
            plot.Plant(7, LegacyParams(), 10f);
            plot.Step(180); // 개화
            plot.Observe();

            bool harvested = plot.TryHarvest(out HarvestResult result);

            Assert.That(harvested, Is.True);
            Assert.That(result.PlantDataId, Is.EqualTo(7));
            Assert.That(result.IsSpecimen, Is.True, "관찰된 개체 = 진짜(영구 표본)");
            Assert.That(plot.Phase, Is.EqualTo(PlotPhase.Empty), "수확 후 빈 칸");
        }

        [Test]
        public void Harvest_WithoutObserve_NotSpecimen()
        {
            GreenhousePlot plot = new();
            plot.Plant(7, LegacyParams(), 10f);
            plot.Step(180); // 개화 (관찰 X)

            bool harvested = plot.TryHarvest(out HarvestResult result);

            Assert.That(harvested, Is.True);
            Assert.That(result.IsSpecimen, Is.False, "안 봐준 작물은 진짜 안 됨");
        }

        [Test]
        public void Harvest_BeforeBloom_Rejected()
        {
            GreenhousePlot plot = new();
            plot.Plant(1, LegacyParams(), 10f);
            plot.Step(60); // 1단계 — 아직 개화 전

            bool harvested = plot.TryHarvest(out _);

            Assert.That(harvested, Is.False);
            Assert.That(plot.Phase, Is.EqualTo(PlotPhase.Growing), "수확 거부 — 작물 유지");
        }

        [Test]
        public void Withered_CannotHarvest()
        {
            GreenhousePlot plot = new();
            plot.Plant(1, WitchParams(), 100f);
            plot.Step(120); // 시듦

            bool harvested = plot.TryHarvest(out _);

            Assert.That(harvested, Is.False);
            Assert.That(plot.Phase, Is.EqualTo(PlotPhase.Withered));
        }

        // ── 변이 (누가 길렀나) ──

        [Test]
        public void Harvest_ReportsDominantCarer()
        {
            GreenhousePlot plot = new();
            plot.Plant(1, LegacyParams(), 10f);
            plot.Tend(carerId: 5);
            plot.Tend(carerId: 3);
            plot.Tend(carerId: 3); // 3이 최다
            plot.Step(180);        // 개화

            plot.TryHarvest(out HarvestResult result);

            Assert.That(result.HasDominantCarer, Is.True);
            Assert.That(result.DominantCarerId, Is.EqualTo(3), "가장 많이 돌본 3 → 변이 가름");
        }

        // ── 빈 칸/시든 칸 가드 + 치우기 ──

        [Test]
        public void TendObserve_OnEmpty_NoOp()
        {
            GreenhousePlot plot = new();

            plot.Tend(1);
            plot.Observe();

            Assert.That(plot.Phase, Is.EqualTo(PlotPhase.Empty));
        }

        [Test]
        public void ClearWithered_FreesPlot_ForReplant()
        {
            GreenhousePlot plot = new();
            plot.Plant(1, WitchParams(), 100f);
            plot.Step(120); // 시듦
            Assert.That(plot.Phase, Is.EqualTo(PlotPhase.Withered));

            bool cleared = plot.ClearWithered();
            Assert.That(cleared, Is.True);
            Assert.That(plot.Phase, Is.EqualTo(PlotPhase.Empty));

            bool replant = plot.Plant(2, WitchParams(), 100f);
            Assert.That(replant, Is.True, "치운 뒤 재심기 가능");
        }

        [Test]
        public void ClearWithered_OnLiving_Rejected()
        {
            GreenhousePlot plot = new();
            plot.Plant(1, WitchParams(), 100f);

            bool cleared = plot.ClearWithered();

            Assert.That(cleared, Is.False, "살아있는 작물은 못 치움");
            Assert.That(plot.Phase, Is.EqualTo(PlotPhase.Growing));
        }
    }
}
