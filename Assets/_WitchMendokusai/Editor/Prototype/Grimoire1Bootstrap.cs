using System.IO;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-013 prototype 단계 D — 마도서 챕터1 「따뜻함」 placeholder 데이터 자동 생성.
	/// 실제 컨텐츠 (대사 텍스트, 재료, 보상) 는 사용자 정사 영역. 본 메뉴는 *시스템 데이터 흐름 검증용*.
	/// 메뉴: WitchMendokusai/Prototype/Generate Grimoire1
	/// </summary>
	public static class Grimoire1Bootstrap
	{
		private const string CHAPTER_PATH = "Assets/_WitchMendokusai/Content/Task/Quest/MagicBook/Chapter_Grimoire1.asset";
		private const string QUEST_DIR = "Assets/_WitchMendokusai/Content/Task/Quest/";
		private const string DIALOGUE_DIR = "Assets/_WitchMendokusai/Content/Narrative/Demo/";

		private const int QUEST_ID_START = 6000;
		private const int NODE_COUNT = 5;
		private const float NODE_X_SPACING = 240f;
		private const int FADE_DURATION_MS = 1000;

		[MenuItem("WitchMendokusai/Prototype/Generate Grimoire1")]
		public static void GenerateGrimoire1()
		{
			EnsureDirectory(Path.GetDirectoryName(CHAPTER_PATH));
			EnsureDirectory(QUEST_DIR);
			EnsureDirectory(DIALOGUE_DIR);

			// 1. DialogueLine 5개
			DialogueLine[] dialogueLines = new DialogueLine[NODE_COUNT];
			for (int i = 0; i < NODE_COUNT; i++)
			{
				string path = $"{DIALOGUE_DIR}DialogueLine_Grimoire1_{i + 1}.asset";
				DialogueLine line = ScriptableObject.CreateInstance<DialogueLine>();
				line.name = $"DialogueLine_Grimoire1_{i + 1}";
				AssetDatabase.CreateAsset(line, path);

				SerializedObject so = new SerializedObject(line);
				so.FindProperty("<Text>k__BackingField").stringValue = $"[prototype] 노드 {i + 1} 따뜻함 샘플 대사";
				so.ApplyModifiedProperties();

				dialogueLines[i] = line;
			}

			// 2. QuestSO 5개
			QuestSO[] quests = new QuestSO[NODE_COUNT];
			for (int i = 0; i < NODE_COUNT; i++)
			{
				int id = QUEST_ID_START + i;
				string path = $"{QUEST_DIR}Q_{id}_prototype{i + 1}.asset";
				QuestSO quest = ScriptableObject.CreateInstance<QuestSO>();
				quest.name = $"Q_{id}_prototype{i + 1}";
				quest.ID = id;
				quest.Name = $"[prototype] 따뜻함 {i + 1}";
				quest.Description = $"prototype 노드 {i + 1} — placeholder";
				AssetDatabase.CreateAsset(quest, path);
				quests[i] = quest;
			}

			// 3. cascade 연결 + 마지막 노드 RewardEffects (PlayDialogue + PlayFade)
			for (int i = 0; i < NODE_COUNT; i++)
			{
				SerializedObject so = new SerializedObject(quests[i]);
				SerializedProperty data = so.FindProperty("<Data>k__BackingField");
				SerializedProperty completeEffects = data.FindPropertyRelative("CompleteEffects");
				SerializedProperty rewardEffects = data.FindPropertyRelative("RewardEffects");

				completeEffects.arraySize = 0;
				rewardEffects.arraySize = 0;

				if (i < NODE_COUNT - 1)
				{
					// 다음 노드 unlock cascade
					completeEffects.arraySize = 1;
					SerializedProperty effect = completeEffects.GetArrayElementAtIndex(0);
					effect.FindPropertyRelative("Type").intValue = (int)EffectType.UnlockQuest;
					effect.FindPropertyRelative("Data").objectReferenceValue = quests[i + 1];
				}
				else
				{
					// 마지막 노드 = dialogue + fade RewardEffects
					rewardEffects.arraySize = 2;

					SerializedProperty dialogueEffect = rewardEffects.GetArrayElementAtIndex(0);
					dialogueEffect.FindPropertyRelative("Type").intValue = (int)EffectType.PlayDialogue;
					dialogueEffect.FindPropertyRelative("Data").objectReferenceValue = dialogueLines[NODE_COUNT - 1];

					SerializedProperty fadeEffect = rewardEffects.GetArrayElementAtIndex(1);
					fadeEffect.FindPropertyRelative("Type").intValue = (int)EffectType.PlayFade;
					fadeEffect.FindPropertyRelative("Value").intValue = FADE_DURATION_MS;
				}

				so.ApplyModifiedProperties();
			}

			// 4. ChapterSO + 5 노드 배치
			ChapterSO chapter = ScriptableObject.CreateInstance<ChapterSO>();
			chapter.name = "Chapter_Grimoire1";
			chapter.ID = 1;
			chapter.Name = "[prototype] 마도서 1장 따뜻함";
			chapter.Description = "prototype — 시스템 데이터 흐름 검증";
			AssetDatabase.CreateAsset(chapter, CHAPTER_PATH);

			SerializedObject chapterSO = new SerializedObject(chapter);
			SerializedProperty nodes = chapterSO.FindProperty("<Nodes>k__BackingField");
			nodes.arraySize = NODE_COUNT;
			for (int i = 0; i < NODE_COUNT; i++)
			{
				SerializedProperty nodeData = nodes.GetArrayElementAtIndex(i);
				nodeData.FindPropertyRelative("Quest").objectReferenceValue = quests[i];
				nodeData.FindPropertyRelative("Position").vector2Value = new Vector2(i * NODE_X_SPACING, 0f);
			}
			chapterSO.ApplyModifiedProperties();

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Debug.Log($"[Grimoire1Bootstrap] Generated: 1 ChapterSO + {NODE_COUNT} QuestSO + {NODE_COUNT} DialogueLine. 「에디터에서 남은 작업」: UIMagicBookPanel.chapterDatas Inspector 에 Chapter_Grimoire1 추가.");

			EditorUtility.FocusProjectWindow();
			Selection.activeObject = chapter;
		}

		private static void EnsureDirectory(string dir)
		{
			if (Directory.Exists(dir) == false)
				Directory.CreateDirectory(dir);
		}
	}
}
