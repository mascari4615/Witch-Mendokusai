using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	// dialogue-world: 플레이어와 가장 가까운 NPC (또는 이름으로 고른 NPC) 사이에서 `@무대 월드` 대화 시험
	public class DialogueWorldSampleCommand : IDevCommand
	{
		public string Name => "dialogue-world";
		public string Usage => "dialogue-world [NPC 이름] | dialogue-world stop";

		private const string STOP_ARG = "stop";

		// 플레이어는 대화 연출 설정에서 월드 플레이어로 표시된 인물 (욘). NPC 이름 칸 {0}
		private const string SCRIPT_TEMPLATE =
			"### 월드 대화 시험\n" +
			"> @방식 미연시\n" +
			"> @무대 월드\n" +
			"> @등장 욘 왼쪽\n" +
			"> @등장 {0} 오른쪽\n" +
			"> 욘: \"플레이하던 자리에서 바로 대화로 넘어가.\"\n" +
			"> {0}: \"말하는 사람을 카메라가 따라와.\"\n" +
			"> @끄덕임 욘\n" +
			"> @동작 욘 마법\n" +
			"> 욘: \"몸짓도 그 자리에서.\"\n" +
			"> @흔들림 {0}\n" +
			"> {0}: \"놀라는 것도.\"\n" +
			"> @무대 2D\n" +
			"> 욘: \"끝나면 원래 화면으로.\"\n";

		public void Execute(DevCommandContext context, string[] args)
		{
			if (DialogueRunner.TryGetExistingInstance(out DialogueRunner runner) == false)
			{
				context.LogError("이 씬에 DialogueRunner 없음. 월드 씬에서 실행");
				return;
			}
			if (args.Length > 0 && args[0] == STOP_ARG)
			{
				runner.Stop();
				context.LogSuccess("월드 대화 시험 멈춤");
				return;
			}

			NPCObject npc = args.Length > 0 ? FindByName(string.Join(" ", args)) : FindNearest();
			if (npc == null)
			{
				context.LogError("NPC 를 찾지 못했다. 있는 이름: " + string.Join(", ", NpcNames()));
				return;
			}
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Format(SCRIPT_TEMPLATE, npc.Data.Name));
			if (parsed.HasIssues)
			{
				context.LogError("시험 원고 오류: " + string.Join(" / ", parsed.Issues.Select(issue => $"{issue.LineNumber}줄 {issue.Message}")));
				return;
			}
			runner.Play(DialogueScriptGraphBuilder.Build(parsed));
			context.LogSuccess($"월드 대화 시험: 욘 과 {npc.Data.Name}");
		}

		private static NPCObject FindNearest()
		{
			Vector3 origin = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
			NPCObject nearest = null;
			float best = float.MaxValue;
			foreach (NPCObject npc in Object.FindObjectsByType<NPCObject>())
			{
				if (npc.Data == null)
					continue;
				float distance = (npc.transform.position - origin).sqrMagnitude;
				if (distance < best)
				{
					best = distance;
					nearest = npc;
				}
			}
			return nearest;
		}

		private static NPCObject FindByName(string npcName) =>
			Object.FindObjectsByType<NPCObject>().FirstOrDefault(npc => npc.Data != null && npc.Data.Name == npcName);

		private static IEnumerable<string> NpcNames() =>
			Object.FindObjectsByType<NPCObject>().Where(npc => npc.Data != null).Select(npc => npc.Data.Name);

		public IEnumerable<string> Suggest(string[] partial) =>
			partial.Length == 1 ? NpcNames().Append(STOP_ARG) : Enumerable.Empty<string>();
	}
}
