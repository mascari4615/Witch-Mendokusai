using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 부서질 때 튀는 조각. 물건과 같은 색 띠의 작은 상자, 고정 수의 풀에서 돌려 씀
	public sealed class BreakableShardPool
	{
		private sealed class Shard
		{
			public Transform Transform;
			public MeshFilter Filter;
			public Vector3 Velocity;
			public Vector3 SpinAxis;
			public float Size;
			public float Age;
			public bool Active;
		}

		private static readonly float HALF_DIAGONAL = Mathf.Sqrt(3f) * .5f;

		private readonly List<Shard> shards = new();
		private readonly Transform parent;
		private int cursor;

		public BreakableShardPool(Transform parent)
		{
			this.parent = parent;
		}

		public void Burst(BreakableSettings settings, Material material, Mesh[] meshes, Vector3 center, Vector3 objectSize, Vector3 awayDirection)
		{
			int count = Random.Range(settings.ShardCount.x, settings.ShardCount.y + 1);
			float objectScale = (objectSize.x + objectSize.y + objectSize.z) / 3f;
			for (int i = 0; i < count; i++)
			{
				Shard shard = Next(settings, material);
				Vector2 circle = Random.insideUnitCircle.normalized;
				Vector3 outward = new Vector3(circle.x, 0f, circle.y) + awayDirection * .6f;
				outward.y = 0f;
				shard.Velocity = outward.normalized * Random.Range(settings.ShardSpeed.x, settings.ShardSpeed.y) + Vector3.up * settings.ShardLift * Random.Range(.6f, 1f);
				shard.SpinAxis = Random.onUnitSphere;
				shard.Size = objectScale * Random.Range(settings.ShardScale.x, settings.ShardScale.y);
				shard.Age = 0f;
				shard.Active = true;
				shard.Filter.sharedMesh = meshes[i % meshes.Length];
				shard.Transform.SetPositionAndRotation(center + Vector3.Scale(Random.insideUnitSphere, objectSize * .35f), Random.rotationUniform);
				shard.Transform.localScale = Vector3.one * shard.Size;
				shard.Transform.gameObject.SetActive(true);
			}
		}

		private Shard Next(BreakableSettings settings, Material material)
		{
			if (shards.Count < settings.MaxShards)
			{
				GameObject shardObject = new("BreakableShard");
				shardObject.transform.SetParent(parent, false);
				MeshFilter filter = shardObject.AddComponent<MeshFilter>();
				MeshRenderer renderer = shardObject.AddComponent<MeshRenderer>();
				renderer.sharedMaterial = material;
				renderer.shadowCastingMode = ShadowCastingMode.Off;
				Shard created = new() { Transform = shardObject.transform, Filter = filter };
				shards.Add(created);
				return created;
			}
			// 다 차면 가장 오래 쓴 칸부터 다시 씀
			Shard reused = shards[cursor];
			cursor = (cursor + 1) % shards.Count;
			return reused;
		}

		public void Tick(BreakableSettings settings, float deltaTime)
		{
			foreach (Shard shard in shards)
			{
				if (shard.Active == false)
					continue;
				shard.Age += deltaTime;
				if (shard.Age >= settings.ShardLife)
				{
					shard.Active = false;
					shard.Transform.gameObject.SetActive(false);
					continue;
				}
				shard.Velocity += Vector3.down * settings.ShardGravity * deltaTime;
				Vector3 position = shard.Transform.position + shard.Velocity * deltaTime;
				// 기울어진 정육면체의 가장 먼 꼭짓점까지
				float floor = settings.GroundHeight + shard.Size * HALF_DIAGONAL;
				if (position.y < floor)
				{
					position.y = floor;
					shard.Velocity = new Vector3(shard.Velocity.x * settings.ShardBounce, -shard.Velocity.y * settings.ShardBounce, shard.Velocity.z * settings.ShardBounce);
				}
				float remaining = 1f - shard.Age / settings.ShardLife;
				shard.Transform.SetPositionAndRotation(position, Quaternion.AngleAxis(settings.ShardSpin * deltaTime * remaining, shard.SpinAxis) * shard.Transform.rotation);
				// 끝 무렵 작아지며 사라짐
				shard.Transform.localScale = Vector3.one * shard.Size * Mathf.Clamp01(remaining * 3f);
			}
		}

		public void Clear()
		{
			foreach (Shard shard in shards)
			{
				shard.Active = false;
				shard.Transform.gameObject.SetActive(false);
			}
		}
	}
}
