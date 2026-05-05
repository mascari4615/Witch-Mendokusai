using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 그래프 안 한 연결 — output port 에서 input port 로. 안정 식별은 (Owner.Id, PortId) 페어.
	/// 포트 인스턴스 직접 참조 X — 인스턴스는 일시적, ID 만 영속.
	/// </summary>
	[Serializable]
	public class NodeConnection
	{
		[SerializeField] private string sourceNodeId;
		[SerializeField] private string sourcePortId;
		[SerializeField] private string targetNodeId;
		[SerializeField] private string targetPortId;

		public string SourceNodeId => sourceNodeId;
		public string SourcePortId => sourcePortId;
		public string TargetNodeId => targetNodeId;
		public string TargetPortId => targetPortId;

		public NodeConnection(string sourceNodeId, string sourcePortId, string targetNodeId, string targetPortId)
		{
			this.sourceNodeId = sourceNodeId;
			this.sourcePortId = sourcePortId;
			this.targetNodeId = targetNodeId;
			this.targetPortId = targetPortId;
		}
	}
}
