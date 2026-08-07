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
