using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 특수시공 개척(TD) 전용 전투 유닛 — 코어/포탑/채집 인형/마수가 전부 이걸 쓴다.
	///
	/// ★ 왜 신규인가 (PlayMode 실측으로 확정, TASK-WM-194):
	///   `UnitObject` 는 abstract 라 직접 못 쓰고, 기존 구현체 둘 다 TD 에 부적합하다.
	///   - `PlayerObject` : `Init` 이 `UnitData.Sprites[0..1]`(머리·몸통)을 인덱싱 →
	///     Sprites 빈 유닛이면 ArgumentOutOfRangeException 으로 스폰 코루틴이 죽고,
	///     더 나쁘게는 `PlayerObjectBoundEvent` 를 발행해 **세울 때마다 "이게 플레이어"** 라고
	///     전역에 알린다(타워 한 기 세울 때마다 플레이어 바인딩 탈취).
	///   - `MonsterObject` : `UnitData as Monster` 전제(OnEnable 이 `UnitData.Material` 접근)라
	///     Doll 데이터면 NRE + 던전 전용 side-effect(loot/킬카운트) 를 달고 다닌다.
	///   WM-165 가 남긴 후속 메모("실 유닛 타입 확정 시 ArenaUnitObject 또는 IsDungeon 가드로 격리")
	///   가 가리키던 바로 그 자리 — 매치 전용 유닛 표면. TD 가 첫 실사용처라 여기 둔다.
	///   ⚠ 투기장과의 공용화(MatchUnitObject 승격)는 TASK-WM-196 범위.
	///
	/// 책임은 최소 — 스탯/스킬/이동/체력은 전부 base(UnitObject)가 하고, 여기서는
	/// DI base-deps 릴레이 + 사망 시 비활성화만. 던전 loot·플레이어 바인딩·HP바 전부 없음.
	/// </summary>
	public class TowerDefenseUnitObject : UnitObject
	{
		[Inject]
		public void Construct(PlayerProvider playerProvider, TimeManager timeManager,
			UnitStatCalculator unitStatCalculator, ObjectPoolManager objectPoolManager)
		{
			SetBaseDeps(timeManager, unitStatCalculator, objectPoolManager, playerProvider);
		}

		protected virtual void OnEnable()
		{
			Health.OnDied += HandleDeath;
		}

		protected virtual void OnDisable()
		{
			Health.OnDied -= HandleDeath;
		}

		// 사망 = 비활성만. 전리품·경험치·킬 카운트 같은 던전 의미론은 개척에 없다
		// (풀 반환은 매치 Dispose 가 단일 경로로 책임 — 여기서 Despawn 하면 이중 반환).
		private void HandleDeath()
		{
			gameObject.SetActive(false);
		}
	}
}
