using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-168 INC-5d/INC-9 — LifeAgent 자가회복(self-care)과 4호 개입(도와주기) behavior 잠금.
	/// 활동이 그 욕구를 스스로 채워(소진보다 크면) 한 활동에 안 박히고 순환하는지 / 결핍 감지·도움이 채우는지.
	/// </summary>
	public sealed class LifeSelfCareTest
	{
		private GameObject go;

		private static NeedProfile Profile()
		{
			Dictionary<NeedKind, NeedSpec> specs = new()
			{
				{ NeedKind.Hunger, new NeedSpec(2f, 40f, 100f) },
				{ NeedKind.Energy, new NeedSpec(1.6f, 40f, 100f) },
				{ NeedKind.Mood, new NeedSpec(1.2f, 40f, 100f) },
				{ NeedKind.Social, new NeedSpec(1f, 40f, 100f) },
			};
			return new NeedProfile(specs);
		}

		private LifeAgent NewAgent(NeedState state, float selfSatisfyPerMinute)
		{
			go = new GameObject("self-care-test");
			LifeAgent agent = go.AddComponent<LifeAgent>();
			agent.Initialize(Profile(), state);
			agent.SetTimeOfDay(TimeOfDay.Afternoon);
			agent.SetSelfSatisfyPerMinute(selfSatisfyPerMinute);
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
		public void SelfCare_ActiveNeed_Recovers()
		{
			// Hunger 30(<40) → Eat. 자가회복 5/분 > 소진 2/분 이므로 먹는 동안 Hunger 가 올라야 한다.
			NeedState state = new(new Dictionary<NeedKind, float>
			{
				{ NeedKind.Hunger, 30f }, { NeedKind.Energy, 90f }, { NeedKind.Mood, 90f }, { NeedKind.Social, 90f },
			});
			LifeAgent agent = NewAgent(state, 5f);
			Assert.That(agent.CurrentActivity, Is.EqualTo(ActivityKind.Eat), "배고픔 → 먹기");

			agent.TickMinutes(5); // Hunger = 30 - 2*5 + 5*5 = 45
			Assert.That(agent.NeedState.Get(NeedKind.Hunger), Is.GreaterThan(30f), "활동(먹기)이 그 욕구를 스스로 채운다");
		}

		[Test]
		public void SelfCare_NoSelfCare_PinsOnOneActivity()
		{
			// 자가회복 0 = 욕구가 바닥에 핀 채 한 활동 고착(self-care 없으면 strobe X, 정지). 대조군.
			NeedState state = new(new Dictionary<NeedKind, float>
			{
				{ NeedKind.Hunger, 10f }, { NeedKind.Energy, 90f }, { NeedKind.Mood, 90f }, { NeedKind.Social, 90f },
			});
			LifeAgent agent = NewAgent(state, 0f);
			HashSet<ActivityKind> seen = new();
			for (int i = 0; i < 30; i++)
			{
				agent.TickMinutes(2);
				seen.Add(agent.CurrentActivity);
			}

			Assert.That(seen, Has.Member(ActivityKind.Eat), "Hunger 바닥 = 계속 Eat");
			Assert.That(seen.Count, Is.LessThanOrEqualTo(2), "자가회복 0 = 활동이 거의 안 바뀜(고착)");
		}

		[Test]
		public void SelfCare_WithRecovery_CyclesActivities()
		{
			// 자가회복이 소진보다 크면 욕구가 임계 위로 회복 → 다음 급한 욕구로 → 활동 순환(여러 종류).
			NeedState state = new(new Dictionary<NeedKind, float>
			{
				{ NeedKind.Hunger, 35f }, { NeedKind.Energy, 55f }, { NeedKind.Mood, 70f }, { NeedKind.Social, 85f },
			});
			LifeAgent agent = NewAgent(state, 5f);
			HashSet<ActivityKind> seen = new();
			for (int i = 0; i < 60; i++)
			{
				agent.TickMinutes(6);
				seen.Add(agent.CurrentActivity);
			}

			Assert.That(seen.Count, Is.GreaterThanOrEqualTo(3), "자가회복으로 여러 활동을 순환(고착 아님)");
		}

		[Test]
		public void Intervention_HasProblem_And_HelpFills()
		{
			NeedState state = new(new Dictionary<NeedKind, float>
			{
				{ NeedKind.Hunger, 10f }, { NeedKind.Energy, 90f }, { NeedKind.Mood, 90f }, { NeedKind.Social, 90f },
			});
			LifeAgent agent = NewAgent(state, 0f);

			Assert.That(agent.HasProblem, Is.True, "결핍 있으면 문제 상태");
			Assert.That(agent.TryHelp(80f), Is.True, "도움이 적용됨");
			Assert.That(agent.NeedState.Get(NeedKind.Hunger), Is.GreaterThan(40f), "급한 욕구가 채워짐");
			Assert.That(agent.HasProblem, Is.False, "채워졌으니 더는 문제 아님");
			Assert.That(agent.TryHelp(80f), Is.False, "문제 없으면 도움은 무효(과잉 X)");
		}
	}
}
