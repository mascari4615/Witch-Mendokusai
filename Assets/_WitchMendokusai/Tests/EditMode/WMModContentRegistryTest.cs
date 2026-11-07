using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-188 — 모드 콘텐츠 등록 seam 회귀 락.
    /// IMod.Initialize(IModContext) 가 게임에 실제 콘텐츠를 등록할 수 있다(껍데기/로그 아님).
    /// HelloMod 실경로(ModLoader AfterAssembliesLoaded → quest 1 등록)는 PlayMode behavior-verified.
    /// 본 테스트 = seam 계약(context → registry 수신) 회귀 락 — Mods.Sample asmdef 미참조라 로컬 FakeMod.
    /// </summary>
    public sealed class WMModContentRegistryTest
    {
        private sealed class FakeMod : IMod
        {
            public string Name => "Fake";
            public string Version => "1.0.0";

            public void Initialize(IModContext context)
            {
                context.Content.RegisterQuest(new ModQuestDefinition("fake_q", "가짜 퀘스트", QuestType.Normal));
            }
        }

        [Test]
        public void Mod_RegistersContent_ThroughContext_NotInert()
        {
            ModContentRegistry registry = new ModContentRegistry();
            IMod mod = new FakeMod();

            mod.Initialize(registry);

            Assert.That(registry.RegisteredQuests.Count, Is.EqualTo(1), "모드가 IModContext 로 콘텐츠 등록 못 함 = seam inert(껍데기 회귀)");
            Assert.That(registry.RegisteredQuests[0].Id, Is.EqualTo("fake_q"));
            Assert.That(registry.RegisteredQuests[0].Type, Is.EqualTo(QuestType.Normal));
        }

        [Test]
        public void Registry_NullQuest_Ignored()
        {
            ModContentRegistry registry = new ModContentRegistry();
            registry.RegisterQuest(null);
            Assert.That(registry.RegisteredQuests.Count, Is.EqualTo(0));
        }

        /// <summary>
        /// 소비측 회귀 락 — 모드 등록 quest 가 live QuestBuffer 에 실제 등장 + 완료가능(inert 아님).
        /// QuestManager.Init 이 동일 InstallInto 를 ModLoader.Content 로 호출 = 모드 콘텐츠 게임 등장.
        /// </summary>
        [Test]
        public void ModQuest_InstalledIntoQuestBuffer_AppearsAndCompletable()
        {
            QuestBuffer buffer = ScriptableObject.CreateInstance<QuestBuffer>();
            System.Collections.Generic.List<ModQuestDefinition> defs = new System.Collections.Generic.List<ModQuestDefinition>
            {
                new ModQuestDefinition("mq1", "모드 등장 퀘스트", QuestType.Normal),
            };

            int installed = ModQuestInstaller.InstallInto(buffer, defs);

            Assert.That(installed, Is.EqualTo(1));
            Assert.That(buffer.Data.Count, Is.EqualTo(1), "모드 quest 가 live QuestBuffer 에 안 들어감 = 소비 inert");
            RuntimeQuest quest = buffer.Data[0];
            Assert.That(quest.Name, Is.EqualTo("모드 등장 퀘스트"));
            Assert.That(quest.Type, Is.EqualTo(QuestType.Normal));
            Assert.That(quest.QuestSOID, Is.EqualTo(-1), "mod quest = QuestSO 없음");
            Assert.That(quest.State, Is.EqualTo(RuntimeQuestState.CanComplete), "criteria 0 → StartQuest 즉시 완료가능");

            Object.DestroyImmediate(buffer);
        }

        /// <summary>실행마다 저장에 1개씩 쌓이던 회귀 락: 설치, 저장, 새 실행의 복원과 재설치를 반복.</summary>
        [Test]
        public void ModQuest_SaveLoadAcrossRuns_StaysSingle()
        {
            List<ModQuestDefinition> definitions = new List<ModQuestDefinition>
            {
                new ModQuestDefinition("mq1", "모드 등장 퀘스트", QuestType.Normal),
            };
            List<RuntimeQuestSaveData> saved = new List<RuntimeQuestSaveData>();

            for (int run = 0; run < 3; run++)
            {
                QuestBuffer buffer = ScriptableObject.CreateInstance<QuestBuffer>();
                foreach (RuntimeQuestSaveData savedQuest in ModQuestInstaller.NormalizeSavedQuests(saved))
                {
                    buffer.Add(RuntimeQuestFactory.FromSaveData(savedQuest));
                }

                ModQuestInstaller.InstallInto(buffer, definitions);

                saved = buffer.Data.ConvertAll(quest => quest.Save());
                Object.DestroyImmediate(buffer);
            }

            Assert.That(saved.Count, Is.EqualTo(1));
            Assert.That(saved[0].ModQuestId, Is.EqualTo("mq1"));
        }

        [Test]
        public void ModQuest_DifferentIds_BothInstalled()
        {
            QuestBuffer buffer = ScriptableObject.CreateInstance<QuestBuffer>();
            List<ModQuestDefinition> definitions = new List<ModQuestDefinition>
            {
                new ModQuestDefinition("mq1", "하나", QuestType.Normal),
                new ModQuestDefinition("mq2", "둘", QuestType.Normal),
            };

            int installed = ModQuestInstaller.InstallInto(buffer, definitions);

            Assert.That(installed, Is.EqualTo(2));
            Object.DestroyImmediate(buffer);
        }

        [Test]
        public void NormalizeSavedQuests_LegacyDuplicates_KeepsFirstOnly()
        {
            RuntimeQuestSaveData first = LegacyModQuest("sample_quest_1");
            List<RuntimeQuestSaveData> saved = new List<RuntimeQuestSaveData>
            {
                first,
                LegacyModQuest("sample_quest_1"),
                LegacyModQuest("sample_quest_1"),
                LegacyModQuest("other_mod_quest"),
            };

            List<RuntimeQuestSaveData> normalized = ModQuestInstaller.NormalizeSavedQuests(saved);

            Assert.That(normalized.Count, Is.EqualTo(2));
            Assert.That(normalized[0].Guid, Is.EqualTo(first.Guid));
            Assert.That(normalized[0].ModQuestId, Is.EqualTo("sample_quest_1"));
            Assert.That(normalized[1].ModQuestId, Is.EqualTo("other_mod_quest"));
        }

        [Test]
        public void NormalizeSavedQuests_NonModQuests_Untouched()
        {
            RuntimeQuestSaveData soQuest = LegacyModQuest("sample_quest_1");
            soQuest.SO_ID = 5000;

            RuntimeQuestSaveData workTimeQuest = LegacyModQuest("sample_quest_1");
            workTimeQuest.WorkTime = 1f;

            RuntimeQuestSaveData plainQuest = LegacyModQuest("sample_quest_1");
            plainQuest.Description = "일반 설명";

            RuntimeQuestSaveData nullDescriptionQuest = LegacyModQuest("sample_quest_1");
            nullDescriptionQuest.Description = null;

            List<RuntimeQuestSaveData> saved = new List<RuntimeQuestSaveData>
            {
                soQuest,
                soQuest,
                workTimeQuest,
                workTimeQuest,
                plainQuest,
                plainQuest,
                nullDescriptionQuest,
                nullDescriptionQuest,
            };

            List<RuntimeQuestSaveData> normalized = ModQuestInstaller.NormalizeSavedQuests(saved);

            Assert.That(normalized.Count, Is.EqualTo(saved.Count));
            Assert.That(normalized.TrueForAll(quest => quest.ModQuestId == null), Is.True);
        }

        [Test]
        public void NormalizeSavedQuests_WorkingDuplicate_Kept()
        {
            RuntimeQuestSaveData working = LegacyModQuest("sample_quest_1");
            working.State = RuntimeQuestState.Working;
            List<RuntimeQuestSaveData> saved = new List<RuntimeQuestSaveData>
            {
                LegacyModQuest("sample_quest_1"),
                working,
            };

            List<RuntimeQuestSaveData> normalized = ModQuestInstaller.NormalizeSavedQuests(saved);

            Assert.That(normalized.Count, Is.EqualTo(2));
        }

        [Test]
        public void HiddenModQuest_NotInstalledByDefault_InstalledWhenIncluded()
        {
            List<ModQuestDefinition> definitions = new List<ModQuestDefinition>
            {
                new ModQuestDefinition("hidden_q", "숨김", QuestType.Normal, visibleInGame: false),
                new ModQuestDefinition("shown_q", "표시", QuestType.Normal),
            };

            QuestBuffer defaultBuffer = ScriptableObject.CreateInstance<QuestBuffer>();
            Assert.That(ModQuestInstaller.InstallInto(defaultBuffer, definitions), Is.EqualTo(1));
            Assert.That(defaultBuffer.Data[0].ModQuestId, Is.EqualTo("shown_q"));
            Object.DestroyImmediate(defaultBuffer);

            QuestBuffer fullBuffer = ScriptableObject.CreateInstance<QuestBuffer>();
            Assert.That(ModQuestInstaller.InstallInto(fullBuffer, definitions, includeHidden: true), Is.EqualTo(2));
            Object.DestroyImmediate(fullBuffer);
        }

        [Test]
        public void Registry_HiddenQuestIds_ListsOnlyHidden()
        {
            ModContentRegistry registry = new ModContentRegistry();
            registry.RegisterQuest(new ModQuestDefinition("hidden_q", "숨김", QuestType.Normal, visibleInGame: false));
            registry.RegisterQuest(new ModQuestDefinition("shown_q", "표시", QuestType.Normal));

            Assert.That(registry.HiddenQuestIds, Is.EquivalentTo(new[] { "hidden_q" }));
            Assert.That(registry.RegisteredQuests.Count, Is.EqualTo(2), "숨김도 등록은 유지");
        }

        [Test]
        public void NormalizeSavedQuests_HiddenId_RemovedExceptWorking()
        {
            RuntimeQuestSaveData hidden = LegacyModQuest("sample_quest_1");
            RuntimeQuestSaveData working = LegacyModQuest("sample_quest_1");
            working.State = RuntimeQuestState.Working;
            RuntimeQuestSaveData other = LegacyModQuest("other_mod_quest");

            List<RuntimeQuestSaveData> normalized = ModQuestInstaller.NormalizeSavedQuests(
                new List<RuntimeQuestSaveData> { hidden, working, other },
                new HashSet<string> { "sample_quest_1" });

            Assert.That(normalized.Count, Is.EqualTo(2));
            Assert.That(normalized[0].State, Is.EqualTo(RuntimeQuestState.Working));
            Assert.That(normalized[1].ModQuestId, Is.EqualTo("other_mod_quest"));
        }

        private static RuntimeQuestSaveData LegacyModQuest(string modQuestId)
        {
            return new RuntimeQuestSaveData
            {
                Guid = System.Guid.NewGuid(),
                State = RuntimeQuestState.CanComplete,
                SO_ID = -1,
                Name = "샘플",
                Description = $"모드 등록 퀘스트 ({modQuestId})",
                Type = QuestType.Normal,
                GameEvents = new List<GameEventType>(),
                Criteria = new List<RuntimeCriteriaSaveData>(),
                CompleteEffects = new List<EffectInfoData>(),
                RewardEffects = new List<EffectInfoData>(),
                Rewards = new List<RewardInfoData>(),
                WorkTime = 0f,
                AutoWork = false,
                AutoComplete = false,
            };
        }
    }
}
