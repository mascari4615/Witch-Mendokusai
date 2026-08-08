using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 같은 씬에 러너가 **둘일 때** 창구가 비지 않는지.
	///
	/// ★ 무슨 일이 나나: 나중에 깨어난 러너는 스스로를 지운다. 그런데 지우면서 창구까지 비우면
	///   **이긴 쪽이 쓰던 창구가 빈다** — 그 뒤로 물건·의뢰 조건이 전부 「없다」로 넘어진다.
	///   안 터지고, 대사가 조금씩 안 나올 뿐이라 원인을 찾기가 아주 어렵다.
	///
	/// 씬에 러너를 실수로 둘 두는 일은 흔하다(프리팹 + 씬 배치).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueRunnerDuplicateTest
	{
		[Test]
		public void TheLoserDoesNotEmptyTheWinnersHistoryBridge()
		{
			// 러너는 「먼저 깬 하나」를 static 으로 들고 있다 — 앞선 시험이 남긴 게 있으면
			// 이 시험의 「이긴 쪽」이 사실은 진 쪽이 된다. 시작 전에 자리를 비운다.
			// (이 얽힘 자체가 static 하나로 관리하는 값이다 — 시험이 그걸 드러낸다.)
			while (DialogueRunner.Instance != null)
			{
				Object.DestroyImmediate(DialogueRunner.Instance);
			}

			// 붙이고 Awake 까지 손으로 돌린다 — 유니티는 EditMode 에서 안 돌려준다(DialogueTestHost 참조).
			GameObject winnerHost = new("DialogueRunnerWinner");
			DialogueRunner winner = DialogueTestHost.Attach<DialogueRunner>(winnerHost);
			Assert.That(DialogueHistoryBridge.Current, Is.SameAs(winner.History), "먼저 깬 쪽이 창구를 쥔다");

			GameObject loserHost = new("DialogueRunnerLoser");
			DialogueTestHost.Attach<DialogueRunner>(loserHost);
			Object.DestroyImmediate(loserHost);

			Assert.That(DialogueHistoryBridge.Current, Is.SameAs(winner.History),
				"진 쪽이 사라져도 이긴 쪽 창구는 그대로여야 한다");

			Object.DestroyImmediate(winnerHost);
		}
	}
}
