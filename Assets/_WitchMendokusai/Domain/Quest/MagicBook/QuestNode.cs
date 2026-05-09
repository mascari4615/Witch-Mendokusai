using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// MagicBook 도메인의 노드 = 퀘스트 ref. ChapterSO (NodeGraph 서브타입) 안 [SerializeReference] 으로 임베디드.
	/// 위치는 <see cref="NodeBase.EditorPosition"/> 그대로 활용.
	///
	/// QuestSO 자체는 자산 (DataSO) — wrapper 가 노드 그래프 컨텍스트 (위치/dependency) 를 더해 줌.
	/// 후속 ★★★ (TASK-WM-034 framework refactor): NodeBase : ScriptableObject 되면 QuestSO 가 직접 NodeBase 됨 = wrapper 폐기 가능.
	///
	/// Pull executor 사용 X — UnlockQuest 는 QuestManager 이벤트 base. CreatePorts/OnEvaluate 빈 구현.
	/// </summary>
	[Serializable]
	public class QuestNode : NodeBase
	{
		[SerializeField] private QuestSO target;

		public QuestSO Target { get => target; set => target = value; }

		protected override IEnumerable<NodePort> CreatePorts()
		{
			yield break;
		}

		protected override void OnEvaluate(NodeExecutionContext context)
		{
		}
	}
}
