using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 원고 자산 인스펙터 (TASK-WM-052) — `DataSOInspector` 확장(기존 자산 인스펙터 선례 그대로).
	///
	/// ★ 왜: 자산을 눌러도 **글 파일 칸 하나만** 보였다. 그 원고가 멀쩡한지, 대사가 몇 줄인지,
	///   어디가 걸리는지는 창을 따로 열어야 알 수 있었다. 제일 자주 보는 자리에 그 답을 둔다.
	///
	/// 무거운 일을 안 한다 — 누를 때 한 번 읽을 뿐이다(원고는 글 파일이라 읽기가 싸다).
	/// </summary>
	[CustomEditor(typeof(DialogueScriptSource), true)]
	[CanEditMultipleObjects]
	public class DialogueScriptSourceInspector : DataSOInspector
	{
		protected override List<(string, Action)> GetCustomButtons()
		{
			return new List<(string, Action)>
			{
				("원고 다시 읽기", () =>
				{
					if (dataSO is DialogueScriptSource source)
					{
						source.Invalidate();
						ReportToConsole(source);
					}
				}),
			};
		}

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			// `is X y == false` 로 쓰면 파일 단위 검사 도구가 그 뒤 변수를 미할당으로 본다(오탐).
			// 검증되는 형태를 고른다 — 앞 바퀴에 정한 방침.
			DialogueScriptSource source = dataSO as DialogueScriptSource;
			if (source == null)
			{
				return;
			}
			DrawSummary(source);
		}

		private static void DrawSummary(DialogueScriptSource source)
		{
			if (source.Script == null)
			{
				EditorGUILayout.HelpBox("글 파일이 안 물려 있다 — 이대로면 게임에서 아무 말도 안 한다.", MessageType.Warning);
				return;
			}

			ParsedDialogueScript parsed = source.ParseFresh();
			int lineCount = 0;
			for (int i = 0; i < parsed.Sections.Count; i++)
			{
				lineCount += parsed.Sections[i].Entries.Count;
			}

			EditorGUILayout.Space(4f);
			EditorGUILayout.LabelField(
				$"장면 {parsed.Sections.Count} · 마디 {lineCount} · 안 읽은 인용줄 {parsed.SkippedQuoteLines.Count}",
				EditorStyles.miniLabel);

			if (parsed.Issues.Count == 0)
			{
				EditorGUILayout.HelpBox("걸린 곳 없음.", MessageType.None);
				return;
			}

			// 줄 번호까지 붙여서 — 원고 파일에서 바로 찾아 고칠 수 있어야 한다.
			for (int i = 0; i < parsed.Issues.Count; i++)
			{
				EditorGUILayout.HelpBox($"L{parsed.Issues[i].LineNumber}: {parsed.Issues[i].Message}", MessageType.Warning);
			}
		}

		private static void ReportToConsole(DialogueScriptSource source)
		{
			ParsedDialogueScript parsed = source.ParseFresh();
			Debug.Log($"[원고] {source.name} — 장면 {parsed.Sections.Count} · 걸림 {parsed.Issues.Count}", source);
			for (int i = 0; i < parsed.Issues.Count; i++)
			{
				Debug.LogWarning($"[원고] L{parsed.Issues[i].LineNumber}: {parsed.Issues[i].Message}", source);
			}
		}
	}
}
