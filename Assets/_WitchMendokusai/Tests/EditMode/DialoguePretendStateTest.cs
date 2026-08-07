using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 「~했다 치고」 상태의 회귀 잠금.
	///
	/// ★ 왜 시험이 붙는가: 이 판단은 원래 미리보기 창 **안에** 있었고, 그 창은
	///   하네스도 안 물고 단일 파일 검사기도 못 본다 — 아무 검사도 안 받는 자리였다.
	///   원고 쓰는 사람이 **무슨 가지를 보게 되는지**를 정하는 로직이 거기 있었는데도 그랬다.
	///
	/// 손으로 적는 칸이라 오타·빈칸·끝쉼표가 흔하다 — 거기서 터지면 도구를 아무도 안 쓴다.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialoguePretendStateTest
	{
		[Test]
		public void CommaSeparatedIdsAreRead()
		{
			Assert.That(DialoguePretendState.ParseIds("1, 2,3"), Is.EqualTo(new[] { 1, 2, 3 }));
		}

		[Test]
		public void JunkIsDroppedInsteadOfThrowing()
		{
			// 빈칸·끝쉼표·오타는 흔하다. 여기서 터지면 그 칸을 아무도 안 쓴다.
			Assert.That(DialoguePretendState.ParseIds("5, , 어이쿠, 7,"), Is.EqualTo(new[] { 5, 7 }));
			Assert.That(DialoguePretendState.ParseIds(null).Count, Is.EqualTo(0));
			Assert.That(DialoguePretendState.ParseIds("   ").Count, Is.EqualTo(0));
		}

		[Test]
		public void SeenIdsBecomeCompletedHistory()
		{
			DialoguePretendState pretend = DialoguePretendState.From("5200", "", "");

			Assert.That(pretend.History.HasSeen(5200, DialogueSeenKind.Completed), Is.True);
			Assert.That(pretend.History.HasSeen(5201, DialogueSeenKind.Completed), Is.False);
		}

		[Test]
		public void OwnedItemsAnswerGenerously()
		{
			// 개수까지 흉내내면 손잡이가 복잡해진다 — 가졌다고 하면 넉넉히 가진 것으로 친다.
			DialoguePretendState pretend = DialoguePretendState.From("", "1001", "");

			Assert.That(pretend.Items.GetItemAmount(1001), Is.EqualTo(DialoguePretendState.PRETEND_ITEM_AMOUNT));
			Assert.That(pretend.Items.GetItemAmount(1002), Is.EqualTo(0));
		}

		[Test]
		public void UnlistedQuestsAreLocked_NotUnknown()
		{
			// 「모른다」로 답하면 조건 쪽이 등록 안 된 것으로 착각한다 — 여기서는 늘 답한다.
			DialoguePretendState pretend = DialoguePretendState.From("", "", "5000");

			Assert.That(pretend.Quests.TryGetQuestState(5000, out QuestState done), Is.True);
			Assert.That(done, Is.EqualTo(QuestState.Completed));

			Assert.That(pretend.Quests.TryGetQuestState(5001, out QuestState other), Is.True);
			Assert.That(other, Is.EqualTo(QuestState.Locked));
		}

		[Test]
		public void RegisteredPretendStateActuallyDrivesConditions()
		{
			// 창구에 끼워야 조건이 그걸 본다 — 만들어만 두고 안 끼우면 아무 가지도 안 바뀐다.
			DialoguePretendState pretend = DialoguePretendState.From("5200", "1001", "5000");
			pretend.Register();
			try
			{
				Assert.That(new DialogueSeenCriteria { DialogueId = 5200 }.Evaluate(), Is.True);
				Assert.That(new DialogueItemCriteria { ItemId = 1001, MinimumCount = 3 }.Evaluate(), Is.True);
				Assert.That(new DialogueQuestCriteria { QuestId = 5000, ExpectedState = QuestState.Completed }.Evaluate(),
					Is.True);
			}
			finally
			{
				pretend.Unregister();
			}

			Assert.That(new DialogueSeenCriteria { DialogueId = 5200 }.Evaluate(), Is.False,
				"뺀 뒤에는 원래대로 — 덜 진행된 쪽으로 넘어진다");
		}
	}
}
