using System;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	// 무기까지 스스로 굽는 모양. SkillMaterialBuilder 는 이 모양이면 공통 무기 (Spawner) 를 따로 굽지 않음
	internal interface ISkillMaterialWeaponForm : ISkillMaterialForm
	{
		void BuildWeapon(SkillMaterialSpec spec);
	}

	// 이미 동작하는 시험 무기 (Resources/DevTrialWeapons 의 Trial*) 를 통째로 바탕 삼는 모양
	// 무기와 탄을 복사해 피해, 주기, 상태이상, 속성색만 바꿈. 돌진, 연쇄, 곡사, 베기, 회전, 자취, 밀집 타격처럼 동작이 복잡한 모양을 빠르게 얻는 길
	// 탄 안의 판정 (DamagingObject) 전부에 피해와 상태이상. 색은 _Color 가 있는 렌더러만 (바탕 무기 고유 그림은 _Color 가 없을 수 있어 건너뜀)
	internal sealed class CloneForm : ISkillMaterialWeaponForm
	{
		public CloneForm(string name) => Name = name;

		public string Name { get; }
		public bool AcceptsLayers => false;

		public GameObject BuildShot(SkillMaterialSpec spec) => throw new InvalidOperationException($"{Name} 은 BuildWeapon 으로 굽는 모양");

		public void BuildWeapon(SkillMaterialSpec spec)
		{
			GameObject weapon = SkillMaterialAssets.OpenTemplate(spec.Text("Template"));
			try
			{
				Spawner spawner = weapon.GetComponent<Spawner>();
				if (spawner == null)
					throw new InvalidOperationException($"{spec.Id}: 바탕 무기에 Spawner 없음 {spec.Text("Template")}");
				// 탄은 ShotTemplate 경로가 있으면 그것. 시험 무기 둘 (퍼짐 돌진, 적 간 추적) 은 탄 참조가 변형 프리팹의 원본 루트 번호를 가리켜 null 로 풀림 (기존 버그)
				string shotPath;
				if (spec.Values.ContainsKey("ShotTemplate"))
				{
					shotPath = spec.Text("ShotTemplate");
				}
				else
				{
					SerializedObject serialized = new(spawner);
					GameObject templateShot = serialized.FindProperty("prefabToSpawn").objectReferenceValue as GameObject;
					if (templateShot == null)
						throw new InvalidOperationException($"{spec.Id}: 바탕 무기의 탄이 비었음. Forms 에 ShotTemplate 경로를 주거나 바탕 무기 참조를 고침");
					shotPath = AssetDatabase.GetAssetPath(templateShot);
				}
				GameObject shot = BuildShotFrom(shotPath, spec);
				SkillMaterialAssets.SetFields(spawner, ("prefabToSpawn", shot), ("baseInterval", spec.Float("Interval")));
				weapon.name = spec.Id;
				SkillMaterialAssets.Save(weapon, SkillMaterialAssets.WEAPON_DIR, spec.Id);
			}
			finally
			{
				SkillMaterialAssets.CloseTemplate(weapon);
			}
		}

		// 탄이 끝날 때 다른 프리팹을 내는 경우 (ExplodeOnEnd: 물약병, 플라스크, 잉크 방울) 그 프리팹도 복제해 판정을 바꿈
		// 판정은 탄과 그 터짐 프리팹을 통틀어 하나 이상 있어야 함
		private const int MAX_CHAIN_DEPTH = 2;

		private static GameObject BuildShotFrom(string shotPath, SkillMaterialSpec spec)
		{
			int hitboxes = 0;
			GameObject shot = BuildChained(shotPath, spec, "_Shot", 0, ref hitboxes);
			if (hitboxes == 0)
				throw new InvalidOperationException($"{spec.Id}: 바탕 탄과 터짐 프리팹에 판정 (DamagingObject) 없음 {shotPath}");
			return shot;
		}

		private static GameObject BuildChained(string path, SkillMaterialSpec spec, string suffix, int depth, ref int hitboxes)
		{
			GameObject root = SkillMaterialAssets.OpenTemplate(path);
			try
			{
				foreach (DamagingObject hitbox in root.GetComponentsInChildren<DamagingObject>(true))
				{
					SkillMaterialAssets.SetFields(hitbox, ("damage", spec.Int("Damage")));
					SkillMaterialAssets.AddEffects(hitbox.gameObject, spec);
					hitboxes++;
				}
				if (depth < MAX_CHAIN_DEPTH)
				{
					int index = 0;
					foreach (ExplodeOnEnd explode in root.GetComponentsInChildren<ExplodeOnEnd>(true))
					{
						SerializedObject serialized = new(explode);
						GameObject blast = serialized.FindProperty("explosionPrefab").objectReferenceValue as GameObject;
						if (blast == null)
							continue;
						GameObject cloned = BuildChained(AssetDatabase.GetAssetPath(blast), spec, $"{suffix}_Blast{index}", depth + 1, ref hitboxes);
						SkillMaterialAssets.SetFields(explode, ("explosionPrefab", cloned));
						index++;
					}
				}
				SkillMaterialAssets.ApplyLook(root, spec, strict: false);
				return SkillMaterialAssets.SaveShot(root, spec.Id + suffix);
			}
			finally
			{
				SkillMaterialAssets.CloseTemplate(root);
			}
		}
	}
}
