using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 원고 글을 고쳐 저장하면 **바로 다시 읽고, 걸린 곳을 콘솔에 찍는다** (TASK-WM-052).
	///
	/// ★ 왜 필요한가 두 가지:
	/// ① **고친 글이 안 나온다** — 대화 자산은 그래프를 한 번만 세우고 재사용한다(같은 대화를 걸 때마다
	///    대사 사본이 쌓이지 않게). 그래서 글을 고쳐도 **그 판에서는 옛 대사가 계속 나온다.**
	///    저장한 순간 그 기억을 지워 줘야 「고치고 바로 확인」이 성립한다.
	/// ② 오타를 **쓴 직후** 안다. 창을 열거나 재생해 볼 때까지 기다릴 이유가 없다.
	///
	/// 자기 원고를 물고 있는 자산만 건드린다(관계 없는 텍스트 파일은 그냥 지나간다).
	/// </summary>
	public sealed class DialogueScriptPostprocessor : AssetPostprocessor
	{
		private static void OnPostprocessAllAssets(
			string[] importedAssets,
			string[] deletedAssets,
			string[] movedAssets,
			string[] movedFromAssetPaths)
		{
			if (importedAssets.Length == 0)
			{
				return;
			}

			HashSet<string> imported = new(importedAssets);
			string[] guids = AssetDatabase.FindAssets("t:" + nameof(DialogueScriptSource));
			for (int i = 0; i < guids.Length; i++)
			{
				DialogueScriptSource source = AssetDatabase.LoadAssetAtPath<DialogueScriptSource>(
					AssetDatabase.GUIDToAssetPath(guids[i]));
				if (source == null || source.Script == null)
				{
					continue;
				}

				string scriptPath = AssetDatabase.GetAssetPath(source.Script);
				if (imported.Contains(scriptPath) == false)
				{
					continue;
				}

				// 세워 둔 그래프를 버린다 — 안 버리면 이 판 내내 옛 대사가 나온다.
				source.Invalidate();
				ReportIssues(source, scriptPath);
			}
		}

		private static void ReportIssues(DialogueScriptSource source, string scriptPath)
		{
			ParsedDialogueScript parsed = source.ParseFresh();
			if (parsed.Issues.Count == 0)
			{
				return;
			}

			// 걸린 게 있을 때만 말한다 — 저장할 때마다 멀쩡하다고 떠들면 콘솔이 쓸모없어진다.
			for (int i = 0; i < parsed.Issues.Count; i++)
			{
				Debug.LogWarning($"[원고] {scriptPath} L{parsed.Issues[i].LineNumber}: {parsed.Issues[i].Message}", source);
			}
		}
	}
}
