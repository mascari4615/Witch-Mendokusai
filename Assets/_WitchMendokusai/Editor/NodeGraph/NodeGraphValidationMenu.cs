using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.NodeGraph
{
	/// <summary>
	/// TASK-WM-051 sub-A1.b — <see cref="NodeGraphValidator"/> 를 디자이너 손에 쥐여주는 Editor 진입점.
	///
	/// A1 (PR #145) 이 정적 검사 *로직* 만 박았고, 호출 위치가 없어 dead infra 상태였음.
	/// 본 메뉴 + sub-A1.c Postprocessor 가 그 substrate 를 살린다.
	///
	/// <list type="bullet">
	/// <item><b>Validate Selected</b> — 프로젝트 창에서 선택한 <see cref="NodeGraph"/>(서브클래스 포함) 검사.
	/// 선택이 NodeGraph 아니면 메뉴 비활성.</item>
	/// <item><b>Validate All</b> — 프로젝트 전 NodeGraph 자산 일괄 검사 + 집계 한 줄.</item>
	/// </list>
	///
	/// 이슈 보고 = <see cref="NodeGraphValidationResult.Format"/> 한 줄 직렬화 + severity 매핑
	/// (Error→LogError / Warning→LogWarning / 그 외→Log). 로그 context = 그래프 자산이라
	/// Console 더블클릭 시 자산 ping (노드는 `[SerializeReference]` 내부 객체라 개별 ping 불가).
	/// </summary>
	public static class NodeGraphValidationMenu
	{
		private const string MENU_VALIDATE_SELECTED = "WM/NodeGraph/Validate Selected";
		private const string MENU_VALIDATE_ALL = "WM/NodeGraph/Validate All";

		[MenuItem(MENU_VALIDATE_SELECTED, false, 200)]
		public static void ValidateSelected()
		{
			NodeGraph graph = Selection.activeObject as NodeGraph;
			if (graph == null)
			{
				Debug.LogWarning("[NodeGraphValidation] 선택된 NodeGraph 자산이 없음. 프로젝트 창에서 그래프를 선택하세요.");
				return;
			}
			LogResult(graph, NodeGraphValidator.Validate(graph));
		}

		[MenuItem(MENU_VALIDATE_SELECTED, true)]
		public static bool ValidateSelected_Enabled()
		{
			return Selection.activeObject is NodeGraph;
		}

		[MenuItem(MENU_VALIDATE_ALL, false, 201)]
		public static void ValidateAll()
		{
			IReadOnlyList<NodeGraph> graphs = LoadAllGraphs();
			if (graphs.Count == 0)
			{
				Debug.Log("[NodeGraphValidation] 프로젝트에 NodeGraph 자산 없음.");
				return;
			}

			int totalErrors = 0;
			int totalWarnings = 0;
			int invalidGraphs = 0;
			for (int i = 0; i < graphs.Count; i++)
			{
				NodeGraph graph = graphs[i];
				NodeGraphValidationResult result = NodeGraphValidator.Validate(graph);
				totalErrors += result.ErrorCount;
				totalWarnings += result.WarningCount;
				if (result.IsValid == false)
				{
					invalidGraphs++;
				}
				if (result.HasErrors || result.HasWarnings)
				{
					LogResult(graph, result);
				}
			}

			string summary = $"[NodeGraphValidation] 전체 검사 완료 — graphs={graphs.Count} invalid={invalidGraphs} " +
				$"errors={totalErrors} warnings={totalWarnings}";
			if (totalErrors > 0)
			{
				Debug.LogError(summary);
			}
			else if (totalWarnings > 0)
			{
				Debug.LogWarning(summary);
			}
			else
			{
				Debug.Log(summary);
			}
		}

		/// <summary>프로젝트 내 모든 <see cref="NodeGraph"/> (서브클래스 포함) 자산 로드.</summary>
		internal static IReadOnlyList<NodeGraph> LoadAllGraphs()
		{
			string[] guids = AssetDatabase.FindAssets($"t:{nameof(NodeGraph)}");
			List<NodeGraph> graphs = new(guids.Length);
			for (int i = 0; i < guids.Length; i++)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[i]);
				NodeGraph graph = AssetDatabase.LoadAssetAtPath<NodeGraph>(path);
				if (graph != null)
				{
					graphs.Add(graph);
				}
			}
			return graphs;
		}

		/// <summary>severity 최대치에 맞춰 Console 한 줄 + 자산 context (더블클릭 ping).</summary>
		internal static void LogResult(NodeGraph graph, NodeGraphValidationResult result)
		{
			string graphName = graph == null ? "<null>" : graph.name;
			string message = $"[NodeGraphValidation] '{graphName}'\n{result.Format()}";
			if (result.HasErrors)
			{
				Debug.LogError(message, graph);
			}
			else if (result.HasWarnings)
			{
				Debug.LogWarning(message, graph);
			}
			else
			{
				Debug.Log(message, graph);
			}
		}
	}
}
