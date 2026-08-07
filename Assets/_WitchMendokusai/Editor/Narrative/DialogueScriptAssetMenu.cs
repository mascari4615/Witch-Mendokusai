using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 원고 텍스트 파일 → 대화 자산 한 번에 (TASK-WM-052).
	///
	/// ★ 왜 필요한가: 원고를 게임에 넣으려면 자산을 만들고 글을 물리고 번호를 정해야 한다.
	///   그걸 손으로 하면(특히 에디터 없이 YAML 로 적으면) **번호 하나 어긋나도 조용히 빈 자산**이 된다.
	///   메뉴 한 번으로 만들면 그 종류의 사고가 아예 없어진다.
	///
	/// 번호는 **이미 있는 것들 중 제일 큰 값 + 1** 로 준다 — 겹치면 「이 대화 봤나」 조건이 엉뚱한 대화를 가리킨다.
	/// 만든 직후 원고를 읽어 **걸린 줄을 콘솔에 찍는다**(만들자마자 문제를 안다).
	///
	/// 메뉴: 원고 `.txt` 를 고르고 → `WM/Narrative/원고로 대화 자산 만들기`
	/// </summary>
	public static class DialogueScriptAssetMenu
	{
		private const string MENU_PATH = "WM/Narrative/원고로 대화 자산 만들기";
		private const int FIRST_DIALOGUE_ID = 5200;
		private const string ASSET_PREFIX = "DialogueScript_";

		[MenuItem(MENU_PATH, true)]
		private static bool ValidateSelection() => Selection.activeObject is TextAsset;

		[MenuItem(MENU_PATH)]
		private static void CreateFromSelection()
		{
			// `is TextAsset script == false` 로 쓰면 옛 컴파일러가 그 뒤의 script 를 「미할당」이라 본다
			// (유니티 본체는 통과하지만 파일 단위 검사 도구가 못 넘긴다). as + null 검사면 어디서나 검증된다.
			TextAsset script = Selection.activeObject as TextAsset;
			if (script == null)
			{
				return;
			}

			string scriptPath = AssetDatabase.GetAssetPath(script);
			string directory = Path.GetDirectoryName(scriptPath);
			string assetPath = AssetDatabase.GenerateUniqueAssetPath(
				Path.Combine(directory, ASSET_PREFIX + Path.GetFileNameWithoutExtension(scriptPath) + ".asset"));

			DialogueScriptSource source = ScriptableObject.CreateInstance<DialogueScriptSource>();
			AssetDatabase.CreateAsset(source, assetPath);

			// private set 프로퍼티라 직렬화 필드로 넣는다 — 인스펙터가 쓰는 것과 같은 경로.
			SerializedObject serialized = new(source);
			serialized.FindProperty("<Script>k__BackingField").objectReferenceValue = script;
			serialized.FindProperty("<ID>k__BackingField").intValue = NextDialogueId();
			serialized.FindProperty("<Name>k__BackingField").stringValue = Path.GetFileNameWithoutExtension(scriptPath);
			serialized.ApplyModifiedPropertiesWithoutUndo();

			AssetDatabase.SaveAssets();
			Selection.activeObject = source;
			EditorGUIUtility.PingObject(source);

			ReportIssues(source, assetPath);
		}

		/// <summary>
		/// 이미 있는 대화 번호 중 제일 큰 값 + 1. 겹치면 「이 대화 봤나」 조건이 **엉뚱한 대화를 가리킨다** —
		/// 그건 한참 뒤에 「왜 이 대사가 안 나오지」로 발견되는 종류다.
		/// </summary>
		private static int NextDialogueId()
		{
			int highest = FIRST_DIALOGUE_ID - 1;
			string[] guids = AssetDatabase.FindAssets("t:" + nameof(DialogueScriptSource));
			for (int i = 0; i < guids.Length; i++)
			{
				DialogueScriptSource existing = AssetDatabase.LoadAssetAtPath<DialogueScriptSource>(
					AssetDatabase.GUIDToAssetPath(guids[i]));
				if (existing != null && existing.ID > highest)
				{
					highest = existing.ID;
				}
			}
			return highest + 1;
		}

		private static void ReportIssues(DialogueScriptSource source, string assetPath)
		{
			ParsedDialogueScript parsed = source.ParseFresh();
			int lineCount = 0;
			List<DialogueScriptSection> sections = parsed.Sections;
			for (int i = 0; i < sections.Count; i++)
			{
				lineCount += sections[i].Entries.Count;
			}

			Debug.Log($"[대화 자산] {assetPath} — 번호 {source.ID} · 장면 {sections.Count} · 마디 {lineCount} · 걸림 {parsed.Issues.Count}", source);
			for (int i = 0; i < parsed.Issues.Count; i++)
			{
				Debug.LogWarning($"[대화 자산] L{parsed.Issues[i].LineNumber}: {parsed.Issues[i].Message}", source);
			}
		}
	}
}
