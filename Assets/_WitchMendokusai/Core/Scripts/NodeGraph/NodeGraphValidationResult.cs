using System.Collections.Generic;

namespace WitchMendokusai.NodeGraph
{
	public enum NodeGraphIssueSeverity
	{
		Info = 0,
		Warning = 1,
		Error = 2,
	}

	public enum NodeGraphIssueKind
	{
		NullNodeEntry = 0,
		DanglingSourceNode = 1,
		DanglingTargetNode = 2,
		MissingSourcePort = 3,
		MissingTargetPort = 4,
		PortDirectionMismatch = 5,
		PortTypeMismatch = 6,
		Cycle = 7,
		DuplicateTargetConnection = 8,
		UnconnectedInput = 9,
	}

	public sealed class NodeGraphIssue
	{
		public NodeGraphIssueKind Kind { get; }
		public NodeGraphIssueSeverity Severity { get; }
		public string Message { get; }
		public string NodeId { get; }
		public string PortId { get; }
		public IReadOnlyList<string> CycleNodeIds { get; }

		public NodeGraphIssue(
			NodeGraphIssueKind kind,
			NodeGraphIssueSeverity severity,
			string message,
			string nodeId = null,
			string portId = null,
			IReadOnlyList<string> cycleNodeIds = null)
		{
			Kind = kind;
			Severity = severity;
			Message = message;
			NodeId = nodeId;
			PortId = portId;
			CycleNodeIds = cycleNodeIds;
		}
	}

	/// <summary>
	/// 정적 그래프 검사 결과. <see cref="NodeGraphValidator.Validate"/> 가 반환.
	/// 사용자는 <see cref="HasErrors"/> / <see cref="HasWarnings"/> 로 1차 분기, <see cref="Issues"/> 로 상세 열람.
	/// </summary>
	public sealed class NodeGraphValidationResult
	{
		private readonly List<NodeGraphIssue> issues;

		public IReadOnlyList<NodeGraphIssue> Issues => issues;
		public int ErrorCount { get; private set; }
		public int WarningCount { get; private set; }
		public int InfoCount { get; private set; }

		public bool HasErrors => ErrorCount > 0;
		public bool HasWarnings => WarningCount > 0;
		public bool IsValid => ErrorCount == 0;

		public NodeGraphValidationResult()
		{
			issues = new List<NodeGraphIssue>();
		}

		public void Add(NodeGraphIssue issue)
		{
			if (issue == null)
				return;
			issues.Add(issue);
			switch (issue.Severity)
			{
				case NodeGraphIssueSeverity.Error:
					ErrorCount++;
					break;
				case NodeGraphIssueSeverity.Warning:
					WarningCount++;
					break;
				case NodeGraphIssueSeverity.Info:
					InfoCount++;
					break;
			}
		}

		/// <summary>모든 이슈를 한 줄씩 직렬화 — Debug.Log / Editor Console 출력용.</summary>
		public string Format()
		{
			System.Text.StringBuilder builder = new();
			builder.Append("[NodeGraphValidation] errors=").Append(ErrorCount)
				.Append(" warnings=").Append(WarningCount)
				.Append(" info=").Append(InfoCount).Append('\n');
			for (int i = 0; i < issues.Count; i++)
			{
				NodeGraphIssue issue = issues[i];
				builder.Append("  [").Append(issue.Severity).Append("] ")
					.Append(issue.Kind).Append(" — ").Append(issue.Message).Append('\n');
			}
			return builder.ToString();
		}
	}
}
