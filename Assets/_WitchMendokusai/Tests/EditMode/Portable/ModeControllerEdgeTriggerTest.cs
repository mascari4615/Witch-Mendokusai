using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-196 단계 7 — 모드 전이 판정.
	///
	/// ★ 무엇을 지키나: 같은 모드 알림이 두 번 와도 입력 방식 교체·판 시작이 **두 번 일어나지 않는 것.**
	///   이게 깨지면 판이 두 번 시작되거나, 씬이 막 세팅한 입력을 덮어써서 조작이 먹통이 된다.
	///   둘 다 안 터지고 조용히 이상해지는 종류라 눈으로는 늦게 발견된다.
	/// </summary>
	public sealed class ModeControllerEdgeTriggerTest
	{
		[Test]
		public void FirstEntryCrosses()
		{
			ModeControllerEdgeTrigger trigger = new();

			Assert.That(trigger.IsActive, Is.False, "처음엔 아무 모드도 아니다");
			Assert.That(trigger.Crossed(true), Is.True, "처음 들어오는 것은 전이다");
			Assert.That(trigger.IsActive, Is.True);
		}

		[Test]
		public void SameStateAgain_DoesNotCross()
		{
			// 같은 모드 알림이 다시 와도 진입 절차를 또 밟으면 안 된다 — 판이 두 번 시작된다.
			ModeControllerEdgeTrigger trigger = new();
			trigger.Crossed(true);

			Assert.That(trigger.Crossed(true), Is.False);
			Assert.That(trigger.IsActive, Is.True, "안 바뀌었어도 지금 상태는 그대로 답한다");
		}

		[Test]
		public void LeavingCrosses_AndComingBackCrossesAgain()
		{
			ModeControllerEdgeTrigger trigger = new();
			trigger.Crossed(true);

			Assert.That(trigger.Crossed(false), Is.True, "나가는 것도 전이다");
			Assert.That(trigger.IsActive, Is.False);

			Assert.That(trigger.Crossed(true), Is.True, "다시 들어오면 또 전이다");
			Assert.That(trigger.IsActive, Is.True);
		}

		[Test]
		public void NeverEntered_LeavingIsNotATransition()
		{
			// 들어온 적 없는데 「나갔다」가 오면 정리 절차가 헛돈다 — 아직 없는 판을 치우려 든다.
			ModeControllerEdgeTrigger trigger = new();

			Assert.That(trigger.Crossed(false), Is.False);
			Assert.That(trigger.IsActive, Is.False);
		}
	}
}
