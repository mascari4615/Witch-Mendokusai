using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-183 INC-W4a — LifeAgent 노동 계층 잠금: 한가할 때(욕구 충족·낮) 성격대로 일해 자원을
    /// 마을 창고(CityEconomy)에 쌓고, 결핍이면 일 멈추고(욕구 우선), 4호 지시가 기본 일을 덮는다. (MonoBehaviour EditMode)
    /// </summary>
    public sealed class LifeAgentWorkTest
    {
        private GameObject go;

        // 느린 소진 — 30분 틱에도 결핍 안 생김(한가 = 노동 가능 상태 확보).
        private static NeedProfile CalmProfile()
        {
            Dictionary<NeedKind, NeedSpec> specs = new()
            {
                { NeedKind.Hunger, new NeedSpec(0.1f, 40f, 100f) },
                { NeedKind.Energy, new NeedSpec(0.1f, 40f, 100f) },
                { NeedKind.Mood, new NeedSpec(0.1f, 40f, 100f) },
                { NeedKind.Social, new NeedSpec(0.1f, 40f, 100f) },
            };
            return new NeedProfile(specs);
        }

        private static NeedState FullNeeds()
        {
            return new NeedState(new Dictionary<NeedKind, float>
            {
                { NeedKind.Hunger, 95f }, { NeedKind.Energy, 95f }, { NeedKind.Mood, 95f }, { NeedKind.Social, 95f },
            });
        }

        private LifeAgent NewAgent(NeedState state)
        {
            go = new GameObject("work-test");
            LifeAgent agent = go.AddComponent<LifeAgent>();
            agent.Initialize(CalmProfile(), state);
            agent.SetTimeOfDay(TimeOfDay.Afternoon); // 낮 — 한가하면 Idle(밤이면 Sleep 이라 노동 X)
            agent.SetSelfSatisfyPerMinute(0f);       // 자가회복 끔 — 결핍 케이스가 결핍 유지(테스트 격리)
            return agent;
        }

        private static WorkProfile WorkProfileOf(WorkKind defaultWork)
        {
            return new WorkProfile(defaultWork, new Dictionary<WorkKind, float>());
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
                go = null;
            }
        }

        [Test]
        public void NoDeficit_WorksAndAccumulatesResources()
        {
            LifeAgent agent = NewAgent(FullNeeds());
            agent.SetWorkProfile(WorkProfileOf(WorkKind.Mine));
            CityEconomy economy = new CityEconomy();
            agent.AttachEconomy(economy);

            Assert.That(agent.CurrentActivity, Is.EqualTo(ActivityKind.Idle), "욕구 충족 = 한가(낮)");
            agent.TickMinutes(30);

            // Mine 분당 Mineral 0.4 × 효율 1.0 × 30분 = 12.
            Assert.That(agent.CurrentWork, Is.EqualTo(WorkKind.Mine), "한가하면 성격 기본 일(Mine)");
            Assert.That(economy.GetStock(KnownResources.Mineral), Is.EqualTo(12f).Within(0.001f), "일한 만큼 마을 창고에 쌓임");
        }

        [Test]
        public void Deficit_StopsWorkingToTendNeed()
        {
            NeedState hungry = new NeedState(new Dictionary<NeedKind, float>
            {
                { NeedKind.Hunger, 10f }, { NeedKind.Energy, 95f }, { NeedKind.Mood, 95f }, { NeedKind.Social, 95f },
            });
            LifeAgent agent = NewAgent(hungry);
            agent.SetWorkProfile(WorkProfileOf(WorkKind.Mine));
            CityEconomy economy = new CityEconomy();
            agent.AttachEconomy(economy);

            Assert.That(agent.CurrentActivity, Is.EqualTo(ActivityKind.Eat), "배고프면 먹기");
            agent.TickMinutes(30);
            Assert.That(economy.GetStock(KnownResources.Mineral), Is.EqualTo(0f), "결핍 중엔 생산 0(욕구가 노동을 이김)");
        }

        [Test]
        public void AssignWork_OverridesDefaultJob()
        {
            LifeAgent agent = NewAgent(FullNeeds());
            agent.SetWorkProfile(WorkProfileOf(WorkKind.Mine));
            CityEconomy economy = new CityEconomy();
            agent.AttachEconomy(economy);

            Assert.That(agent.AssignWork(WorkKind.Cook, 60), Is.True, "4호 지시 적용");
            agent.TickMinutes(30);

            Assert.That(agent.CurrentWork, Is.EqualTo(WorkKind.Cook), "지시받은 일(Cook) 우선");
            Assert.That(economy.GetStock(KnownResources.Food), Is.GreaterThan(0f), "Cook → 식량 생산");
            Assert.That(economy.GetStock(KnownResources.Mineral), Is.EqualTo(0f), "기본 일(Mine) 은 안 함");
        }

        [Test]
        public void NoWorkProfile_NeverWorks()
        {
            LifeAgent agent = NewAgent(FullNeeds());
            CityEconomy economy = new CityEconomy();
            agent.AttachEconomy(economy);
            agent.TickMinutes(30);

            Assert.That(agent.CurrentWork, Is.EqualTo(WorkKind.Idle), "노동 성격 없으면 Idle");
            Assert.That(economy.Stock.Count, Is.EqualTo(0), "생산 0");
        }
    }
}
