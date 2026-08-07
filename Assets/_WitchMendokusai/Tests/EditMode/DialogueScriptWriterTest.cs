using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 원고 되돌려 쓰기의 회귀 잠금. 핵심은 **왕복**이다:
	/// 읽고 → 다시 쓰고 → 또 읽었을 때 뜻이 같아야 한다. 같지 않으면 읽기와 쓰기 중 하나가 틀린 것이고,
	/// 읽기 규칙만 있으면 그 어긋남을 **아무도 못 본다**.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueScriptWriterTest
	{
		/// <summary>읽고 → 쓰고 → 또 읽어서 같은 뜻인지 본다.</summary>
		private static void AssertRoundTrip(string scriptText)
		{
			ParsedDialogueScript first = DialogueScriptParser.Parse(scriptText);
			ParsedDialogueScript second = DialogueScriptParser.Parse(DialogueScriptWriter.Write(first));

			Assert.That(second.HasIssues, Is.False, "되돌려 쓴 글이 다시 안 읽히면 쓰기가 틀린 것이다");
			Assert.That(second.Sections.Count, Is.EqualTo(first.Sections.Count), "장면 수");

			for (int s = 0; s < first.Sections.Count; s++)
			{
				Assert.That(second.Sections[s].Name, Is.EqualTo(first.Sections[s].Name), "장면 이름");
				Assert.That(second.Sections[s].Entries.Count, Is.EqualTo(first.Sections[s].Entries.Count),
					$"장면 \"{first.Sections[s].Name}\" 의 마디 수");

				for (int e = 0; e < first.Sections[s].Entries.Count; e++)
				{
					DialogueScriptEntry before = first.Sections[s].Entries[e];
					DialogueScriptEntry after = second.Sections[s].Entries[e];
					Assert.That(after.Kind, Is.EqualTo(before.Kind), "마디 종류");
					Assert.That(after.Speaker, Is.EqualTo(before.Speaker), "말하는 이");
					Assert.That(after.Text, Is.EqualTo(before.Text), "대사");
					Assert.That(after.StageDirection, Is.EqualTo(before.StageDirection), "지문");
				}
			}
		}

		[Test]
		public void SpeakAndStageDirection_RoundTrip()
		{
			AssertRoundTrip(string.Join("\n",
				"### 장면 3 — 알리사 등장",
				"> 알리사: \"주인님, 아침입니다.\"",
				"> 욘: (이불 속) \"...\"",
				"> 욘: (오래 바라본다)",
				"> \"우리는 진짜야?\""));
		}

		[Test]
		public void ChoicesAndJumps_RoundTrip()
		{
			AssertRoundTrip(string.Join("\n",
				"## 물어보기",
				"> 링: \"무슨 일 있었어?\"",
				"> - 응, 좀. -> 사정설명",
				"> - 그 얘기 다시 [봤음 4615] -> 사정설명",
				// 선택지 아래에 갈 곳을 두면 그 줄은 절대 안 나온다(선택지가 흐름을 가로챈다).
				// 왕복이 관심사니 조건 없는 갈 곳은 살리되, 실제로 밟히는 자리로 옮긴다.
				"## 사정설명",
				"> ?안봤음 10 -> 끝인사",
				"> 욘: \"별거 아니야.\"",
				"> -> 끝인사",
				"## 끝인사",
				"> 링: \"그래!\""));
		}

		[Test]
		public void EveryConditionKind_RoundTrips()
		{
			// 예전엔 되돌려 쓸 때 **종류를 안 보고** 늘 「봤음/안봤음」으로 적었다.
			// 글은 멀쩡해 보이는데 뜻이 조용히 달라지는, 제일 나쁜 종류의 어긋남이다.
			AssertRoundTrip(string.Join("\n",
				"## 문앞",
				"> - 열쇠를 쓴다 [아이템 1001] -> 열림",
				"> - 재료를 센다 [아이템 1002 3] -> 열림",
				"> - 빈손임을 보인다 [아이템없음 1001] -> 열림",
				"> - 다 끝냈다 [퀘스트완료 5000] -> 열림",
				"> - 아직이다 [퀘스트미완 5000] -> 열림",
				"> - 열려는 있다 [퀘스트열림 5000] -> 열림",
				"> - 그때 거절했다 [골랐음 5200 거절한다] -> 열림",
				"> - 그때 말 안 했다 [안골랐음 5200 그냥 간다] -> 열림",
				"> - 그냥 간다 -> 열림",
				"## 열림",
				"> 욘: \"열렸다.\""));
		}

		[Test]
		public void WaitsAndEffects_RoundTrip()
		{
			AssertRoundTrip(string.Join("\n",
				"## 보상",
				"> wait 2s",
				"> wait event boss-defeated",
				"> !아이템 1001 3",
				"> !카드 200",
				"> 욘: \"가져가.\""));
		}

		[Test]
		public void WrittenFormIsReadableByAHuman()
		{
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"## 만남",
				"> 욘: (한숨) \"응.\"",
				"> - 간다 [안봤음 7] -> 만남"));

			string written = DialogueScriptWriter.Write(parsed);

			Assert.That(written.Contains("## 만남"), Is.True);
			Assert.That(written.Contains("> 욘: (한숨) \"응.\""), Is.True, "사람이 쓰던 모양 그대로 나와야 한다");
			Assert.That(written.Contains("> - 간다 [안봤음 7] -> 만남"), Is.True);
		}

		[Test]
		public void EmptyScript_WritesEmpty()
		{
			Assert.That(DialogueScriptWriter.Write(DialogueScriptParser.Parse("")), Is.Empty);
			Assert.That(DialogueScriptWriter.Write(null), Is.Empty);
		}
	}
}
