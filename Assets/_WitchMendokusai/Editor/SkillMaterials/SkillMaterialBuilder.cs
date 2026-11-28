using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 스킬 재료 굽기. 장비를 조립할 재료 창고 (시전 모양 + 적중 효과 + 이펙트) 를 시험 무기로 굽기
	/// - 재료 목록 정본 skill-materials.json (SkillMaterialCatalog 가 읽고 검증), 모양별 굽는 법 SkillMaterialForms
	/// - 결과 무기는 Resources/DevTrialWeapons/SkillMaterials, 진열장에서 개발 창 weapon trial:&lt;재료 이름&gt;
	/// - 다시 구우면 같은 경로를 덮고, 목록에서 빠진 재료의 결과물은 지움. 값은 손으로 고치지 말고 목록에서
	/// - 기획 정본 memo changes/wm-skill-materials.md
	/// 메뉴: WM/SkillMaterials/Build
	/// </summary>
	public static class SkillMaterialBuilder
	{
		[MenuItem("WM/SkillMaterials/Build")]
		public static void Build()
		{
			if (EditorApplication.isPlayingOrWillChangePlaymode)
				throw new InvalidOperationException("Play 중에는 굽지 않음");
			List<SkillMaterialSpec> specs = SkillMaterialCatalog.Load();
			EnsureFolder(SkillMaterialAssets.WEAPON_DIR);
			EnsureFolder(SkillMaterialAssets.SHOT_DIR);
			int removed = RemoveStale(specs);
			List<string> failures = BuildSpecs(specs);
			if (failures.Count > 0)
				throw new InvalidOperationException($"[SkillMaterials] 실패 {failures.Count}개: {string.Join(" | ", failures)}");
			Debug.Log($"[SkillMaterials] {specs.Count}개 구움, 지난 결과물 {removed}개 지움 -> {SkillMaterialAssets.WEAPON_DIR}");
		}

		// 나눠 굽기. 재료가 수백 개면 한 번에 구우면 원격 명령 시간 한도를 넘음. 목록 전체를 검증한 뒤 [from, from+count) 만
		// 지난 결과물 정리는 하지 않음 (다 구운 뒤 Clean 한 번)
		public static string BuildRange(int from, int count)
		{
			if (EditorApplication.isPlayingOrWillChangePlaymode)
				throw new InvalidOperationException("Play 중에는 굽지 않음");
			List<SkillMaterialSpec> specs = SkillMaterialCatalog.Load();
			EnsureFolder(SkillMaterialAssets.WEAPON_DIR);
			EnsureFolder(SkillMaterialAssets.SHOT_DIR);
			List<SkillMaterialSpec> range = specs.Skip(from).Take(count).ToList();
			List<string> failures = BuildSpecs(range);
			return $"{from}+{range.Count}/{specs.Count} fails={failures.Count} {string.Join(" | ", failures)}";
		}

		public static int Clean() => RemoveStale(SkillMaterialCatalog.Load());

		// 재료 하나가 실패해도 나머지는 굽고 실패를 모아 돌려줌 (바탕 프리팹 문제 하나로 수백 개가 멈추지 않게). 실패는 숨기지 않고 호출자가 보고
		private static List<string> BuildSpecs(List<SkillMaterialSpec> specs)
		{
			List<string> failures = new();
			foreach (SkillMaterialSpec spec in specs)
			{
				try
				{
					ISkillMaterialForm form = SkillMaterialForms.Get(spec.Form);
					if (form is ISkillMaterialWeaponForm weaponForm)
						weaponForm.BuildWeapon(spec);
					else
						BuildWeapon(spec, form.BuildShot(spec));
				}
				catch (Exception exception)
				{
					failures.Add($"{spec.Id}: {exception.Message}");
					Debug.LogError($"[SkillMaterials] {spec.Id} 굽기 실패: {exception}");
				}
			}
			AssetDatabase.SaveAssets();
			return failures;
		}

		// 무기는 모양과 무관하게 Spawner 하나. 투사체 수 카드는 SpawnerInitializer 가 받음 (기존 시험 무기와 같은 꼴)
		private static void BuildWeapon(SkillMaterialSpec spec, GameObject shot)
		{
			GameObject weapon = new(spec.Id);
			try
			{
				weapon.AddComponent<SkillObject>();
				weapon.AddComponent<PoolingObject>();
				Spawner spawner = weapon.AddComponent<Spawner>();
				SkillMaterialAssets.SetFields(spawner,
					("prefabToSpawn", shot),
					("spawnPositionStrategy", (int)Enum.Parse(typeof(SpawnPositionStrategy), spec.Text("SpawnPosition"))),
					("spawnAimStrategy", (int)Enum.Parse(typeof(SpawnAimStrategy), spec.Text("SpawnAim"))),
					("baseInterval", spec.Float("Interval")),
					("targetRange", spec.Float("Range")),
					("spreadDegrees", spec.Float("Spread")),
					("attackSpeedBonusStatType", (int)UnitStatType.NONE),
					("scaleBonusStatType", (int)UnitStatType.NONE),
					("damageBonusStatType", (int)UnitStatType.NONE));
				spawner.SpawnDelay = spec.Float("SpawnDelay");
				SpawnerInitializer initializer = weapon.AddComponent<SpawnerInitializer>();
				SkillMaterialAssets.SetFields(initializer,
					("spawner", spawner),
					("spawnCountStat", (int)UnitStatType.PROJECTILE_COUNT_BONUS),
					("baseCount", spec.Int("Count")));
				SkillMaterialAssets.Save(weapon, SkillMaterialAssets.WEAPON_DIR, spec.Id);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(weapon);
			}
		}

		// 목록에 없는 재료의 무기와 탄을 지움. 속성 머티리얼은 쓰는 재료가 하나도 없을 때
		private static int RemoveStale(List<SkillMaterialSpec> specs)
		{
			HashSet<string> keep = new(specs.Select(spec => spec.Id));
			HashSet<string> elements = new(specs.Select(spec => spec.Element));
			int removed = 0;
			foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { SkillMaterialAssets.WEAPON_DIR, SkillMaterialAssets.SHOT_DIR }))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				string name = System.IO.Path.GetFileNameWithoutExtension(path);
				// 탄은 <재료>_Shot, 터짐 복제는 <재료>_Shot_Blast0 꼴. 재료 이름은 _Shot 앞까지
				int shotMark = name.IndexOf("_Shot", StringComparison.Ordinal);
				string id = shotMark >= 0 ? name.Substring(0, shotMark) : name;
				if (keep.Contains(id))
					continue;
				AssetDatabase.DeleteAsset(path);
				removed++;
			}
			foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { SkillMaterialAssets.SHOT_DIR }))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				string name = System.IO.Path.GetFileNameWithoutExtension(path);
				string element = name.Substring(name.LastIndexOf('_') + 1);
				if (elements.Contains(element))
					continue;
				AssetDatabase.DeleteAsset(path);
				removed++;
			}
			return removed;
		}

		private static void EnsureFolder(string path)
		{
			if (AssetDatabase.IsValidFolder(path))
				return;
			int slash = path.LastIndexOf('/');
			EnsureFolder(path.Substring(0, slash));
			AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
		}
	}
}
