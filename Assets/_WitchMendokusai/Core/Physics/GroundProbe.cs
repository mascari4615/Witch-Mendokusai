using UnityEngine;

namespace WitchMendokusai
{
	// 공유 지면 높이 seam (TASK-WM-181 INC-1) — 배치 시스템 통합의 첫 토대 + "Y=0 평탄" root fix.
	//
	// (x,z) 위에서 아래로 raycast → 걸어다닐 수 있는 지면(GroundSurface) 의 *실제* 높이. 복셀 청크 메시가
	// MeshCollider + GroundSurface 를 달고 있어(ChunkPool) = heightmap 샘플이 아니라 **편집된 실제 복셀 지형
	// top**(지형 깎으면 따라감 — 복셀-네이티브 종착 Option 2 정합). 빌더 마커·건물·도시 큐브가 전부 이 단일
	// 법칙을 호출 → 3개로 갈라졌던 Y 계산(복셀 int / 빌더 0.01 / 도시 평면)을 하나로.
	//
	// 순수 정적 — 상태 0, Physics 질의만. 지면 없으면(빈 월드/복셀 미생성) fallbackY 폴백(평탄 무회귀 가드).
	public static class GroundProbe
	{
		// 위에서 쏘는 기본 높이 — 월드 최대 고저차(복셀 CHUNK_SIZE_Y=64) 커버. 고저차 큰 씬은 호출자가 키움.
		public const float DEFAULT_PROBE_HEIGHT = 64f;
		// 지면에서 살짝 띄움 (z-fighting 방지) — 기존 GetWorldPosition 의 0.01 계약 유지.
		public const float DEFAULT_SURFACE_OFFSET = 0.01f;
		// 지면 못 찾을 때 폴백 평면 Y (기존 동작 — 평탄 월드/지면 없는 씬 무회귀).
		public const float DEFAULT_FALLBACK_Y = 0.01f;

		// (worldX, worldZ) 의 지면 표면 Y. fromY = 셀의 현재 평면 Y(보통 grid plane) — 위로 probeHeight 만큼
		// 올라가 아래로 쏨. 가장 가까운(=위) GroundSurface.IsWalkable 채택. 없으면 fallbackY.
		public static float SampleSurfaceY(
			float worldX, float worldZ, float fromY,
			float probeHeight = DEFAULT_PROBE_HEIGHT,
			float surfaceOffset = DEFAULT_SURFACE_OFFSET,
			float fallbackY = DEFAULT_FALLBACK_Y)
		{
			Vector3 origin = new(worldX, fromY + probeHeight, worldZ);
			RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, probeHeight * 2f, ~0, QueryTriggerInteraction.Ignore);

			float bestY = fallbackY;
			float bestDistance = float.MaxValue;
			bool found = false;
			for (int i = 0; i < hits.Length; i++)
			{
				GroundSurface surface = hits[i].collider.GetComponent<GroundSurface>();
				if (surface == null || surface.IsWalkable == false)
					continue;

				if (hits[i].distance < bestDistance)
				{
					bestDistance = hits[i].distance;
					bestY = hits[i].point.y + surfaceOffset;
					found = true;
				}
			}

			return found ? bestY : fallbackY;
		}

		// worldPos 의 (x,z) 에서 지면 표면 Y 를 샘플해 그 Y 로 교체한 위치 반환. GetWorldPosition 류 1줄 교체용.
		public static Vector3 OnSurface(Vector3 worldPos, float fallbackY = DEFAULT_FALLBACK_Y)
		{
			worldPos.y = SampleSurfaceY(worldPos.x, worldPos.z, worldPos.y, DEFAULT_PROBE_HEIGHT, DEFAULT_SURFACE_OFFSET, fallbackY);
			return worldPos;
		}
	}
}
