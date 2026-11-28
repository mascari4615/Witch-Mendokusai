using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 바늘 탄에 기존 이동 부품을 붙인 모양. 곧게 나는 이동 (BulletMovement) 과 연쇄 피해 (ChainOnHit) 는 떼고 부품 하나가 이동을 맡음
	// 시험 무기 (퍼짐 돌진, 적 간 추적) 를 복제하지 않는 이유: 두 무기의 탄 프리팹이 그림만 남은 반쪽 파일이라 판정이 없음 (기존 버그)
	internal abstract class MoverShotForm : ISkillMaterialForm
	{
		protected MoverShotForm(string name) => Name = name;

		public string Name { get; }
		public bool AcceptsLayers => false;

		public GameObject BuildShot(SkillMaterialSpec spec)
		{
			GameObject root = SkillMaterialAssets.OpenTemplate(spec.Text("Template"));
			try
			{
				foreach (ChainOnHit chain in root.GetComponentsInChildren<ChainOnHit>(true))
					UnityEngine.Object.DestroyImmediate(chain);
				foreach (BulletMovement mover in root.GetComponentsInChildren<BulletMovement>(true))
					UnityEngine.Object.DestroyImmediate(mover);
				DamagingObject hitbox = root.GetComponent<DamagingObject>();
				if (hitbox == null)
					throw new InvalidOperationException($"{spec.Id}: 바탕 탄 루트에 판정 없음 {spec.Text("Template")}");
				SkillMaterialAssets.SetFields(root.GetComponent<ObjectWithTime>(), ("duration", spec.Float("Lifetime")));
				AddMover(root, hitbox, spec);
				SkillMaterialAssets.AddEffects(root, spec);
				SkillMaterialAssets.ApplyLook(root, spec);
				return SkillMaterialAssets.SaveShot(root, spec.Id + "_Shot");
			}
			finally
			{
				SkillMaterialAssets.CloseTemplate(root);
			}
		}

		protected abstract void AddMover(GameObject root, DamagingObject hitbox, SkillMaterialSpec spec);
	}

	// 돌진: 퍼졌다가 멈춰 겨눈 뒤 표적으로 빠르게 (FanDashMovement). 지나가는 적을 모두 맞힘
	internal sealed class DashForm : MoverShotForm
	{
		public DashForm(string name) : base(name)
		{
		}

		protected override void AddMover(GameObject root, DamagingObject hitbox, SkillMaterialSpec spec)
		{
			SkillMaterialAssets.SetFields(hitbox, ("damage", spec.Int("Damage")), ("useHitCount", true), ("hitCount", spec.Int("HitCount")), ("disableWhenInvalid", true), ("hitOncePerTarget", true), ("knockbackForce", spec.Float("Knockback")));
			FanDashMovement dash = root.AddComponent<FanDashMovement>();
			SkillMaterialAssets.SetFields(dash, ("spreadSpeed", spec.Float("SpreadSpeed")), ("spreadSeconds", spec.Float("SpreadSeconds")), ("aimSeconds", spec.Float("AimSeconds")), ("dashSpeed", spec.Float("DashSpeed")), ("targetRange", spec.Float("Range")));
		}
	}

	// 연쇄: 탄이 적에서 적으로 날아가 맞힘 (HomingChainMovement). 횟수는 이동 부품이 셈
	internal sealed class ChainForm : MoverShotForm
	{
		public ChainForm(string name) : base(name)
		{
		}

		protected override void AddMover(GameObject root, DamagingObject hitbox, SkillMaterialSpec spec)
		{
			SkillMaterialAssets.SetFields(hitbox, ("damage", spec.Int("Damage")), ("useHitCount", false), ("knockbackForce", spec.Float("Knockback")));
			HomingChainMovement chain = root.AddComponent<HomingChainMovement>();
			SkillMaterialAssets.SetFields(chain, ("moveSpeed", spec.Float("ShotSpeed")), ("acquireRange", spec.Float("Range")), ("retargetRange", spec.Float("RetargetRange")), ("hitPauseSeconds", spec.Float("HitPause")), ("maxHits", spec.Int("ChainHits")));
		}
	}
}
