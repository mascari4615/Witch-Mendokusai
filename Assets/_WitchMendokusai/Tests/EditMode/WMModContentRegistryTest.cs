using NUnit.Framework;

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
    }
}
