using System;
using System.Collections.Generic;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// 지형 그래프의 *진입 노드* — 호출자 (TerrainGenerator.SampleHeight) 가 evaluate 전에
	/// `context.SetGlobalInput("worldX", ...)` / `"worldZ"` 로 값 박음. 노드 자체에 mutation 없음 →
	/// background chunk gen 다발 호출 thread-safe.
	/// </summary>
	[Serializable]
	[NodeDomain(NodeDomain.Terrain)]
	public class WorldPositionInputNode : NodeBase
	{
		public const string KEY_WORLD_X = "worldX";
		public const string KEY_WORLD_Z = "worldZ";

		private NodePort<float> outX;
		private NodePort<float> outZ;

		protected override IEnumerable<NodePort> CreatePorts()
		{
			outX = new NodePort<float>(this, "x", PortDirection.Output);
			outZ = new NodePort<float>(this, "z", PortDirection.Output);
			yield return outX;
			yield return outZ;
		}

		protected override void OnEvaluate(NodeExecutionContext context)
		{
			if (context.TryGetGlobalInput(KEY_WORLD_X, out float x))
				context.SetOutput(outX, x);
			if (context.TryGetGlobalInput(KEY_WORLD_Z, out float z))
				context.SetOutput(outZ, z);
		}
	}
}
