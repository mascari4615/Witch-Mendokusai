using UnityEngine;

namespace WitchMendokusai.Mods.Sample
{
	/// <summary>
	/// TASK-WM-083 Phase A+B — Mods Sample.
	/// asmdef references = [WitchMendokusai.DomainSDK] 만 → DomainSDK type 만 호출 가능.
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

		public void Initialize()
		{
			Debug.Log($"[Mod:{Name} v{Version}] Initialize — DomainSDK sandbox 검증 ✅. DefaultQuestType={DefaultQuestType}, DefaultEffectType={DefaultEffectType}, DefaultWorkerID={DefaultWorkerID}");
		}
	}
}
