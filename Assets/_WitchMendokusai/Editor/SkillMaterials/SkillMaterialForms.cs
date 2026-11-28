using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 시전 모양 하나의 굽는 법. 탄 (또는 장판, 돌진체) 프리팹을 SHOT_DIR 에 저장하고 돌려줌
	// 무기 (Spawner) 는 SkillMaterialBuilder 가 공통으로 구움
	internal interface ISkillMaterialForm
	{
		string Name { get; }
		// 이펙트 겹을 받는지. 탄은 맞은 자리에 겹이 나야 해서 적중 겹이 생기기 전까지 안 받음 (목록 검증에서 거름)
		bool AcceptsLayers { get; }
		GameObject BuildShot(SkillMaterialSpec spec);
	}

	// 모양 등록표. 새 모양은 클래스 하나와 여기 한 줄, skill-materials.json 의 Forms 에 기본값
	// 같은 굽는 법을 이름만 달리 여럿 등록할 수 있음 (직선탄과 관통탄은 적중 수 기본값만 다름)
	internal static class SkillMaterialForms
	{
		private static readonly Dictionary<string, ISkillMaterialForm> forms = Register(
			new LineShotForm("LineShot"),
			new LineShotForm("PierceShot"),
			// 갈고리: 음수 넉백으로 쏜 쪽에 끌어옴. 부채꼴: 여러 발을 퍼짐 각도로 동시에
			new LineShotForm("Hook"),
			new LineShotForm("Fan"),
			new AreaForm("Zone"),
			new AreaForm("NovaSelf"),
			new AreaForm("GroundCircle"),
			// 오라: 나를 따라다니는 장판 (오라, 정신 집중, 전역 근사)
			new AreaForm("Aura", followUser: true),
			new DashForm("Dash"),
			new ChainForm("Chain"),
			new CloneForm("Lob"),
			new CloneForm("Slash"),
			new CloneForm("Orbit"),
			new CloneForm("Trail"),
			new CloneForm("Strike"),
			new SummonForm("Summon"),
			new ReturnForm("Return"),
			new BeamForm("Beam"),
			new SelfForm("SelfBuff"),
			new SelfForm("SelfShield"),
			new SelfForm("SelfHeal"),
			new SelfForm("SelfTransform"));

		public static bool Has(string name) => forms.ContainsKey(name);

		public static ISkillMaterialForm Get(string name) => forms[name];

		private static Dictionary<string, ISkillMaterialForm> Register(params ISkillMaterialForm[] list)
		{
			Dictionary<string, ISkillMaterialForm> table = new();
			foreach (ISkillMaterialForm form in list)
			{
				if (table.ContainsKey(form.Name))
					throw new InvalidOperationException($"모양 이름 중복: {form.Name}");
				table.Add(form.Name, form);
			}
			return table;
		}
	}

	// 직선 투사체. 적중 수만큼 맞히고 멈춤 (관통탄은 적중 수가 큼)
	internal sealed class LineShotForm : ISkillMaterialForm
	{
		public LineShotForm(string name) => Name = name;

		public string Name { get; }
		public bool AcceptsLayers => false;

		public GameObject BuildShot(SkillMaterialSpec spec)
		{
			GameObject root = SkillMaterialAssets.OpenTemplate(spec.Text("Template"));
			try
			{
				foreach (ChainOnHit chain in root.GetComponentsInChildren<ChainOnHit>(true))
					UnityEngine.Object.DestroyImmediate(chain);
				SkillMaterialAssets.SetFields(root.GetComponent<BulletMovement>(), ("moveSpeed", spec.Float("ShotSpeed")), ("useAutoAim", true));
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

	// 원형 범위. 수명 동안 재적중 간격마다 맞힘. 간격을 수명보다 길게 두면 한 번 터지는 폭발
	// 놓이는 자리 (내 발밑, 가까운 적) 는 무기의 SpawnPosition 이 정함
	internal sealed class AreaForm : ISkillMaterialForm
	{
		private readonly bool followUser;

		public AreaForm(string name, bool followUser = false)
		{
			Name = name;
			this.followUser = followUser;
		}

		public string Name { get; }
		public bool AcceptsLayers => true;

		public GameObject BuildShot(SkillMaterialSpec spec)
		{
			GameObject root = SkillMaterialAssets.OpenTemplate(spec.Text("Template"));
			try
			{
				SphereCollider area = root.GetComponent<SphereCollider>();
				root.transform.localScale = Vector3.one * (spec.Float("Radius") / area.radius);
				SkillMaterialAssets.SetFields(root.GetComponent<RepeatingDamagingObject>(), ("damage", spec.Int("Damage")), ("rehitInterval", spec.Float("RehitInterval")), ("knockbackForce", spec.Float("Knockback")), ("hitstopDuration", spec.Float("Hitstop")));
				// 바탕 (독 장판) 의 착탄 조각은 초록에 흔들림 0. 속성색과 모양별 흔들림으로
				SurvivalFeelBurst feel = root.GetComponent<SurvivalFeelBurst>();
				SkillMaterialAssets.SetFields(feel, ("moment.Shake", spec.Float("Shake")));
				SkillMaterialAssets.SetColor(feel, "moment.Color", spec.Color);
				SkillMaterialAssets.SetFields(root.GetComponent<ObjectWithTime>(), ("duration", spec.Float("Lifetime")));
				if (followUser)
					root.AddComponent<FollowUser>();
				SkillMaterialAssets.AddEffects(root, spec);
				SkillMaterialAssets.AddLayers(root, spec);
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
