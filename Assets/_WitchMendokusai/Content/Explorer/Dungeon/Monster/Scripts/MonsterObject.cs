using System.Collections;
using FMODUnity;
using UnityEngine;
using static WitchMendokusai.WMHelper;

namespace WitchMendokusai
{
	public class MonsterObject : UnitObject, IDamageable
	{
		[Header("_" + nameof(MonsterObject))]
		[SerializeField] private GameObject hitEffectPrefab;
		[SerializeField] private GameObject dieEffectPrefab;

		private Coroutine flashRoutine;
		[SerializeField] private Transform hpBar;

		public new Monster UnitData => base.UnitData as Monster;

		protected virtual void OnEnable()
		{
			SpriteRenderer.sharedMaterial = UnitData.Material;
			ObjectBufferManager.AddObject(ObjectType.Monster, gameObject);
			hpBar.localScale = Vector3.one;
			hpBar.gameObject.SetActive(false);
		}

		protected virtual void OnDisable()
		{
			if (IsPlaying)
				ObjectBufferManager.RemoveObject(ObjectType.Monster, gameObject);
			StopAllCoroutines();
			hpBar.gameObject.SetActive(false);
		}

		private void Update()
		{
			// hpBar가 항상 카메라를 바라보도록 설정 (이때, Y축만 회전함. X나 Z축은 회전하지 않음)
			hpBar.LookAt(Camera.main.transform.position, Vector3.up);
			hpBar.rotation = Quaternion.Euler(0, hpBar.rotation.eulerAngles.y, 0);
		}

		public override void ReceiveDamage(DamageInfo damageInfo)
		{
			base.ReceiveDamage(damageInfo);
			UIManager.Instance.PopDamage(damageInfo, transform.position + Vector3.forward * 1);

			SOManager.Instance.LastHitMonsterObject.RuntimeValue = this;
			hpBar.localScale = new Vector3((float)UnitStat[UnitStatType.HP_CUR] / UnitStat[UnitStatType.HP_MAX], 1, 1);
			hpBar.gameObject.SetActive(true);

			GameObject hitEffect = ObjectPoolManager.Instance.Spawn(hitEffectPrefab);
			hitEffect.transform.position = transform.position + (Vector3.Normalize(Player.Instance.transform.position - transform.position) * .5f);
			hitEffect.SetActive(true);

			/*
            if (DataManager.Instance.wgItemInven.Items.Contains(DataManager.Instance.ItemDic[36]))
            {
                if ((float)hp / MaxHp <= 0.1f * DataManager.Instance.wgItemInven.itemCountDic[36])
                {
                    ObjectManager.Instance.PopObject("AnimatedText", transform.position + Vector3.up)
                        .GetComponent<AnimatedText>().SetText("처형", Color.red);
                    RuntimeManager.PlayOneShot(hurtSFX, transform.position);
                    StopAllCoroutines();
                    Collapse();
                    return;
                }
            }
            */

			if (IsAlive == false)
				return;

			if (flashRoutine != null)
			{
				StopCoroutine(flashRoutine);
			}

			flashRoutine = StartCoroutine(FlashRoutine());

			RuntimeManager.PlayOneShot("event:/SFX/Monster/Hit", transform.position);

			switch (UnitStat[UnitStatType.HP_CUR])
			{
				case > 0:
					// Animator.SetTrigger("AHYA");
					break;
			}
		}

		protected override void OnDied()
		{
			base.OnDied();
			DropLoot();

			if (UnitData.Type == MonsterType.Boss)
				DataManager.Instance.DungeonStat[DungeonStatType.BOSS_KILL]++;
			DataManager.Instance.DungeonStat[DungeonStatType.MONSTER_KILL]++;

			RuntimeManager.PlayOneShot("event:/SFX/Monster/Die", transform.position);
			StopAllCoroutines();

			// Animator.SetTrigger("COLLAPSE");
			if (IsPlaying)
				ObjectBufferManager.RemoveObject(ObjectType.Monster, gameObject);

			GameObject dieEffect = ObjectPoolManager.Instance.Spawn(dieEffectPrefab);
			dieEffect.transform.position = transform.position + (Vector3.Normalize(Player.Instance.transform.position - transform.position) * .5f);
			dieEffect.SetActive(true);

			gameObject.SetActive(false);
		}

		protected virtual void DropLoot()
		{
			GameLogic.SpawnLootItem(UnitData.Loots, transform.position);
			GameLogic.SpawnGameItem(transform.position);
			GameLogic.SpawnExpOrb(transform.position);
		}

		private IEnumerator FlashRoutine()
		{
			SpriteRenderer.material.SetFloat("_Emission", 1);
			yield return new WaitForSeconds(.1f);
			SpriteRenderer.material.SetFloat("_Emission", 0);
			flashRoutine = null;
		}

		protected Vector3 GetRot()
		{
			return new Vector3(0, 0,
				(Mathf.Atan2(Player.Instance.transform.position.y - (transform.position.y + 0.8f),
					Player.Instance.transform.position.x - transform.position.x) * Mathf.Rad2Deg) - 90);
		}

		protected Vector3 GetDirection()
		{
			return (Player.Instance.transform.position - transform.position).normalized;
		}

		protected bool IsPlayerRight()
		{
			return Player.Instance.transform.position.x > transform.position.x;
		}
	}
}