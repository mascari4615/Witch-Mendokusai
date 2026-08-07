using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — **작가 문서에 적힌 말이 실제로 읽히는지** 잠근다.
	///
	/// ★ 왜 필요한가(실측): 조건을 둘 더 늘리고서 `memo/wm/design/narrative/원고-쓰는-법.md` 를
	///   **안 고쳤다.** 문서가 코드보다 낡으면 작가는 「안 되는 말」을 쓰고, 원고는 조용히 조건 없이 흐른다.
	///   말(어휘)은 코드와 문서가 갈라지기 제일 쉬운 자리라, 여기서 기계가 붙들어 둔다.
	///
	/// 이 시험이 빨개지면 **둘 중 하나를 고쳐야 한다** — 말을 되살리거나, 문서에서 지우거나.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueScriptVocabularyTest
	{
		/// <summary>문서 표에 적힌 조건 말 전부(한국어·영어).</summary>
		private static readonly string[] ConditionWords =
		{
			"봤음 1", "안봤음 1", "시작함 1",
			"seen 1", "unseen 1", "started 1",
			"아이템 1001", "아이템 1001 3", "아이템없음 1001",
			"item 1001", "item 1001 3", "noitem 1001",
			"퀘스트완료 5000", "퀘스트미완 5000", "퀘스트열림 5000",
			"questdone 5000", "questnotdone 5000", "questopen 5000",
		};

		/// <summary>문서 표에 적힌 효과 말 전부.</summary>
		private static readonly string[] EffectWords =
		{
			"아이템 1001", "아이템 1001 3", "카드 200", "퀘스트추가 5000", "퀘스트열기 5000", "레시피 1001",
			"item 1001", "card 200", "quest 5000", "unlockquest 5000", "recipe 1001",
		};

		[Test]
		public void EveryDocumentedConditionWord_IsUnderstood()
		{
			List<string> unread = new();
			foreach (string word in ConditionWords)
			{
				ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
					"## 시작",
					$"> ?{word} -> 시작"));
				if (parsed.HasIssues)
				{
					unread.Add(word);
				}
			}

			Assert.That(unread, Is.Empty,
				"문서에 적힌 조건 말이 안 읽힌다 — 작가가 쓴 조건이 조용히 무시된다");
		}

		[Test]
		public void EveryDocumentedEffectWord_IsUnderstood()
		{
			List<string> unread = new();
			foreach (string word in EffectWords)
			{
				ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
					"## 시작",
					$"> !{word}"));
				if (parsed.HasIssues)
				{
					unread.Add(word);
				}
			}

			Assert.That(unread, Is.Empty, "문서에 적힌 효과 말이 안 읽힌다 — 적어 둔 보상이 안 나간다");
		}

		[Test]
		public void UndocumentedWord_IsStillRejected()
		{
			// 반대쪽도 잠근다 — 아무 말이나 받아 주면 위 시험이 아무것도 증명하지 않는다.
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(string.Join("\n",
				"## 시작",
				"> ?없는조건말 1 -> 시작"));

			Assert.That(parsed.HasIssues, Is.True);
		}
	}
}
