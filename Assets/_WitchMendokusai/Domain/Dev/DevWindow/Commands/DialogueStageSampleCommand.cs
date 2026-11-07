using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	// dialogue-stage: 대화 연출 시험 대본 재생
	public class DialogueStageSampleCommand : IDevCommand
	{
		public string Name => "dialogue-stage";
		public string Usage => "dialogue-stage [stop]";

		private const string STOP_ARG = "stop";

		public void Execute(DevCommandContext context, string[] args)
		{
			if (DialogueRunner.TryGetExistingInstance(out DialogueRunner runner) == false)
			{
				context.LogError("이 씬에 DialogueRunner 없음");
				return;
			}

			if (args.Length > 0 && args[0] == STOP_ARG)
			{
				runner.Stop();
				context.LogSuccess("대화 연출 시험 멈춤");
				return;
			}

			DialogueStageSettings settings = DialogueStageSettings.Load();
			runner.Play(settings.SampleScript);
			context.LogSuccess($"대화 연출 시험 재생: {settings.SampleScript.name}");
		}

		public IEnumerable<string> Suggest(string[] partial) =>
			partial.Length == 1 ? new[] { STOP_ARG } : Enumerable.Empty<string>();
	}
}
