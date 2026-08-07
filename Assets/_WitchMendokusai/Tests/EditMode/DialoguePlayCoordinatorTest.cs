using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 「지금 걸까 / 줄 세울까 / 다음 걸 이어 걸까」의 회귀 잠금.
	///
	/// 이 판단은 원래 러너(MonoBehaviour) 안에 있어서 **게임을 켜야만 확인됐다**.
	/// 밖으로 빼면서 잠근 것: ① 비어 있으면 바로 건다 ② 말하는 중이면 줄을 선다
	/// ③ 끝나면 **순서대로** 이어 건다 ④ 이어 거는 사이에 「안 바쁨」 틈이 없다
	/// (그 틈에 들어온 요청이 줄을 건너뛰면 이야기 순서가 뒤집힌다) ⑤ 그만두면 기다리던 것도 접는다.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialoguePlayCoordinatorTest
	{
		private static DialogueScriptSource NewScript(string name)
		{
			DialogueScriptSource source = ScriptableObject.CreateInstance<DialogueScriptSource>();
			source.name = name;
			return source;
		}

		private static DialoguePlayRequest Request(DialogueScriptSource script) => new(script, null, null);

		[Test]
		public void IdleRequest_StartsImmediately()
		{
			DialoguePlayCoordinator coordinator = new();
			List<string> started = new();
			coordinator.OnStartRequested += request => started.Add(request.Script.name);

			Assert.That(coordinator.Request(Request(NewScript("첫째"))), Is.True);

			Assert.That(started, Is.EqualTo(new[] { "첫째" }).AsCollection);
			Assert.That(coordinator.IsBusy, Is.True);
			Assert.That(coordinator.PendingCount, Is.Zero);
		}

		[Test]
		public void RequestWhileBusy_Waits()
		{
			DialoguePlayCoordinator coordinator = new();
			List<string> started = new();
			coordinator.OnStartRequested += request => started.Add(request.Script.name);

			coordinator.Request(Request(NewScript("첫째")));
			coordinator.Request(Request(NewScript("둘째")));

			Assert.That(started.Count, Is.EqualTo(1), "말하는 중엔 새 대화가 앞을 끊지 않는다");
			Assert.That(coordinator.PendingCount, Is.EqualTo(1));
		}

		[Test]
		public void Finished_StartsNextInOrder()
		{
			DialoguePlayCoordinator coordinator = new();
			List<string> started = new();
			coordinator.OnStartRequested += request => started.Add(request.Script.name);

			coordinator.Request(Request(NewScript("첫째")));
			coordinator.Request(Request(NewScript("둘째")));
			coordinator.Request(Request(NewScript("셋째")));

			coordinator.NotifyFinished();
			coordinator.NotifyFinished();

			Assert.That(started, Is.EqualTo(new[] { "첫째", "둘째", "셋째" }).AsCollection, "이야기는 순서가 뜻이다");
			Assert.That(coordinator.IsBusy, Is.True, "셋째가 아직 말하는 중");
		}

		[Test]
		public void NoGapBetweenDialogues()
		{
			DialoguePlayCoordinator coordinator = new();
			DialoguePlayCoordinator watched = coordinator;
			bool sawIdleWhileHandingOver = false;
			coordinator.OnStartRequested += _ =>
			{
				if (watched.IsBusy == false)
				{
					sawIdleWhileHandingOver = true;
				}
			};

			coordinator.Request(Request(NewScript("첫째")));
			coordinator.Request(Request(NewScript("둘째")));
			coordinator.NotifyFinished();

			Assert.That(sawIdleWhileHandingOver, Is.False,
				"넘겨주는 사이에 「안 바쁨」 틈이 생기면 그때 들어온 요청이 줄을 건너뛴다");
		}

		[Test]
		public void StartingWithNobodyListening_FailsLoudly()
		{
			// 듣는 쪽이 없으면 이 대화는 그냥 사라진다. 그런데 바쁨 표시는 켜지므로
			// **그 뒤에 온 대화까지 전부** 줄에서 기다리다 같이 묻힌다 — 화면엔 아무 일도 안 난 것처럼 보인다.
			// 러너가 귀를 붙이는 자리가 유니티 수명주기라, 다른 컴포넌트가 먼저 대화를 걸면 실제로 이렇게 된다.
			DialoguePlayCoordinator coordinator = new();

			Assert.That(() => coordinator.Request(Request(NewScript("혼자"))), Throws.InvalidOperationException);
		}

		[Test]
		public void AfterALoudFailure_TheCoordinatorStillWorks()
		{
			// 크게 알리려던 것이 **조용한 영구 정지**가 되면 안 된다.
			// 바쁨을 켠 채로 터지면 그 뒤에 온 대화가 전부 시작도 못 한 대화 뒤에 줄을 서서 영영 안 나온다.
			DialoguePlayCoordinator coordinator = new();

			Assert.That(() => coordinator.Request(Request(NewScript("혼자"))), Throws.InvalidOperationException);
			Assert.That(coordinator.IsBusy, Is.False, "터졌으면 바쁨 표시를 되돌려야 한다");

			int started = 0;
			coordinator.OnStartRequested += _ => started++;

			Assert.That(coordinator.Request(Request(NewScript("다음"))), Is.True);
			Assert.That(started, Is.EqualTo(1), "귀가 붙은 뒤에는 정상으로 걸린다");
		}

		[Test]
		public void QueuedOneAlsoFailsLoudly_IfTheListenerLeft()
		{
			// 걸 때는 있었는데 끝날 때 없어진 경우 — 다음 것을 거는 자리도 같은 판단이라야 한다.
			DialoguePlayCoordinator coordinator = new();
			Action<DialoguePlayRequest> listener = _ => { };
			coordinator.OnStartRequested += listener;
			coordinator.Request(Request(NewScript("첫째")));
			coordinator.Request(Request(NewScript("둘째")));
			coordinator.OnStartRequested -= listener;

			Assert.That(() => coordinator.NotifyFinished(), Throws.InvalidOperationException);
		}

		[Test]
		public void FinishedWithEmptyQueue_GoesQuiet()
		{
			// 듣는 쪽을 붙여 둔다 — 아무도 안 들으면 조정자가 터진다(대화가 조용히 사라지는 걸 막는 판단).
			DialoguePlayCoordinator coordinator = new();
			coordinator.OnStartRequested += _ => { };
			coordinator.Request(Request(NewScript("혼자")));

			coordinator.NotifyFinished();

			Assert.That(coordinator.IsBusy, Is.False);
		}

		[Test]
		public void FinishedWhenNotBusy_DoesNothing()
		{
			DialoguePlayCoordinator coordinator = new();
			int startCount = 0;
			coordinator.OnStartRequested += _ => startCount++;

			coordinator.NotifyFinished();

			Assert.That(startCount, Is.Zero, "끝나지도 않은 것을 끝났다고 해도 엉뚱한 게 걸리면 안 된다");
			Assert.That(coordinator.IsBusy, Is.False);
		}

		[Test]
		public void Reset_DropsPendingAndGoesQuiet()
		{
			DialoguePlayCoordinator coordinator = new();
			coordinator.OnStartRequested += _ => { };
			coordinator.Request(Request(NewScript("첫째")));
			coordinator.Request(Request(NewScript("둘째")));

			coordinator.Reset();

			Assert.That(coordinator.IsBusy, Is.False);
			Assert.That(coordinator.PendingCount, Is.Zero, "「지금 대화 그만」이면 기다리던 것도 접는다");
		}
	}
}
