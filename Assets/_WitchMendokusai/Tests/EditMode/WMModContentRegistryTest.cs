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
    }
}
