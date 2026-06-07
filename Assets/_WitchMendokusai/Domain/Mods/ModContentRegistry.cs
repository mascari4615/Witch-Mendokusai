using System.Collections.Generic;

namespace WitchMendokusai
{
    /// <summary>
    /// IModContext + IModContentRegistry 의 Core 구현 (Bridge 수신측). 모드가 Initialize(context) 에서
    /// 등록한 콘텐츠를 수집 → 게임이 RegisteredQuests 로 조회·소비. ModLoader 가 생성·주입·노출.
    /// TASK-WM-188 — 껍데기 IMod 를 실기능(콘텐츠 등록)으로 만드는 seam 의 수신측.
    /// </summary>
    public sealed class ModContentRegistry : IModContext, IModContentRegistry
    {
        private readonly List<ModQuestDefinition> _quests = new();

        public IReadOnlyList<ModQuestDefinition> RegisteredQuests => _quests;

        // IModContext — 모드에 자신을 registry 로 노출.
        public IModContentRegistry Content => this;

        // IModContentRegistry — 모드가 콘텐츠 등록.
        public void RegisterQuest(ModQuestDefinition quest)
        {
            if (quest == null)
                return;
            _quests.Add(quest);
        }
    }
}
