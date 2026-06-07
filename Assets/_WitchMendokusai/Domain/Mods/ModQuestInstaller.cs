using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
    /// <summary>
    /// 모드가 등록한 ModQuestDefinition(DomainSDK POCO)을 live RuntimeQuest 로 변환·QuestBuffer 설치 (Bridge 소비측).
    /// = 모드 콘텐츠가 게임 퀘스트로 실제 등장(inert 해소). QuestManager.Init 이 ModLoader.Content 로 호출. TASK-WM-188 deepening.
    /// </summary>
    public static class ModQuestInstaller
    {
        public static RuntimeQuest ToRuntimeQuest(ModQuestDefinition def)
        {
            RuntimeQuestSaveData saveData = new RuntimeQuestSaveData
            {
                Guid = System.Guid.NewGuid(),
                State = RuntimeQuestState.InProgress,
                SO_ID = -1, // mod quest = QuestSO 없음
                Name = def.DisplayName,
                Description = $"모드 등록 퀘스트 ({def.Id})",
                Type = def.Type,
                GameEvents = new List<GameEventType>(),
                Criteria = new List<RuntimeCriteriaSaveData>(),
                CompleteEffects = new List<EffectInfoData>(),
                RewardEffects = new List<EffectInfoData>(),
                Rewards = new List<RewardInfoData>(),
                WorkTime = 0f,
                AutoWork = false,
                AutoComplete = false,
            };

            RuntimeQuest quest = new RuntimeQuest(saveData);
            // RuntimeQuest.Load 는 Criteria 를 세팅하지 않음(null) → Evaluate foreach NRE 방지로 empty 명시.
            // criteria 0 = StartQuest 시 즉시 CanComplete (최소 mod quest, 후속 = criteria/reward 확장).
            quest.Criteria = new List<RuntimeCriteria>();
            return quest;
        }

        /// <summary>모드 등록 quest 들을 QuestBuffer 에 설치 + StartQuest. 설치 수 반환.</summary>
        public static int InstallInto(QuestBuffer buffer, IReadOnlyList<ModQuestDefinition> quests)
        {
            if (buffer == null || quests == null)
                return 0;

            int installed = 0;
            foreach (ModQuestDefinition def in quests)
            {
                if (def == null)
                    continue;

                RuntimeQuest quest = ToRuntimeQuest(def);
                buffer.Add(quest);
                quest.StartQuest();
                installed++;
            }
            return installed;
        }
    }
}
