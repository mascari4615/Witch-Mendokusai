// 게임에 실제로 들어간 원고 파일을 그대로 읽어 본다 — 「자산으로 넣었다」와 「읽힌다」는 다른 문제다.
using System;
using System.IO;
using WitchMendokusai;

internal static class ShippedScript
{
	public static void Run(string path, Action<string, bool> check)
	{
		if (File.Exists(path) == false)
		{
			check("게임 원고 파일이 있다", false);
			return;
		}

		ParsedDialogueScript parsed = DialogueScriptParser.Parse(File.ReadAllText(path));
		int lines = 0;
		foreach (DialogueScriptSection section in parsed.Sections)
		{
			lines += section.Entries.Count;
		}

		check("게임 원고: 걸림 0", parsed.Issues.Count == 0);
		check("게임 원고: 장면 3", parsed.Sections.Count == 3);
		check("게임 원고: 대사 9", lines == 9);

		DialogueGraph graph = DialogueScriptGraphBuilder.Build(parsed);
		DialoguePlayback playback = new(graph);
		playback.Begin();
		check("게임 원고: 첫 대사가 알리사", playback.CurrentLine != null
			&& playback.CurrentLine.ResolveSpeakerName() == "알리사"
			&& playback.CurrentLine.Text == "주인님, 아침입니다.");

		int spoken = 0;
		while (playback.IsPlaying && spoken < 50)
		{
			spoken++;
			playback.Advance();
		}
		check("게임 원고: 끝까지 흐른다(9줄)", spoken == 9 && playback.IsPlaying == false);
	}
}
