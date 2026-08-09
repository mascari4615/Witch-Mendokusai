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
	/// TASK-WM-176 Phase 3 INC-3 — <see cref="PowerSourceRegistry"/> 발전소 명부 회귀 잠금.
	///
	/// 셀→PowerSourceData 멱등 Add/Has/Remove + Save 스냅샷. WorldStage 통합 round-trip/replace 는
	/// WorldStageCitySaveTest 가 형제 배선과 함께 검증. new() + Assert.That.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class PowerSourceRegistryTest
	{
		[Test]
		public void Add_ThenHas()
		{
			PowerSourceRegistry registry = new();
			Vector3Int cell = new(2, 3, 0);

			Assert.That(registry.Has(cell), Is.False, "추가 전 없음");
			registry.Add(cell, range: 5);

			Assert.That(registry.Has(cell), Is.True);
			Assert.That(registry.Sources[cell].Range, Is.EqualTo(5));
		}

		[Test]
		public void Remove_ThenNotHas()
		{
			PowerSourceRegistry registry = new();
			Vector3Int cell = new(0, 0, 0);
			registry.Add(cell, 3);

			registry.Remove(cell);

			Assert.That(registry.Has(cell), Is.False);
		}

		[Test]
		public void Add_Idempotent_Overwrites()
		{
			PowerSourceRegistry registry = new();
			Vector3Int cell = new(1, 1, 0);

			registry.Add(cell, 3);
			registry.Add(cell, 7); // 멱등 덮어쓰기

			Assert.That(registry.Sources.Count, Is.EqualTo(1), "중복 셀 = 1개");
			Assert.That(registry.Sources[cell].Range, Is.EqualTo(7), "range 덮어씀");
		}

		[Test]
		public void Save_ReturnsIndependentSnapshot()
		{
			PowerSourceRegistry registry = new();
			registry.Add(new Vector3Int(0, 0, 0), 4);

			List<KeyValuePair<Vector3Int, PowerSourceData>> saved = registry.Save();
			registry.Add(new Vector3Int(1, 0, 0), 4);

			Assert.That(saved.Count, Is.EqualTo(1), "Save 스냅샷은 이후 Add 무영향");
		}
	}
}
