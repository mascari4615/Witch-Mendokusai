using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
// ★ 좌표는 판정 쪽 (TASK-WM-214).
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2 = WitchMendokusai.Numerics.Vector2;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-164 Phase 1 step4 — <see cref="WorldStage"/> 도시 레이어(건물+도로+존) 영속화 배선 잠금.
	///
	/// 핵심 2가지: (1) GridData+RoadGraph+ZoneGrid 가 한 WorldStageSaveData 로 동시 round-trip,
	/// (2) 옛 세이브 호환 — Phase 1 이전 세이브(RoadSaveData/ZoneSaveData = null)를 Load 해도
	/// NRE 없이 건물만 복원. SaveManager 는 WorldStageSaveData 통째 왕복이라 무수정.
	///
	/// WorldStage 는 Stage:DataSO(ScriptableObject) — CreateInstance 로 생성, Grid/Road/Zone 은
	/// = new() 초기화라 즉시 사용 가능. Editor/PlayMode 무관(직렬화 POCO 왕복만).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class WorldStageCitySaveTest
	{
		private static WorldStage NewStage()
		{
			return ScriptableObject.CreateInstance<WorldStage>();
		}

		[Test]
		public void SaveLoad_RoundTrip_PreservesBuildingRoadZone()
		{
			WorldStage original = NewStage();
			original.GridData.AddBuildingAt(new Vector3Int(0, 0, 0), new BuildingInstanceData(100));
			original.RoadGraph.AddRoad(new Vector3Int(1, 0, 0));
			original.RoadGraph.AddRoad(new Vector3Int(2, 0, 0));
			original.ZoneGrid.Paint(new Vector3Int(3, 0, 0), ZoneType.Residential);

			WorldStageSaveData saved = original.Save();

			WorldStage restored = NewStage();
			restored.Load(saved);

			Assert.That(restored.GridData.HasBuildingAt(new Vector3Int(0, 0, 0)), Is.True, "건물 복원");
			Assert.That(restored.RoadGraph.HasRoad(new Vector3Int(1, 0, 0)), Is.True, "도로 복원");
			Assert.That(restored.RoadGraph.HasRoad(new Vector3Int(2, 0, 0)), Is.True, "도로 복원2");
			Assert.That(restored.ZoneGrid.GetZone(new Vector3Int(3, 0, 0)), Is.EqualTo(ZoneType.Residential), "존 복원");
		}

		[Test]
		public void Load_LegacySaveWithoutCityLayers_DoesNotThrow()
		{
			// Phase 1 이전 세이브 = BuildingSaveData 만, Road/Zone 필드 null.
			WorldStageSaveData legacy = new()
			{
				BuildingSaveData = new List<KeyValuePair<Vector3Int, BuildingInstanceData>>
				{
					new(new Vector3Int(5, 5, 0), new BuildingInstanceData(200))
				},
				RoadSaveData = null,
				ZoneSaveData = null
			};

			WorldStage stage = NewStage();

			Assert.DoesNotThrow(() => stage.Load(legacy), "옛 세이브(Road/Zone null) Load 시 NRE 금지");
			Assert.That(stage.GridData.HasBuildingAt(new Vector3Int(5, 5, 0)), Is.True, "건물은 복원");
			Assert.That(stage.RoadGraph.RoadData.Count, Is.Zero, "도로 없음(빈 그래프)");
			Assert.That(stage.ZoneGrid.ZoneData.Count, Is.Zero, "존 없음(빈 격자)");
			Assert.That(stage.CityEconomy.Stock.Count, Is.Zero, "경제 없음(legacy EconomySaveData=default → CityEconomy.Load skip)");
			Assert.That(stage.CitizenRegistry.Citizens.Count, Is.Zero, "시민 없음(legacy CitizensSaveData=null → skip)");
			Assert.That(stage.PowerSourceRegistry.Sources.Count, Is.Zero, "발전소 없음(legacy PowerSourceSaveData=null → skip)");
		}

		[Test]
		public void Load_Twice_ReplacesNotAccumulates()
		{
			// WorldStage 는 SO 자산 → 인스턴스가 재로드 간 살아남음. Load 가 merge 면 이전 도시가
			// 누적된다(review major finding). Load = replace 여야 함 — 두 번째 Load 후 첫 도시 잔존 X.
			WorldStage stage = NewStage();

			WorldStageSaveData cityA = new()
			{
				BuildingSaveData = new List<KeyValuePair<Vector3Int, BuildingInstanceData>>
				{
					new(new Vector3Int(0, 0, 0), new BuildingInstanceData(1))
				},
				RoadSaveData = new List<KeyValuePair<Vector3Int, RoadCellData>>
				{
					new(new Vector3Int(0, 0, 0), new RoadCellData(RoadType.Basic))
				},
				ZoneSaveData = new List<KeyValuePair<Vector3Int, ZoneCellData>>
				{
					new(new Vector3Int(0, 0, 0), new ZoneCellData(ZoneType.Residential))
				}
			};
			WorldStageSaveData cityB = new()
			{
				BuildingSaveData = new List<KeyValuePair<Vector3Int, BuildingInstanceData>>
				{
					new(new Vector3Int(9, 9, 0), new BuildingInstanceData(2))
				},
				RoadSaveData = new List<KeyValuePair<Vector3Int, RoadCellData>>
				{
					new(new Vector3Int(9, 9, 0), new RoadCellData(RoadType.Basic))
				},
				ZoneSaveData = new List<KeyValuePair<Vector3Int, ZoneCellData>>
				{
					new(new Vector3Int(9, 9, 0), new ZoneCellData(ZoneType.Commercial))
				}
			};

			stage.Load(cityA);
			stage.Load(cityB); // 재로드 = 도시 교체

			Assert.That(stage.GridData.BuildingData.Count, Is.EqualTo(1), "건물: cityA 잔존 X (replace)");
			Assert.That(stage.GridData.HasBuildingAt(new Vector3Int(0, 0, 0)), Is.False, "cityA 건물 제거됨");
			Assert.That(stage.GridData.HasBuildingAt(new Vector3Int(9, 9, 0)), Is.True, "cityB 건물만");
			Assert.That(stage.RoadGraph.RoadData.Count, Is.EqualTo(1), "도로: cityA 잔존 X");
			Assert.That(stage.RoadGraph.HasRoad(new Vector3Int(9, 9, 0)), Is.True);
			Assert.That(stage.ZoneGrid.ZoneData.Count, Is.EqualTo(1), "존: cityA 잔존 X");
			Assert.That(stage.ZoneGrid.GetZone(new Vector3Int(9, 9, 0)), Is.EqualTo(ZoneType.Commercial));
		}

		[Test]
		public void Save_EmptyCity_ProducesNonNullLayers()
		{
			// 새 WorldStage Save → Road/Zone 빈 리스트(null 아님) → 다음 Load 안전.
			WorldStage stage = NewStage();

			WorldStageSaveData saved = stage.Save();

			Assert.That(saved.RoadSaveData, Is.Not.Null, "빈 도시도 RoadSaveData 비-null");
			Assert.That(saved.ZoneSaveData, Is.Not.Null, "빈 도시도 ZoneSaveData 비-null");
			Assert.That(saved.RoadSaveData.Count, Is.Zero);
			Assert.That(saved.ZoneSaveData.Count, Is.Zero);
			Assert.That(saved.EconomySaveData.StockSaveData, Is.Not.Null, "빈 도시도 경제 재고 비-null");
			Assert.That(saved.EconomySaveData.StockSaveData.Count, Is.Zero);
			Assert.That(saved.CitizensSaveData, Is.Not.Null, "빈 도시도 시민 명부 비-null");
			Assert.That(saved.CitizensSaveData.Count, Is.Zero);
			Assert.That(saved.PowerSourceSaveData, Is.Not.Null, "빈 도시도 발전소 명부 비-null");
			Assert.That(saved.PowerSourceSaveData.Count, Is.Zero);
		}

		[Test]
		public void SaveLoad_RoundTrip_PreservesEconomyStock()
		{
			WorldStage original = NewStage();
			original.CityEconomy.AddStock(new ResourceId(0), 50f);
			original.CityEconomy.AddStock(new ResourceId(1), 12.5f);

			WorldStageSaveData saved = original.Save();

			WorldStage restored = NewStage();
			restored.Load(saved);

			Assert.That(restored.CityEconomy.GetStock(new ResourceId(0)), Is.EqualTo(50f).Within(0.0001f), "자원0 재고 복원");
			Assert.That(restored.CityEconomy.GetStock(new ResourceId(1)), Is.EqualTo(12.5f).Within(0.0001f), "자원1 재고 복원");
		}

		[Test]
		public void Load_Twice_ReplacesEconomyStock()
		{
			// 경제도 도시 레이어 형제 — 재로드 = replace(누적 X). GridData/Road/Zone 동일 계약.
			WorldStage stage = NewStage();

			WorldStage seedA = NewStage();
			seedA.CityEconomy.AddStock(new ResourceId(0), 99f);
			stage.Load(seedA.Save());

			WorldStage seedB = NewStage();
			seedB.CityEconomy.AddStock(new ResourceId(1), 7f);
			stage.Load(seedB.Save());

			Assert.That(stage.CityEconomy.GetStock(new ResourceId(0)), Is.EqualTo(0f), "cityA 재고(자원0) 잔존 X (replace)");
			Assert.That(stage.CityEconomy.GetStock(new ResourceId(1)), Is.EqualTo(7f).Within(0.0001f), "cityB 재고만");
			Assert.That(stage.CityEconomy.Stock.Count, Is.EqualTo(1), "재고 키 1개(자원1)만");
		}

		[Test]
		public void SaveLoad_RoundTrip_PreservesCitizens()
		{
			WorldStage original = NewStage();
			original.CitizenRegistry.Add(new CitizenSaveData(new Vector3Int(0, 0, 0), new Vector3Int(5, 0, 0), CitizenState.GoingToWork));
			original.CitizenRegistry.Add(new CitizenSaveData(new Vector3Int(1, 0, 0), new Vector3Int(5, 0, 0), CitizenState.AtHome));

			WorldStageSaveData saved = original.Save();

			WorldStage restored = NewStage();
			restored.Load(saved);

			Assert.That(restored.CitizenRegistry.Citizens.Count, Is.EqualTo(2), "시민 2명 복원");
			Assert.That(restored.CitizenRegistry.Citizens[0].HomeCell, Is.EqualTo(new Vector3Int(0, 0, 0)), "집 셀 복원");
			Assert.That(restored.CitizenRegistry.Citizens[0].State, Is.EqualTo(CitizenState.GoingToWork), "상태 복원");
		}

		[Test]
		public void Load_Twice_ReplacesCitizens()
		{
			WorldStage stage = NewStage();

			WorldStage seedA = NewStage();
			seedA.CitizenRegistry.Add(new CitizenSaveData(new Vector3Int(0, 0, 0), new Vector3Int(5, 0, 0), CitizenState.AtHome));
			seedA.CitizenRegistry.Add(new CitizenSaveData(new Vector3Int(2, 0, 0), new Vector3Int(5, 0, 0), CitizenState.AtHome));
			stage.Load(seedA.Save());

			WorldStage seedB = NewStage();
			seedB.CitizenRegistry.Add(new CitizenSaveData(new Vector3Int(9, 9, 0), new Vector3Int(8, 8, 0), CitizenState.AtWork));
			stage.Load(seedB.Save());

			Assert.That(stage.CitizenRegistry.Citizens.Count, Is.EqualTo(1), "cityA 시민 2명 잔존 X (replace)");
			Assert.That(stage.CitizenRegistry.Citizens[0].HomeCell, Is.EqualTo(new Vector3Int(9, 9, 0)), "cityB 시민만");
		}

		[Test]
		public void SaveLoad_RoundTrip_PreservesPowerSources()
		{
			WorldStage original = NewStage();
			original.PowerSourceRegistry.Add(new Vector3Int(0, 0, 0), range: 5);
			original.PowerSourceRegistry.Add(new Vector3Int(3, 0, 0), range: 8);

			WorldStageSaveData saved = original.Save();

			WorldStage restored = NewStage();
			restored.Load(saved);

			Assert.That(restored.PowerSourceRegistry.Has(new Vector3Int(0, 0, 0)), Is.True, "발전소 복원");
			Assert.That(restored.PowerSourceRegistry.Sources[new Vector3Int(3, 0, 0)].Range, Is.EqualTo(8), "range 복원");
			Assert.That(restored.PowerSourceRegistry.Sources.Count, Is.EqualTo(2));
		}

		[Test]
		public void Load_Twice_ReplacesPowerSources()
		{
			WorldStage stage = NewStage();

			WorldStage seedA = NewStage();
			seedA.PowerSourceRegistry.Add(new Vector3Int(0, 0, 0), 5);
			stage.Load(seedA.Save());

			WorldStage seedB = NewStage();
			seedB.PowerSourceRegistry.Add(new Vector3Int(9, 9, 0), 3);
			stage.Load(seedB.Save());

			Assert.That(stage.PowerSourceRegistry.Has(new Vector3Int(0, 0, 0)), Is.False, "cityA 발전소 잔존 X (replace)");
			Assert.That(stage.PowerSourceRegistry.Has(new Vector3Int(9, 9, 0)), Is.True, "cityB 발전소만");
			Assert.That(stage.PowerSourceRegistry.Sources.Count, Is.EqualTo(1));
		}
	}
}
