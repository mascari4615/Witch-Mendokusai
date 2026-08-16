using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Act;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai
{
	// 복셀 땅 위의 밭 (TASK-WM-410) — 「갈기 → 심기 → 수확」을 <b>블록 좌표</b>에서 판정하는 얇은 접합.
	//
	// ★ 이 컴포넌트가 하는 일은 통역뿐이다:
	//   ① 블록의 임시 번호(RuntimeId) ↔ 영구 이름(wm:dirt) 변환 — 규칙(FarmGround)은 이름만 안다.
	//   ② 대가 판정은 전부 행동 원장(ActLedger, WM-408) — 여기서 기운·시간을 직접 깎지 않는다.
	//   ③ 작물 상태는 전부 GreenhousePlot(POCO) — 엔티티는 <b>뷰</b>다(밭이 커지면 뷰만 갈아끼운다).
	//
	// ★ 왜 온실(WitchGreenhouseObject)과 따로인가: 온실은 좌표 없는 「칸 N개」다(FarmCoord.Legacy).
	//   이쪽은 진짜 땅에 박힌 밭이다. 둘은 같은 Greenhouse/GreenhousePlot 모델을 공유하므로
	//   시듦·돌봄·표본 규칙이 갈라지지 않는다 — 갈라진 건 「어디에 있나」뿐이다.
	public sealed class FarmGroundObject : MonoBehaviour
	{
		private const int DEFAULT_TILL_MINUTES = 30;
		private const int DEFAULT_PLANT_MINUTES = 10;
		private const int DEFAULT_HARVEST_MINUTES = 15;
		private const float DEFAULT_TILL_ENERGY = 8f;
		private const float DEFAULT_PLANT_ENERGY = 5f;
		private const float DEFAULT_HARVEST_ENERGY = 3f;
		private const string DEFAULT_TILLED_BLOCK = "wm:tilled_soil";
		private const float DEFAULT_REAL_SECONDS_PER_MINUTE = 60f;

		[Header("행동의 대가 (수치노출 룰 — 장르색은 전부 여기 산다)")]
		[SerializeField, Min(0)] private int tillMinutes = DEFAULT_TILL_MINUTES;
		[SerializeField, Min(0f)] private float tillEnergy = DEFAULT_TILL_ENERGY;
		[SerializeField, Min(0)] private int plantMinutes = DEFAULT_PLANT_MINUTES;
		[SerializeField, Min(0f)] private float plantEnergy = DEFAULT_PLANT_ENERGY;
		[SerializeField, Min(0)] private int harvestMinutes = DEFAULT_HARVEST_MINUTES;
		[SerializeField, Min(0f)] private float harvestEnergy = DEFAULT_HARVEST_ENERGY;

		[Header("땅 규칙 (모드·바이옴이 늘릴 수 있게 데이터로)")]
		[SerializeField] private string tilledBlock = DEFAULT_TILLED_BLOCK;
		[SerializeField] private List<string> tillableBlocks = new() { "wm:dirt", "wm:grass" };

		[Header("바깥 현실 시계 (Real 을 고른 작물만 탄다)")]
		[Tooltip("현실 몇 초가 성장 1분인가. 60 = 현실 1분 = 성장 1분.")]
		[SerializeField, Min(1f)] private float realSecondsPerGrowthMinute = DEFAULT_REAL_SECONDS_PER_MINUTE;

		[Header("작물 뷰 (없으면 상태만 — 눈엔 안 보임)")]
		[SerializeField] private EntityData cropEntity;

		private Greenhouse greenhouse = new();
		private FarmGround ground;
		private ChunkManager chunkManager;
		private GreenhouseTimeRider timeRider;
		private IBlockGround blockGround;
		// 아직 1분이 안 된 현실 초 — 버리면 짧은 프레임이 영원히 안 쌓인다(WorldCalendar 의 minuteRemainder 선례).
		private float realSecondsCarry;

		// 몸·창고·하늘. 상위(세계)가 준다 — 없으면 밭은 아무 대가도 못 물리므로 아무 것도 안 한다.
		public ActContext World { get; set; }

		public Greenhouse Model => greenhouse;

		/// <summary>이 밭이 시간을 타는 입구 — 세계의 rider 묶음에 넣으면 자는 동안 자란다.</summary>
		public IActTimeRider TimeRider => timeRider;

		public FarmGround Ground => ground;

		private void Reset()
		{
			tillMinutes = DEFAULT_TILL_MINUTES;
			tillEnergy = DEFAULT_TILL_ENERGY;
			plantMinutes = DEFAULT_PLANT_MINUTES;
			plantEnergy = DEFAULT_PLANT_ENERGY;
			harvestMinutes = DEFAULT_HARVEST_MINUTES;
			harvestEnergy = DEFAULT_HARVEST_ENERGY;
			tilledBlock = DEFAULT_TILLED_BLOCK;
			tillableBlocks = new List<string> { "wm:dirt", "wm:grass" };
			realSecondsPerGrowthMinute = DEFAULT_REAL_SECONDS_PER_MINUTE;
		}

		private void Awake()
		{
			// 인스펙터 직렬화 디폴트(빈 값)로 덮여도 밭이 죽지 않게 자가보정 (온실 선례).
			if (string.IsNullOrEmpty(tilledBlock))
			{
				tilledBlock = DEFAULT_TILLED_BLOCK;
			}

			if (tillableBlocks == null || tillableBlocks.Count == 0)
			{
				tillableBlocks = new List<string> { "wm:dirt", "wm:grass" };
			}

			if (realSecondsPerGrowthMinute <= 0f)
			{
				realSecondsPerGrowthMinute = DEFAULT_REAL_SECONDS_PER_MINUTE;
			}

			ground = new FarmGround(tilledBlock, tillableBlocks);
			timeRider = new GreenhouseTimeRider(greenhouse);
			blockGround ??= new ChunkBlockGround(EnsureChunkManager);
		}

		/// <summary>
		/// 이 밭이 쓸 칸 모음을 갈아 끼운다 (TASK-WM-410) — 정본은 스테이지(WorldStage.Farm)다.
		/// 밭이 씬 오브젝트를 따라다니면 스테이지를 나갔다 오는 사이 심은 것이 사라진다.
		/// </summary>
		public void UseModel(Greenhouse model)
		{
			if (model == null)
			{
				return;
			}

			greenhouse = model;
			timeRider = new GreenhouseTimeRider(greenhouse);
		}

		/// <summary>땅을 갈아 끼운다 — 복셀 없이 규칙만 재현·검증할 때(그리고 미래의 다른 땅).</summary>
		public void UseGround(IBlockGround ground)
		{
			blockGround = ground;
		}

		/// <summary>세계·땅을 세운다(멱등). Awake 가 부르고, 검증도 직접 부른다.</summary>
		public void Initialize()
		{
			if (ground == null)
			{
				Awake();
			}
		}

		/// <summary>이 자리를 갈 수 있나 — 세계를 안 건드리는 미리보기(UI 회색 처리).</summary>
		public bool CanTill(FarmCoord soil)
		{
			return HasWorld && ground.CanTill(BlockNameAt(soil));
		}

		// 대가를 물 세계(몸·하늘)가 없으면 밭은 아무 일도 안 한다.
		// ★ 원장은 「빈 세계에 건 행동」을 성공으로 돌려준다(아무 일도 안 일어난 것 = 실패 아님) —
		//   그 관용을 여기서 받아 쓰면 세계 배선을 잊었을 때 <b>공짜로 밭이 갈린다</b>. 그래서 여기서 막는다.
		private bool HasWorld => ground != null && World != null;

		/// <summary>땅을 간다 — 되면 블록이 바뀐다. 대가는 원장이 문다.</summary>
		public bool TryTill(FarmCoord soil, out ActOutcome outcome)
		{
			outcome = default;

			if (CanTill(soil) == false)
			{
				return false;
			}

			ActSpec spec = new(tillMinutes, new[] { new ActNeedDelta(DomainSDK.Life.NeedKind.Energy, -tillEnergy) });
			if (ActLedger.TryApply(spec, World, out outcome) == false)
			{
				return false;
			}

			SetBlockName(soil, tilledBlock);
			return true;
		}

		/// <summary>
		/// 갈린 밭에 심는다 — 작물 상태는 POCO 에, 보이는 몸은 흙 바로 위 엔티티에.
		/// 씨앗 한 톨은 <b>원장이</b> 문다(창고가 비면 원장이 거절한다 — 여기서 가방을 직접 뒤지지 않는다).
		/// </summary>
		public bool TryPlant(FarmCoord soil, SeedItemData seed, out ActOutcome outcome)
		{
			outcome = default;

			WitchPlantSO plant = seed == null ? null : seed.Plant;
			if (plant == null || HasWorld == false || ground.CanPlantOn(BlockNameAt(soil)) == false)
			{
				return false;
			}

			GreenhousePlot plot = greenhouse.GetPlot(soil);
			if (plot != null && plot.IsPlanted)
			{
				return false;
			}

			ActSpec spec = new(
				plantMinutes,
				new[] { new ActNeedDelta(DomainSDK.Life.NeedKind.Energy, -plantEnergy) },
				new[] { new ActResourceDelta(new ResourceId(seed.ID), -1) });
			if (ActLedger.TryApply(spec, World, out outcome) == false)
			{
				return false;
			}

			plot ??= greenhouse.AddPlot(soil);
			plot.Plant(plant.ID, plant.ToGrowthParams(), plant.StartVitality, plant.Clock);
			SpawnCropView(soil);
			return true;
		}

		/// <summary>개화한 칸을 거둔다 — 판정은 밭(GreenhousePlot)이, 대가는 원장이.</summary>
		public bool TryHarvest(FarmCoord soil, out HarvestResult harvest, out ActOutcome outcome)
		{
			harvest = default;
			outcome = default;

			GreenhousePlot plot = greenhouse.GetPlot(soil);
			if (HasWorld == false || plot == null || plot.Phase != PlotPhase.Bloomed)
			{
				return false;
			}

			ActSpec spec = new(harvestMinutes, new[] { new ActNeedDelta(DomainSDK.Life.NeedKind.Energy, -harvestEnergy) });
			if (ActLedger.TryApply(spec, World, out outcome) == false)
			{
				return false;
			}

			return plot.TryHarvest(out harvest);
		}

		private void Update()
		{
			// 게임이 켜져 있는 동안의 현실 시간. 꺼 둔 동안의 몫은 저장(심은 현실 시각)이 붙을 때 채운다.
			TickRealSeconds(Time.unscaledDeltaTime);
		}

		/// <summary>
		/// 바깥 현실이 이만큼 흘렀다 — <see cref="PlantClock.Real"/> 을 고른 작물만 그만큼 자란다.
		/// 세계의 하늘을 탄 작물은 여기서 1초도 안 움직인다(시계가 섞이면 두 감각이 다 죽는다).
		/// </summary>
		public void TickRealSeconds(float seconds)
		{
			if (seconds <= 0f)
			{
				return;
			}

			realSecondsCarry += seconds;
			int minutes = (int)(realSecondsCarry / realSecondsPerGrowthMinute);
			if (minutes <= 0)
			{
				return;
			}

			realSecondsCarry -= minutes * realSecondsPerGrowthMinute;
			greenhouse.TickWithCarers(null, minutes, PlantClock.Real);
		}


		/// <summary>이 밭을 기억한다 — 칸마다 「마지막으로 본 시각」을 제 시계 단위로 적는다.</summary>
		public string SaveToJson()
		{
			FarmSaveData save = FarmPersistence.Save(greenhouse, WorldMinutesNow(), RealUnixSecondsNow());
			return JsonUtility.ToJson(save);
		}

		/// <summary>
		/// 기억에서 되살린다 — 못 본 사이(현실 작물은 꺼 둔 동안, 하늘 작물은 흐른 하늘)를 메운다.
		/// 카탈로그에서 사라진 작물 수를 돌려준다(조용히 없애지 않는다).
		/// </summary>
		public int LoadFromJson(string json)
		{
			if (string.IsNullOrEmpty(json))
			{
				return 0;
			}

			Initialize();
			FarmSaveData save = JsonUtility.FromJson<FarmSaveData>(json);
			return FarmPersistence.Load(greenhouse, save, WorldMinutesNow(), RealUnixSecondsNow(), realSecondsPerGrowthMinute, GrowthParamsOf);
		}

		private static PlantGrowthParams? GrowthParamsOf(int plantDataId)
		{
			WitchPlantSO plant = SOHelper.Get<WitchPlantSO>(plantDataId);
			return plant == null ? null : plant.ToGrowthParams();
		}

		private long WorldMinutesNow()
		{
			return World == null || World.Calendar == null ? 0L : World.Calendar.TotalMinutes();
		}

		private static long RealUnixSecondsNow() => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		/// <summary>플레이어(Fourth)가 봐준다 — 「봐준 것만 진짜(표본)」. 대가 없는 행동.</summary>
		public void Observe(FarmCoord soil)
		{
			greenhouse.GetPlot(soil)?.Observe();
		}

		// 블록의 영구 이름 — 규칙은 임시 번호(RuntimeId)를 절대 안 본다(부팅마다 달라진다).
		private string BlockNameAt(FarmCoord coord)
		{
			return blockGround == null ? null : blockGround.BlockNameAt(coord);
		}

		private void SetBlockName(FarmCoord coord, string identifier)
		{
			blockGround?.SetBlock(coord, identifier);
		}

		// 작물의 보이는 몸 — 흙 바로 위 칸에 선다. 없으면 상태만 굴러간다(뷰는 나중에 갈아끼울 수 있다).
		private void SpawnCropView(FarmCoord soil)
		{
			if (cropEntity == null || blockGround == null)
			{
				return;
			}

			blockGround.SpawnEntity(FarmGround.PlantSpotAbove(soil), cropEntity);
		}

		// 스테이지 스코프라 사용 시점 lazy resolve (BuildManager 선례).
		private ChunkManager EnsureChunkManager()
		{
			if (chunkManager == null)
			{
				chunkManager = FindAnyObjectByType<ChunkManager>();
			}

			return chunkManager;
		}
	}
}
