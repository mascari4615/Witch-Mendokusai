using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-183 INC-W1 — <see cref="WorkModel"/>/<see cref="WorkProductionTable"/> 노동 산출 코어 회귀 잠금 (순수, Play 무관).
    /// 일 × 효율 × 시간 = 생산량 결정성 + 일↔자원 매핑 완전성. (패턴: NeedModelTest)
    /// </summary>
    public sealed class WorkModelTest
    {
        private static WorkProfile MakeProfile(WorkKind defaultWork, float efficiency)
        {
            return new WorkProfile(defaultWork, new Dictionary<WorkKind, float>
            {
                { WorkKind.Mine, efficiency },
                { WorkKind.Forage, efficiency },
                { WorkKind.Cook, efficiency },
            });
        }

        [Test]
        public void Produce_MultipliesBaseRateByEfficiencyAndMinutes()
        {
            // Forage 분당 Acorn 0.5 × 효율 1.0 × 60분 = 30, Herb 0.2 × 60 = 12.
            WorkProfile profile = MakeProfile(WorkKind.Forage, 1f);
            IReadOnlyList<ResourceFlow> produced = WorkModel.Produce(WorkKind.Forage, profile, 60);

            Assert.That(produced.Count, Is.EqualTo(2), "Forage = 도토리 + 약초 두 자원");
            Assert.That(AmountOf(produced, KnownResources.Acorn), Is.EqualTo(30f).Within(0.001f), "0.5/분 × 60분");
            Assert.That(AmountOf(produced, KnownResources.Herb), Is.EqualTo(12f).Within(0.001f), "0.2/분 × 60분");
        }

        [Test]
        public void Produce_EfficiencyScalesOutput()
        {
            // 효율 1.5 → Mineral 0.4 × 1.5 × 60 = 36.
            WorkProfile profile = MakeProfile(WorkKind.Mine, 1.5f);
            IReadOnlyList<ResourceFlow> produced = WorkModel.Produce(WorkKind.Mine, profile, 60);

            Assert.That(AmountOf(produced, KnownResources.Mineral), Is.EqualTo(36f).Within(0.001f), "0.4 × 1.5 × 60");
        }

        [Test]
        public void Produce_UnspecifiedEfficiency_DefaultsToOne()
        {
            // Cook 효율 미지정 프로필 → 기본 1.0(누구나 기본 숙련). Food 0.6 × 1.0 × 30 = 18.
            WorkProfile profile = new WorkProfile(WorkKind.Forage, new Dictionary<WorkKind, float>());
            IReadOnlyList<ResourceFlow> produced = WorkModel.Produce(WorkKind.Cook, profile, 30);

            Assert.That(AmountOf(produced, KnownResources.Food), Is.EqualTo(18f).Within(0.001f), "미지정 효율 = 기본 1.0");
        }

        [Test]
        public void Produce_Idle_YieldsNothing()
        {
            WorkProfile profile = MakeProfile(WorkKind.Forage, 1f);
            Assert.That(WorkModel.Produce(WorkKind.Idle, profile, 60).Count, Is.EqualTo(0), "Idle = 생산 0");
        }

        [Test]
        public void Produce_ZeroMinutes_YieldsNothing()
        {
            WorkProfile profile = MakeProfile(WorkKind.Forage, 1f);
            Assert.That(WorkModel.Produce(WorkKind.Forage, profile, 0).Count, Is.EqualTo(0), "0분 = 생산 0");
        }

        [Test]
        public void ProductionTable_EveryNonIdleKind_ProducesSomething()
        {
            // 일↔자원 매핑 완전성 — 새 WorkKind 추가 시 테이블 누락(빈 산출) 잡힘. Idle 만 빈 산출.
            foreach (WorkKind kind in WorkKinds.OrderedKinds)
            {
                int count = WorkProductionTable.BaseFlowsPerMinute(kind).Count;
                if (kind == WorkKind.Idle)
                {
                    Assert.That(count, Is.EqualTo(0), "Idle 은 생산 0");
                }
                else
                {
                    Assert.That(count, Is.GreaterThan(0), $"{kind} 은 자원을 생산해야(테이블 매핑 누락 방지)");
                }
            }
        }

        private static float AmountOf(IReadOnlyList<ResourceFlow> flows, ResourceId resource)
        {
            foreach (ResourceFlow flow in flows)
            {
                if (flow.Resource.Equals(resource))
                {
                    return flow.Rate;
                }
            }

            return 0f;
        }
    }
}
