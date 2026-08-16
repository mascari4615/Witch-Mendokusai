using System;
using UnityEngine;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = "WS_", menuName = "WM/Data/" + nameof(WorldStage))]
	public class WorldStage : Stage, ISavable<WorldStageSaveData>
	{
		[field: NonSerialized] public GridData GridData { get; private set; } = new();

		// SimCity Phase 1 (TASK-WM-164): 도로/존 레이어. GridData 형제 — 같은 셀 좌표계(Vector3Int,
		// z=0) 공유, 책임 분리. WorldStage 가 셋 다 소유(NonSerialized 런타임, ISavable 로 영속).
		[field: NonSerialized] public RoadGraph RoadGraph { get; private set; } = new();
		[field: NonSerialized] public ZoneGrid ZoneGrid { get; private set; } = new();

		// SimCity Phase 2 (TASK-WM-166 INC-4): GlassBox 경제 상태(자원 누계 재고). 도시 레이어 형제 —
		// WorldStage 가 소유(NonSerialized 런타임, ISavable 영속). INC-5 일일 틱이 생산/소비를 여기 누적.
		[field: NonSerialized] public CityEconomy CityEconomy { get; private set; } = new();

		// SimCity Phase 2 (TASK-WM-166 INC-6): 통근 시민 명부. 도시 레이어 형제. INC-7 이동이 여기서 spawn/추종.
		[field: NonSerialized] public CitizenRegistry CitizenRegistry { get; private set; } = new();

		// SimCity Phase 3 (TASK-WM-176 INC-3): 발전소(전력원) 명부. 도시 레이어 형제. INC-5 가 매일 전력 전파 시드.
		[field: NonSerialized] public PowerSourceRegistry PowerSourceRegistry { get; private set; } = new();

		// 밭 (TASK-WM-410): 이 스테이지의 밭 칸들. 도시 레이어 형제 - 스테이지가 소유(런타임 NonSerialized,
		// ISavable 영속)하고, 씬의 FarmGroundObject 는 이걸 <b>빌려 쓴다</b>. 밭이 씬 오브젝트를 따라다니면
		// 스테이지를 나갔다 오는 사이 심은 것이 사라진다.
		[field: NonSerialized] public Greenhouse Farm { get; private set; } = new();

		[Header("밭 - 바깥 현실 시계 환산 (수치노출 룰: 같은 수를 두 곳에 안 적는다)")]
		[Tooltip("현실 몇 초가 성장 1분인가. 60 = 현실 1분 = 성장 1분.")]
		[field: SerializeField, Min(1f)] public float RealSecondsPerGrowthMinute { get; private set; } = 60f;

		public void Load(WorldStageSaveData saveData)
		{
			// Load = replace, not merge. WorldStage 는 SO 자산이라 인스턴스가 stage 재진입/재로드
			// 간 살아남음 → 비우지 않으면 이전 도시 데이터가 누적(예: 게임 두 번 로드). 전부 clear 선행.
			GridData.BuildingData.Clear();
			RoadGraph.RoadData.Clear();
			ZoneGrid.ZoneData.Clear();
			CityEconomy.Stock.Clear();
			CitizenRegistry.Citizens.Clear();
			PowerSourceRegistry.Sources.Clear();
			ClearFarm();

			GridData.Load(saveData.BuildingSaveData);

			// 옛 세이브엔 Road/Zone/Economy 필드 부재(null) — 이전 도시는 도로/존/경제 없음. null skip.
			if (saveData.RoadSaveData != null)
			{
				RoadGraph.Load(saveData.RoadSaveData);
			}
			if (saveData.ZoneSaveData != null)
			{
				ZoneGrid.Load(saveData.ZoneSaveData);
			}

			// EconomySaveData 는 struct(항상 비-null) — 내부 StockSaveData null 가드는 CityEconomy.Load 가 자체 처리(legacy 세이브 = default → skip).
			CityEconomy.Load(saveData.EconomySaveData);

			// 옛 세이브엔 시민 부재(null) — null skip.
			if (saveData.CitizensSaveData != null)
			{
				CitizenRegistry.Load(saveData.CitizensSaveData);
			}

			// 옛 세이브엔 발전소 부재(null) — null skip.
			if (saveData.PowerSourceSaveData != null)
			{
				PowerSourceRegistry.Load(saveData.PowerSourceSaveData);
			}
		}

		public WorldStageSaveData Save()
		{
			return new WorldStageSaveData()
			{
				BuildingSaveData = GridData.Save(),
				RoadSaveData = RoadGraph.Save(),
				ZoneSaveData = ZoneGrid.Save(),
				EconomySaveData = CityEconomy.Save(),
				CitizensSaveData = CitizenRegistry.Save(),
				PowerSourceSaveData = PowerSourceRegistry.Save(),
				FarmSaveData = FarmPersistence.Save(Farm, WorldMinutesNow(), RealUnixSecondsNow())
			};
		}

		// 스테이지 SO 는 재진입 사이 살아남는다 - 안 비우면 지난 밭이 겹쳐 쌓인다(Load = replace 규약).
		private void ClearFarm()
		{
			Farm = new Greenhouse();
		}

		private static long WorldMinutesNow()
		{
			if (WorldClock.TryGetExistingInstance(out WorldClock clock) == false || clock.Config == null)
			{
				return 0L;
			}

			long totalDays = ((long)(clock.Year - 1) * clock.Config.SeasonsPerYear + clock.Season) * clock.Config.DaysPerSeason + (clock.Day - 1);
			return (totalDays * clock.Config.HoursPerDay + clock.Hour) * 60L + clock.Minute;
		}

		private static long RealUnixSecondsNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		// 성장 수치는 게임 데이터(작물 SO) - 코어가 안 든다.
		private static PlantGrowthParams? GrowthParamsOf(int plantDataId)
		{
			WitchPlantSO plant = SOHelper.Get<WitchPlantSO>(plantDataId);
			return plant == null ? null : plant.ToGrowthParams();
		}
	}
}