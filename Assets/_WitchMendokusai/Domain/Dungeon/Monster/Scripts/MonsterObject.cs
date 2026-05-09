using System.Collections;
using FMODUnity;
using UnityEngine;
using static WitchMendokusai.WMHelper;

namespace WitchMendokusai
{
	public class MonsterObject : UnitObject
	{
		[Header("_" + nameof(MonsterObject))]
		[SerializeField] private Transform hpBar;

		public new Monster UnitData => base.UnitData as Monster;

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
			if (IsPlaying)
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
			SOManager.Instance.LastHitMonsterObject.RuntimeValue = this;
			hpBar.localScale = new Vector3((float)UnitStat[UnitStatType.HP_CUR] / UnitStat[UnitStatType.HP_MAX], 1, 1);
			hpBar.gameObject.SetActive(true);
		}

		protected virtual void HandleDeathEffects()
		{
			DropLoot();

			if (UnitData.Type == MonsterType.Boss)
				DataManager.Instance.DungeonStat[DungeonStatType.BOSS_KILL]++;
			DataManager.Instance.DungeonStat[DungeonStatType.MONSTER_KILL]++;

			StopAllCoroutines();

			// Animator.SetTrigger("COLLAPSE");
			if (IsPlaying)
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
				(Mathf.Atan2(PlayerProvider.Instance.Current.transform.position.y - (transform.position.y + 0.8f),
					PlayerProvider.Instance.Current.transform.position.x - transform.position.x) * Mathf.Rad2Deg) - 90);
		}

		protected Vector3 GetDirection()
		{
			return (PlayerProvider.Instance.Current.transform.position - transform.position).normalized;
		}

		protected bool IsPlayerRight()
		{
			return PlayerProvider.Instance.Current.transform.position.x > transform.position.x;
		}
	}
}