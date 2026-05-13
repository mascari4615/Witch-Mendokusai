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

		private PlayerProvider playerProvider;
		private SOManager soManager;
		private DataManager dataManager;

		[Inject]
		public void Construct(PlayerProvider playerProvider, SOManager soManager, DataManager dataManager)
		{
			this.playerProvider = playerProvider;
			this.soManager = soManager;
			this.dataManager = dataManager;
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
			soManager.LastHitMonsterObject.RuntimeValue = this;
			hpBar.localScale = new Vector3((float)UnitStat[UnitStatType.HP_CUR] / UnitStat[UnitStatType.HP_MAX], 1, 1);
			hpBar.gameObject.SetActive(true);
		}

		protected virtual void HandleDeathEffects()
		{
			DropLoot();

			if (UnitData.Type == MonsterType.Boss)
				dataManager.DungeonStat[DungeonStatType.BOSS_KILL]++;
			dataManager.DungeonStat[DungeonStatType.MONSTER_KILL]++;

			StopAllCoroutines();

			// Animator.SetTrigger("COLLAPSE");
			ObjectBufferManager.RemoveObject(ObjectType.Monster, gameObject);

			gameObject.SetActive(false);
		}

		protected virtual void DropLoot()
		{
			GameLogic.SpawnLootItem(UnitData.Loots, transform.position);
			GameLogic.SpawnGameItem(transform.position);
			GameLogic.SpawnExpOrb(transform.position);
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
