using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 적이 몰린 곳 고르기. 원본의 고정 격자 대신 이웃 수가 가장 많은 적을 중심으로 잡아 칸 경계에서 무리가 쪼개지지 않음
	public static class DensityTargeting
	{
		// 메인 스레드 전용 재사용 버퍼
		private static readonly List<Vector3> RangeBuffer = new();
		private static readonly List<Vector3> MonsterBuffer = new();

		// origin 에서 range 안의 점 중, clusterRadius 안에 이웃이 가장 많은 곳의 이웃 평균 위치
		// 같은 수면 origin 에 가까운 쪽. 이웃 수가 minCount 보다 적으면 실패
		public static bool TryFindDensest(IReadOnlyList<Vector3> positions, Vector3 origin, float range, float clusterRadius, int minCount, out Vector3 point, out int count)
		{
			point = Vector3.zero;
			count = 0;
			RangeBuffer.Clear();
			float rangeSquared = range * range;
			for (int i = 0; i < positions.Count; i++)
				if (HorizontalSquared(positions[i], origin) <= rangeSquared)
					RangeBuffer.Add(positions[i]);

			float radiusSquared = clusterRadius * clusterRadius;
			float bestOriginDistance = float.MaxValue;
			for (int i = 0; i < RangeBuffer.Count; i++)
			{
				Vector3 sum = Vector3.zero;
				int neighbors = 0;
				for (int j = 0; j < RangeBuffer.Count; j++)
				{
					if (HorizontalSquared(RangeBuffer[i], RangeBuffer[j]) > radiusSquared)
						continue;
					sum += RangeBuffer[j];
					neighbors++;
				}
				Vector3 center = sum / neighbors;
				float originDistance = HorizontalSquared(center, origin);
				if (neighbors < count || (neighbors == count && originDistance >= bestOriginDistance))
					continue;
				count = neighbors;
				bestOriginDistance = originDistance;
				point = center;
			}
			RangeBuffer.Clear();
			return count > 0 && count >= minCount;
		}

		public static bool TryFindDensestMonsters(Vector3 origin, float range, float clusterRadius, int minCount, out Vector3 point, out int count)
		{
			MonsterBuffer.Clear();
			List<GameObject> monsters = ObjectBufferManager.GetObjects(ObjectType.Monster);
			for (int i = 0; i < monsters.Count; i++)
			{
				if (MonsterObject.IsHiddenMonster(monsters[i]) == false)
					MonsterBuffer.Add(monsters[i].transform.position);
			}
			bool found = TryFindDensest(MonsterBuffer, origin, range, clusterRadius, minCount, out point, out count);
			MonsterBuffer.Clear();
			return found;
		}

		private static float HorizontalSquared(Vector3 left, Vector3 right)
		{
			float deltaX = left.x - right.x;
			float deltaZ = left.z - right.z;
			return deltaX * deltaX + deltaZ * deltaZ;
		}
	}
}
