using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// `TerrainGenerator.SampleHeight` 의 multi-octave Perlin 식을 *그래프 노드* 로 재현.
	/// 입력 worldX/worldZ + 필드 frequency/amplitude/octaves/persistence/lacunarity/seedOffset.
	/// 출력 height (`±amplitude` 범위, 정규화 후 amplitude 곱).
	///
	/// 1차 minimal — 파라미터는 *필드* (인스펙터에서 직접 편집). 향후 input port 화 시 `Constant` 노드 연결 가능.
	/// </summary>
	[Serializable]
	[NodeDomain(NodeDomain.Terrain)]
	public class FractalPerlinNode : NodeBase
	{
		[SerializeField, Range(1, 8)] private int octaves = 4;
		[SerializeField] private float frequency = 0.01f;
		[SerializeField] private float amplitude = 32f;
		[SerializeField, Range(0f, 1f)] private float persistence = 0.5f;
		[SerializeField] private float lacunarity = 2f;
		[SerializeField] private int seed = 0;

		// Perlin 이 음수 좌표에서 미러링되는 것을 피하기 위한 큰 양수 오프셋 — TerrainGenerator 와 동일.
		private const float COORD_OFFSET = 100000f;

		private NodePort<float> inX;
		private NodePort<float> inZ;
		private NodePort<float> outHeight;

		public int Octaves { get => octaves; set => octaves = Mathf.Clamp(value, 1, 8); }
		public float Frequency { get => frequency; set => frequency = Mathf.Max(0.0001f, value); }
		public float Amplitude { get => amplitude; set => amplitude = value; }
		public float Persistence { get => persistence; set => persistence = Mathf.Clamp01(value); }
		public float Lacunarity { get => lacunarity; set => lacunarity = Mathf.Max(1f, value); }
		public int Seed { get => seed; set => seed = value; }

		protected override IEnumerable<NodePort> CreatePorts()
		{
			inX = new NodePort<float>(this, "x", PortDirection.Input);
			inZ = new NodePort<float>(this, "z", PortDirection.Input);
			outHeight = new NodePort<float>(this, "height", PortDirection.Output);
			yield return inX;
			yield return inZ;
			yield return outHeight;
		}

		protected override void OnEvaluate(NodeExecutionContext context)
		{
			float x = context.GetInput(inX);
			float z = context.GetInput(inZ);

			float total = 0f;
			float maxValue = 0f;
			float curAmplitude = 1f;
			float curFrequency = frequency;

			float seedOffsetX = (seed * 0.7341f) % 10000f;
			float seedOffsetZ = (seed * 1.2917f) % 10000f;

			for (int i = 0; i < octaves; i++)
			{
				float sampleX = (x + COORD_OFFSET + seedOffsetX) * curFrequency;
				float sampleZ = (z + COORD_OFFSET + seedOffsetZ) * curFrequency;
				float perlin = Mathf.PerlinNoise(sampleX, sampleZ) * 2f - 1f;

				total += perlin * curAmplitude;
				maxValue += curAmplitude;

				curAmplitude *= persistence;
				curFrequency *= lacunarity;
			}

			float normalized = maxValue > 0f ? total / maxValue : 0f;
			context.SetOutput(outHeight, normalized * amplitude);
		}
	}
}
