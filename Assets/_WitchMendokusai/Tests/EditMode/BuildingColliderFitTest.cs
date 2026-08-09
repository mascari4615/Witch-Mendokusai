using NUnit.Framework;
using UnityEditor;
using UnityEngine;
// 이 시험이 만지는 건 엔진 쪽 값이다(화면 배치·충돌체·모터 문맥) — 좌표 별칭 없음 (TASK-WM-214).

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-181 — <see cref="BuildingObject.TryComputeInteractionWorldBounds"/> 데이터 전제 회귀 락.
	///
	/// 모든 Building prefab 이 비퇴화 인터랙션 bounds 를 낸다 = 콜라이더 없던 연구소·메쉬 따로인
	/// 용광로/모루/솥·루트 오프셋 마녀의집 무관하게 「일관 클릭」 박스 보장(좌클릭 제거 / 우클릭 적층 견고).
	/// 렌더러 0 = 콜라이더 미생성 = 클릭 불가 → fail. 새 건물 추가 시 렌더러 누락·퇴화 prefab 을 잡는다.
	///
	/// 라이브 ground-truth(2026-06-06): edit-mode raycast→GetComponentInParent&lt;BuildingObject&gt; resolve 11/11.
	/// </summary>
	public sealed class BuildingColliderFitTest
	{
		private const float MIN_BOUNDS_EXTENT = 0.0001f; // 비퇴화 인터랙션 bounds 판정 임계

		[Test]
		public void EveryBuildingPrefab_YieldsNonDegenerateInteractionBounds()
		{
			string[] guids = AssetDatabase.FindAssets("t:Building");
			Assert.That(guids.Length, Is.GreaterThan(0), "Building SO 가 하나도 없음 — AssetDatabase 필터 확인");

			int checkedCount = 0;
			foreach (string guid in guids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				Building building = AssetDatabase.LoadAssetAtPath<Building>(path);
				if (building == null || building.Prefab == null)
					continue;

				GameObject instance = Object.Instantiate(building.Prefab);
				try
				{
					instance.transform.position = Vector3.zero;
					instance.transform.rotation = Quaternion.identity;
					instance.transform.localScale = Vector3.one;

					bool computed = BuildingObject.TryComputeInteractionWorldBounds(instance, out Bounds worldBounds);
					Assert.That(computed, Is.True, $"{building.name} ({building.Prefab.name}): 렌더러 0 → 콜라이더 미생성 = 클릭 불가");

					Vector3 size = worldBounds.size;
					Assert.That(size.x, Is.GreaterThan(MIN_BOUNDS_EXTENT), $"{building.name}: 인터랙션 bounds.x 퇴화");
					Assert.That(size.y, Is.GreaterThan(MIN_BOUNDS_EXTENT), $"{building.name}: 인터랙션 bounds.y 퇴화");
					Assert.That(size.z, Is.GreaterThan(MIN_BOUNDS_EXTENT), $"{building.name}: 인터랙션 bounds.z 퇴화");
					checkedCount++;
				}
				finally
				{
					Object.DestroyImmediate(instance);
				}
			}

			Assert.That(checkedCount, Is.GreaterThan(0), "검증한 Building prefab 이 0개 — 전부 Prefab=null?");
		}
	}
}
