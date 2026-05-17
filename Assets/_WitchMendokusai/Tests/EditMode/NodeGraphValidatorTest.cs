using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WitchMendokusai.NodeGraph;
// `WitchMendokusai.Tests` 에서 unqualified `NodeGraph` 는 *네임스페이스*
// `WitchMendokusai.NodeGraph` 로 바인딩(CS0118) — ChapterSO 와 동일 alias 패턴으로 타입 명시.
using NodeGraphAsset = WitchMendokusai.NodeGraph.NodeGraph;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-051 sub-A1.d — <see cref="NodeGraphValidator"/> 회귀 잠금 (결정적, Editor/PlayMode 무관).
	///
	/// A1 (PR #145) 로직 + A1.b 메뉴 + A1.c Postprocessor 가 substrate 인데, 검증기 *자체* 가
	/// 회귀하면 모든 게이트가 무음 통과 → 본 테스트가 그 바닥을 잠근다. 6 동기 「퀄리티 first-use
	/// + 회귀 0」 정합.
	///
	/// 그래프는 Test 노드(ConstantFloat / AddFloat / OutputFloat — 전부 float)로 프로그래매틱
	/// 구성. 공개 API 가 막는 케이스(dangling)는 검증기의 존재 이유 자체라 reflection 으로 .asset
	/// 손상 시나리오를 재현.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class NodeGraphValidatorTest
	{
		private static NodeGraphAsset NewGraph()
		{
			return ScriptableObject.CreateInstance<NodeGraphAsset>();
		}

		[Test]
		public void ValidGraph_HasNoErrorsOrWarnings()
		{
			NodeGraphAsset graph = NewGraph();
			ConstantFloatNode c3 = new() { Value = 3f };
			ConstantFloatNode c5 = new() { Value = 5f };
			AddFloatNode add = new();
			OutputFloatNode output = new();
			graph.AddNode(c3);
			graph.AddNode(c5);
			graph.AddNode(add);
			graph.AddNode(output);

			Assert.That(graph.Connect(c3.FindPort("out"), add.FindPort("a")), Is.True);
			Assert.That(graph.Connect(c5.FindPort("out"), add.FindPort("b")), Is.True);
			Assert.That(graph.Connect(add.FindPort("result"), output.FindPort("in")), Is.True);

			NodeGraphValidationResult result = NodeGraphValidator.Validate(graph);

			Assert.That(result.IsValid, Is.True, result.Format());
			Assert.That(result.ErrorCount, Is.Zero, result.Format());
			Assert.That(result.WarningCount, Is.Zero, result.Format());
		}

		[Test]
		public void Cycle_ReportsCycleError()
		{
			NodeGraphAsset graph = NewGraph();
			AddFloatNode add1 = new();
			AddFloatNode add2 = new();
			graph.AddNode(add1);
			graph.AddNode(add2);

			Assert.That(graph.Connect(add1.FindPort("result"), add2.FindPort("a")), Is.True);
			Assert.That(graph.Connect(add2.FindPort("result"), add1.FindPort("a")), Is.True);

			NodeGraphValidationResult result = NodeGraphValidator.Validate(graph);

			Assert.That(result.HasErrors, Is.True, result.Format());
			Assert.That(HasKind(result, NodeGraphIssueKind.Cycle), Is.True, result.Format());
		}

		[Test]
		public void UnconnectedInput_IsInfoNotError()
		{
			NodeGraphAsset graph = NewGraph();
			AddFloatNode add = new();
			graph.AddNode(add);

			NodeGraphValidationResult result = NodeGraphValidator.Validate(graph);

			Assert.That(result.IsValid, Is.True, "UnconnectedInput 은 Info — 그래프를 invalid 로 만들면 안 됨.\n" + result.Format());
			Assert.That(HasKind(result, NodeGraphIssueKind.UnconnectedInput), Is.True, result.Format());
		}

		[Test]
		public void DanglingConnection_ReportsError()
		{
			NodeGraphAsset graph = NewGraph();
			OutputFloatNode output = new();
			graph.AddNode(output);

			// 공개 API 는 미존재 노드로의 연결을 막는다 (Connect 가 port 검증). 검증기의 존재 이유 =
			// 외부 에디터가 .asset 을 직접 손상시킨 케이스 → private connections 리스트에 직접 주입해 재현.
			InjectConnection(graph, new NodeConnection("ghost-source-id", "out", output.Id, "in"));

			NodeGraphValidationResult result = NodeGraphValidator.Validate(graph);

			Assert.That(result.HasErrors, Is.True, result.Format());
			Assert.That(HasKind(result, NodeGraphIssueKind.DanglingSourceNode), Is.True, result.Format());
		}

		private static bool HasKind(NodeGraphValidationResult result, NodeGraphIssueKind kind)
		{
			for (int i = 0; i < result.Issues.Count; i++)
			{
				if (result.Issues[i].Kind == kind)
				{
					return true;
				}
			}
			return false;
		}

		private static void InjectConnection(NodeGraphAsset graph, NodeConnection connection)
		{
			FieldInfo field = typeof(NodeGraphAsset).GetField(
				"connections",
				BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(field, Is.Not.Null, "NodeGraph.connections 필드 이름이 바뀜 — 테스트 주입 경로 회귀.");
			List<NodeConnection> list = (List<NodeConnection>)field.GetValue(graph);
			list.Add(connection);
		}
	}
}
