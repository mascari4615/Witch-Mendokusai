using System.Collections;
using FMODUnity;
using UnityEngine;
using VContainer;
using static WitchMendokusai.WMHelper;

namespace WitchMendokusai
{
	public class MonsterObject : UnitObject
	{
		[Header("_" + nameof(MonsterObject))]
		[SerializeField] private Transform hpBar;
		[SerializeField, Min(1)] private int healthPerExperienceTier = 10;
		[SerializeField, Min(1)] private int maxExperienceMultiplier = 5;

		public new Monster UnitData => base.UnitData as Monster;
		public bool IsElite => eliteProfile != null;
		public override bool IsActionSuspended => UnitData != null && EnemyFreezeState.IsFrozen(UnitData.Type == MonsterType.Boss);

		// TASK-WM-107 Slice 4 — playerProvider 는 base UnitObject.playerProvider (protected) 재사용 (CS0108 hide 해소).
		private SOManager soManager;
		private DataManager dataManager;
		private GameLogic gameLogic;
		private EliteProfile eliteProfile;
		private bool eliteGuaranteedChest;
		private DungeonThreatEffect threat = DungeonThreatEffect.Identity;
		private float hpBarWidth = 1f;

		[Inject]
		public void Construct(PlayerProvider playerProvider, SOManager soManager, DataManager dataManager, TimeManager timeManager, UnitStatCalculator unitStatCalculator,
			ObjectPoolManager objectPoolManager, GameLogic gameLogic)
		{
			this.soManager = soManager;
			this.dataManager = dataManager;
			this.gameLogic = gameLogic;
			SetBaseDeps(timeManager, unitStatCalculator, objectPoolManager, playerProvider);
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			if (UnitData.Type == MonsterType.Boss)
				GroundGenerator.RegisterAnchor(UnitMovement);
			SpriteRenderer.sharedMaterial = UnitData.Material;
			ObjectBufferManager.AddObject(ObjectType.Monster, gameObject);
			hpBarWidth = 1f;
			hpBar.localScale = Vector3.one;
			hpBar.gameObject.SetActive(false);

			Health.OnTakeDamage += HandleDamageEffects;
			Health.OnDied += HandleDeathEffects;

			if (UnitData.Type == MonsterType.Boss && DungeonManagerBridge.IsDungeon)
			{
				SurvivalFeel.Instance.OnBossSpawned(transform.position);
				EventBusBridge.Publish(new BossSpawnedEvent { Monster = UnitData });
			}
		}

		protected override void Start()
		{
			base.Start();
			if (eliteProfile != null)
				ApplyEliteHealth();
		}

		// 풀 첫 사용은 Start 의 재-Init 포함, 적용 자리는 Init 안
		public void SetThreat(DungeonThreatEffect threatEffect) => threat = threatEffect;

		public override void Init(Unit unitData)
		{
			base.Init(unitData);
			ApplyThreat();
		}

		private void ApplyThreat()
		{
			if (threat.Level == 0)
				return;
			UnitStat[UnitStatType.HP_MAX_STAT] = threat.ScaleEnemyHealth(UnitStat[UnitStatType.HP_MAX_STAT]);
			UnitStat[UnitStatType.HP_MAX] = threat.ScaleEnemyHealth(UnitStat[UnitStatType.HP_MAX]);
			UnitStat[UnitStatType.HP_CUR] = UnitStat[UnitStatType.HP_MAX];
			UnitStat[UnitStatType.MOVEMENT_SPEED] = threat.ScaleEnemySpeed(UnitStat[UnitStatType.MOVEMENT_SPEED]);
		}

		public void MakeElite(EliteProfile profile, bool guaranteedChest)
		{
			eliteProfile = profile;
			eliteGuaranteedChest = guaranteedChest;
			ApplyEliteHealth();
			Health.SetScaleMultiplier(profile.ScaleMultiplier);
			SpriteRenderer.color = profile.Tint;
			hpBarWidth = profile.HpBarScale;
			hpBar.localScale = new Vector3(hpBarWidth, 1, 1);
			hpBar.gameObject.SetActive(true);
			SurvivalFeel.Instance.RegisterElite(this);
		}

		private void ApplyEliteHealth()
		{
			UnitStat[UnitStatType.HP_MAX_STAT] = Mathf.CeilToInt(UnitStat[UnitStatType.HP_MAX_STAT] * eliteProfile.HealthMultiplier);
			UpdateStat();
			UnitStat[UnitStatType.HP_CUR] = UnitStat[UnitStatType.HP_MAX];
		}

		private void ClearElite()
		{
			if (eliteProfile == null)
				return;
			eliteProfile = null;
			eliteGuaranteedChest = false;
			Health.SetScaleMultiplier(1f);
			SpriteRenderer.color = Color.white;
			if (SurvivalFeel.Instance != null)
				SurvivalFeel.Instance.UnregisterElite(this);
		}

		protected override void OnDisable()
		{
			base.OnDisable();
			GroundGenerator.UnregisterAnchor(UnitMovement);
			ObjectBufferManager.RemoveObject(ObjectType.Monster, gameObject);
			StopAllCoroutines();
			hpBar.gameObject.SetActive(false);
			ClearElite();
			threat = DungeonThreatEffect.Identity;

			Health.OnTakeDamage -= HandleDamageEffects;
			Health.OnDied -= HandleDeathEffects;
		}

		private void Update()
		{
			if (hpBar.gameObject.activeSelf == false)
				return;
			// hpBar가 항상 카메라를 바라보도록 설정 (이때, Y축만 회전함. X나 Z축은 회전하지 않음)
			hpBar.LookAt(Camera.main.transform.position, Vector3.up);
			hpBar.rotation = Quaternion.Euler(0, hpBar.rotation.eulerAngles.y, 0);
		}

		private void HandleDamageEffects(DamageInfo damageInfo)
		{
			// 던전 UI 추적(LastHitMonsterObject)은 던전에서만 — 아레나 피격이 던전 "마지막 피격 몬스터" 패널을 오염 X.
			if (DungeonManagerBridge.IsDungeon)
			{
				soManager.LastHitMonsterObject.RuntimeValue = this;
				if (IsAlive)
					SurvivalFeel.Instance.OnMonsterHit(transform.position, damageInfo);
			}

			// hpBar 갱신은 컨텍스트 무관(아레나 관전서도 체력 표시 유용).
			hpBar.localScale = new Vector3((float)UnitStat[UnitStatType.HP_CUR] / UnitStat[UnitStatType.HP_MAX] * hpBarWidth, 1, 1);
			hpBar.gameObject.SetActive(true);
		}

		protected virtual void HandleDeathEffects()
		{
			// 던전 전용 side-effect(loot 드랍·킬 카운트) 는 던전에서만 — 아레나(비-던전)서 사망 시
			// 전리품/경험치 오브가 바닥에 흩뿌려지거나 DungeonStat 이 오염되지 않게 격리(PlayerObject 선례).
			if (DungeonManagerBridge.IsDungeon)
			{
				DropLoot();

				if (UnitData.Type == MonsterType.Boss)
					SurvivalFeel.Instance.OnBossKilled(transform.position);
				else if (eliteProfile != null)
					SurvivalFeel.Instance.OnEliteKilled(transform.position);
				else
					SurvivalFeel.Instance.OnMonsterKilled(transform.position);

				if (eliteProfile != null)
				{
					gameLogic.SpawnEliteReward(eliteProfile, transform.position, eliteGuaranteedChest);
					EventBusBridge.Publish(new EliteKilledEvent { Monster = UnitData, ExpValue = eliteProfile.ExpValue });
				}

				if (UnitData.Type == MonsterType.Boss)
					dataManager.DungeonStat[DungeonStatType.BOSS_KILL]++;
				dataManager.DungeonStat[DungeonStatType.MONSTER_KILL]++;
			}

			StopAllCoroutines();

			// Animator.SetTrigger("COLLAPSE");
			ObjectBufferManager.RemoveObject(ObjectType.Monster, gameObject);

			gameObject.SetActive(false); // 사망 비활성은 컨텍스트 무관(아레나 유닛도 죽으면 사라짐).
		}

		protected virtual void DropLoot()
		{
			gameLogic.SpawnLootItem(UnitData.Loots, transform.position);
			gameLogic.SpawnGameItem(transform.position);
			// 경험치는 위협 단계 체력 배율과 무관
			int rewardMultiplier = Mathf.Clamp(Mathf.CeilToInt((float)threat.UnscaleEnemyHealth(UnitStat[UnitStatType.HP_MAX_STAT]) / healthPerExperienceTier), 1, maxExperienceMultiplier);
			gameLogic.SpawnExpOrb(transform.position, rewardMultiplier);
		}

		protected Vector3 GetRot()
		{
			return new Vector3(0, 0,
				(Mathf.Atan2(playerProvider.Current.transform.position.y - (transform.position.y + 0.8f),
					playerProvider.Current.transform.position.x - transform.position.x) * Mathf.Rad2Deg) - 90);
		}

		protected Vector3 GetDirection()
		{
			return (playerProvider.Current.transform.position - transform.position).normalized;
		}

		protected bool IsPlayerRight()
		{
			return playerProvider.Current.transform.position.x > transform.position.x;
		}
	}
}
