using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — **게임에 실제로 들어간 원고 자산**이 멀쩡한지 (`ArenaShippedConfigTests` 동형).
	///
	/// ★ 왜 이 시험이 따로 필요한가: 이 자산(`.asset`)은 유니티 에디터 없이 **손으로 적은 YAML** 이다.
	///   스크립트 guid 나 필드 이름이 한 글자만 어긋나도 유니티는 **조용히 빈 자산**으로 임포트한다 —
	///   컴파일도 통과하고 다른 시험도 다 초록인데 **게임에서만 아무 말도 안 하는** 상태가 된다.
	///   그건 눈으로만 잡히는 종류라, 여기서 기계가 잡게 한다.
	///
	/// 이 시험은 에디터 자산 데이터베이스를 쓰므로 **유니티 밖 하네스에서는 안 돈다**(거기선 제외돼 있다).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueShippedScriptTests
	{
		private const string OPENING_ASSET_PATH =
			"Assets/_WitchMendokusai/Domain/Narrative/Demo/DialogueScript_Opening.asset";

		private static DialogueScriptSource LoadOpening()
		{
			DialogueScriptSource source = AssetDatabase.LoadAssetAtPath<DialogueScriptSource>(OPENING_ASSET_PATH);
			Assert.That(source, Is.Not.Null,
				$"원고 자산을 못 읽었다: {OPENING_ASSET_PATH} — 손으로 적은 YAML 이라 스크립트 guid 가 어긋나면 여기서 걸린다");
			return source;
		}

		[Test]
		public void OpeningAsset_Imports()
		{
			DialogueScriptSource source = LoadOpening();

			Assert.That(source.Script, Is.Not.Null,
				"원고 글 파일이 안 물려 있다 — 자산은 떴는데 내용이 비면 게임에선 아무 말도 안 한다");
			Assert.That(source.Script.text, Is.Not.Empty);
		}

		[Test]
		public void OpeningAsset_ParsesWithoutIssues()
		{
			ParsedDialogueScript parsed = LoadOpening().ParseFresh();

			Assert.That(parsed.Issues, Is.Empty, "게임에 들어간 원고에 걸림이 있으면 안 된다");
			Assert.That(parsed.Sections.Count, Is.EqualTo(3), "장면 3 (오프닝 3~5)");
		}

		/// <summary>
		/// 게임에 들어간 **모든** 대화 자산 — 하나를 경로로 박아 두면 두 번째 원고부터는 아무도 안 본다.
		/// 원고는 늘어나라고 만든 것이므로 검사도 같이 늘어나야 한다(하네스 쪽과 같은 판단).
		/// </summary>
		private static IEnumerable<DialogueScriptSource> LoadAllScripts()
		{
			string[] guids = AssetDatabase.FindAssets("t:" + nameof(DialogueScriptSource));
			for (int i = 0; i < guids.Length; i++)
			{
				DialogueScriptSource source = AssetDatabase.LoadAssetAtPath<DialogueScriptSource>(
					AssetDatabase.GUIDToAssetPath(guids[i]));
				if (source != null)
				{
					yield return source;
				}
			}
		}

		[Test]
		public void EveryShippedScript_ParsesAndValidatesClean()
		{
			StringBuilder problems = new();
			int checkedCount = 0;

			foreach (DialogueScriptSource source in LoadAllScripts())
			{
				checkedCount++;
				string path = AssetDatabase.GetAssetPath(source);

				if (source.Script == null)
				{
					problems.AppendLine($"{path}: 글 파일이 안 물려 있다");
					continue;
				}

				ParsedDialogueScript parsed = source.ParseFresh();
				for (int i = 0; i < parsed.Issues.Count; i++)
				{
					problems.AppendLine($"{path} L{parsed.Issues[i].LineNumber}: {parsed.Issues[i].Message}");
				}

				// 읽기만 되고 그래프가 이상한 경우 — 못 닿는 마디·조건 없는 분기 등은 여기서만 잡힌다.
				DialogueGraphValidationResult validation = DialogueGraphValidator.Validate(source.BuildGraph());
				for (int i = 0; i < validation.Issues.Count; i++)
				{
					if (validation.Issues[i].Severity == NodeGraph.NodeGraphIssueSeverity.Error)
					{
						problems.AppendLine($"{path}: {validation.Issues[i].Message}");
					}
				}
			}

			Assert.That(checkedCount, Is.GreaterThan(0), "대화 자산이 하나도 없다 — 검사가 아무것도 안 보고 있다");
			Assert.That(problems.ToString(), Is.Empty, "게임에 들어간 원고는 깨끗해야 한다");
		}

		[Test]
		public void ShippedScriptIds_AreUnique()
		{
			Dictionary<int, string> byId = new();
			StringBuilder duplicates = new();

			foreach (DialogueScriptSource source in LoadAllScripts())
			{
				string path = AssetDatabase.GetAssetPath(source);
				if (byId.TryGetValue(source.ID, out string existing))
				{
					// 번호가 겹치면 「이 대화 봤나」 조건이 엉뚱한 대화를 가리킨다 —
					// 한참 뒤에 「왜 이 대사가 안 나오지」로 발견되는 종류다.
					duplicates.AppendLine($"번호 {source.ID} 가 겹친다: {existing} ↔ {path}");
					continue;
				}
				byId[source.ID] = path;
			}

			Assert.That(duplicates.ToString(), Is.Empty);
		}

		[Test]
		public void OpeningAsset_PlaysFromFirstLineToEnd()
		{
			DialoguePlayback playback = new(LoadOpening().BuildGraph());
			playback.Begin();

			Assert.That(playback.CurrentLine, Is.Not.Null);
			Assert.That(playback.CurrentLine.ResolveSpeakerName(), Is.EqualTo("알리사"));
			Assert.That(playback.CurrentLine.Text, Is.EqualTo("주인님, 아침입니다."));

			int spoken = 0;
			while (playback.IsPlaying && spoken < 50)
			{
				spoken++;
				playback.Advance();
			}

			Assert.That(spoken, Is.EqualTo(9), "대사 9줄이 끝까지 흐른다");
			Assert.That(playback.IsPlaying, Is.False);
		}
	}
}
