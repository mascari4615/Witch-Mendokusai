using System.Linq;
using UnityEngine;
using static WitchMendokusai.WMHelper;

namespace WitchMendokusai
{
	public class ResourceNodeObject : UnitObject
	{
		[Header("_" + nameof(ResourceNodeObject))]
		[SerializeField] private Transform hpBar;

		public new ResourceNode UnitData => base.UnitData as ResourceNode;

		protected virtual void OnEnable()
		{
			SpriteRenderer.sprite = UnitData.Sprite;
			SpriteRenderer.sharedMaterial = UnitData.Material;
			hpBar.localScale = Vector3.one;
			hpBar.gameObject.SetActive(false);

			ObjectBufferManager.AddObject(ObjectType.ResourceNode, gameObject);
			Health.OnTakeDamage += HandleDamageEffects;
			Health.OnDied += HandleDeathEffects;
		}

		protected virtual void OnDisable()
		{
			ObjectBufferManager.RemoveObject(ObjectType.ResourceNode, gameObject);
			StopAllCoroutines();
			hpBar.gameObject.SetActive(false);

			Health.OnTakeDamage -= HandleDamageEffects;
			Health.OnDied -= HandleDeathEffects;
		}

		private void Update()
		{
			hpBar.LookAt(Camera.main.transform.position, Vector3.up);
			hpBar.rotation = Quaternion.Euler(0, hpBar.rotation.eulerAngles.y, 0);
		}

		private void HandleDamageEffects(DamageInfo damageInfo)
		{
			hpBar.localScale = new Vector3((float)UnitStat[UnitStatType.HP_CUR] / UnitStat[UnitStatType.HP_MAX], 1, 1);
			hpBar.gameObject.SetActive(true);
		}

		protected virtual void HandleDeathEffects()
		{
			DropLoot();
			StopAllCoroutines();
			gameObject.SetActive(false);
		}

		protected virtual void DropLoot()
		{
			GameLogic.SpawnLootItem(UnitData.Loots, transform.position);
		}

		public override void ReceiveDamage(DamageInfo damageInfo)
		{
			bool isPickaxe = damageInfo.equipmentData != null && damageInfo.equipmentData.EquipmentType == EquipmentType.Pickaxe;

			// Pickaxe가 아니면 데미지 1/10, 최소 1
			if (isPickaxe == false)
			{
				damageInfo.damage = Mathf.Max(1, damageInfo.damage / 10);
			}

			base.ReceiveDamage(damageInfo);
		}
	}
}