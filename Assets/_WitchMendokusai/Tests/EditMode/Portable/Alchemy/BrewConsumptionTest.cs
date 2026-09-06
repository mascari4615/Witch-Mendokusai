using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Alchemy;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-183 INC-W7 — 브루잉 ↔ 마을 경제 루프 닫기.
    /// "주민 노동 → CityEconomy 적재 → 욘 제조(BrewConsumptionModel) 소비" 전 고리 + 확인-후-차감/비-부분차감/이중차감-X 회귀 잠금.
    /// 순수(Play 무관) — WorkModel 산출과 BrewConsumptionModel 소비가 같은 CityEconomy 원장을 공유. (패턴: WorkModelTest/LifeAgentWorkTest)
    /// </summary>
    public sealed class BrewConsumptionTest
    {
        // 채광 효율 1.0 프로필 — Mine 분당 Mineral 0.4.
        private static WorkProfile MinerProfile()
        {
            return new WorkProfile(WorkKind.Mine, new Dictionary<WorkKind, float>());
        }

        // 주민이 minutes 분 채광해 마을 창고에 쌓는다(LifeAgent.TickMinutes 의 생산 경로와 동형 = WorkModel→AddStock).
        private static void ResidentWorks(CityEconomy economy, WorkKind kind, WorkProfile profile, int minutes)
        {
            foreach (ResourceFlow flow in WorkModel.Produce(kind, profile, minutes))
            {
                economy.AddStock(flow.Resource, flow.Rate);
            }
        }

        private static List<ResourceFlow> Cost(ResourceId resource, float amount)
        {
            return new List<ResourceFlow> { new ResourceFlow(resource, amount) };
        }

        [Test]
        public void FullLoop_ResidentLaborFeedsBrewConsumption()
        {
            // 주민 채광 30분 → Mineral 0.4 × 1.0 × 30 = 12 가 마을 창고에 쌓임.
            CityEconomy economy = new CityEconomy();
            ResidentWorks(economy, WorkKind.Mine, MinerProfile(), 30);
            Assert.That(economy.GetStock(KnownResources.Mineral), Is.EqualTo(12f).Within(0.001f), "노동이 마을 창고에 적재");

            // 욘 제조가 그 광물 10 을 소비 → 12 - 10 = 2 잔여. "주민이 만든 자원을 욘이 씀".
            bool consumed = BrewConsumptionModel.Consume(economy, Cost(KnownResources.Mineral, 10f));

            Assert.That(consumed, Is.True, "재고 충분 → 소비 성공");
            Assert.That(economy.GetStock(KnownResources.Mineral), Is.EqualTo(2f).Within(0.001f), "제조가 마을 창고에서 차감(루프 닫힘)");
        }

        [Test]
        public void CanAfford_TrueWhenStockEnough_FalseWhenShort()
        {
            CityEconomy economy = new CityEconomy();
            economy.AddStock(KnownResources.Herb, 5f);

            Assert.That(BrewConsumptionModel.CanAfford(economy, Cost(KnownResources.Herb, 5f)), Is.True, "정확히 같으면 가능");
            Assert.That(BrewConsumptionModel.CanAfford(economy, Cost(KnownResources.Herb, 5.01f)), Is.False, "0.01 부족하면 불가");
        }

        [Test]
        public void Consume_InsufficientStock_DeductsNothing()
        {
            // 확인-후-차감: 부족하면 false + 재고 그대로(섣불리 깎지 않음).
            CityEconomy economy = new CityEconomy();
            economy.AddStock(KnownResources.Mineral, 3f);

            bool consumed = BrewConsumptionModel.Consume(economy, Cost(KnownResources.Mineral, 10f));

            Assert.That(consumed, Is.False, "재고 부족 → 소비 실패");
            Assert.That(economy.GetStock(KnownResources.Mineral), Is.EqualTo(3f).Within(0.001f), "실패 시 차감 0(GetStock 확인이 먼저)");
        }

        [Test]
        public void Consume_MultiInput_AtomicNoPartialDeduct()
        {
            // 다입력 레시피: 한 자원이 부족하면 *다른 자원도 새지 않음*(원자성).
            CityEconomy economy = new CityEconomy();
            economy.AddStock(KnownResources.Mineral, 10f);
            economy.AddStock(KnownResources.Herb, 1f); // 약초 부족.

            List<ResourceFlow> costs = new List<ResourceFlow>
            {
                new ResourceFlow(KnownResources.Mineral, 5f),
                new ResourceFlow(KnownResources.Herb, 5f),
            };
            bool consumed = BrewConsumptionModel.Consume(economy, costs);

            Assert.That(consumed, Is.False, "한 재료라도 부족하면 전체 실패");
            Assert.That(economy.GetStock(KnownResources.Mineral), Is.EqualTo(10f).Within(0.001f), "충분했던 광물도 안 깎임(부분차감 X)");
            Assert.That(economy.GetStock(KnownResources.Herb), Is.EqualTo(1f).Within(0.001f), "부족한 약초도 그대로");
        }

        [Test]
        public void Consume_EmptyOrNullCost_IsFreeAndNoOp()
        {
            // 비용 0(현 placeholder 레시피 전부) = 무료 제조 = true + 차감 0 → 기존 동작 회귀 보호.
            CityEconomy economy = new CityEconomy();
            economy.AddStock(KnownResources.Food, 7f);

            Assert.That(BrewConsumptionModel.Consume(economy, new List<ResourceFlow>()), Is.True, "빈 비용 = 무료");
            Assert.That(BrewConsumptionModel.Consume(economy, null), Is.True, "null 비용 = 무료");
            Assert.That(economy.GetStock(KnownResources.Food), Is.EqualTo(7f).Within(0.001f), "무료 제조는 재고 불변");
        }

        [Test]
        public void Consume_SingleCallDeductsExactlyOnce_NoDoubleDeduct()
        {
            // 이중차감 회피 회귀: Consume 1회 = 정확히 1배만 차감(같은 비용을 또 부르면 또 차감 = 호출자 책임).
            CityEconomy economy = new CityEconomy();
            economy.AddStock(KnownResources.Mineral, 30f);

            BrewConsumptionModel.Consume(economy, Cost(KnownResources.Mineral, 10f));
            Assert.That(economy.GetStock(KnownResources.Mineral), Is.EqualTo(20f).Within(0.001f), "1회 호출 = 정확히 1배 차감");
        }

        [Test]
        public void Consume_DuplicateResourceLines_AggregatedNotDoubleCheckedWrong()
        {
            // 같은 자원 두 줄(모딩/작성 실수) = 소요량 합산해 확인·차감(중복 키 안전).
            CityEconomy economy = new CityEconomy();
            economy.AddStock(KnownResources.Mineral, 12f);

            List<ResourceFlow> costs = new List<ResourceFlow>
            {
                new ResourceFlow(KnownResources.Mineral, 5f),
                new ResourceFlow(KnownResources.Mineral, 4f),
            };
            bool consumed = BrewConsumptionModel.Consume(economy, costs);

            Assert.That(consumed, Is.True, "합산 9 ≤ 재고 12 → 성공");
            Assert.That(economy.GetStock(KnownResources.Mineral), Is.EqualTo(3f).Within(0.001f), "두 줄 합산(9)만큼 차감");
        }
    }
}
