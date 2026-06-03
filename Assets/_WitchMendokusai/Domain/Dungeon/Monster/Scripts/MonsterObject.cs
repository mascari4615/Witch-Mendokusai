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

		public new Monster UnitData => base.UnitData as Monster;

		// TASK-WM-107 Slice 4 — playerProvider 는 base UnitObject.playerProvider (protected) 재사용 (CS0108 hide 해소).
		private SOManager soManager;
		private DataManager dataManager;
		private GameLogic gameLogic;

		[Inject]
		public void Construct(PlayerProvider playerProvider, SOManager soManager, DataManager dataManager, TimeManager timeManager, UnitStatCalculator unitStatCalculator,
			ObjectPoolManager objectPoolManager, GameLogic gameLogic)
		{
			this.soManager = soManager;
			this.dataManager = dataManager;
			this.gameLogic = gameLogic;
			SetBaseDeps(timeManager, unitStatCalculator, objectPoolManager, playerProvider);
		}

		protected virtual void OnEnable()
		{
			SpriteRenderer.sharedMaterial = UnitData.Material;
			ObjectBufferManager.AddObject(ObjectType.Monster, gameObject);
			hpBar.localScale = Vector3.one;
			hpBar.gameObject.SetActive(false);

			Health.OnTakeDamage += HandleDamageEffects;
			Health.OnDied += HandleDeathEffects;
		}

		protected virtual void OnDisable()
		{
			ObjectBufferManager.RemoveObject(ObjectType.Monster, gameObject);
			StopAllCoroutines();
			hpBar.gameObject.SetActive(false);

			Health.OnTakeDamage -= HandleDamageEffects;
			Health.OnDied -= HandleDeathEffects;
		}

		private void Update()
		{
			// hpBar가 항상 카메라를 바라보도록 설정 (이때, Y축만 회전함. X나 Z축은 회전하지 않음)
			hpBar.LookAt(Camera.main.transform.position, Vector3.up);
			hpBar.rotation = Quaternion.Euler(0, hpBar.rotation.eulerAngles.y, 0);
		}

		private void HandleDamageEffects(DamageInfo damageInfo)
		{
			// 던전 UI 추적(LastHitMonsterObject)은 던전에서만 — 아레나 피격이 던전 "마지막 피격 몬스터" 패널을 오염 X.
			if (DungeonManagerBridge.IsDungeon)
				soManager.LastHitMonsterObject.RuntimeValue = this;

			// hpBar 갱신은 컨텍스트 무관(아레나 관전서도 체력 표시 유용).
			hpBar.localScale = new Vector3((float)UnitStat[UnitStatType.HP_CUR] / UnitStat[UnitStatType.HP_MAX], 1, 1);
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
			gameLogic.SpawnExpOrb(transform.position);
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
