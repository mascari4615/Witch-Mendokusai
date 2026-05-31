using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WitchMendokusai;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-168 Tomodachi 자율 삶 레이어 INC-5b — <see cref="LifeAgent"/> 틱→활동전환 behavior 잠금.
	/// UnitObject 비의존 리팩터로 EditMode 직접 인스턴스화 가능(GameObject+AddComponent). 순수 모델(INC-1~2)
	/// 위 MonoBehaviour 배선이 실제로 욕구를 소진시키고 활동을 바꾸는지 검증. (씬 이동/애니 시각화는 INC-5c)
	/// </summary>
	public sealed class LifeAgentTest
	{
		private GameObject go;

		// Hunger 빨리 줆(분당 2, 임계 30, 상한 100) — 짧은 틱으로 결핍 유도.
		private static NeedProfile MakeProfile()
		{
			Dictionary<NeedKind, NeedSpec> specs = new()
			{
				{ NeedKind.Hunger, new NeedSpec(2f, 30f, 100f) },
				{ NeedKind.Energy, new NeedSpec(0.5f, 20f, 100f) },
				{ NeedKind.Mood, new NeedSpec(0.5f, 10f, 50f) },
				{ NeedKind.Social, new NeedSpec(0.5f, 30f, 100f) },
			};
			return new NeedProfile(specs);
		}

		private static NeedState Satisfied()
		{
			return new NeedState(new Dictionary<NeedKind, float>
			{
				{ NeedKind.Hunger, 100f }, { NeedKind.Energy, 100f }, { NeedKind.Mood, 50f }, { NeedKind.Social, 100f },
			});
		}

		private LifeAgent CreateAgent(NeedState state, TimeOfDay timeOfDay)
		{
			go = new GameObject("life-agent-test");
			LifeAgent agent = go.AddComponent<LifeAgent>();
			agent.Initialize(MakeProfile(), state);
			agent.SetTimeOfDay(timeOfDay);
			return agent;
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
		public void Initialize_AllSatisfied_Day_StartsIdle()
		{
			LifeAgent agent = CreateAgent(Satisfied(), TimeOfDay.Afternoon);
			Assert.That(agent.CurrentActivity, Is.EqualTo(ActivityKind.Idle), "다 만족한 낮 = 배회");
		}

		[Test]
		public void TickMinutes_DepletesHunger_SwitchesToEat()
		{
			LifeAgent agent = CreateAgent(Satisfied(), TimeOfDay.Afternoon);

			agent.TickMinutes(40); // Hunger 100 - 2*40 = 20 < 임계 30 → 문제 → Eat
			Assert.That(agent.NeedState.Get(NeedKind.Hunger), Is.EqualTo(20f), "틱이 욕구를 실제로 소진");
			Assert.That(agent.CurrentActivity, Is.EqualTo(ActivityKind.Eat), "배고픔 결핍 → 먹기로 전환");
		}

		[Test]
		public void OnActivityChanged_FiresOnceOnTransition()
		{
			LifeAgent agent = CreateAgent(Satisfied(), TimeOfDay.Afternoon);
			int fireCount = 0;
			ActivityKind last = ActivityKind.Idle;
			agent.OnActivityChanged += activity => { fireCount++; last = activity; };

			agent.TickMinutes(40); // Idle → Eat (1회 전환)
			Assert.That(fireCount, Is.EqualTo(1), "전환 1회 = 이벤트 1회");
			Assert.That(last, Is.EqualTo(ActivityKind.Eat));
		}

		[Test]
		public void TickMinutes_NoActivityChange_NoEvent()
		{
			LifeAgent agent = CreateAgent(Satisfied(), TimeOfDay.Afternoon);
			agent.TickMinutes(40); // → Eat
			int fireAfterFirst = 0;
			agent.OnActivityChanged += _ => fireAfterFirst++;

			agent.TickMinutes(5); // 여전히 Hunger 최저 → Eat 유지
			Assert.That(agent.CurrentActivity, Is.EqualTo(ActivityKind.Eat));
			Assert.That(fireAfterFirst, Is.EqualTo(0), "활동 안 바뀌면 이벤트 없음");
		}
	}
}
