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
		private Transform hpBar;

		[Inject]
		public void Construct(PlayerProvider playerProvider, TimeManager timeManager,
			UnitStatCalculator unitStatCalculator, ObjectPoolManager objectPoolManager)
		{
			SetBaseDeps(timeManager, unitStatCalculator, objectPoolManager, playerProvider);
		}

		/// <summary>
		/// 유닛 데이터의 그림을 실제로 입힌다 — 이게 없으면 코어/포탑/채집이 전부 프리팹 원본(슬라임)
		/// 모습이라 화면에서 구분이 안 된다(사용자 실증: "건물도 그냥 슬라임이고 적과 내가 똑같다").
		/// 「건물 = 인형」 캐논도 여기서 성립: DataSO 의 Sprite 가 곧 그 건물의 인형 모습.
		/// </summary>
		public override void Init(Unit unitData)
		{
			base.Init(unitData);

			if (SpriteRenderer != null && unitData != null && unitData.Sprite != null)
				SpriteRenderer.sprite = unitData.Sprite;

			EnsureHpBar();
			UpdateHpBar();
		}

		protected virtual void OnEnable()
		{
			Health.OnDied += HandleDeath;
			Health.OnTakeDamage += HandleDamaged;
		}

		protected virtual void OnDisable()
		{
			Health.OnDied -= HandleDeath;
			Health.OnTakeDamage -= HandleDamaged;
		}

		// 프리팹의 HPBar 는 원래 MonsterObject 가 켜고 껐다 — 그 컴포넌트를 뺐으므로 관리 주체가
		// 사라져 *항상 떠 있는 노이즈*가 됐다(실측). 여기서 다시 소유해 "다쳤을 때만" 보이게 한다.
		private void EnsureHpBar()
		{
			if (hpBar == null)
				hpBar = transform.Find("Mesh/Pivot/Scaler/HPBar") ?? FindChildByName(transform, "HPBar");
		}

		private static Transform FindChildByName(Transform root, string childName)
		{
			foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
			{
				if (child.name == childName)
					return child;
			}
			return null;
		}

		private void HandleDamaged(DamageInfo damageInfo) => UpdateHpBar();

		private void UpdateHpBar()
		{
			EnsureHpBar();
			if (hpBar == null)
				return;

			int max = UnitStat[UnitStatType.HP_MAX];
			int cur = UnitStat[UnitStatType.HP_CUR];
			bool damaged = max > 0 && cur < max;

			hpBar.gameObject.SetActive(damaged);
			if (damaged)
				hpBar.localScale = new Vector3((float)cur / max, 1f, 1f);
		}

		// 사망 = 비활성만. 전리품·경험치·킬 카운트 같은 던전 의미론은 개척에 없다
		// (풀 반환은 매치 Dispose 가 단일 경로로 책임 — 여기서 Despawn 하면 이중 반환).
		private void HandleDeath()
		{
			gameObject.SetActive(false);
		}
	}
}
