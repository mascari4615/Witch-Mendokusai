using System.Linq;
using UnityEngine;

namespace WitchMendokusai.NodeGraph
{
	/// <summary>
	/// 노드 별 64×64 grayscale heightmap 미리보기 — float-output 노드만 (terrain domain primary).
	/// 노드 별 N×N 좌표 evaluate → output 값을 min/max 정규화 → grayscale Texture2D.
	///
	/// 비싸지만 (4k eval/node) Editor preview 라 1회 캐시 충분 — graph 변경 시 NodeGraphView 가 invalidate.
	/// </summary>
	public static class NodePreview
	{
		public const int PREVIEW_SIZE = 64;
		// 미리보기 sample 영역 — 월드 ±32m 가 1 청크 정도 보임. Perlin frequency 0.01 기준 작은 변화 visible.
		public const float PREVIEW_WORLD_RANGE = 64f;

		// 키 컨벤션 — terrain 도메인 (WorldPositionInputNode.KEY_WORLD_X 와 일치). 다른 도메인은 다른 키 →
		// 도메인 별 preview 분리 필요. 1차는 terrain 가정. 향후 attribute / registry 방식.
		private const string KEY_WORLD_X = "worldX";
		private const string KEY_WORLD_Z = "worldZ";

		/// <summary>node 의 첫 float-output port 결과를 PREVIEW_SIZE 격자로 evaluate → grayscale Texture2D.
		/// 출력 포트 없거나 float 타입 아니면 null. graph 도 null 이면 null.</summary>
		public static Texture2D RenderFloatOutputPreview(NodeBase node, NodeGraph graph)
		{
			if (node == null || graph == null)
				return null;
			NodePort floatOutput = node.OutputPorts.FirstOrDefault(p => p.DataType == typeof(float));
			if (floatOutput == null)
				return null;
			if (floatOutput is not NodePort<float> typedOutput)
				return null;

			float[] values = new float[PREVIEW_SIZE * PREVIEW_SIZE];
			float minVal = float.MaxValue;
			float maxVal = float.MinValue;

			for (int z = 0; z < PREVIEW_SIZE; z++)
			{
				for (int x = 0; x < PREVIEW_SIZE; x++)
				{
					float worldX = (x / (float)(PREVIEW_SIZE - 1) - 0.5f) * PREVIEW_WORLD_RANGE;
					float worldZ = (z / (float)(PREVIEW_SIZE - 1) - 0.5f) * PREVIEW_WORLD_RANGE;

					NodeExecutionContext ctx = new(graph);
					ctx.SetGlobalInput(KEY_WORLD_X, worldX);
					ctx.SetGlobalInput(KEY_WORLD_Z, worldZ);
					ctx.Evaluate(node);
					float v = ctx.GetOutput(typedOutput);

					values[z * PREVIEW_SIZE + x] = v;
					if (v < minVal) minVal = v;
					if (v > maxVal) maxVal = v;
				}
			}

			float range = maxVal - minVal;
			if (range < 0.0001f)
				range = 1f;

			Texture2D tex = new(PREVIEW_SIZE, PREVIEW_SIZE, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
				name = $"NodePreview_{node.Id}",
				hideFlags = HideFlags.HideAndDontSave,
			};

			Color[] pixels = new Color[PREVIEW_SIZE * PREVIEW_SIZE];
			for (int i = 0; i < pixels.Length; i++)
			{
				float t = (values[i] - minVal) / range;
				pixels[i] = new Color(t, t, t, 1f);
			}
			tex.SetPixels(pixels);
			tex.Apply();
			return tex;
		}
	}
}
