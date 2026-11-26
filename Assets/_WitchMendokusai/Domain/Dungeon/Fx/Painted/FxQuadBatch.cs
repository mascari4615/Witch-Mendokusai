using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 쿼드 한 장의 겉모습. 기본값은 그대로 그리기 (더하기, 안 깎임)
	public struct FxQuadLook
	{
		public Color Color;
		// 0 온전, 1 다 깎임
		public float Dissolve;
		public float Seed;
		// 0 더하기, 1 덮기
		public float Cover;
		public float Swirl;
		public float Turn;
		public float Distort;

		public static FxQuadLook Of(Color color, float cover = 0f, float dissolve = 0f, float seed = 0f) => new() { Color = color, Cover = cover, Dissolve = dissolve, Seed = seed };
	}

	// 같은 텍스처를 쓰는 이펙트 쿼드 묶음. 매 프레임 Begin, Add, Commit 으로 다시 채워 층마다 그리기 한 번
	// - 앞에 넣은 쿼드가 먼저 그려짐
	public sealed class FxQuadBatch
	{
		private static readonly int MAIN_TEX_ID = Shader.PropertyToID("_MainTex");
		private static readonly int Z_TEST_ID = Shader.PropertyToID("_ZTest");
		private static readonly int NOISE_TEX_ID = Shader.PropertyToID("_NoiseTex");
		private static readonly Rect FULL = new(0f, 0f, 1f, 1f);
		private static readonly Bounds EVERYWHERE = new(Vector3.zero, Vector3.one * 4000f);

		private readonly Mesh mesh;
		private readonly Material material;
		private readonly GameObject target;
		private readonly List<Vector3> positions = new();
		private readonly List<Color> colors = new();
		private readonly List<Vector4> uvs = new();
		private readonly List<Vector4> looks = new();
		private readonly List<int> indices = new();

		public Material Material => material;

		// onTop 이면 지형에 안 가려짐 (서 있는 판이 바닥에 잘리지 않게). order 는 스프라이트와 겹치는 차례
		public FxQuadBatch(string name, Transform root, Shader shader, Texture texture, Texture noise, int queue, bool onTop = false, int order = 0)
		{
			material = new Material(shader) { name = name, renderQueue = queue };
			material.SetTexture(MAIN_TEX_ID, texture);
			material.SetTexture(NOISE_TEX_ID, noise);
			if (onTop)
				material.SetFloat(Z_TEST_ID, (float)CompareFunction.Always);
			mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
			mesh.MarkDynamic();
			target = UltimateFxStage.CreateMeshObject(name, root, mesh, material);
			target.GetComponent<Renderer>().sortingOrder = order;
		}

		public void Begin()
		{
			positions.Clear();
			colors.Clear();
			uvs.Clear();
			looks.Clear();
			indices.Clear();
		}

		// center 를 가운데로, halfRight 와 halfUp 은 반 변
		public void Add(Vector3 center, Vector3 halfRight, Vector3 halfUp, in FxQuadLook look) => Add(center, halfRight, halfUp, FULL, look);

		public void Add(Vector3 center, Vector3 halfRight, Vector3 halfUp, Rect uv, in FxQuadLook look)
		{
			int first = positions.Count;
			positions.Add(center - halfRight - halfUp);
			positions.Add(center - halfRight + halfUp);
			positions.Add(center + halfRight + halfUp);
			positions.Add(center + halfRight - halfUp);
			uvs.Add(new Vector4(uv.xMin, uv.yMin, look.Dissolve, look.Seed));
			uvs.Add(new Vector4(uv.xMin, uv.yMax, look.Dissolve, look.Seed));
			uvs.Add(new Vector4(uv.xMax, uv.yMax, look.Dissolve, look.Seed));
			uvs.Add(new Vector4(uv.xMax, uv.yMin, look.Dissolve, look.Seed));
			Vector4 packed = new(look.Cover, look.Swirl, look.Turn, look.Distort);
			for (int i = 0; i < 4; i++)
			{
				colors.Add(look.Color);
				looks.Add(packed);
			}
			indices.Add(first);
			indices.Add(first + 1);
			indices.Add(first + 2);
			indices.Add(first);
			indices.Add(first + 2);
			indices.Add(first + 3);
		}

		// 시작과 끝을 잇는 띠. 카메라를 향해 폭을 폄. uv.x 는 시작에서 끝
		public void AddStrip(Vector3 from, Vector3 to, float width, Vector3 viewForward, Rect uv, in FxQuadLook look)
		{
			Vector3 half = (to - from) * 0.5f;
			Add(from + half, half, Side(half, viewForward) * (width * 0.5f), uv, look);
		}

		// 진행 방향에 직각이고 화면에서 위쪽을 보는 축
		public static Vector3 Side(Vector3 along, Vector3 viewForward)
		{
			Vector3 side = Vector3.Cross(viewForward, along).normalized;
			return side.y < 0f ? -side : side;
		}

		// 플립북 칸. 왼쪽 위부터
		public static Rect Cell(int index, int columns, int rows)
		{
			float width = 1f / columns;
			float height = 1f / rows;
			return new Rect(index % columns * width, 1f - (index / columns + 1) * height, width, height);
		}

		public void Commit()
		{
			mesh.Clear(true);
			target.SetActive(positions.Count > 0);
			if (positions.Count == 0)
				return;
			mesh.SetVertices(positions);
			mesh.SetColors(colors);
			mesh.SetUVs(0, uvs);
			mesh.SetUVs(1, looks);
			mesh.SetTriangles(indices, 0, false);
			mesh.bounds = EVERYWHERE;
		}

		public void Clear()
		{
			Begin();
			Commit();
		}

		public void Destroy()
		{
			Object.Destroy(mesh);
			Object.Destroy(material);
		}
	}
}
