using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// 읽어들인 대본을 실제 대화 그래프로 세운다 (TASK-WM-052).
	///
	/// 세우는 규칙은 **원고를 읽는 순서 그대로**다:
	/// <list type="bullet">
	/// <item>장면 안에서는 위에서 아래로 이어진다.</item>
	/// <item>장면이 끝나면 **다음 장면으로 그냥 넘어간다** — 종이에 쓰인 대로 읽으면 그렇게 된다.
	///   중간에서 끊고 싶으면 원고에 `-> 다른장면` 을 적으면 된다.</item>
	/// <item>선택지·건너뛰기는 그 장면의 **첫 줄**로 이어진다.</item>
	/// </list>
	///
	/// 만들어지는 대사 줄은 **자산이 아니라 메모리에만** 산다 = 글만으로 대화가 돌아간다
	/// (6 동기 중 모딩·UGC 쪽 첫 발판). 에디터에서 자산으로 굽는 건 별도 작업.
	/// </summary>
	public static class DialogueScriptGraphBuilder
	{
		public static DialogueGraph Build(ParsedDialogueScript script)
		{
			DialogueGraph graph = ScriptableObject.CreateInstance<DialogueGraph>();
			if (script == null || script.Sections.Count == 0)
			{
				return graph;
			}

			DialogueStartNode start = new();
			graph.AddNode(start);

			// 1) 먼저 노드를 다 만든다 — 이어붙이려면 「그 장면의 첫 노드」를 미리 알아야 한다.
			List<List<NodeBase>> nodesBySection = new();
			Dictionary<string, int> sectionIndexByName = new();
			for (int s = 0; s < script.Sections.Count; s++)
			{
				DialogueScriptSection section = script.Sections[s];
				// 이름이 겹치면 **첫 번째**를 쓴다 — 이름으로 찾는 쪽(FindSection)이 첫 번째를 집기 때문.
				// 덮어쓰면 「검사는 A 를 보고 재생은 B 로 가는」 어긋남이 생긴다(파서가 그 겹침을 오류로 알린다).
				if (sectionIndexByName.ContainsKey(section.Name) == false)
				{
					sectionIndexByName[section.Name] = s;
				}
				nodesBySection.Add(CreateNodes(graph, section));
			}

			// 2) 이어붙인다.
			NodeBase entryNode = FindFirstNode(nodesBySection, 0);
			if (entryNode != null)
			{
				Connect(graph, start, DialogueStartNode.PORT_NEXT, entryNode);
			}

			for (int s = 0; s < script.Sections.Count; s++)
			{
				List<DialogueScriptEntry> entries = script.Sections[s].Entries;
				List<NodeBase> nodes = nodesBySection[s];

				for (int e = 0; e < entries.Count; e++)
				{
					NodeBase node = nodes[e];
					NodeBase followingInSection = e + 1 < nodes.Count ? nodes[e + 1] : FindFirstNode(nodesBySection, s + 1);
					ConnectEntry(graph, entries[e], node, followingInSection, nodesBySection, sectionIndexByName);
				}
			}

			return graph;
		}

		private static List<NodeBase> CreateNodes(DialogueGraph graph, DialogueScriptSection section)
		{
			List<NodeBase> nodes = new();
			for (int e = 0; e < section.Entries.Count; e++)
			{
				DialogueScriptEntry entry = section.Entries[e];
				NodeBase node = CreateNode(entry);
				nodes.Add(node);
				graph.AddNode(node);
			}
			return nodes;
		}

		private static NodeBase CreateNode(DialogueScriptEntry entry)
		{
			switch (entry.Kind)
			{
				case DialogueScriptEntryKind.Speak:
					return new DialogueSpeakNode { Line = DialogueLine.CreateRuntime(entry.Speaker, entry.Text, 0f, entry.StageDirection) };

				case DialogueScriptEntryKind.Choice:
					DialogueChoiceNode choiceNode = new();
					for (int i = 0; i < entry.Choices.Count; i++)
					{
						choiceNode.Options.Add(new DialogueChoiceOption(
							entry.Choices[i].Label, CreateCriteria(entry.Choices[i].Condition)));
					}
					return choiceNode;

				case DialogueScriptEntryKind.ConditionalGoto:
					return new DialogueBranchNode { Condition = CreateCriteria(entry.Condition) };

				case DialogueScriptEntryKind.Effect:
					return new DialogueEffectNode { EffectData = new List<EffectInfoData>(entry.Effects) };

				case DialogueScriptEntryKind.WaitTime:
					return new DialogueWaitNode { Kind = DialogueWaitKind.Time, Seconds = entry.Seconds };

				case DialogueScriptEntryKind.WaitEvent:
					return new DialogueWaitNode { Kind = DialogueWaitKind.Event, EventId = entry.EventId };

				default:
					// 건너뛰기는 노드가 아니라 *연결*이다. 자리를 지키려고 빈 대기(0초)를 둔다 —
					// 앞 줄이 이어질 곳이 있어야 하고, 0초 대기는 즉시 지나간다.
					return new DialogueWaitNode { Kind = DialogueWaitKind.Time, Seconds = 0f };
			}
		}

		private static void ConnectEntry(DialogueGraph graph, DialogueScriptEntry entry, NodeBase node, NodeBase following,
			List<List<NodeBase>> nodesBySection, Dictionary<string, int> sectionIndexByName)
		{
			switch (entry.Kind)
			{
				case DialogueScriptEntryKind.Speak:
					Connect(graph, node, DialogueSpeakNode.PORT_NEXT, following);
					return;

				case DialogueScriptEntryKind.WaitTime:
				case DialogueScriptEntryKind.WaitEvent:
					Connect(graph, node, DialogueWaitNode.PORT_NEXT, following);
					return;

				case DialogueScriptEntryKind.Effect:
					Connect(graph, node, DialogueEffectNode.PORT_NEXT, following);
					return;

				case DialogueScriptEntryKind.Goto:
					Connect(graph, node, DialogueWaitNode.PORT_NEXT,
						FindSectionEntry(entry.TargetSection, nodesBySection, sectionIndexByName));
					return;

				case DialogueScriptEntryKind.ConditionalGoto:
					// 맞으면 적힌 장면으로, 아니면 **바로 다음 줄로** — 원고를 위에서 아래로 읽는 감각 그대로.
					Connect(graph, node, DialogueBranchNode.PORT_TRUE,
						FindSectionEntry(entry.TargetSection, nodesBySection, sectionIndexByName));
					Connect(graph, node, DialogueBranchNode.PORT_FALSE, following);
					return;

				case DialogueScriptEntryKind.Choice:
					for (int i = 0; i < entry.Choices.Count; i++)
					{
						Connect(graph, node, DialogueChoiceNode.ChoicePortId(i),
							FindSectionEntry(entry.Choices[i].TargetSection, nodesBySection, sectionIndexByName));
					}
					return;
			}
		}

		/// <summary>그 장면의 첫 노드. 장면이 비어 있으면 그 다음 장면으로 미끄러진다.</summary>
		private static NodeBase FindSectionEntry(string sectionName, List<List<NodeBase>> nodesBySection,
			Dictionary<string, int> sectionIndexByName)
		{
			if (sectionName == null || sectionIndexByName.TryGetValue(sectionName, out int index) == false)
			{
				return null;
			}
			return FindFirstNode(nodesBySection, index);
		}

		private static NodeBase FindFirstNode(List<List<NodeBase>> nodesBySection, int startIndex)
		{
			for (int s = startIndex; s < nodesBySection.Count; s++)
			{
				if (nodesBySection[s].Count > 0)
				{
					return nodesBySection[s][0];
				}
			}
			return null;
		}

		/// <summary>이을 곳이 없으면(원고 끝·오타 난 장면 이름) 그냥 안 잇는다 = 거기서 대화가 끝난다.</summary>
		private static void Connect(DialogueGraph graph, NodeBase source, string sourcePort, NodeBase target)
		{
			if (target == null)
			{
				return;
			}
			graph.Connect(source.FindPort(sourcePort), target.FindPort(InputPortOf(target)));
		}

		/// <summary>
		/// 대본에 적힌 조건 → 실제 조건 객체. **여기서만 만든다** — 읽는 쪽(파서)은 조건을 모르는 채
		/// 「무엇이 적혔나」만 담아 오고, 조건 종류가 늘어도 파서는 안 바뀐다.
		/// </summary>
		private static Criteria CreateCriteria(DialogueScriptCondition condition)
		{
			if (condition.HasCondition == false)
			{
				return null;
			}

			if (condition.Kind == DialogueScriptConditionKind.QuestState)
			{
				return new DialogueQuestCriteria
				{
					QuestId = condition.DialogueId,
					ExpectedState = condition.QuestState,
					ExpectedMatch = condition.Expected,
				};
			}

			if (condition.Kind == DialogueScriptConditionKind.Chosen)
			{
				return new DialogueChosenCriteria
				{
					DialogueId = condition.DialogueId,
					Label = condition.Label,
					ExpectedChosen = condition.Expected,
				};
			}

			if (condition.Kind == DialogueScriptConditionKind.ItemCount)
			{
				return new DialogueItemCriteria
				{
					ItemId = condition.DialogueId,
					MinimumCount = condition.Amount,
					ExpectedHave = condition.Expected,
				};
			}

			return new DialogueSeenCriteria
			{
				DialogueId = condition.DialogueId,
				Kind = condition.Started ? DialogueSeenKind.Started : DialogueSeenKind.Completed,
				ExpectedSeen = condition.Expected,
			};
		}

		private static string InputPortOf(NodeBase node)
		{
			if (node is DialogueSpeakNode)
			{
				return DialogueSpeakNode.PORT_IN;
			}
			if (node is DialogueBranchNode)
			{
				return DialogueBranchNode.PORT_IN;
			}
			if (node is DialogueEffectNode)
			{
				return DialogueEffectNode.PORT_IN;
			}
			if (node is DialogueChoiceNode)
			{
				return DialogueChoiceNode.PORT_IN;
			}
			return DialogueWaitNode.PORT_IN;
		}
	}
}
