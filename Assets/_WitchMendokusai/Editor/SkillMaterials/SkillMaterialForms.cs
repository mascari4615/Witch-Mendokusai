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
		GameObject BuildShot(SkillMaterialSpec spec);
	}

	// 모양 등록표. 새 모양은 클래스 하나와 여기 한 줄, skill-materials.json 의 Forms 에 기본값
	internal static class SkillMaterialForms
	{
		private static readonly Dictionary<string, ISkillMaterialForm> forms = Register(
			new LineShotForm());

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

	// 직선 투사체. 첫 적중 수만큼 맞히고 멈춤
	internal sealed class LineShotForm : ISkillMaterialForm
	{
		public string Name => "LineShot";

		public GameObject BuildShot(SkillMaterialSpec spec)
		{
			GameObject root = SkillMaterialAssets.OpenTemplate(spec.Text("Template"));
			try
			{
				foreach (ChainOnHit chain in root.GetComponentsInChildren<ChainOnHit>(true))
					UnityEngine.Object.DestroyImmediate(chain);
				SkillMaterialAssets.SetFields(root.GetComponent<BulletMovement>(), ("moveSpeed", spec.Float("ShotSpeed")), ("useAutoAim", true));
				SkillMaterialAssets.SetFields(root.GetComponent<DamagingObject>(), ("damage", spec.Int("Damage")), ("useHitCount", true), ("hitCount", spec.Int("HitCount")), ("disableWhenInvalid", true));
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
