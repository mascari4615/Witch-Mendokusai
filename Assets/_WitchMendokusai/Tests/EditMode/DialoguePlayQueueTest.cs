using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 대화 차례의 회귀 잠금.
	///
	/// 여태 재생 중에 다른 대화가 시작되면 **앞 대화를 그냥 끊었다** — 퀘스트 보상 대사와 NPC 말이
	/// 겹치면 한 편이 통째로 사라진다. 여기서 잠그는 것: ① 들어온 순서 ② 같은 걸 또 넣지 않음
	/// ③ 꽉 차면 **새 것을 버린다**(오래된 걸 버리면 앞 이야기가 사라져 순서가 깨진다).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialoguePlayQueueTest
	{
		private static DialogueScriptSource NewScript(string name)
		{
			DialogueScriptSource source = ScriptableObject.CreateInstance<DialogueScriptSource>();
			source.name = name;
			return source;
		}

		private static DialoguePlayRequest Request(DialogueScriptSource script) => new(script, null, null);

		[Test]
		public void FirstInFirstOut()
		{
			DialoguePlayQueue queue = new();
			DialogueScriptSource first = NewScript("first");
			DialogueScriptSource second = NewScript("second");

			Assert.That(queue.Enqueue(Request(first)), Is.True);
			Assert.That(queue.Enqueue(Request(second)), Is.True);

			Assert.That(queue.TryDequeue(out DialoguePlayRequest out1), Is.True);
			Assert.That(out1.Script, Is.SameAs(first), "이야기는 순서가 뜻이다");
			Assert.That(queue.TryDequeue(out DialoguePlayRequest out2), Is.True);
			Assert.That(out2.Script, Is.SameAs(second));
			Assert.That(queue.IsEmpty, Is.True);
		}

		[Test]
		public void SameContentIsNotQueuedTwice()
		{
			DialoguePlayQueue queue = new();
			DialogueScriptSource script = NewScript("greeting");

			Assert.That(queue.Enqueue(Request(script)), Is.True);
			Assert.That(queue.Enqueue(Request(script)), Is.False, "연타로 같은 대화가 두 번 나오면 안 된다");
			Assert.That(queue.Count, Is.EqualTo(1));
		}

		[Test]
		public void EmptyRequestIsRejected()
		{
			DialoguePlayQueue queue = new();

			Assert.That(queue.Enqueue(new DialoguePlayRequest(null, null, null)), Is.False);
			Assert.That(queue.IsEmpty, Is.True);
		}

		[Test]
		public void WhenFull_TheNewestIsDropped_NotTheOldest()
		{
			DialoguePlayQueue queue = new(2);
			DialogueScriptSource first = NewScript("first");
			DialogueScriptSource second = NewScript("second");
			DialogueScriptSource third = NewScript("third");

			queue.Enqueue(Request(first));
			queue.Enqueue(Request(second));
			Assert.That(queue.Enqueue(Request(third)), Is.False);

			Assert.That(queue.TryDequeue(out DialoguePlayRequest out1), Is.True);
			Assert.That(out1.Script, Is.SameAs(first), "오래된 걸 버리면 앞 이야기가 사라져 순서가 깨진다");
		}

		[Test]
		public void ClearDropsEverything()
		{
			DialoguePlayQueue queue = new();
			queue.Enqueue(Request(NewScript("a")));
			queue.Enqueue(Request(NewScript("b")));

			queue.Clear();

			Assert.That(queue.IsEmpty, Is.True);
			Assert.That(queue.TryDequeue(out DialoguePlayRequest _), Is.False);
		}

		[Test]
		public void CapacityBelowOne_StillHoldsOne()
		{
			DialoguePlayQueue queue = new(0);

			Assert.That(queue.Enqueue(Request(NewScript("only"))), Is.True,
				"잘못 설정해도 아무것도 못 담는 줄이 되면 대화가 통째로 사라진다");
		}
	}
}
