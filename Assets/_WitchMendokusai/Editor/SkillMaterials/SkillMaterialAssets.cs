using System;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	// 굽는 법들이 같이 쓰는 자산 조작. 바탕 프리팹 열기, 칸 채우기, 속성 색, 저장
	internal static class SkillMaterialAssets
	{
		public const string WEAPON_DIR = "Assets/_WitchMendokusai/Domain/Dev/TrialWeapons/Resources/DevTrialWeapons/SkillMaterials";
		// 탄, 장판, 머티리얼은 시험 무기 목록에 안 뜨게 Resources 밖
		public const string SHOT_DIR = "Assets/_WitchMendokusai/Domain/Dev/TrialWeapons/SkillMaterialShots";

		// 바탕 프리팹을 장면 밖 (프리팹 편집 장면) 에 열어 고침. 끝나면 CloseTemplate
		public static GameObject OpenTemplate(string path)
		{
			if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
				throw new InvalidOperationException($"바탕 프리팹 없음: {path}");
			return PrefabUtility.LoadPrefabContents(path);
		}

		public static void CloseTemplate(GameObject root) => PrefabUtility.UnloadPrefabContents(root);

		public static GameObject SaveShot(GameObject root, string fileName) => Save(root, SHOT_DIR, fileName);

		public static GameObject Save(GameObject root, string directory, string fileName)
		{
			string path = $"{directory}/{fileName}.prefab";
			GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
			if (success == false)
				throw new InvalidOperationException($"프리팹 저장 실패: {path}");
			return saved;
		}

		// 적중 효과마다 StatusOnHit 하나
		public static void AddEffects(GameObject shot, SkillMaterialSpec spec)
		{
			foreach (SkillMaterialEffect effect in spec.Effects)
			{
				StatusOnHit status = shot.AddComponent<StatusOnHit>();
				SetFields(status, ("statusType", (int)effect.Status), ("statusSeconds", effect.Seconds), ("slowFactor", effect.SlowFactor));
			}
		}

		// 이펙트 겹. Burst 는 켜지는 순간 그린 터짐 (플립북, 바닥 고리, 바닥 어둠). 플립북과 고리 모두 속성색
		public static void AddLayers(GameObject shot, SkillMaterialSpec spec)
		{
			foreach (SkillMaterialLayer layer in spec.Layers)
			{
				switch (layer.Kind)
				{
					case "Burst":
						PaintedFxSet art = AssetDatabase.LoadAssetAtPath<PaintedFxSet>(spec.ElementArt);
						if (art == null)
							throw new InvalidOperationException($"{spec.Id}: 그림 벌 없음 {spec.ElementArt}");
						PaintedBurst burst = shot.AddComponent<PaintedBurst>();
						// PaintedBurst 는 반지름에 오브젝트 배율을 곱함. 장판처럼 배율로 크기를 맞춘 탄에서도 세계 반지름이 겹 값이 되게 나눔
						SetFields(burst, ("art", art), ("radius", layer.Radius / shot.transform.localScale.x), ("seconds", layer.Seconds), ("ringSeconds", layer.RingSeconds));
						// 그림 벌은 흰색으로 구운 PFX_White 라 플립북도 속성색으로 물듦. 심은 흰색 쪽으로 BodyWhiten 만큼
						SetColor(burst, "tint", Color.Lerp(spec.Color, Color.white, spec.BodyWhiten));
						SetColor(burst, "ringTint", spec.Color);
						break;
					default:
						throw new InvalidOperationException($"{spec.Id}: 굽는 법 없는 겹 {layer.Kind}");
				}
			}
		}

		public static void SetColor(UnityEngine.Object target, string name, Color color)
		{
			SerializedObject serialized = new(target);
			SerializedProperty property = serialized.FindProperty(name);
			if (property == null)
				throw new InvalidOperationException($"{target.GetType().Name} 에 칸 없음: {name}");
			property.colorValue = color;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		// 속성 색. 스프라이트는 흰색 쪽으로 BodyWhiten 만큼, 트레일은 속성 머티리얼 사본
		// strict 면 트레일과 메시에 _Color 가 꼭 있어야 함. 복제 모양 (CloneForm) 은 바탕 무기 고유 그림이 있어 strict 를 끔
		public static void ApplyLook(GameObject shot, SkillMaterialSpec spec, bool strict = true)
		{
			foreach (SpriteRenderer sprite in shot.GetComponentsInChildren<SpriteRenderer>(true))
				sprite.color = Color.Lerp(spec.Color, Color.white, spec.BodyWhiten);
			// 셰이더 (SurvivalWeaponFx) 의 칼날, 바닥 모양은 정점 색을 안 받아 머티리얼 _Color 로. _Color 없는 머티리얼은 그대로
			foreach (Renderer renderer in shot.GetComponentsInChildren<Renderer>(true))
			{
				// 스프라이트는 위에서 색, 입자는 아래에서 시작색. 나머지 (트레일, 메시) 는 _Color 가 있어야 함 (없으면 속성색 없이 구워지니 바로 실패)
				if (renderer is SpriteRenderer || renderer is ParticleSystemRenderer)
					continue;
				if (renderer.sharedMaterial == null || renderer.sharedMaterial.HasProperty("_Color") == false)
				{
					if (strict)
						throw new InvalidOperationException($"{spec.Id}: {renderer.name} 머티리얼에 _Color 없음, 속성색을 못 넣음");
					continue;
				}
				renderer.sharedMaterial = ElementMaterial(renderer.sharedMaterial, spec.Element, spec.Color);
			}
			// 입자는 시작 색. 알파는 바탕 값 그대로
			foreach (ParticleSystem particles in shot.GetComponentsInChildren<ParticleSystem>(true))
			{
				ParticleSystem.MainModule main = particles.main;
				Color start = main.startColor.color;
				main.startColor = new Color(spec.Color.r, spec.Color.g, spec.Color.b, start.a);
			}
		}

		// 바탕 머티리얼을 매번 새로 베껴 속성 색만 바꿈. 바탕이 바뀌면 다시 구울 때 따라감
		private static Material ElementMaterial(Material source, string element, Color color)
		{
			string path = $"{SHOT_DIR}/{source.name}_{element}.mat";
			Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
			if (material == null)
			{
				material = new Material(source);
				AssetDatabase.CreateAsset(material, path);
			}
			material.shader = source.shader;
			material.CopyPropertiesFromMaterial(source);
			material.SetColor("_Color", color);
			EditorUtility.SetDirty(material);
			return material;
		}

		// 숨은 직렬화 칸을 이름으로 채움. 이름이 틀리면 바로 실패 (조용히 기본값으로 굽지 않게)
		// enum 칸은 실제 값 (intValue). 순번 (enumValueIndex) 은 값이 띄엄띄엄이거나 음수면 어긋남
		public static void SetFields(UnityEngine.Object target, params (string name, object value)[] fields)
		{
			if (target == null)
				throw new InvalidOperationException("채울 대상 없음");
			SerializedObject serialized = new(target);
			foreach ((string name, object value) in fields)
			{
				SerializedProperty property = serialized.FindProperty(name);
				if (property == null)
					throw new InvalidOperationException($"{target.GetType().Name} 에 칸 없음: {name}");
				switch (value)
				{
					case float number:
						property.floatValue = number;
						break;
					case int number:
						property.intValue = number;
						break;
					case bool flag:
						property.boolValue = flag;
						break;
					case UnityEngine.Object reference:
						property.objectReferenceValue = reference;
						break;
					default:
						throw new InvalidOperationException($"모르는 값 종류: {name}={value}");
				}
			}
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}
	}
}
