using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 스폰 자리 공급자 (레이). 몸 높이가 아니라 바닥, 여러 층이면 기준에 맞는 층, 덮인 바닥은 버림
	public sealed class RaycastSpawnSiteSourceTest
	{
		// 장면의 다른 물체와 안 겹치게 멀리서
		private static readonly Vector3 ORIGIN = new(5000f, 0f, 5000f);

		private readonly List<GameObject> objects = new();
		private RaycastSpawnSiteSettings settings;
		private RaycastSpawnSiteSource source;

		[SetUp]
		public void SetUp()
		{
			settings = new RaycastSpawnSiteSettings();
			source = new RaycastSpawnSiteSource(settings);
		}

		[TearDown]
		public void TearDown()
		{
			foreach (GameObject item in objects)
				Object.DestroyImmediate(item);
			objects.Clear();
		}

		// 윗면 높이 topHeight 인 판. 두께가 크면 언덕 덩어리
		private GameObject Slab(float topHeight, float size = 20f, float thickness = 1f, float tiltDegrees = 0f)
		{
			GameObject slab = new("Slab");
			objects.Add(slab);
			slab.transform.SetPositionAndRotation(ORIGIN + Vector3.up * (topHeight - thickness * 0.5f), Quaternion.Euler(tiltDegrees, 0f, 0f));
			slab.AddComponent<BoxCollider>().size = new Vector3(size, thickness, size);
			Physics.SyncTransforms();
			return slab;
		}

		private bool Project(float referenceHeight, out SpawnSite site, SpawnSiteBand band = SpawnSiteBand.NearReference, SpawnLayer layer = SpawnLayer.Ground)
		{
			SpawnSiteQuery query = new()
			{
				Candidate = ORIGIN,
				ReferenceHeight = referenceHeight,
				Band = band,
				Layer = layer,
			};
			return source.TryProject(query, out site);
		}

		[Test]
		public void Jumping_player_anchors_to_floor_below_not_bridge_above()
		{
			Slab(0f);
			Slab(5f, 4f, 0.5f);
			// 다리 밑에서 3m 뛰어오른 몸. 다리 (2m 위) 가 바닥 (3m 아래) 보다 가깝지만 발밑은 바닥
			Assert.IsTrue(Project(3f, out SpawnSite floor, SpawnSiteBand.AtOrBelowReference));
			Assert.AreEqual(0f, floor.Ground.y, 0.001f);
		}

		[Test]
		public void Hill_on_ground_gives_hill_top_not_buried_ground()
		{
			Slab(0f);
			Slab(4f, 6f, 4f);
			Assert.IsTrue(Project(0f, out SpawnSite site));
			Assert.AreEqual(4f, site.Ground.y, 0.001f);
		}

		[Test]
		public void Bridge_and_floor_pick_the_one_near_the_reference()
		{
			Slab(0f);
			Slab(5f, 4f, 0.5f);
			Assert.IsTrue(Project(0f, out SpawnSite lower));
			Assert.AreEqual(0f, lower.Ground.y, 0.001f);
			Assert.IsTrue(Project(5f, out SpawnSite upper));
			Assert.AreEqual(5f, upper.Ground.y, 0.001f);
		}

		[Test]
		public void Low_gap_under_a_slab_is_not_standable()
		{
			Slab(0f);
			// 바닥 위 1m 높이에 떠 있는 판. 밑 틈이 몸 높이보다 낮음
			Slab(1.5f, 6f, 0.5f);
			Assert.IsTrue(Project(0f, out SpawnSite site));
			Assert.AreEqual(1.5f, site.Ground.y, 0.001f);
		}

		[Test]
		public void Gentle_slope_is_standable()
		{
			// 같은 충돌체는 머리 위 검사에서 빠져서 제 경사면에 안 걸림
			Slab(0f, 6f, 0.05f, 30f);
			Assert.IsTrue(Project(0f, out SpawnSite _));
		}

		[Test]
		public void Steep_slope_is_not_standable()
		{
			// 얇은 판. 두꺼운 상자를 크게 기울이면 옆면이 누워서 바닥이 됨
			Slab(0f, 4f, 0.05f, 70f);
			Assert.IsFalse(Project(0f, out SpawnSite _));
		}

		[Test]
		public void No_floor_fails()
		{
			Assert.IsFalse(Project(0f, out SpawnSite _));
		}

		[Test]
		public void Trigger_is_not_ground()
		{
			Slab(0f).GetComponent<BoxCollider>().isTrigger = true;
			Assert.IsFalse(Project(0f, out SpawnSite _));
		}

		[Test]
		public void Air_layer_floats_above_ground()
		{
			Slab(0f);
			Assert.IsTrue(Project(0f, out SpawnSite site, layer: SpawnLayer.Air));
			Assert.AreEqual(0f, site.Ground.y, 0.001f);
			Assert.AreEqual(settings.AirHeight, site.Position.y, 0.001f);
		}
	}
}
