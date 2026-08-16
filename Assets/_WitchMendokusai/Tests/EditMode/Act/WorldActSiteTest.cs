using NUnit.Framework;
using UnityEngine;
using WitchMendokusai.DomainSDK.Act;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-410 — 세계가 서는 자리(몸 하나·하늘 하나·시간을 타는 것들).
	/// 시계 없이도 검증되도록 <c>Initialize()</c> 로 세계만 세워 확인한다
	/// (D 패턴 — MonoBehaviour 를 무거운 의존 없이 EditMode 에서 직접 돌린다).
	/// </summary>
	public sealed class WorldActSiteTest
	{
		private const float NEED_MAX = 100f;
		private const int ONE_HOUR = 60;

		private static WorldActSite NewSite(out GameObject owner)
		{
			owner = new GameObject(nameof(WorldActSiteTest));
			WorldActSite site = owner.AddComponent<WorldActSite>();
			site.Initialize();
			return site;
		}

		[Test]
		public void Act_SpendsBody_AndTimeRidersAgeTheBody()
		{
			WorldActSite site = NewSite(out GameObject owner);
			try
			{
				// 밭 갈기 한 시간, 기운 -8. 선언한 소모 + 흐른 한 시간의 자연 감소가 함께 걸린다.
				ActSpec till = new(ONE_HOUR, new[] { new ActNeedDelta(NeedKind.Energy, -8f) });

				bool applied = site.Do(till, out ActOutcome outcome);

				Assert.That(applied, Is.True);
				Assert.That(outcome.Applied, Is.True);
				Assert.That(site.Body.Get(NeedKind.Energy) < NEED_MAX - 8f, Is.True, "선언한 소모 위에 흐른 시간이 더 든다");
				Assert.That(site.Body.Get(NeedKind.Hunger) < NEED_MAX, Is.True, "한 시간이 흘렀으니 배도 고파진다");
			}
			finally
			{
				Object.DestroyImmediate(owner);
			}
		}

		[Test]
		public void Act_MovesTheSky()
		{
			WorldActSite site = NewSite(out GameObject owner);
			try
			{
				int before = site.Calendar.TotalMinutes();
				site.Do(new ActSpec(ONE_HOUR), out _);

				Assert.That(site.Calendar.TotalMinutes(), Is.EqualTo(before + ONE_HOUR), "행동이 먹은 시간만큼 하늘이 간다");
			}
			finally
			{
				Object.DestroyImmediate(owner);
			}
		}

		[Test]
		public void NoEnergy_NoAct_AndNothingMoves()
		{
			WorldActSite site = NewSite(out GameObject owner);
			try
			{
				// 기운을 바닥까지 쓴다 — 감당 못 하는 행동은 세계를 1도 안 건드린다.
				ActSpec heavy = new(ONE_HOUR, new[] { new ActNeedDelta(NeedKind.Energy, -NEED_MAX * 2f) });
				int skyBefore = site.Calendar.TotalMinutes();

				bool applied = site.Do(heavy, out ActOutcome outcome);

				Assert.That(applied, Is.False);
				Assert.That(outcome.Rejection, Is.EqualTo(ActRejection.Need));
				Assert.That(outcome.RejectedNeed, Is.EqualTo(NeedKind.Energy));
				Assert.That(site.Body.Get(NeedKind.Energy), Is.EqualTo(NEED_MAX), "거절이면 몸은 그대로");
				Assert.That(site.Calendar.TotalMinutes(), Is.EqualTo(skyBefore), "거절이면 하늘도 그대로");
			}
			finally
			{
				Object.DestroyImmediate(owner);
			}
		}

		[Test]
		public void FreeAct_ChangesNothing_EvenWithAWorldAttached()
		{
			// 세계가 다 붙어 있어도 대가 0 행동은 아무것도 안 건드린다 — 캐비닛 게임이 사는 자리.
			WorldActSite site = NewSite(out GameObject owner);
			try
			{
				int skyBefore = site.Calendar.TotalMinutes();

				site.Do(ActSpec.Free, out _);

				Assert.That(site.Calendar.TotalMinutes(), Is.EqualTo(skyBefore));
				Assert.That(site.Body.Get(NeedKind.Energy), Is.EqualTo(NEED_MAX));
				Assert.That(site.Body.Get(NeedKind.Hunger), Is.EqualTo(NEED_MAX));
			}
			finally
			{
				Object.DestroyImmediate(owner);
			}
		}

		[Test]
		public void RiddenThings_AgeOnlyWhenTimePasses()
		{
			WorldActSite site = NewSite(out GameObject owner);
			try
			{
				CountingRider rider = new();
				site.Ride(rider);

				site.Do(ActSpec.Free, out _);
				Assert.That(rider.Minutes, Is.EqualTo(0), "시간을 안 먹는 행동엔 아무도 안 늙는다");

				site.Do(new ActSpec(ONE_HOUR), out _);
				Assert.That(rider.Minutes, Is.EqualTo(ONE_HOUR), "흐른 만큼만 태운다");
			}
			finally
			{
				Object.DestroyImmediate(owner);
			}
		}

		private sealed class CountingRider : IActTimeRider
		{
			public int Minutes { get; private set; }

			public void RideMinutes(int minutes, bool dayChanged)
			{
				Minutes += minutes;
			}
		}
	}
}
