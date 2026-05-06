using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// 마도서 재료 노드 — 한 ItemData 의 보유량 vs 요구량 비율을 출력 (0~1).
	/// 외부 인벤토리 시스템 ref 는 진행 매니저 (`ResearchProgressManager`, A 후속) 가 평가 시점에
	/// `NodeExecutionContext.SetGlobalInput(ITEM_KEY_PREFIX + itemId, ratio)` 로 박는다.
	/// 본 단계 (A2) 는 인터페이스만 — context 미설정 시 fallback 0.0.
	/// </summary>
	[Serializable]
	[NodeDomain(NodeDomain.MagicBook)]
	public class IngredientNode : NodeBase
	{
		/// <summary>외부 매니저가 context.SetGlobalInput 시 사용할 key 접두사.</summary>
		public const string ITEM_KEY_PREFIX = "magicbook.ingredient.";

		[SerializeField, Tooltip("재료 ItemData ID (예: 'I_0_나무'). A 후속에 ItemData ref 직접 주입으로 promote.")]
		private string itemId = string.Empty;

		[SerializeField, Tooltip("이 재료를 몇 개 모아야 100% 충족되는가."), Min(1)]
		private int requiredAmount = 1;

		private NodePort<float> outRatio;

		public string ItemId => itemId;
		public int RequiredAmount => requiredAmount;

		protected override IEnumerable<NodePort> CreatePorts()
		{
			outRatio = new NodePort<float>(this, "ratio", PortDirection.Output);
			List<NodePort> list = new() { outRatio };
			return list;
		}

		protected override void OnEvaluate(NodeExecutionContext context)
		{
			// 외부 매니저 (`ResearchProgressManager`) 가 평가 전 SetGlobalInput 으로 보유량 비율을 박는다.
			// 미설정 = fallback 0.0 (재료 미보유). FastFail X — 그래프 평가 자체는 정상 동작 보장.
			string key = ITEM_KEY_PREFIX + itemId;
			float ratio = context.TryGetGlobalInput(key, out float storedRatio) ? storedRatio : 0f;
			context.SetOutput(outRatio, Mathf.Clamp01(ratio));
		}
	}
}
