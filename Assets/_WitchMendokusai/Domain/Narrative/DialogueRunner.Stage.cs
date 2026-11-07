namespace WitchMendokusai
{
	// DialogueRunner 의 무대 신호 부분
	public partial class DialogueRunner
	{
		private DialogueStagePlan stagePlan = new();

		private static IDialogueStageHost StageHost => DialogueStageBridge.Current;

		private void BeginStage(DialoguePlayback starting)
		{
			stagePlan = new DialogueStagePlan();
			starting.OnStageCues += stagePlan.Queue;
			StageHost?.Begin(this);
		}

		private void FinishStage() => StageHost?.Finish();

		private void ApplyStageLine(DialogueLine line)
		{
			DialogueStageDelta delta = stagePlan.FlushForLine(line?.ResolveSpeakerName());
			bool isBubbleLine = StageHost == null || delta.Presentation == DialogueStagePresentation.World;
			if (playback != null)
			{
				playback.ManualSpeakAdvance = isBubbleLine == false;
			}
			StageHost?.ApplyLine(delta, line?.ResolveSpeakerName(), line?.Text);
			if (isBubbleLine)
			{
				ShowBubble(line);
			}
		}

		private void ApplyPendingStageCues()
		{
			if (stagePlan.HasPending)
			{
				DialogueStageDelta delta = stagePlan.Flush();
				StageHost?.ApplyCues(delta);
			}
		}
	}
}
