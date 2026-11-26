using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 길이 0 법선은 URP Lit 에서 NaN 픽셀이 되어 블룸이 번짐 (바구니 열매 조각 사고)
	public class BreakableMeshNormalTest
	{
		private const int SEEDS = 8;
		private const int ROWS = 6;

		[Test]
		public void Shard_normals_are_unit_length([Values] BreakableShape shape, [NUnit.Framework.Range(0, BreakableShardShapes.SHARDS_PER_VARIANT - 1)] int shard)
		{
			for (int seed = 0; seed < SEEDS; seed++)
			{
				BreakableMeshBuilder builder = NewBuilder(seed, out int[] rows);
				BreakableShardShapes.Build(builder, shape, rows, shard);
				AssertUnit(builder);
			}
		}

		[Test]
		public void Body_normals_are_unit_length([Values] BreakableShape shape)
		{
			for (int seed = 0; seed < SEEDS; seed++)
			{
				BreakableMeshBuilder builder = NewBuilder(seed, out int[] rows);
				BreakableShapes.Build(builder, shape, Vector3.one * .6f, rows);
				AssertUnit(builder);
			}
		}

		[Test]
		public void Quad_collapsed_to_a_point_on_one_side_faces_outward()
		{
			BreakableMeshBuilder builder = NewBuilder(0, out int[] rows);
			builder.AddQuad(Vector3.zero, Vector3.zero, new Vector3(1f, 1f, 0f), new Vector3(0f, 1f, 0f), rows[0]);
			Assert.That(Vector3.Dot(builder.Normals[0], Vector3.Cross(new Vector3(1f, 1f, 0f), Vector3.up).normalized), Is.EqualTo(1f).Within(.0001f));
		}

		[Test]
		public void Quad_collapsed_at_the_far_side_faces_outward()
		{
			BreakableMeshBuilder builder = NewBuilder(0, out int[] rows);
			builder.AddQuad(Vector3.zero, Vector3.right, Vector3.up, Vector3.up, rows[0]);
			Assert.That(Vector3.Dot(builder.Normals[0], Vector3.forward), Is.EqualTo(1f).Within(.0001f));
		}

		[Test]
		public void Zero_area_face_still_has_a_unit_normal()
		{
			BreakableMeshBuilder builder = NewBuilder(0, out int[] rows);
			builder.AddBox(Vector3.zero, new Vector3(1f, 0f, 1f), Quaternion.identity, rows[0]);
			builder.AddTriangle(Vector3.zero, Vector3.zero, Vector3.zero, rows[0]);
			AssertUnit(builder);
		}

		[Test]
		public void Shipped_bodies_and_top_props_have_unit_normals([Values] BreakableTopProp.Kind propKind)
		{
			BreakableSettings settings = Resources.Load<BreakableSettings>(nameof(BreakableSettings));
			foreach (BreakableKind kind in settings.Kinds)
			{
				BreakableMeshBuilder builder = NewBuilder(1, out int[] rows);
				builder.SetMatrix(Matrix4x4.Rotate(Quaternion.Euler(settings.TiltDegrees, 0f, -settings.TiltDegrees)));
				BreakableShapes.Build(builder, kind.Shape, kind.Size, rows);
				BreakableTopProp.Add(builder, settings, propKind, Vector3.up * kind.Size.y, 30f, kind.TopPropScale, rows[0], rows[1]);
				AssertUnit(builder);
			}
		}

		[Test]
		public void Tiny_face_keeps_its_normal()
		{
			BreakableMeshBuilder builder = NewBuilder(0, out int[] rows);
			builder.AddBox(Vector3.zero, Vector3.one * .002f, Quaternion.identity, rows[0]);
			AssertUnit(builder);
		}

		private static BreakableMeshBuilder NewBuilder(int seed, out int[] rows)
		{
			BreakableAtlas atlas = new(ROWS, 4);
			rows = new int[ROWS];
			for (int i = 0; i < ROWS; i++)
				rows[i] = atlas.Add(Color.white);
			return new BreakableMeshBuilder(atlas, new System.Random(seed), 1f);
		}

		private static void AssertUnit(BreakableMeshBuilder builder)
		{
			Assert.That(builder.Normals.Count, Is.GreaterThan(0));
			for (int i = 0; i < builder.Normals.Count; i++)
				Assert.That(builder.Normals[i].magnitude, Is.EqualTo(1f).Within(.001f), $"정점 {i}");
		}
	}
}
