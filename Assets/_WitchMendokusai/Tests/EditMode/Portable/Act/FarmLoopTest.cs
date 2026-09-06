using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Act;
using WitchMendokusai.DomainSDK.Farming;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-408 — 밭 한 칸이 실제로 한 바퀴 돈다: <b>갈기 → 심기 → 보기 → 자기 → 자람 → 수확</b>.
	///
	/// 전부 같은 원장(<see cref="ActLedger"/>) 하나로 걸린다. 여기서 확인하는 것:
	///   ① 행동이 제 대가를 스스로 말한다(시간·기운·씨앗) — 코어에 농사 규칙이 없다.
	///   ② 흐른 시간은 <see cref="IActTimeRider"/> 가 탄다 — 자는 동안 작물이 자라고 배가 고파진다.
	///   ③ 같은 원장에 대가 0 행동을 걸면 세계는 그대로다 — 농사 게임 옆에서 캐비닛 게임이 산다.
	/// </summary>
	public sealed class FarmLoopTest
	{
		private const int HOURS_PER_DAY = 24;
		private const int DAYS_PER_SEASON = 28;
		private const int SEASONS_PER_YEAR = 4;

		private const int PLOT_ID = 1;
		private const int CARER_ID = 7;
		private const int PLANT_DATA_ID = 4615;

		private const int MINUTES_PER_STAGE = 120;
		private const int MAX_STAGE = 3;
		private const float MAX_VITALITY = 100f;
		private const float DRAIN_PER_MINUTE = 0.05f;
		private const float TEND_RESTORE = 30f;

		private const float NEED_MAX = 100f;
		private const int TILL_MINUTES = 30;
		private const int PLANT_MINUTES = 10;
		private const int OBSERVE_MINUTES = 5;
		private const int SLEEP_MINUTES = 8 * 60;
		private const int HARVEST_MINUTES = 15;

		private static readonly ResourceId SEED = new ResourceId(900);
		private static readonly ResourceId CROP = new ResourceId(901);

		private sealed class Satchel : IActResourcePool
		{
			private readonly Dictionary<ResourceId, int> amountById = new();

			public Satchel(params (ResourceId Resource, int Amount)[] initial)
			{
				foreach ((ResourceId resource, int amount) in initial)
				{
					amountById[resource] = amount;
				}
			}

			public int AmountOf(ResourceId resource) => amountById.TryGetValue(resource, out int amount) ? amount : 0;

			public void Add(ResourceId resource, int amount) => amountById[resource] = AmountOf(resource) + amount;
		}

		private static NeedProfile Profile()
		{
			Dictionary<NeedKind, NeedSpec> specs = new()
			{
				{ NeedKind.Hunger, new NeedSpec(0.03f, 50f, NEED_MAX) },
				{ NeedKind.Energy, new NeedSpec(0.02f, 50f, NEED_MAX) },
				{ NeedKind.Mood, new NeedSpec(0.01f, 50f, NEED_MAX) },
				{ NeedKind.Social, new NeedSpec(0.01f, 50f, NEED_MAX) },
			};
			return new NeedProfile(specs);
		}

		private static PlantGrowthParams GrowthParams()
		{
			return new PlantGrowthParams(MINUTES_PER_STAGE, MAX_STAGE, MAX_VITALITY, DRAIN_PER_MINUTE, TEND_RESTORE);
		}

		// 행동 선언 — 장르색(스타듀의 「하루를 태워 쓴다」)은 전부 이 수치에 산다.
		private static ActSpec Till() => new(TILL_MINUTES, new[] { new ActNeedDelta(NeedKind.Energy, -8f) });

		private static ActSpec Plant() => new(PLANT_MINUTES, new[] { new ActNeedDelta(NeedKind.Energy, -5f) }, new[] { new ActResourceDelta(SEED, -1) });

		private static ActSpec Observe() => new(OBSERVE_MINUTES);

		private static ActSpec Sleep() => new(SLEEP_MINUTES, new[] { new ActNeedDelta(NeedKind.Energy, +100f) });

		private static ActSpec Harvest() => new(HARVEST_MINUTES, new[] { new ActNeedDelta(NeedKind.Energy, -3f) }, new[] { new ActResourceDelta(CROP, 1) });

		[Test]
		public void OneFullDay_TillPlantSleepGrowHarvest()
		{
			Greenhouse greenhouse = new();
			Satchel satchel = new((SEED, 2), (CROP, 0));
			NeedState body = new(new Dictionary<NeedKind, float>
			{
				{ NeedKind.Hunger, 100f }, { NeedKind.Energy, 100f }, { NeedKind.Mood, 100f }, { NeedKind.Social, 100f },
			});
			NeedProfile profile = Profile();
			WorldCalendar calendar = new(HOURS_PER_DAY, DAYS_PER_SEASON, SEASONS_PER_YEAR, 20, 0);

			ActTimeRiders riders = new(
				new GreenhouseTimeRider(greenhouse, new[] { CARER_ID }),
				new NeedDecayTimeRider(body, profile));
			ActContext world = new(body, profile, satchel, calendar, riders);

			// ① 갈기 — 칸이 생긴다.
			Assert.That(ActLedger.TryApply(Till(), world, out _), Is.True, "기운이 있으면 밭은 갈린다");
			GreenhousePlot plot = greenhouse.AddPlot(PLOT_ID);
			Assert.That(plot.Phase, Is.EqualTo(PlotPhase.Empty));

			// ② 심기 — 씨앗이 줄고 작물이 선다.
			Assert.That(plot.Plant(PLANT_DATA_ID, GrowthParams(), MAX_VITALITY), Is.True);
			Assert.That(ActLedger.TryApply(Plant(), world, out _), Is.True);
			Assert.That(satchel.AmountOf(SEED), Is.EqualTo(1), "심으면 씨앗이 준다");
			Assert.That(plot.Phase, Is.EqualTo(PlotPhase.Growing));

			// ③ 보기 — 대가 없는 행동이지만 「봐준 것만 진짜」의 자격이 붙는다.
			plot.Observe();
			Assert.That(ActLedger.TryApply(Observe(), world, out _), Is.True);
			Assert.That(plot.Observed, Is.True);

			// ④ 자기 — 하루가 넘어가고, 자는 동안 작물이 자란다.
			int dayBefore = calendar.TotalDays();
			Assert.That(ActLedger.TryApply(Sleep(), world, out ActOutcome slept), Is.True);
			Assert.That(slept.DayChanged, Is.True, "밤을 건너면 다음 날이다");
			Assert.That(calendar.TotalDays(), Is.EqualTo(dayBefore + 1));
			Assert.That(calendar.Hour, Is.EqualTo(4), "20시에 시작해 30+10+5+480분 = 다음날 새벽 4시");

			// ⑤ 자람 — 아무도 「자라라」고 명령하지 않았다. 시간을 탔을 뿐이다.
			Assert.That(plot.Phase, Is.EqualTo(PlotPhase.Bloomed), "잠든 사이 개화한다");
			Assert.That(plot.IsSpecimenNow, Is.True, "봐준 것만 진짜가 된다");

			// ⑥ 수확 — 판정은 밭이, 대가는 원장이.
			Assert.That(plot.TryHarvest(out HarvestResult harvest), Is.True);
			Assert.That(ActLedger.TryApply(Harvest(), world, out _), Is.True);
			Assert.That(harvest.PlantDataId, Is.EqualTo(PLANT_DATA_ID));
			Assert.That(harvest.IsSpecimen, Is.True);
			Assert.That(harvest.HasDominantCarer, Is.True, "인형이 돌본 기록이 변이 입력으로 남는다");
			Assert.That(harvest.DominantCarerId, Is.EqualTo(CARER_ID));
			Assert.That(satchel.AmountOf(CROP), Is.EqualTo(1), "한 바퀴 돌아 작물 1이 손에 남는다");
			Assert.That(plot.Phase, Is.EqualTo(PlotPhase.Empty), "수확한 칸은 다시 빈 칸");

			// 몸 — 하루를 태워 썼다. 자고 나서도 기운은 무한이 아니고, 밥은 안 먹어 배가 고프다.
			Assert.That(body.Get(NeedKind.Hunger) < 100f, Is.True, "시간을 탄 몸은 배가 고파진다");
			Assert.That(body.Get(NeedKind.Energy) < NEED_MAX, Is.True, "회복 뒤에도 흐른 시간만큼은 든다");
			Assert.That(body.Get(NeedKind.Energy) > 80f, Is.True, "그래도 자고 났으니 기운은 돈다");
		}

		[Test]
		public void CannotPlant_WithoutSeed_AndTheDayDoesNotMove()
		{
			Greenhouse greenhouse = new();
			Satchel emptySatchel = new((SEED, 0));
			NeedState body = new(new Dictionary<NeedKind, float>
			{
				{ NeedKind.Hunger, 100f }, { NeedKind.Energy, 100f }, { NeedKind.Mood, 100f }, { NeedKind.Social, 100f },
			});
			NeedProfile profile = Profile();
			WorldCalendar calendar = new(HOURS_PER_DAY, DAYS_PER_SEASON, SEASONS_PER_YEAR, 9, 0);
			GreenhousePlot plot = greenhouse.AddPlot(PLOT_ID);
			plot.Plant(PLANT_DATA_ID, GrowthParams(), MAX_VITALITY);

			ActContext world = new(body, profile, emptySatchel, calendar,
				new ActTimeRiders(new GreenhouseTimeRider(greenhouse), new NeedDecayTimeRider(body, profile)));

			bool applied = ActLedger.TryApply(Plant(), world, out ActOutcome outcome);

			Assert.That(applied, Is.False);
			Assert.That(outcome.RejectedResource, Is.EqualTo(SEED));
			Assert.That(calendar.Hour, Is.EqualTo(9), "못 한 행동은 시간도 안 먹는다");
			Assert.That(body.Get(NeedKind.Energy), Is.EqualTo(100f), "못 한 행동은 기운도 안 먹는다");
			Assert.That(plot.Vitality, Is.EqualTo(MAX_VITALITY), "못 한 행동 동안 작물도 안 늙는다");
		}

		[Test]
		public void CabinetGame_RunsBeside_TheFarm_WithoutTouchingIt()
		{
			// 같은 세계·같은 원장. 대가 0 행동은 농사판 옆에서 아무것도 안 건드린다.
			Greenhouse greenhouse = new();
			GreenhousePlot plot = greenhouse.AddPlot(PLOT_ID);
			plot.Plant(PLANT_DATA_ID, GrowthParams(), MAX_VITALITY);
			NeedState body = new(new Dictionary<NeedKind, float> { { NeedKind.Energy, 50f } });
			NeedProfile profile = Profile();
			WorldCalendar calendar = new(HOURS_PER_DAY, DAYS_PER_SEASON, SEASONS_PER_YEAR, 15, 0);
			int minutesBefore = calendar.TotalMinutes();

			ActContext world = new(body, profile, null, calendar,
				new ActTimeRiders(new GreenhouseTimeRider(greenhouse), new NeedDecayTimeRider(body, profile)));

			Assert.That(ActLedger.TryApply(ActSpec.Free, world, out _), Is.True);

			Assert.That(calendar.TotalMinutes(), Is.EqualTo(minutesBefore), "캐비닛 앞에선 하늘이 안 움직인다");
			Assert.That(plot.Vitality, Is.EqualTo(MAX_VITALITY), "밭도 그대로");
			Assert.That(body.Get(NeedKind.Energy), Is.EqualTo(50f), "몸도 그대로");
		}
	}
}
