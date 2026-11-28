using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 부메랑. 바늘 탄 바탕에서 곧은 이동 (BulletMovement) 을 떼고 나갔다 돌아오는 이동 (ReturningMovement) 을 붙임
	// 관통하며 같은 적은 한 번씩 (오가는 길 합쳐서). 수명 (Lifetime) 은 돌아오기 전에 끊기지 않게 잡는 안전 상한
	// 방향은 생성 때의 회전 (Spawner 가 조준 방향으로 LookRotation) 을 ReturningMovement 가 읽음
	internal sealed class ReturnForm : ISkillMaterialForm
	{
		private const string VISUAL_NAME = "Visual";

		public ReturnForm(string name) => Name = name;

		public string Name { get; }
		public bool AcceptsLayers => false;

		public GameObject BuildShot(SkillMaterialSpec spec)
		{
			GameObject root = SkillMaterialAssets.OpenTemplate(spec.Text("Template"));
			try
			{
				foreach (ChainOnHit chain in root.GetComponentsInChildren<ChainOnHit>(true))
					UnityEngine.Object.DestroyImmediate(chain);
				BulletMovement straight = root.GetComponent<BulletMovement>();
				if (straight == null)
					throw new InvalidOperationException($"{spec.Id}: 바탕 탄에 BulletMovement 없음 (바늘 탄 바탕이어야 함) {spec.Text("Template")}");
				UnityEngine.Object.DestroyImmediate(straight);
				Transform visual = root.transform.Find(VISUAL_NAME);
				if (visual == null)
					throw new InvalidOperationException($"{spec.Id}: 바탕 탄에 그림 자식 {VISUAL_NAME} 없음 {spec.Text("Template")}");
				ReturningMovement returning = root.AddComponent<ReturningMovement>();
				SkillMaterialAssets.SetFields(returning,
					("outboundSpeed", spec.Float("OutboundSpeed")),
					("outboundSeconds", spec.Float("OutboundSeconds")),
					("returnSpeed", spec.Float("ReturnSpeed")),
					("catchDistance", spec.Float("CatchDistance")),
					("spinTransform", visual),
					("spinDegreesPerSecond", spec.Float("SpinDegrees")));
				SkillMaterialAssets.SetFields(root.GetComponent<DamagingObject>(), ("damage", spec.Int("Damage")), ("useHitCount", true), ("hitCount", spec.Int("HitCount")), ("disableWhenInvalid", true), ("hitOncePerTarget", true), ("knockbackForce", spec.Float("Knockback")));
				SkillMaterialAssets.SetFields(root.GetComponent<ObjectWithTime>(), ("duration", spec.Float("Lifetime")));
				SkillMaterialAssets.AddEffects(root, spec);
				SkillMaterialAssets.ApplyLook(root, spec);
				return SkillMaterialAssets.SaveShot(root, spec.Id + "_Shot");
			}
			finally
			{
				SkillMaterialAssets.CloseTemplate(root);
			}
		}
	}

	// 레이저. 즉발 직선 판정이 수명 동안 켜졌다 꺼짐. 바탕은 책갈피빔 탄 (BeamShot): 앞 (+z) 으로 뻗는 BoxCollider 와 같은 길이의 판 그림 (FadeOverLife)
	// 판정과 그림은 탄의 자식 좌표라 생성 회전 (Spawner 조준) 을 그대로 따름. 길이와 폭은 판정과 그림을 같이 바꿈
	// 같은 적은 한 번만 (수명 안에 다시 들어와도 재적중 없음)
	internal sealed class BeamForm : ISkillMaterialForm
	{
		public BeamForm(string name) => Name = name;

		public string Name { get; }
		public bool AcceptsLayers => false;

		public GameObject BuildShot(SkillMaterialSpec spec)
		{
			GameObject root = SkillMaterialAssets.OpenTemplate(spec.Text("Template"));
			try
			{
				float length = spec.Float("BeamLength");
				float width = spec.Float("BeamWidth");
				BoxCollider hitbox = root.GetComponent<BoxCollider>();
				if (hitbox == null)
					throw new InvalidOperationException($"{spec.Id}: 바탕 탄에 BoxCollider 없음 {spec.Text("Template")}");
				// 판정은 원점에서 앞으로 length 만큼. 중심은 그 절반
				hitbox.size = new Vector3(width, hitbox.size.y, length);
				hitbox.center = new Vector3(hitbox.center.x, hitbox.center.y, length * 0.5f);

				// 그림 판은 눕힌 쿼드 (x 폭, y 길이). 판정과 같은 자리, 같은 크기
				FadeOverLife fade = root.GetComponentInChildren<FadeOverLife>(true);
				if (fade == null)
					throw new InvalidOperationException($"{spec.Id}: 바탕 탄에 그림 판 (FadeOverLife) 없음 {spec.Text("Template")}");
				Transform visual = fade.transform;
				visual.localPosition = new Vector3(visual.localPosition.x, visual.localPosition.y, length * 0.5f);
				visual.localScale = new Vector3(width, length, visual.localScale.z);
				SkillMaterialAssets.SetFields(fade, ("duration", spec.Float("Lifetime")));

				SkillMaterialAssets.SetFields(root.GetComponent<DamagingObject>(), ("damage", spec.Int("Damage")), ("useHitCount", false), ("disableWhenInvalid", false), ("hitOncePerTarget", true), ("knockbackForce", spec.Float("Knockback")), ("hitstopDuration", spec.Float("Hitstop")));
				SkillMaterialAssets.SetFields(root.GetComponent<ObjectWithTime>(), ("duration", spec.Float("Lifetime")));
				SkillMaterialAssets.AddEffects(root, spec);
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
