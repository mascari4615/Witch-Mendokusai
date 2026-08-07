// 게임에 실제로 들어간 원고 **전부**를 훑는다 — 「자산으로 넣었다」와 「읽힌다」는 다른 문제다.
using System;
using System.Collections.Generic;
using System.IO;
using WitchMendokusai;

internal static class ShippedScript
{
	/// <summary>
	/// `Assets/` 아래에서 대화 원고로 보이는 글(인용줄이 있는 `.txt`)을 모두 찾아 읽어 본다.
	///
	/// ★ 왜 「전부」인가: 처음엔 오프닝 하나를 경로로 박아 뒀는데, 그러면 **두 번째 원고부터는
	///   아무도 안 본다.** 원고는 늘어나라고 만든 것이므로 검사도 같이 늘어나야 한다.
	///
	/// 걸린 곳이 하나라도 있으면 실패로 센다 — 게임에 들어간 원고는 깨끗해야 한다.
	/// </summary>
	public static void Run(string assetsRoot, Action<string, bool> check)
	{
		if (Directory.Exists(assetsRoot) == false)
		{
			check($"자산 폴더가 있다 ({assetsRoot})", false);
			return;
		}

		List<string> scripts = new();
		foreach (string path in Directory.GetFiles(assetsRoot, "*.txt", SearchOption.AllDirectories))
		{
			string text = File.ReadAllText(path);
			if (LooksLikeDialogue(text))
			{
				scripts.Add(path);
			}
		}

		check("게임에 들어간 원고가 하나 이상 있다", scripts.Count > 0);

		foreach (string path in scripts)
		{
			string name = Path.GetFileName(path);
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(File.ReadAllText(path));

			int lines = 0;
			foreach (DialogueScriptSection section in parsed.Sections)
			{
				lines += section.Entries.Count;
			}

			for (int i = 0; i < parsed.Issues.Count; i++)
			{
				Console.WriteLine($"      L{parsed.Issues[i].LineNumber}: {parsed.Issues[i].Message}");
			}
			check($"{name}: 걸림 0 (장면 {parsed.Sections.Count} · 마디 {lines})", parsed.Issues.Count == 0);

			// 세워서 실제로 끝까지 흐르는지 — 읽히기만 하고 못 도는 원고가 있으면 안 된다.
			DialoguePlayback playback = new(DialogueScriptGraphBuilder.Build(parsed));
			playback.Begin();
			int steps = 0;
			while (playback.IsPlaying && steps < 500)
			{
				steps++;
				playback.Advance();
				playback.Tick(30f);
			}
			check($"{name}: 끝까지 흐른다", playback.IsPlaying == false && steps > 0);
		}
	}

	/// <summary>대화 원고인지 — 인용줄이 하나라도 있으면 그렇게 본다(설명·자료 텍스트는 걸러진다).</summary>
	private static bool LooksLikeDialogue(string text)
	{
		foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
		{
			if (line.TrimStart().StartsWith(">", StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}
}
