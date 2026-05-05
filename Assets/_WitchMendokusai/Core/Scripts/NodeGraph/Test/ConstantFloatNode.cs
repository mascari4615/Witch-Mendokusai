using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 검증용 dummy 노드 — 입력 X, 고정 float 값 1개 output.
	/// TASK-WM-034 단계 A 의 Pull 실행기 sanity 체크용.
	/// </summary>
	[Serializable]
	public class ConstantFloatNode : NodeBase
	{
		[SerializeField] private float value;

		public float Value
		{
			get => value;
			set => this.value = value;
		}

		private NodePort<float> outPort;

		protected override IEnumerable<NodePort> CreatePorts()
		{
			outPort = new NodePort<float>(this, "out", PortDirection.Output);
			yield return outPort;
		}

		protected override void OnEvaluate(NodeExecutionContext context)
		{
			context.SetOutput(outPort, value);
		}
	}
}
