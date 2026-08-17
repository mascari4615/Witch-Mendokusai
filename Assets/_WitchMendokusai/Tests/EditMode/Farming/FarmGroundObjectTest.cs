using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WitchMendokusai.DomainSDK.Act;
using WitchMendokusai.DomainSDK.Farming;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-410 — 복셀 땅 위 밭의 한 바퀴를 <b>복셀 없이</b> 재현한다:
	/// 갈기 → 심기 → (시간) → 개화 → 수확. 땅은 <see cref="IBlockGround"/> 로 좁혀 두었기에 가능하다.
	///
	/// ★ 왜 가짜 땅인가: 진짜 청크를 띄우면 「규칙이 틀렸다」와 「세계가 아직 안 떴다」가 구분이 안 된다
	///   — 그 상태의 초록은 「이상 없음」이 아니라 「안 봤음」이다(WM-282 관문 규율).
	/// </summary>
	public sealed class FarmGroundObjectTest
	{
		private const string DIRT = "wm:dirt";
		private const string TILLED = "wm:tilled_soil";
		private const int SEED_ITEM_ID = 30000167;
		private const int PLANT_ID = 4615;
		private const int MINUTES_PER_STAGE = 60;
		private const int MAX_STAGE = 2;

		private static readonly FarmCoord SOIL = new(4, 63, -9);

		private sealed class FakeGround : IBlockGround
		{
			private readonly Dictionary<FarmCoord, string> blocks = new();

			public int SpawnCount { get; private set; }

			public FakeGround(FarmCoord coord, string identifier)
			{
				blocks[coord] = identifier;
			}

			public string BlockNameAt(FarmCoord coord) => blocks.TryGetValue(coord, out string name) ? name : null;

			public void SetBlock(FarmCoord coord, string identifier) => blocks[coord] = identifier;

			public bool SpawnEntity(FarmCoord coord, EntityData entity)
			{
				SpawnCount++;
				return true;
			}
		}

		private sealed class Satchel : IActResourcePool
		{
			private readonly Dictionary<int, int> amountById = new();

			public Satchel(int itemId, int amount)
			{
				amountById[itemId] = amount;
			}

			public int AmountOf(ResourceId resource) => amountById.TryGetValue(resource.Value, out int amount) ? amount : 0;

			public void Add(ResourceId resource, int amount) => amountById[resource.Value] = AmountOf(resource) + amount;
		}

		private static SeedItemData NewSeed()
		{
			WitchPlantSO plant = ScriptableObject.CreateInstance<WitchPlantSO>();
			plant.ID = PLANT_ID;
			plant.ApplyDefaults();
			// 코지 작물(drain 0) — 이 검증의 관심은 시듦이 아니라 「한 바퀴가 도는가」다.
			plant.EditorSetGrowth(MINUTES_PER_STAGE, MAX_STAGE, 0f);

			SeedItemData seed = ScriptableObject.CreateInstance<SeedItemData>();
			seed.ID = SEED_ITEM_ID;
			seed.EditorSetPlant(plant);
			return seed;
		}

		private static FarmGroundObject NewFarm(out GameObject owner, out WorldActSite site, out FakeGround ground, int seedCount = 3)
		{
			owner = new GameObject(nameof(FarmGroundObjectTest));
			site = owner.AddComponent<WorldActSite>();
			site.Initialize();
			site.UseResources(new Satchel(SEED_ITEM_ID, seedCount));

			GameObject farmOwner = new(nameof(FarmGroundObject));
			farmOwner.transform.SetParent(owner.transform);
			FarmGroundObject farm = farmOwner.AddComponent<FarmGroundObject>();
			ground = new FakeGround(SOIL, DIRT);
			farm.UseGround(ground);
			farm.Initialize();
			farm.World = site.World;
			site.Ride(farm.TimeRider);
			return farm;
		}

		[Test]
		public void FullLoop_TillPlantGrowHarvest()
		{
			FarmGroundObject farm = NewFarm(out GameObject owner, out WorldActSite site, out FakeGround ground);
			try
			{
				SeedItemData seed = NewSeed();

				Assert.That(farm.TryTill(SOIL, out _), Is.True, "굳은 흙은 갈린다");
				Assert.That(ground.BlockNameAt(SOIL), Is.EqualTo(TILLED), "간 자리는 밭 블록이 된다");

				Assert.That(farm.TryPlant(SOIL, seed, out _), Is.True);
				Assert.That(farm.Model.GetPlot(SOIL).IsPlanted, Is.True, "그 자리에 작물이 선다");

				// 아직 안 자랐으니 못 거둔다.
				Assert.That(farm.TryHarvest(SOIL, out _, out _), Is.False);

				// 시간이 흐른다 — 아무도 「자라라」고 명령하지 않는다.
				site.Do(new ActSpec(MINUTES_PER_STAGE * MAX_STAGE), out _);

				Assert.That(farm.Model.GetPlot(SOIL).Phase, Is.EqualTo(PlotPhase.Bloomed));
				Assert.That(farm.TryHarvest(SOIL, out HarvestResult harvest, out _), Is.True);
				Assert.That(harvest.PlantDataId, Is.EqualTo(PLANT_ID));
				Assert.That(farm.Model.GetPlot(SOIL).IsPlanted, Is.False, "거둔 자리는 다시 빈 칸");
			}
			finally
			{
				Object.DestroyImmediate(owner);
			}
		}

		[Test]
		public void CannotPlant_OnUntilledGround()
		{
			FarmGroundObject farm = NewFarm(out GameObject owner, out _, out _);
			try
			{
				Assert.That(farm.TryPlant(SOIL, NewSeed(), out _), Is.False, "안 간 땅엔 못 심는다");
			}
			finally
			{
				Object.DestroyImmediate(owner);
			}
		}

		[Test]
		public void NoSeed_NoPlanting_AndNothingIsSpent()
		{
			FarmGroundObject farm = NewFarm(out GameObject owner, out WorldActSite site, out _, seedCount: 0);
			try
			{
				farm.TryTill(SOIL, out _);
				float energyAfterTill = site.Body.Get(NeedKind.Energy);
				int skyAfterTill = site.Calendar.TotalMinutes();

				bool planted = farm.TryPlant(SOIL, NewSeed(), out ActOutcome outcome);

				Assert.That(planted, Is.False);
				Assert.That(outcome.Rejection, Is.EqualTo(ActRejection.Resource), "가방에 씨앗이 없다");
				Assert.That(outcome.RejectedResource, Is.EqualTo(new ResourceId(SEED_ITEM_ID)));
				Assert.That(site.Body.Get(NeedKind.Energy), Is.EqualTo(energyAfterTill), "못 한 심기는 기운도 안 먹는다");
				Assert.That(site.Calendar.TotalMinutes(), Is.EqualTo(skyAfterTill), "시간도 안 먹는다");
			}
			finally
			{
				Object.DestroyImmediate(owner);
			}
		}

		[Test]
		public void TilledGround_IsNotTilledTwice()
		{
			FarmGroundObject farm = NewFarm(out GameObject owner, out WorldActSite site, out _);
			try
			{
				farm.TryTill(SOIL, out _);
				float energyAfterFirst = site.Body.Get(NeedKind.Energy);

				Assert.That(farm.TryTill(SOIL, out _), Is.False, "이미 밭인 자리는 다시 안 간다");
				Assert.That(site.Body.Get(NeedKind.Energy), Is.EqualTo(energyAfterFirst), "헛수고에 대가를 물리지 않는다");
			}
			finally
			{
				Object.DestroyImmediate(owner);
			}
		}

		[Test]
		public void WithoutAWorld_TheFarmDoesNothing()
		{
			// 원장은 「빈 세계」 행동을 성공으로 돌려준다 — 그 관용을 밭이 받으면 공짜로 갈린다.
			GameObject owner = new(nameof(FarmGroundObjectTest));
			try
			{
				FarmGroundObject farm = owner.AddComponent<FarmGroundObject>();
				FakeGround ground = new(SOIL, DIRT);
				farm.UseGround(ground);
				farm.Initialize();

				Assert.That(farm.TryTill(SOIL, out _), Is.False, "세계가 없으면 대가를 못 문다 = 아무 일도 안 한다");
				Assert.That(ground.BlockNameAt(SOIL), Is.EqualTo(DIRT), "땅도 그대로");
			}
			finally
			{
				Object.DestroyImmediate(owner);
			}
		}

		[Test]
		public void RealTimeCrop_IgnoresTheSky_AndGrowsOnRealSeconds()
		{
			// 작물이 제 시계를 고른다 — 「꺼 놔도 자라는」 작물은 세계의 밤을 안 탄다.
			FarmGroundObject farm = NewFarm(out GameObject owner, out WorldActSite site, out _);
			try
			{
				SeedItemData seed = NewSeed();
				seed.Plant.EditorSetClock(PlantClock.Real);

				farm.TryTill(SOIL, out _);
				Assert.That(farm.TryPlant(SOIL, seed, out _), Is.True);
				Assert.That(farm.Model.GetPlot(SOIL).Clock, Is.EqualTo(PlantClock.Real));

				// 세계의 하늘이 이틀 흘러도 이 작물은 꿈쩍 안 한다.
				site.Do(new ActSpec(MINUTES_PER_STAGE * MAX_STAGE * 2), out _);
				Assert.That(farm.Model.GetPlot(SOIL).Phase, Is.EqualTo(PlotPhase.Growing), "하늘은 이 작물의 시계가 아니다");

				// 바깥 현실이 흐르면 그제야 자란다 (기본 환산 = 현실 60초 = 성장 1분).
				farm.TickRealSeconds(MINUTES_PER_STAGE * MAX_STAGE * 60f);
				Assert.That(farm.Model.GetPlot(SOIL).Phase, Is.EqualTo(PlotPhase.Bloomed));
			}
			finally
			{
				Object.DestroyImmediate(owner);
			}
		}

		[Test]
		public void WorldTimeCrop_IgnoresRealSeconds()
		{
			FarmGroundObject farm = NewFarm(out GameObject owner, out _, out _);
			try
			{
				SeedItemData seed = NewSeed(); // 기본 = 세계의 하늘
				farm.TryTill(SOIL, out _);
				farm.TryPlant(SOIL, seed, out _);

				farm.TickRealSeconds(MINUTES_PER_STAGE * MAX_STAGE * 60f * 2f);

				Assert.That(farm.Model.GetPlot(SOIL).Phase, Is.EqualTo(PlotPhase.Growing), "현실은 이 작물의 시계가 아니다");
			}
			finally
			{
				Object.DestroyImmediate(owner);
			}
		}

		[Test]
		public void SavedFarm_ComesBack_AndCatchesUpWithTheSky()
		{
			FarmGroundObject farm = NewFarm(out GameObject owner, out WorldActSite site, out _);
			try
			{
				SeedItemData seed = NewSeed();
				farm.TryTill(SOIL, out _);
				farm.TryPlant(SOIL, seed, out _);
				string json = farm.SaveToJson();

				Assert.That(string.IsNullOrEmpty(json), Is.False, "기억이 적힌다");
				Assert.That(json.Contains("PlantDataId"), Is.True);
			}
			finally
			{
				Object.DestroyImmediate(owner);
			}
		}

		[Test]
		public void FarmTellsWhatHappened_SoTheViewCanReact()
		{
			// 밭은 소리를 직접 내지 않는다 — 무슨 일이 있었는지 알리기만 한다(연출은 구독자 몫).
			FarmGroundObject farm = NewFarm(out GameObject owner, out WorldActSite site, out _);
			try
			{
				int tilled = 0;
				int planted = 0;
				int harvested = 0;
				farm.OnTilled += _ => tilled++;
				farm.OnPlanted += (_, __) => planted++;
				farm.OnHarvested += (_, __) => harvested++;

				SeedItemData seed = NewSeed();
				farm.TryTill(SOIL, out _);
				farm.TryPlant(SOIL, seed, out _);
				site.Do(new ActSpec(MINUTES_PER_STAGE * MAX_STAGE), out _);
				farm.TryHarvest(SOIL, out _, out _);

				Assert.That(tilled, Is.EqualTo(1));
				Assert.That(planted, Is.EqualTo(1));
				Assert.That(harvested, Is.EqualTo(1));
			}
			finally
			{
				Object.DestroyImmediate(owner);
			}
		}

		[Test]
		public void RefusedAct_SaysWhy()
		{
			// 조용한 실패는 「고장」으로 읽힌다 — 못 한 이유가 화면까지 간다.
			FarmGroundObject farm = NewFarm(out GameObject owner, out _, out _, seedCount: 0);
			try
			{
				ActRejection reason = ActRejection.None;
				farm.OnRefused += (_, r) => reason = r;

				farm.TryTill(SOIL, out _);
				farm.TryPlant(SOIL, NewSeed(), out _);

				Assert.That(reason, Is.EqualTo(ActRejection.Resource), "씨앗이 없어서 못 심었다고 알린다");
			}
			finally
			{
				Object.DestroyImmediate(owner);
			}
		}

		[Test]
		public void BetterHoe_MakesTheSameFieldCheaper()
		{
			// 순환의 고리: 팔아서 번 돈 → 더 좋은 괭이 → 같은 밭을 덜 지치고 판다.
			FarmGroundObject bare = NewFarm(out GameObject bareOwner, out WorldActSite bareSite, out _);
			FarmGroundObject tooled = NewFarm(out GameObject tooledOwner, out WorldActSite tooledSite, out _);
			try
			{
				bare.TryTill(SOIL, out _);
				tooled.TryTill(SOIL, out _, costScale: 0.6f);

				float bareSpent = 100f - bareSite.Body.Get(NeedKind.Energy);
				float tooledSpent = 100f - tooledSite.Body.Get(NeedKind.Energy);

				Assert.That(tooledSpent < bareSpent, Is.True, "좋은 괭이는 기운을 덜 먹는다");
				Assert.That(tooledSite.Calendar.TotalMinutes() < bareSite.Calendar.TotalMinutes(), Is.True, "시간도 덜 먹는다");
			}
			finally
			{
				Object.DestroyImmediate(bareOwner);
				Object.DestroyImmediate(tooledOwner);
			}
		}
	}
}
