using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// 마도서 마법 노드 — 한 페이지 = 한 SpellNode. 입력 `ingredient` (재료 충족 비율 0~1) → 출력
	/// `readiness` (시작 가능 비율). 본 단계 (A2) 는 단일 재료 placeholder — 1:1 매핑.
	///
	/// 후속 단계:
	/// - A3: prerequisites edge (앞 SpellNode 완료 → 본 노드 언락)
	/// - A4: multi-재료 합성 노드 (MagicBookSumNode / MinNode 등)
	/// - F: 마법 효과 시스템 (`SpellEffectSO` ref) — 완성 시 trigger
	/// - D: 진척 내레이션 5단계 SerializeField (현 단계는 시스템 인터페이스만)
	///
	/// 진척 매니저 (`ResearchProgressManager`, A 후속) 가 본 노드 evaluate → readiness polling.
	/// </summary>
	[Serializable]
	[NodeDomain(NodeDomain.MagicBook)]
	public class SpellNode : NodeBase
	{
		[SerializeField, Tooltip("페이지 식별자 (예: 'spell.warmth'). 진척 매니저 / 저장 데이터 키.")]
		private string spellId = string.Empty;

		[SerializeField, Tooltip("3 카테고리 분류 — 인형 마법 / 세계 탐구 / 욘 자신 (research.md 정합).")]
		private MagicBookCategory category = MagicBookCategory.Doll;

		private NodePort<float> inIngredient;
		private NodePort<float> outReadiness;

		public string SpellId => spellId;
		public MagicBookCategory Category => category;
		public NodePort<float> ReadinessOutput
		{
			get
			{
				_ = Ports;
				return outReadiness;
			}
		}

		protected override IEnumerable<NodePort> CreatePorts()
		{
			inIngredient = new NodePort<float>(this, "ingredient", PortDirection.Input);
			outReadiness = new NodePort<float>(this, "readiness", PortDirection.Output);
			List<NodePort> list = new() { inIngredient, outReadiness };
			return list;
		}

		protected override void OnEvaluate(NodeExecutionContext context)
		{
			float ingredient = context.GetInput(inIngredient);
			context.SetOutput(outReadiness, Mathf.Clamp01(ingredient));
		}
	}
}
