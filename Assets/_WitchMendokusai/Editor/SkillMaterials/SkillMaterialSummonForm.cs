using System;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	// 소환 (소환수, 포탑, 분신). 주기마다 곁에 소환물을 하나 놓고, 그것이 수명 동안 가까운 적에게 탄을 쏘다 사라짐
	// 바탕 무기는 짚인형 소환 (EquipmentStrawDollSummon). 소환 주기, 수명, 동시 수, 발사 간격, 사거리를 이미 칸으로 가짐
	// 포탑 (EquipmentTurret) 은 한 번 놓으면 안 사라지고 TURRET 전용 능력치를 받아 '주기마다 놓고 사라짐' 에 안 맞음
	// 소환물이 쏘는 탄은 바늘 (판정이 탄 자체에 있음). 짚인형 원래 탄 (종이 폭탄) 은 폭발 프리팹에 판정이 있어 프리팹이 둘로 늘고,
	// 둘째 프리팹은 _Shot 접미사가 아니라 SkillMaterialBuilder.Clean 이 지움
	internal sealed class SummonForm : ISkillMaterialWeaponForm
	{
		public SummonForm(string name) => Name = name;

		public string Name { get; }
		public bool AcceptsLayers => false;

		public GameObject BuildShot(SkillMaterialSpec spec) => throw new InvalidOperationException($"{Name} 은 BuildWeapon 으로 굽는 모양");

		public void BuildWeapon(SkillMaterialSpec spec)
		{
			GameObject weapon = SkillMaterialAssets.OpenTemplate(spec.Text("Template"));
			try
			{
				if (weapon.GetComponent<SkillObject>() == null)
					throw new InvalidOperationException($"{spec.Id}: 바탕 무기 루트에 SkillObject 없음 {spec.Text("Template")}");
				EquipmentStrawDollSummon summon = weapon.GetComponent<EquipmentStrawDollSummon>();
				if (summon == null)
					throw new InvalidOperationException($"{spec.Id}: 바탕 무기에 EquipmentStrawDollSummon 없음 {spec.Text("Template")}");
				GameObject shot = BuildSummonShot(spec);
				SkillMaterialAssets.SetFields(summon,
					("bulletPrefab", shot),
					("summonCooldown", spec.Float("Interval")),
					("lifetime", spec.Float("SummonLifetime")),
					("maxActive", spec.Int("MaxActive")),
					("fireInterval", spec.Float("FireInterval")),
					("range", spec.Float("Range")));
				weapon.name = spec.Id;
				SkillMaterialAssets.Save(weapon, SkillMaterialAssets.WEAPON_DIR, spec.Id);
			}
			finally
			{
				SkillMaterialAssets.CloseTemplate(weapon);
			}
		}

		// 방향은 소환물이 쏠 때 정함 (Fire 가 SetMoveDirection). 그래서 자동 조준은 끔
		private static GameObject BuildSummonShot(SkillMaterialSpec spec)
		{
			GameObject root = SkillMaterialAssets.OpenTemplate(spec.Text("ShotTemplate"));
			try
			{
				foreach (ChainOnHit chain in root.GetComponentsInChildren<ChainOnHit>(true))
					UnityEngine.Object.DestroyImmediate(chain);
				DamagingObject hitbox = root.GetComponent<DamagingObject>();
				if (hitbox == null)
					throw new InvalidOperationException($"{spec.Id}: 소환물 탄 루트에 판정 (DamagingObject) 없음 {spec.Text("ShotTemplate")}");
				SkillMaterialAssets.SetFields(root.GetComponent<BulletMovement>(), ("moveSpeed", spec.Float("ShotSpeed")), ("useAutoAim", false));
				SkillMaterialAssets.SetFields(hitbox, ("damage", spec.Int("Damage")), ("useHitCount", true), ("hitCount", spec.Int("HitCount")), ("disableWhenInvalid", true), ("hitOncePerTarget", true), ("knockbackForce", spec.Float("Knockback")));
				SkillMaterialAssets.SetFields(root.GetComponent<ObjectWithTime>(), ("duration", spec.Float("Lifetime")));
				SkillMaterialAssets.AddEffects(root, spec);
				// 바탕 탄은 직선탄 (LineShotForm) 과 같은 바늘이라 트레일 머티리얼에 _Color 가 있음. strict 유지
				SkillMaterialAssets.ApplyLook(root, spec);
				return SkillMaterialAssets.SaveShot(root, spec.Id + "_Shot");
			}
			finally
			{
				SkillMaterialAssets.CloseTemplate(root);
			}
		}
	}
}
