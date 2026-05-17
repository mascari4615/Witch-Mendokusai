using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.NodeGraph
{
	/// <summary>
	/// TASK-WM-051 sub-A1.c — 그래프 자산 저장/임포트 시 <see cref="NodeGraphValidator"/> 자동 호출.
	///
	/// 디자이너가 외부 에디터 / 인스펙터로 .asset 을 직접 건드려 망가뜨린 케이스(검증기가 잡으려고
	/// 만들어진 바로 그 시나리오)를 *저장 즉시* Console 로 surface. 비차단 (저장 자체는 막지 않음 —
	/// 작업 흐름 보존, 다만 무음 손실 X).
	///
	/// CI 게이트 = <see cref="ValidateAllForCI"/> — batchmode `-executeMethod` 진입점. 그래프에
	/// Error 가 하나라도 있으면 exit 1 (Unity CI Compile Gate, TASK-WM-047 와 동근 — 검증 자동화
	/// = 6 동기 「퀄리티 9.5/10 ceiling」 first-use).
	/// </summary>
	public sealed class NodeGraphValidationPostprocessor : AssetPostprocessor
	{
		/// <summary>true 동안 검증 스킵 — 검증기 자체 테스트나 대량 마이그레이션 중 잡음 억제용.</summary>
		public static bool Suppress { get; set; }

		private static void OnPostprocessAllAssets(
			string[] importedAssets,
			string[] deletedAssets,
			string[] movedAssets,
			string[] movedFromAssetPaths)
		{
			if (Suppress)
			{
				return;
			}

			for (int i = 0; i < importedAssets.Length; i++)
			{
				string path = importedAssets[i];
				if (path.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase) == false)
				{
					continue;
				}
				NodeGraph graph = AssetDatabase.LoadAssetAtPath<NodeGraph>(path);
				if (graph == null)
				{
					continue;
				}

				NodeGraphValidationResult result = NodeGraphValidator.Validate(graph);
				if (result.HasErrors || result.HasWarnings)
				{
					NodeGraphValidationMenu.LogResult(graph, result);
				}
			}
		}

		/// <summary>
		/// CI 진입점 — `unity -batchmode -quit -executeMethod WitchMendokusai.NodeGraph.NodeGraphValidationPostprocessor.ValidateAllForCI`.
		/// 모든 NodeGraph 자산 검사, Error 발견 시 exit 1 (배치모드) — 그래프 무결성 회귀를 CI 에서 차단.
		/// </summary>
		public static void ValidateAllForCI()
		{
			IReadOnlyList<NodeGraph> graphs = NodeGraphValidationMenu.LoadAllGraphs();
			int totalErrors = 0;
			int totalWarnings = 0;
			for (int i = 0; i < graphs.Count; i++)
			{
				NodeGraphValidationResult result = NodeGraphValidator.Validate(graphs[i]);
				totalErrors += result.ErrorCount;
				totalWarnings += result.WarningCount;
				if (result.HasErrors || result.HasWarnings)
				{
					NodeGraphValidationMenu.LogResult(graphs[i], result);
				}
			}

			string summary = $"[NodeGraphValidation][CI] graphs={graphs.Count} errors={totalErrors} warnings={totalWarnings}";
			if (totalErrors > 0)
			{
				Debug.LogError(summary);
				if (Application.isBatchMode)
				{
					EditorApplication.Exit(1);
				}
				return;
			}

			Debug.Log(summary);
			if (Application.isBatchMode)
			{
				EditorApplication.Exit(0);
			}
		}
	}
}
