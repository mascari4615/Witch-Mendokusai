using UnityEngine;
using WitchMendokusai.NodeGraph;
// `WitchMendokusai.NodeGraph` 가 namespace 와 class 동명 — ambiguity 회피용 alias.
using NodeGraphAsset = WitchMendokusai.NodeGraph.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// 지형 도메인 노드 그래프 — 진입 `WorldPositionInputNode` (worldX/worldZ globalInput), 출구 `HeightOutputNode`.
	/// `SampleHeight(x, z)` 가 evaluate per-call. 새 NodeExecutionContext 매번 생성 → background 다발 호출 thread-safe.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(TerrainGraph), menuName = "WM/Terrain/" + nameof(TerrainGraph))]
	public class TerrainGraph : NodeGraphAsset
	{
		public override NodeDomain Domain => NodeDomain.Terrain;

		// TASK-WM-119: SampleHeight 콜당 new NodeExecutionContext (256/청크 × 백그라운드 다발)
		// → thread-static 풀. [ThreadStatic] = 스레드별 인스턴스라 background chunk gen 다발
		// 호출 thread-safe (per-thread 격리). Reset() 으로 재사용 — 결과·평가순서 불변.
		[System.ThreadStatic] private static NodeExecutionContext tlsContext;

		/// <summary>
		/// (worldX, worldZ) 기준 그래프 평가 결과 height. terminal `HeightOutputNode` 누락 시 0.
		/// **background thread 안전** — context per-call 인스턴스, NodeBase 데이터 read-only.
		/// </summary>
		public float SampleHeight(float worldX, float worldZ)
		{
			HeightOutputNode output = FindNode<HeightOutputNode>();
			if (output == null)
			{
				Debug.LogWarning($"[TerrainGraph] HeightOutputNode 누락 ({name}). 0 반환.");
				return 0f;
			}

			NodeExecutionContext context = tlsContext;
			if (context == null || context.Graph != this)
			{
				context = new NodeExecutionContext(this);
				tlsContext = context;
			}
			else
			{
				context.Reset();
			}
			context.SetGlobalInput(WorldPositionInputNode.KEY_WORLD_X, worldX);
			context.SetGlobalInput(WorldPositionInputNode.KEY_WORLD_Z, worldZ);
			context.Evaluate(output);
			// terminal 인스턴스 mutation 없음 — context cache 에서 cached 값 읽음 (per-thread 격리).
			return context.GetInput(output.HeightInput);
		}
	}
}
