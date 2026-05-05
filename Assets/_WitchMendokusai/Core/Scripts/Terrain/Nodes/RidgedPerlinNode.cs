using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// Ridged multifractal Perlin — 표준 Perlin (-1~1) 을 `1 - |perlin|` 로 변환 (peak at perlin=0) 후 제곱 → 산봉우리 ridge.
	/// 옥타브 누적으로 multifractal 효과. 부드러운 FractalPerlin 과 달리 *날카로운 산맥* 모양.
	///
	/// 출력 0~amplitude (이미 양수 — ridge = 봉우리). 정점 ridge, 골짜기 0.
	/// </summary>
	[Serializable]
	public class RidgedPerlinNode : NodeBase
	{
		[SerializeField, Range(1, 8)] private int octaves = 4;
		[SerializeField] private float frequency = 0.01f;
		[SerializeField] private float amplitude = 32f;
		[SerializeField, Range(0f, 1f)] private float persistence = 0.5f;
		[SerializeField] private float lacunarity = 2f;
		[SerializeField] private int seed = 0;

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
				float perlin = Mathf.PerlinNoise(sampleX, sampleZ) * 2f - 1f; // -1 ~ 1
				float ridged = 1f - Mathf.Abs(perlin);                         // 0 ~ 1, peak at perlin=0
				ridged *= ridged;                                              // 강조 — sharp ridge

				total += ridged * curAmplitude;
				maxValue += curAmplitude;

				curAmplitude *= persistence;
				curFrequency *= lacunarity;
			}

			float normalized = maxValue > 0f ? total / maxValue : 0f;
			context.SetOutput(outHeight, normalized * amplitude);
		}
	}
}
