using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	// dialogue-scene: 대화 중 `@연출 <이름>` 3D 장면을 켜고 2D 선택지로 돌아오는 시험
	public class DialogueSceneSampleCommand : IDevCommand
	{
		public string Name => "dialogue-scene";
		public string Usage => "dialogue-scene list | dialogue-scene <연출 이름> | dialogue-scene stop";

		private const string LIST_ARG = "list";
		private const string STOP_ARG = "stop";

		// 3D 위 대사 두 줄, 연출 끝, 2D 선택지. 이름 칸 {0}
		private const string SCRIPT_TEMPLATE =
			"### 3D 연출 시험\n" +
			"> @방식 자막\n" +
			"> @연출 {0}\n" +
			"> 링: \"3D 연출 위로 대사가 흘러.\"\n" +
			"> 욘: \"응.\"\n" +
			"> @연출 끝\n" +
			"> 링: \"2D 로 돌아왔어. 어떻게 할까?\"\n" +
			"> - 끝낸다 -> 3D 연출 시험 끝\n" +
			"\n" +
			"### 3D 연출 시험 끝\n" +
			"> 욘: \"끝.\"\n";

		public void Execute(DevCommandContext context, string[] args)
		{
			IDialogueSceneStage sceneStage = DialogueSceneStageBridge.Current;
			if (sceneStage == null)
			{
				context.LogError("3D 연출 어댑터가 등록되지 않았다 (DialogueSequenceStage)");
				return;
			}

			if (args.Length == 0 || args[0] == LIST_ARG)
			{
				IReadOnlyList<string> keys = sceneStage.SceneKeys;
				context.LogInfo(keys.Count == 0 ? "보관함이 비었다. 장면 편집기에서 '게임 보관함에 내보내기'" : "연출 이름: " + string.Join(", ", keys));
				return;
			}

			if (DialogueRunner.TryGetExistingInstance(out DialogueRunner runner) == false)
			{
				context.LogError("이 씬에 DialogueRunner 없음");
				return;
			}

			if (args[0] == STOP_ARG)
			{
				runner.Stop();
				context.LogSuccess("3D 연출 시험 멈춤");
				return;
			}

			string key = string.Join(" ", args);
			if (sceneStage.SceneKeys.Contains(key) == false)
			{
				context.LogError($"보관함에 없는 연출: \"{key}\". dialogue-scene list 로 이름 확인");
				return;
			}

			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Format(SCRIPT_TEMPLATE, key));
			if (parsed.HasIssues)
			{
				context.LogError("시험 원고 오류: " + string.Join(" / ", parsed.Issues.Select(issue => $"{issue.LineNumber}줄 {issue.Message}")));
				return;
			}
			runner.Play(DialogueScriptGraphBuilder.Build(parsed));
			context.LogSuccess($"3D 연출 시험 재생: {key}");
		}

		public IEnumerable<string> Suggest(string[] partial)
		{
			if (partial.Length != 1)
				return Enumerable.Empty<string>();

			List<string> words = new() { LIST_ARG, STOP_ARG };
			if (DialogueSceneStageBridge.Current != null)
				words.AddRange(DialogueSceneStageBridge.Current.SceneKeys);
			return words;
		}
	}
}
