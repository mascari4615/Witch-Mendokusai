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
	/// TASK-WM-166 Phase 2 INC-6 — <see cref="CitizenRegistry"/> 시민 명부 회귀 잠금.
	///
	/// 통근 시민 영속 데이터(집/직장/상태) 누적 + Save 스냅샷 독립성. WorldStage 통합 round-trip/replace 는
	/// WorldStageCitySaveTest 가 형제 배선과 함께 검증. new() + Assert.That.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class CitizenRegistryTest
	{
		[Test]
		public void Add_Accumulates()
		{
			CitizenRegistry registry = new();
			registry.Add(new CitizenSaveData(new Vector3Int(0, 0, 0), new Vector3Int(5, 0, 0), CitizenState.AtHome));
			registry.Add(new CitizenSaveData(new Vector3Int(1, 0, 0), new Vector3Int(5, 0, 0), CitizenState.GoingToWork));

			Assert.That(registry.Citizens.Count, Is.EqualTo(2), "시민 2명 누적");
		}

		[Test]
		public void Save_ReturnsIndependentSnapshot()
		{
			CitizenRegistry registry = new();
			registry.Add(new CitizenSaveData(new Vector3Int(0, 0, 0), new Vector3Int(5, 0, 0), CitizenState.AtWork));

			List<CitizenSaveData> saved = registry.Save();
			registry.Add(new CitizenSaveData(new Vector3Int(1, 0, 0), new Vector3Int(5, 0, 0), CitizenState.AtHome));

			Assert.That(saved.Count, Is.EqualTo(1), "Save 스냅샷은 이후 Add 에 영향 없음(복사본)");
		}
	}
}
