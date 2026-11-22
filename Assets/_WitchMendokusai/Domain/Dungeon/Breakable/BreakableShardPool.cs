using UnityEngine;

namespace WitchMendokusai
{
	// 부서질 때 튀는 조각. 물건과 같은 색 띠의 작은 상자를 바닥 잔해 (RemainsWorld.Debris) 로 띄움. 남고 밀리고 사라지는 것은 잔해 쪽
	public static class BreakableShardPool
	{
		// 기울어진 정육면체의 가장 먼 꼭짓점까지 (크기 1 기준)
		private static readonly float HALF_DIAGONAL = Mathf.Sqrt(3f) * .5f;
		// 면으로 누운 정육면체의 중심 높이 (크기 1 기준)
		private const float HALF_SIDE = .5f;

		public static void Burst(RemainsWorld remains, BreakableSettings settings, Material material, Mesh[] meshes, Vector3 center, Vector3 objectSize, Vector3 awayDirection)
		{
			int count = Random.Range(settings.ShardCount.x, settings.ShardCount.y + 1);
			float objectScale = (objectSize.x + objectSize.y + objectSize.z) / 3f;
			for (int i = 0; i < count; i++)
			{
				Vector2 circle = Random.insideUnitCircle.normalized;
				Vector3 outward = new Vector3(circle.x, 0f, circle.y) + awayDirection * .6f;
				outward.y = 0f;
				DebrisSpawn spawn = new()
				{
					Mesh = meshes[i % meshes.Length],
					Material = material,
					Position = center + Vector3.Scale(Random.insideUnitSphere, objectSize * .35f),
					Rotation = Random.rotationUniform,
					Velocity = outward.normalized * Random.Range(settings.ShardSpeed.x, settings.ShardSpeed.y) + Vector3.up * settings.ShardLift * Random.Range(.6f, 1f),
					SpinAxis = Random.onUnitSphere,
					SpinDegrees = settings.ShardSpin,
					Size = objectScale * Random.Range(settings.ShardScale.x, settings.ShardScale.y),
					FlyClearance = HALF_DIAGONAL,
					RestClearance = HALF_SIDE,
					Gravity = settings.ShardGravity,
					Bounce = settings.ShardBounce,
					GroundHeight = settings.GroundHeight,
				};
				remains.Debris.Spawn(remains.Settings, spawn);
			}
		}
	}
}
