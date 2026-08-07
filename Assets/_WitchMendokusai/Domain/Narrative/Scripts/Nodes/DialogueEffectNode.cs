using System;
using System.Collections.Generic;
using WitchMendokusai.NodeGraph;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 대화가 뭔가를 *일으키는* 노드 (TASK-WM-052). 입력 `in` + 출력 `next` — Speak 동형.
	///
	/// ★ 왜 필요한가: 여태 대화는 상태를 **읽기만** 했다(분기·선택지 조건). 물건을 주거나 퀘스트를
	///   열어 주는 쪽이 없어서, 「대화의 결과」를 항상 대화 *바깥* 어딘가에 또 적어야 했다 —
	///   그러면 「어느 대사에서 무엇이 일어나는가」가 두 곳으로 갈라진다.
	///
	/// 효과 표현은 게임이 이미 쓰는 <see cref="EffectInfo"/> 를 그대로 쓴다(퀘스트 보상·카드·아이템·
	/// 변수 전부 이미 그 목록으로 표현된다). 새 효과 체계를 만들지 않는다.
	///
	/// 적용은 traversal 이 아니라 <see cref="DialoguePlayback"/> 이 한다 — traversal 은 *읽기만* 하는
	/// 순수 순회여야 되감기·미리보기가 안전하다(같은 그래프를 두 번 걸어도 물건이 두 번 안 생긴다).
	/// 소비자(UI)에게는 이 노드가 안 보인다 — 분기와 같은 결.
	/// </summary>
	[Serializable]
	public class DialogueEffectNode : NodeBase
	{
		public const string PORT_IN = "in";
		public const string PORT_NEXT = "next";

		[SerializeField] private List<EffectInfo> effects = new();

		[SerializeField]
		[Tooltip("글로 적은 효과 — 자산 대신 번호로 가리킨다. 대본에서 세워진 노드가 이쪽을 쓴다.")]
		private List<EffectInfoData> effectData = new();

		public List<EffectInfo> Effects { get => effects; set => effects = value ?? new(); }
		public List<EffectInfoData> EffectData { get => effectData; set => effectData = value ?? new(); }

		protected override IEnumerable<NodePort> CreatePorts()
		{
			yield return new NodePort<FlowSignal>(this, PORT_IN, PortDirection.Input);
			yield return new NodePort<FlowSignal>(this, PORT_NEXT, PortDirection.Output);
		}

		protected override void OnEvaluate(NodeExecutionContext context)
		{
		}
	}
}
