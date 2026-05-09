using System.IO;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-013 prototype 단계 D' — *기능 테스트용 프로토타입 챕터 2* 데이터 자동 생성.
	/// PrototypeChapter1Bootstrap 의 동일 패턴, ID/이름만 chapter2 로. cascade 검증 1회 더 + narrative slice 추가.
	/// 메뉴: WitchMendokusai/Prototype/Generate Prototype Chapter 2
	/// </summary>
	public static class PrototypeChapter2Bootstrap
	{
		private const string CHAPTER_PATH = "Assets/_WitchMendokusai/Domain/Quest/MagicBook/Chapter_Prototype2.asset";
		private const string QUEST_DIR = "Assets/_WitchMendokusai/Domain/Quest/";
		private const string DIALOGUE_DIR = "Assets/_WitchMendokusai/Domain/Narrative/Demo/";

		private const int QUEST_ID_START = 6100;
		private const int CHAPTER_ID = 2;
		private const int NODE_COUNT = 5;
		private const float NODE_X_SPACING = 240f;
		private const int FADE_DURATION_MS = 1500;

		[MenuItem("WitchMendokusai/Prototype/Generate Prototype Chapter 2")]
		public static void GeneratePrototypeChapter2()
		{
			EnsureDirectory(Path.GetDirectoryName(CHAPTER_PATH));
			EnsureDirectory(QUEST_DIR);
			EnsureDirectory(DIALOGUE_DIR);

			// 1. DialogueLine 5개
			DialogueLine[] dialogueLines = new DialogueLine[NODE_COUNT];
			for (int i = 0; i < NODE_COUNT; i++)
			{
				string path = $"{DIALOGUE_DIR}DialogueLine_Prototype2_{i + 1}.asset";
				DialogueLine line = ScriptableObject.CreateInstance<DialogueLine>();
				line.name = $"DialogueLine_Prototype2_{i + 1}";
				AssetDatabase.CreateAsset(line, path);

				SerializedObject so = new SerializedObject(line);
				so.FindProperty("<Text>k__BackingField").stringValue = $"[prototype] 챕터2 노드 {i + 1} 샘플 대사";
				so.ApplyModifiedProperties();

				dialogueLines[i] = line;
			}

			// 2. QuestSO 5개
			QuestSO[] quests = new QuestSO[NODE_COUNT];
			for (int i = 0; i < NODE_COUNT; i++)
			{
				int id = QUEST_ID_START + i;
				string path = $"{QUEST_DIR}Q_{id}_prototype2_{i + 1}.asset";
				QuestSO quest = ScriptableObject.CreateInstance<QuestSO>();
				quest.name = $"Q_{id}_prototype2_{i + 1}";
				quest.ID = id;
				quest.Name = $"[prototype] 챕터2 노드 {i + 1}";
				quest.Description = $"prototype 챕터2 노드 {i + 1} — placeholder";
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
					completeEffects.arraySize = 1;
					SerializedProperty effect = completeEffects.GetArrayElementAtIndex(0);
					effect.FindPropertyRelative("Type").intValue = (int)EffectType.UnlockQuest;
					effect.FindPropertyRelative("Data").objectReferenceValue = quests[i + 1];
				}
				else
				{
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
			chapter.name = "Chapter_Prototype2";
			chapter.ID = CHAPTER_ID;
			chapter.Name = "[prototype] 프로토타입 챕터 2";
			chapter.Description = "prototype — 챕터 cascade + narrative slice 검증 (사용자 정사 챕터 X)";
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

			Debug.Log($"[PrototypeChapter2Bootstrap] Generated: 1 ChapterSO + {NODE_COUNT} QuestSO + {NODE_COUNT} DialogueLine. 「에디터에서 남은 작업」: UIMagicBookPanel.chapterDatas Inspector 에 Chapter_Prototype2 추가.");

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
