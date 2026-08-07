// 진짜 원고에 대고 대본 읽기를 돌려 본다 — 만든 규칙이 실제 글과 맞는지.
using System;
using System.IO;
using WitchMendokusai;

internal static class RealDocs
{
	public static void Run(string folder)
	{
		if (Directory.Exists(folder) == false)
		{
			Console.WriteLine($"  (원고 폴더 없음: {folder})");
			return;
		}

		foreach (string path in Directory.GetFiles(folder, "*.md"))
		{
			string text = File.ReadAllText(path);
			ParsedDialogueScript parsed = DialogueScriptParser.Parse(text);
			int speak = 0, choice = 0, wait = 0, jump = 0;
			foreach (DialogueScriptSection section in parsed.Sections)
			{
				foreach (DialogueScriptEntry entry in section.Entries)
				{
					if (entry.Kind == DialogueScriptEntryKind.Speak) speak++;
					else if (entry.Kind == DialogueScriptEntryKind.Choice) choice++;
					else if (entry.Kind == DialogueScriptEntryKind.Goto) jump++;
					else wait++;
				}
			}
			if (speak + choice + wait + jump == 0 && parsed.Issues.Count == 0)
			{
				continue;
			}
			Console.WriteLine($"  {Path.GetFileName(path),-24} 장면 {parsed.Sections.Count,3} · 대사 {speak,3} · 선택 {choice} · 대기 {wait} · 점프 {jump} · 걸림 {parsed.Issues.Count}");
			for (int i = 0; i < parsed.Issues.Count && i < 6; i++)
			{
				Console.WriteLine($"      L{parsed.Issues[i].LineNumber}: {parsed.Issues[i].Message}");
			}

			// 진짜 원고로 왕복까지 확인 — 지어낸 예제만으로는 쓰기 규칙의 구멍이 안 드러난다.
			ParsedDialogueScript again = DialogueScriptParser.Parse(DialogueScriptWriter.Write(parsed));
			int againLines = 0;
			foreach (DialogueScriptSection section in again.Sections)
			{
				againLines += section.Entries.Count;
			}
			bool sameShape = again.Sections.Count == parsed.Sections.Count && againLines == speak + choice + wait + jump && again.Issues.Count == 0;
			Console.WriteLine($"      왕복: {(sameShape ? "같음" : "!! 어긋남 !!")} (장면 {again.Sections.Count} · 마디 {againLines} · 걸림 {again.Issues.Count})");
		}
	}
}
