using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// 옛 대사 사슬(<see cref="DialogueLine.Choices"/>)을 그래프로 옮긴다 (TASK-WM-052).
	///
	/// ★ 왜: 대사 사슬을 트는 길이 **따로** 있었다. 코루틴이 직접 말풍선을 띄우며 걸었고,
	///   그래서 그 길로 나온 대화는 건너뛰기도, 시간 주입도, 그래프 검사도 못 받았다.
	///   같은 일을 두 군데서 다르게 하고 있으면 한쪽은 반드시 뒤처진다 — 실제로 뒤처져 있었다.
	///
	/// ★ 뜻은 그대로 옮긴다: 옛 길은 갈래가 여럿이어도 **늘 첫째만** 갔다(플레이어에게 묻지 않았다).
	///   여기서 그걸 「진짜 선택지」로 바꾸지 않는다 — 지금 고르는 화면이 없어서, 바꾸는 순간
	///   기존 대사들이 15초 멈췄다가 접힌다. 대신 <see cref="Build"/> 가 그 사실을 **알린다**
	///   (조용히 첫째만 가던 것을 눈에 보이게 만든다).
	///
	/// 사슬이 자기에게 돌아오면 거기서 끊는다 — 안 그러면 그래프를 세우다 영영 안 끝난다.
	/// </summary>
	public static class DialogueLineChainGraphBuilder
	{
		/// <param name="skippedBranchCount">
		/// 갈래가 둘 이상이라 **안 간 길**의 수. 0 이 아니면 옛 사슬이 조용히 버리던 가지가 있다는 뜻.
		/// </param>
		public static DialogueGraph Build(DialogueLine first, out int skippedBranchCount)
		{
			skippedBranchCount = 0;

			DialogueGraph graph = ScriptableObject.CreateInstance<DialogueGraph>();
			DialogueStartNode start = new();
			graph.AddNode(start);

			if (first == null)
			{
				return graph;
			}

			HashSet<DialogueLine> visited = new();
			NodeBase previous = start;
			string previousPort = DialogueStartNode.PORT_NEXT;
			DialogueLine current = first;

			while (current != null && visited.Add(current))
			{
				DialogueSpeakNode speakNode = new() { Line = current };
				graph.AddNode(speakNode);
				graph.Connect(previous.FindPort(previousPort), speakNode.FindPort(DialogueSpeakNode.PORT_IN));

				previous = speakNode;
				previousPort = DialogueSpeakNode.PORT_NEXT;

				if (current.Choices.Count > 1)
				{
					skippedBranchCount += current.Choices.Count - 1;
				}
				current = current.Choices.Count > 0 ? current.Choices[0] : null;
			}

			return graph;
		}
	}
}
