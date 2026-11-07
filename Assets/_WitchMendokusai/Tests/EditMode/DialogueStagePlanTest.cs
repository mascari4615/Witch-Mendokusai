using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	// 상태 누적과 줄별 변경분
	public sealed class DialogueStagePlanTest
	{
		private static DialogueStageCue Cue(string text)
		{
			Assert.That(DialogueStageCueText.TryRead(text, out DialogueStageCue cue, out string problem), Is.True, problem);
			return cue;
		}

		private static DialogueStagePlan PlanWith(params string[] cues)
		{
			DialogueStagePlan plan = new();
			foreach (string text in cues)
			{
				plan.Queue(new[] { Cue(text) });
			}
			return plan;
		}

		[Test]
		public void DefaultPresentation_IsWorld_AndNoAutoEnter()
		{
			DialogueStagePlan plan = new();
			DialogueStageDelta delta = plan.FlushForLine("욘");

			Assert.That(delta.Presentation, Is.EqualTo(DialogueStagePresentation.World));
			Assert.That(delta.Entered.Count, Is.EqualTo(0));
		}

		[Test]
		public void SpeakerInNovel_AutoEntersAtFreeSlot()
		{
			DialogueStagePlan plan = PlanWith("방식 미연시", "등장 링 가운데");
			DialogueStageDelta delta = plan.FlushForLine("욘");

			Assert.That(delta.Entered.Count, Is.EqualTo(2));
			Assert.That(delta.Entered[1].Actor, Is.EqualTo("욘"));
			Assert.That(delta.Entered[1].Slot, Is.EqualTo(DialogueStageSlot.Left));
			Assert.That(delta.Highlighted, Is.EqualTo("욘"));
		}

		[Test]
		public void SecondFlush_ReportsOnlyWhatChanged()
		{
			DialogueStagePlan plan = PlanWith("방식 미연시", "등장 링 왼쪽");
			plan.FlushForLine("링");

			plan.Queue(new[] { Cue("이동 링 오른쪽") });
			DialogueStageDelta delta = plan.FlushForLine("링");

			Assert.That(delta.PresentationChanged, Is.False);
			Assert.That(delta.SpeakerChanged, Is.False);
			Assert.That(delta.Entered.Count, Is.EqualTo(0));
			Assert.That(delta.Moved.Count, Is.EqualTo(1));
			Assert.That(delta.Actors[0].Slot, Is.EqualTo(DialogueStageSlot.Right));
		}

		[Test]
		public void EnterThenExitOnSameLine_LeavesNoChange()
		{
			DialogueStagePlan plan = PlanWith("등장 링 왼쪽", "퇴장 링");
			DialogueStageDelta delta = plan.Flush();

			Assert.That(delta.Entered.Count, Is.EqualTo(0));
			Assert.That(delta.Exited.Count, Is.EqualTo(0));
			Assert.That(delta.Actors.Count, Is.EqualTo(0));
		}

		[Test]
		public void ExitAll_RemovesEveryActor()
		{
			DialogueStagePlan plan = PlanWith("등장 링 왼쪽", "등장 욘 오른쪽");
			plan.Flush();

			plan.Queue(new[] { Cue("퇴장 모두 페이드") });
			DialogueStageDelta delta = plan.Flush();

			Assert.That(delta.Exited.Count, Is.EqualTo(2));
			Assert.That(delta.Exited[0].Entrance, Is.EqualTo(DialogueStageEntrance.Fade));
			Assert.That(plan.Actors.Count, Is.EqualTo(0));
		}

		[Test]
		public void HighlightOverride_WinsUntilTurnedOff()
		{
			DialogueStagePlan plan = PlanWith("방식 미연시", "강조 링");
			Assert.That(plan.FlushForLine("욘").Highlighted, Is.EqualTo("링"));

			plan.Queue(new[] { Cue("강조 끔") });
			Assert.That(plan.FlushForLine("욘").Highlighted, Is.EqualTo("욘"));
		}

		[Test]
		public void NarrationClearsSpeaker()
		{
			DialogueStagePlan plan = PlanWith("방식 미연시");
			plan.FlushForLine("욘");
			DialogueStageDelta delta = plan.FlushForLine(null);

			Assert.That(delta.Speaker, Is.Null);
			Assert.That(delta.SpeakerChanged, Is.True);
		}

		[Test]
		public void ShotStaysUntilShotEnd()
		{
			DialogueStagePlan plan = PlanWith("샷 플레이어 클로즈");
			DialogueStageDelta first = plan.Flush();
			Assert.That(first.ShotChanged, Is.True);
			Assert.That(first.ShotTarget, Is.EqualTo("플레이어"));
			Assert.That(first.Shot, Is.EqualTo(DialogueStageShot.Close));

			DialogueStageDelta second = plan.Flush();
			Assert.That(second.ShotChanged, Is.False);
			Assert.That(second.ShotTarget, Is.EqualTo("플레이어"));

			plan.Queue(new[] { Cue("샷 끝") });
			DialogueStageDelta third = plan.Flush();
			Assert.That(third.ShotChanged, Is.True);
			Assert.That(third.ShotTarget, Is.Null);
		}

		[Test]
		public void OneShots_DoNotRepeatOnNextFlush()
		{
			DialogueStagePlan plan = PlanWith("들썩임 링", "아이콘 링 놀람", "이펙트 반짝 링");
			DialogueStageDelta first = plan.Flush();
			Assert.That(first.Motions.Count, Is.EqualTo(1));
			Assert.That(first.Emotes.Count, Is.EqualTo(1));
			Assert.That(first.Effects.Count, Is.EqualTo(1));

			DialogueStageDelta second = plan.Flush();
			Assert.That(second.Motions.Count, Is.EqualTo(0));
			Assert.That(second.Emotes.Count, Is.EqualTo(0));
			Assert.That(second.Effects.Count, Is.EqualTo(0));
		}

		[Test]
		public void CinemaChangeIsReportedOnce()
		{
			DialogueStagePlan plan = PlanWith("띠 켬");
			Assert.That(plan.Flush().CinemaChanged, Is.True);
			plan.Queue(new[] { Cue("띠 켬") });
			DialogueStageDelta same = plan.Flush();
			Assert.That(same.CinemaChanged, Is.False);
			Assert.That(same.Cinema, Is.True);
		}
	}
}
