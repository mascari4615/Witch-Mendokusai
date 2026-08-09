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
	/// TASK-WM-164 Phase 1 step2 — <see cref="ZoneGrid"/> 회귀 잠금 (RoadGraph step1 동형).
	///
	/// ZoneGrid 는 R/C/I 페인트 레이어 — CountByType 이 RciDemandModel 입력원이라 집계 정확성이
	/// 수요 피드백의 바닥. 순수 POCO(Vector3Int 값타입) — Editor/PlayMode 무관.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class ZoneGridTest
	{
		[Test]
		public void Paint_ThenHasZoneAndGetZone()
		{
			ZoneGrid grid = new();
			Vector3Int cell = new(2, 3, 0);

			Assert.That(grid.HasZone(cell), Is.False);
			Assert.That(grid.GetZone(cell), Is.EqualTo(ZoneType.Empty), "미페인트 = Empty");

			grid.Paint(cell, ZoneType.Residential);

			Assert.That(grid.HasZone(cell), Is.True);
			Assert.That(grid.GetZone(cell), Is.EqualTo(ZoneType.Residential));
		}

		[Test]
		public void Paint_Empty_RemovesZone()
		{
			ZoneGrid grid = new();
			Vector3Int cell = new(0, 0, 0);
			grid.Paint(cell, ZoneType.Commercial);

			grid.Paint(cell, ZoneType.Empty); // Empty 페인트 = 해제

			Assert.That(grid.HasZone(cell), Is.False, "Empty 페인트는 키 제거");
			Assert.That(grid.GetZone(cell), Is.EqualTo(ZoneType.Empty));
		}

		[Test]
		public void Paint_Idempotent_Overwrite()
		{
			ZoneGrid grid = new();
			Vector3Int cell = new(1, 1, 0);

			grid.Paint(cell, ZoneType.Residential);
			grid.Paint(cell, ZoneType.Industrial); // 재페인트 = 덮어쓰기

			Assert.That(grid.ZoneData.Count, Is.EqualTo(1), "재페인트해도 셀 1개");
			Assert.That(grid.GetZone(cell), Is.EqualTo(ZoneType.Industrial), "마지막 타입");
		}

		[Test]
		public void CountByType_CountsEachZoneCorrectly()
		{
			ZoneGrid grid = new();
			grid.Paint(new Vector3Int(0, 0, 0), ZoneType.Residential);
			grid.Paint(new Vector3Int(1, 0, 0), ZoneType.Residential);
			grid.Paint(new Vector3Int(2, 0, 0), ZoneType.Commercial);
			grid.Paint(new Vector3Int(3, 0, 0), ZoneType.Industrial);

			Assert.That(grid.CountByType(ZoneType.Residential), Is.EqualTo(2));
			Assert.That(grid.CountByType(ZoneType.Commercial), Is.EqualTo(1));
			Assert.That(grid.CountByType(ZoneType.Industrial), Is.EqualTo(1));
			Assert.That(grid.CountByType(ZoneType.Empty), Is.Zero, "Empty 는 키 부재 = 0");
		}

		[Test]
		public void Clear_RemovesZone()
		{
			ZoneGrid grid = new();
			Vector3Int cell = new(5, 5, 0);
			grid.Paint(cell, ZoneType.Commercial);

			grid.Clear(cell);

			Assert.That(grid.HasZone(cell), Is.False);
		}

		[Test]
		public void SaveLoad_RoundTrip_PreservesAllZones()
		{
			ZoneGrid original = new();
			original.Paint(new Vector3Int(0, 0, 0), ZoneType.Residential);
			original.Paint(new Vector3Int(1, 0, 0), ZoneType.Commercial);
			original.Paint(new Vector3Int(-2, 4, 0), ZoneType.Industrial); // 음수 좌표도

			List<KeyValuePair<Vector3Int, ZoneCellData>> saved = original.Save();

			ZoneGrid restored = new();
			restored.Load(saved);

			Assert.That(restored.ZoneData.Count, Is.EqualTo(original.ZoneData.Count));
			foreach ((Vector3Int cell, ZoneCellData data) in original.ZoneData)
			{
				Assert.That(restored.GetZone(cell), Is.EqualTo(data.Type), $"{cell} 존 유지");
			}
		}
	}
}
