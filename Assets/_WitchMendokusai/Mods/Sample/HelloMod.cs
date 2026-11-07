using UnityEngine;

namespace WitchMendokusai.Mods.Sample
{
	/// <summary>
	/// TASK-WM-083 Phase A+B — Mods Sample.
	/// asmdef references 는 DomainSDK 조각만 -> DomainSDK type 만 호출 가능
	/// IMod 구현 = ModLoader 가 reflection 으로 발견 + Initialize 호출.
	/// </summary>
	public class HelloMod : IMod
	{
		public string Name => "Sample";
		public string Version => "0.1.0";

		// DomainSDK type reference 검증 (단방향 OK).
		public QuestType DefaultQuestType => QuestType.Normal;
		public EffectType DefaultEffectType => EffectType.UnitStat;
		public int DefaultWorkerID => WorkConstants.NONE_WORKER_ID;

		public void Initialize(IModContext context)
		{
			// 껍데기(로그) → 실기능: DomainSDK seam(IModContentRegistry)으로 게임에 quest 1개 등록.
			// visibleInGame false, 등록 경로 시연만 하고 게임 퀘스트 목록에서 제외
			context.Content.RegisterQuest(new ModQuestDefinition("sample_quest_1", "샘플 모드 퀘스트", DefaultQuestType, visibleInGame: false));
			Debug.Log($"[Mod:{Name} v{Version}] Initialize, quest 1개 등록, 게임 목록에는 숨김 (DomainSDK sandbox OK). DefaultQuestType={DefaultQuestType}, DefaultEffectType={DefaultEffectType}, DefaultWorkerID={DefaultWorkerID}");
		}
	}
}
