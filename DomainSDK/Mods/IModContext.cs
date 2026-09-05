namespace WitchMendokusai
{
    /// <summary>
    /// 모드 Initialize 에 주입되는 컨텍스트 — DomainSDK 를 통해 게임에 콘텐츠 등록(껍데기 IMod 를 실기능으로).
    /// 모드 .dll 은 DomainSDK 만 reference → 본 interface 로만 게임에 개입(sandbox 유지).
    /// Core 가 IModContentRegistry 구현·수신(Bridge 패턴). TASK-WM-188.
    /// </summary>
    public interface IModContext
    {
        IModContentRegistry Content { get; }
    }

    /// <summary>
    /// 모드가 게임에 콘텐츠를 등록하는 seam. DomainSDK POCO 만 받는다(UnityEngine 의존 0).
    /// first-use = Quest 1종. 회귀 0 확정 후 RegisterEffect/RegisterItem… 확장.
    /// </summary>
    public interface IModContentRegistry
    {
        void RegisterQuest(ModQuestDefinition quest);
    }

    /// <summary>
    /// 모드가 기여하는 퀘스트 정의 — 순수 DomainSDK POCO. Core 가 게임 QuestSO/Runtime 으로 변환·소비.
    /// </summary>
    public sealed class ModQuestDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public QuestType Type { get; }

        public ModQuestDefinition(string id, string displayName, QuestType type)
        {
            Id = id;
            DisplayName = displayName;
            Type = type;
        }
    }
}
