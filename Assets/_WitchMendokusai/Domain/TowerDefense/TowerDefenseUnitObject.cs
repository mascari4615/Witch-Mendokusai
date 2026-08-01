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
		[Header("_" + nameof(TowerDefenseUnitObject) + " — 체력 표시")]
		[Tooltip("체력 가득할 때 색.")]
		[SerializeField] private Color hpFullColor = new Color(0.42f, 0.92f, 0.55f, 1f);
		[Tooltip("체력 절반쯤일 때 색.")]
		[SerializeField] private Color hpMidColor = new Color(1f, 0.86f, 0.35f, 1f);
		[Tooltip("체력 바닥일 때 색.")]
		[SerializeField] private Color hpLowColor = new Color(1f, 0.34f, 0.32f, 1f);
		[Tooltip("이 비율 아래부터 '절반쯤' 색으로.")]
		[SerializeField, Range(0f, 1f)] private float hpMidThreshold = 0.6f;
		[Tooltip("이 비율 아래부터 '바닥' 색으로.")]
		[SerializeField, Range(0f, 1f)] private float hpLowThreshold = 0.3f;

		private Transform hpBar;
		private Transform hpBarFill;

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

		// 프리팹의 HPBar 는 원래 MonsterObject 가 켜고 껐다 — 그 컴포넌트를 뺐으므로 여기서 다시 소유한다.
		// 채워지는 막대(Sprite_HPBar)와 그 뒤판은 **별개 오브젝트**다. 예전엔 부모(HPBar)를 통째 줄여서
		// 뒤판까지 같이 줄어들었고, 그러면 막대는 언제나 "가득 찬 것처럼" 보인다 — 체력이 안 읽히던 원인.
		private void EnsureHpBar()
		{
			if (hpBar == null)
				hpBar = transform.Find("Mesh/Pivot/Scaler/HPBar") ?? FindChildByName(transform, "HPBar");
			if (hpBarFill == null && hpBar != null)
				hpBarFill = FindChildByName(hpBar, "Sprite_HPBar");
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

		/// <summary>
		/// 체력 표시 갱신 — 개척에서는 **항상 보인다**(사용자 실증: "건물 별 체력 상황을 모르겠음").
		/// 다쳤을 때만 뜨는 방식은 던전에서는 맞지만, 어느 방어선이 무너지는 중인지 한눈에 봐야 하는
		/// 타워디펜스에서는 정보가 필요할 때 이미 늦다. 색도 같이 바뀌어 멀리서도 위급함이 읽힌다.
		/// </summary>
		private void UpdateHpBar()
		{
			EnsureHpBar();
			if (hpBar == null)
				return;

			int max = UnitStat[UnitStatType.HP_MAX];
			int cur = UnitStat[UnitStatType.HP_CUR];
			if (max <= 0)
			{
				hpBar.gameObject.SetActive(false);
				return;
			}

			hpBar.gameObject.SetActive(true);

			float ratio = Mathf.Clamp01((float)cur / max);
			// 뒤판은 그대로 두고 채워지는 막대만 줄인다 — 둘 다 줄면 항상 가득 찬 것처럼 보인다.
			Transform fill = hpBarFill != null ? hpBarFill : hpBar;
			fill.localScale = new Vector3(ratio, 1f, 1f);

			SpriteRenderer fillRenderer = fill.GetComponent<SpriteRenderer>();
			if (fillRenderer != null)
			{
				fillRenderer.color = ratio <= hpLowThreshold ? hpLowColor
					: ratio <= hpMidThreshold ? hpMidColor
					: hpFullColor;
			}
		}

		// 사망 = 비활성만. 전리품·경험치·킬 카운트 같은 던전 의미론은 개척에 없다
		// (풀 반환은 매치 Dispose 가 단일 경로로 책임 — 여기서 Despawn 하면 이중 반환).
		private void HandleDeath()
		{
			gameObject.SetActive(false);
		}
	}
}
