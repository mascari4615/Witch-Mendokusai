using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// UnitObject 를 아레나 전투 참가자로 래핑. 인형·사역마·적이 모두 동일 컴포넌트(TeamId 만 다름).
	/// TeamId·CombatantId 는 매치 셋업(ArenaMatch)이 스폰 시 SetTeam 으로 부여 —
	/// 매치 스코프 진영(로어 UnitAffiliation 과 별개).
	/// </summary>
	[RequireComponent(typeof(UnitObject))]
	public class ArenaCombatant : MonoBehaviour, ICombatant
	{
		private UnitObject unitObject;

		public int CombatantId { get; private set; } = -1;
		public int TeamId { get; private set; } = -1;
		public UnitObject UnitObject => unitObject;

		public bool IsAlive => unitObject != null && unitObject.IsAlive;
		public Vector3 Position => transform.position;
		public int Hp => unitObject.UnitStat[UnitStatType.HP_CUR];
		public int HpMax => unitObject.UnitStat[UnitStatType.HP_MAX];

		private void Awake()
		{
			unitObject = GetComponent<UnitObject>();
		}

		/// <summary> 매치 셋업이 스폰 시 호출 — 진영 + 결정적 타이브레이크 id 부여. </summary>
		public void SetTeam(int teamId, int combatantId)
		{
			TeamId = teamId;
			CombatantId = combatantId;
		}
	}
}
