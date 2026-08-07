using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 「물건을 가졌으면」 조건의 회귀 잠금.
	///
	/// 잠그는 것: ① 원고 글자가 진짜 조건이 된다 ② 개수 비교 ③ 「없으면」 뒤집기
	/// ④ **창구가 없으면 「없다」로 친다**(터지지 않고, 안 뜨는 쪽으로 넘어진다).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueItemConditionTest
	{
		private const int KEY_ITEM = 1001;

		/// <summary>가진 개수를 그냥 알려 주는 대역 — 진짜 가방 없이 조건만 본다.</summary>
		private sealed class FakeItemCounts : IDialogueItemCountSource
		{
			private readonly int amount;

			public FakeItemCounts(int amount)
			{
				this.amount = amount;
			}

			public int GetItemAmount(int itemId) => itemId == KEY_ITEM ? amount : 0;
		}

		private static DialoguePlayback PlayScript(string scriptText)
		{
			DialoguePlayback playback = new(DialogueScriptGraphBuilder.Build(DialogueScriptParser.Parse(scriptText)));
			playback.Begin();
			return playback;
		}

		[Test]
		public void WrittenItemCondition_BecomesARealCondition()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"## 문앞",
				"> ?아이템 1001 -> 열림",
				"> 욘: \"잠겨 있다.\"",
				"## 열림",
				"> 욘: \"열쇠가 있네.\""));

			Assert.That(parsed.HasIssues, Is.False);
			Assert.That(parsed.Sections[0].Entries[0].Condition.Kind, Is.EqualTo(DialogueScriptConditionKind.ItemCount));
			Assert.That(parsed.Sections[0].Entries[0].Condition.DialogueId, Is.EqualTo(KEY_ITEM));
		}

		[Test]
		public void HavingTheItem_TakesTheBranch()
		{
			string script = string.Join("\n",
				"## 문앞",
				"> ?아이템 1001 -> 열림",
				"> 욘: \"잠겨 있다.\"",
				"## 열림",
				"> 욘: \"열쇠가 있네.\"");

			DialogueItemBridge.Register(new FakeItemCounts(0));
			try
			{
				Assert.That(PlayScript(script).CurrentLine.Text, Is.EqualTo("잠겨 있다."));
			}
			finally
			{
				DialogueItemBridge.Clear(DialogueItemBridge.Current);
			}

			DialogueItemBridge.Register(new FakeItemCounts(1));
			try
			{
				Assert.That(PlayScript(script).CurrentLine.Text, Is.EqualTo("열쇠가 있네."));
			}
			finally
			{
				DialogueItemBridge.Clear(DialogueItemBridge.Current);
			}
		}

		[Test]
		public void AmountIsCompared()
		{
			DialogueItemCriteria needsThree = new() { ItemId = KEY_ITEM, MinimumCount = 3 };

			DialogueItemBridge.Register(new FakeItemCounts(2));
			try
			{
				Assert.That(needsThree.Evaluate(), Is.False);
			}
			finally
			{
				DialogueItemBridge.Clear(DialogueItemBridge.Current);
			}

			DialogueItemBridge.Register(new FakeItemCounts(3));
			try
			{
				Assert.That(needsThree.Evaluate(), Is.True, "「이만큼 이상」이다");
			}
			finally
			{
				DialogueItemBridge.Clear(DialogueItemBridge.Current);
			}
		}

		[Test]
		public void NoItemCondition_IsInverted()
		{
			// 퇴로 한 칸을 같이 둔다 — 조건부 칸만 있으면 「전부 조건부」 검사에 걸린다(그게 맞다:
			// 물건을 가진 저장에서는 이 칸이 사라져 아무것도 안 뜬다).
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"## 문앞",
				"> - 그냥 간다 [아이템없음 1001] -> 문앞",
				"> - 문을 본다 -> 문앞"));

			Assert.That(parsed.HasIssues, Is.False);
			Assert.That(parsed.Sections[0].Entries[0].Choices[0].Condition.Expected, Is.False);
		}

		[Test]
		public void WithoutTheBridge_CountsAsNotHaving()
		{
			DialogueItemBridge.Clear(DialogueItemBridge.Current);
			DialogueItemCriteria criteria = new() { ItemId = KEY_ITEM, MinimumCount = 1 };

			Assert.That(criteria.Evaluate(), Is.False,
				"가방이 아직 없을 때 「있다」고 우기면, 없는 물건을 쓰는 대사가 나온다 — 안 뜨는 쪽으로 넘어진다");
		}

		[Test]
		public void SeenConditionWithThreeWords_IsStillRejected()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"## 시작",
				"> ?봤음 10 3 -> 시작"));

			Assert.That(parsed.Issues.Count, Is.EqualTo(1),
				"이력 조건엔 개수가 없다 — 셋을 적었으면 오타로 보고 알린다");
		}
	}
}
