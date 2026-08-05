using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 매치(투기장·개척 등)의 전투 참가자 — 진영(TeamId) + 생존/위치/HP. IDamageSource(공격자 마커) 확장.
	/// Domain(MatchCombatant)이 UnitObject 를 래핑해 구현. DomainSDK 가 Domain 타입 직접 의존 회피.
	/// TargetingSystem 은 이 인터페이스만 보고 동작 → EditMode 에서 스텁으로 테스트 가능.
	/// </summary>
	public interface ICombatant : IDamageSource
	{
		// 결정적 타이브레이크용 안정 id. 매치 셋업이 스폰 시 0..N 부여(InstanceID 아님 — 리플레이 결정성).
		int CombatantId { get; }
		int TeamId { get; }
		bool IsAlive { get; }
		Vector3 Position { get; }
		int Hp { get; }    // 현재 HP
		int HpMax { get; } // 최대 HP (0 이하 가드는 소비자 책임)
	}
}
