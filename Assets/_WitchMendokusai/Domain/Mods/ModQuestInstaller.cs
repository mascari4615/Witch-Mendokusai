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
        private const string DESCRIPTION_PREFIX = "모드 등록 퀘스트 (";
        private const string DESCRIPTION_SUFFIX = ")";

        public static RuntimeQuest ToRuntimeQuest(ModQuestDefinition def)
        {
            RuntimeQuestSaveData saveData = new RuntimeQuestSaveData
            {
                Guid = System.Guid.NewGuid(),
                State = RuntimeQuestState.InProgress,
                SO_ID = -1, // mod quest = QuestSO 없음
                ModQuestId = def.Id,
                Name = def.DisplayName,
                Description = DESCRIPTION_PREFIX + def.Id + DESCRIPTION_SUFFIX,
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

        /// <summary>모드 등록 quest 들을 QuestBuffer 에 설치 + StartQuest. 같은 모드 ID 가 이미 있으면 건너뜀. 설치 수 반환.</summary>
        public static int InstallInto(QuestBuffer buffer, IReadOnlyList<ModQuestDefinition> quests)
        {
            if (buffer == null || quests == null)
                return 0;

            int installed = 0;
            foreach (ModQuestDefinition def in quests)
            {
                if (def == null)
                    continue;

                if (buffer.Data.Exists(existing => existing.ModQuestId == def.Id))
                    continue;

                RuntimeQuest quest = ToRuntimeQuest(def);
                buffer.Add(quest);
                quest.StartQuest();
                installed++;
            }
            return installed;
        }

        /// <summary>
        /// 저장된 퀘스트 목록 정리. 옛 저장의 모드 quest 에 ModQuestId 부여, 같은 모드 ID 중복 제거 (첫 항목 유지).
        /// ModQuestId 없고 옛 형식도 아닌 항목은 그대로 통과. 진행 중 Work 가 Guid 를 참조하는 Working 항목도 유지.
        /// </summary>
        public static List<RuntimeQuestSaveData> NormalizeSavedQuests(List<RuntimeQuestSaveData> savedQuests)
        {
            List<RuntimeQuestSaveData> normalized = new List<RuntimeQuestSaveData>(savedQuests.Count);
            HashSet<string> seenModQuestIds = new HashSet<string>();

            foreach (RuntimeQuestSaveData savedQuest in savedQuests)
            {
                RuntimeQuestSaveData quest = savedQuest;
                if (string.IsNullOrEmpty(quest.ModQuestId) && TryGetLegacyModQuestId(quest, out string legacyModQuestId))
                {
                    quest.ModQuestId = legacyModQuestId;
                }

                if (string.IsNullOrEmpty(quest.ModQuestId))
                {
                    normalized.Add(quest);
                    continue;
                }

                bool isDuplicate = seenModQuestIds.Add(quest.ModQuestId) == false;
                if (isDuplicate && quest.State != RuntimeQuestState.Working)
                    continue;

                normalized.Add(quest);
            }
            return normalized;
        }

        // 옛 형식 판정: ToRuntimeQuest 가 만든 값과 전 항목 일치할 때만
        private static bool TryGetLegacyModQuestId(RuntimeQuestSaveData quest, out string modQuestId)
        {
            modQuestId = null;

            bool hasInstallerShape = quest.SO_ID == -1
                && quest.GameEvents.Count == 0
                && quest.Criteria.Count == 0
                && quest.CompleteEffects.Count == 0
                && quest.RewardEffects.Count == 0
                && quest.Rewards.Count == 0
                && quest.WorkTime == 0f
                && quest.AutoWork == false
                && quest.AutoComplete == false;
            if (hasInstallerShape == false || quest.Description == null)
                return false;

            int idLength = quest.Description.Length - DESCRIPTION_PREFIX.Length - DESCRIPTION_SUFFIX.Length;
            bool hasDescriptionShape = idLength > 0
                && quest.Description.StartsWith(DESCRIPTION_PREFIX, StringComparison.Ordinal)
                && quest.Description.EndsWith(DESCRIPTION_SUFFIX, StringComparison.Ordinal);
            if (hasDescriptionShape == false)
                return false;

            modQuestId = quest.Description.Substring(DESCRIPTION_PREFIX.Length, idLength);
            return true;
        }
    }
}
