using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// UnitObject 를 <b>한 판(match)의 전투 참가자</b>로 래핑. 인형·사역마·적이 모두 동일 컴포넌트
	/// (TeamId 만 다름). TeamId·CombatantId 는 스폰 관문 <see cref="CombatUnitSpawner"/>.Enlist 를 거쳐
	/// SetTeam 으로 부여된다 — 매치 스코프 진영이라 로어의 UnitAffiliation 과는 별개다.
	///
	/// ★ 이름에 「Arena」가 없는 이유: 투기장과 개척(TD)이 **같은 컴포넌트를 쓴다**(TASK-WM-196 —
	///   `ArenaCombatant` → `MatchCombatant` 개명). 부르는 쪽은 `ArenaMatch` 와 `TowerDefenseMatch`
	///   둘 다다. 여기에 어느 한 게임의 사정을 넣으면 다른 게임이 그걸 상속한다.
	/// </summary>
	[RequireComponent(typeof(UnitObject))]
	public class MatchCombatant : MonoBehaviour, ICombatant
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
