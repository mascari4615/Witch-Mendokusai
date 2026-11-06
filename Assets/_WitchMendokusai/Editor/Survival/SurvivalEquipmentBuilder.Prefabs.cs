using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace WitchMendokusai
{
	public static partial class SurvivalEquipmentBuilder
	{
		private const float VISUAL_HEIGHT = 0.8f;

		private static Material EnsureMaterial(string folder, string materialName, Color color)
		{
			string path = folder + "/" + materialName + ".mat";
			Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
			if (material == null)
			{
				material = new Material(LoadRequired<Material>(BASE_MATERIAL));
				AssetDatabase.CreateAsset(material, path);
			}
			material.SetColor("_BaseColor", color);
			material.SetColor("_Color", color);
			EditorUtility.SetDirty(material);
			return material;
		}

		private static GameObject SavePrefab(GameObject root, string folder, string prefabName)
		{
			root.name = prefabName;
			GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/" + prefabName + ".prefab");
			Object.DestroyImmediate(root);
			return prefab;
		}

		private static GameObject CreateVisual(GameObject parent, PrimitiveType primitive, Vector3 localPosition, Vector3 localEuler, Vector3 localScale, Material material)
		{
			GameObject visual = GameObject.CreatePrimitive(primitive);
			Object.DestroyImmediate(visual.GetComponent<Collider>());
			visual.name = "Visual";
			visual.transform.SetParent(parent.transform, false);
			visual.transform.localPosition = localPosition;
			visual.transform.localEulerAngles = localEuler;
			visual.transform.localScale = localScale;
			MeshRenderer meshRenderer = visual.GetComponent<MeshRenderer>();
			meshRenderer.sharedMaterial = material;
			meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			meshRenderer.receiveShadows = false;
			return visual;
		}

		private static void AddBase(GameObject root, float lifeSeconds)
		{
			root.AddComponent<SkillObject>();
			root.AddComponent<PoolingObject>();
			if (lifeSeconds > 0f)
			{
				SerializedObject life = new(root.AddComponent<ObjectWithTime>());
				Set(life, "duration", lifeSeconds);
				life.ApplyModifiedPropertiesWithoutUndo();
			}
		}

		private static SphereCollider AddSphereTrigger(GameObject root, float radius)
		{
			SphereCollider sphere = root.AddComponent<SphereCollider>();
			sphere.isTrigger = true;
			sphere.radius = radius;
			sphere.center = new Vector3(0f, VISUAL_HEIGHT, 0f);
			return sphere;
		}

		private static BoxCollider AddBoxTrigger(GameObject root, float width, float length)
		{
			BoxCollider box = root.AddComponent<BoxCollider>();
			box.isTrigger = true;
			box.size = new Vector3(width, 2f, length);
			box.center = new Vector3(0f, 1f, length * 0.5f);
			return box;
		}

		private static DamagingObject AddDamage(GameObject root, DamageSpec damage)
		{
			DamagingObject damagingObject = damage.RehitInterval > 0f ? root.AddComponent<RepeatingDamagingObject>() : root.AddComponent<DamagingObject>();
			SerializedObject serialized = new(damagingObject);
			Set(serialized, "damage", damage.Damage);
			Set(serialized, "isTrigger", true);
			Set(serialized, "useHitCount", damage.HitCount > 0);
			Set(serialized, "hitCount", Mathf.Max(damage.HitCount, 1));
			Set(serialized, "disableWhenInvalid", damage.HitCount > 0);
			Set(serialized, "knockbackForce", damage.Knockback);
			Set(serialized, "knockbackDuration", 0.15f);
			Set(serialized, "hitstopDuration", damage.Hitstop);
			Set(serialized, "usedByPlayer", true);
			if (damage.RehitInterval > 0f)
				Set(serialized, "rehitInterval", damage.RehitInterval);
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return damagingObject;
		}

		private static void AddStatus(GameObject root, float slowFactor, float slowSeconds)
		{
			SerializedObject serialized = new(root.AddComponent<StatusOnHit>());
			Set(serialized, "slowFactor", slowFactor);
			Set(serialized, "slowSeconds", slowSeconds);
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		private static GameObject MakeChainLink(string folder, string prefabName, Material material)
		{
			GameObject root = new(prefabName);
			root.AddComponent<PoolingObject>();
			SerializedObject life = new(root.AddComponent<ObjectWithTime>());
			Set(life, "duration", 0.12f);
			life.ApplyModifiedPropertiesWithoutUndo();
			LineRenderer lineRenderer = root.AddComponent<LineRenderer>();
			lineRenderer.useWorldSpace = true;
			lineRenderer.widthMultiplier = 0.12f;
			lineRenderer.sharedMaterial = material;
			lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
			SerializedObject link = new(root.AddComponent<ChainLink>());
			Set(link, "lineRenderer", lineRenderer);
			link.ApplyModifiedPropertiesWithoutUndo();
			return SavePrefab(root, folder, prefabName);
		}

		private static void AddChain(GameObject root, string chainStat, int baseChain, float range, GameObject linkPrefab)
		{
			SerializedObject serialized = new(root.AddComponent<ChainOnHit>());
			Set(serialized, "chainCountStat", (int)(UnitStatType)Enum.Parse(typeof(UnitStatType), chainStat));
			Set(serialized, "baseChainCount", baseChain);
			Set(serialized, "chainRange", range);
			Set(serialized, "linkPrefab", linkPrefab);
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		private static GameObject MakeBullet(string folder, string prefabName, Material material, Vector3 visualScale, float speed, bool autoAim, float lifeSeconds, float radius, DamageSpec damage, Action<GameObject> extra = null)
		{
			GameObject root = new(prefabName);
			AddBase(root, lifeSeconds);
			AddSphereTrigger(root, radius);
			AddDamage(root, damage);
			SerializedObject movement = new(root.AddComponent<BulletMovement>());
			Set(movement, "moveSpeed", speed);
			Set(movement, "useAutoAim", autoAim);
			movement.ApplyModifiedPropertiesWithoutUndo();
			CreateVisual(root, PrimitiveType.Capsule, new Vector3(0f, VISUAL_HEIGHT, 0f), new Vector3(90f, 0f, 0f), visualScale, material);
			extra?.Invoke(root);
			return SavePrefab(root, folder, prefabName);
		}

		private static GameObject MakeBlast(string folder, string prefabName, Material material, float radius, float lifeSeconds, DamageSpec damage, Action<GameObject> extra = null)
		{
			GameObject root = new(prefabName);
			AddBase(root, lifeSeconds);
			AddSphereTrigger(root, radius);
			AddDamage(root, damage);
			GameObject visual = CreateVisual(root, PrimitiveType.Sphere, new Vector3(0f, VISUAL_HEIGHT, 0f), Vector3.zero, Vector3.one * radius * 2f, material);
			SerializedObject scale = new(visual.AddComponent<ScaleOverLife>());
			Set(scale, "duration", lifeSeconds);
			RequireProperty(scale, "startScale").vector3Value = Vector3.one * radius * 0.6f;
			RequireProperty(scale, "endScale").vector3Value = Vector3.one * radius * 2f;
			scale.ApplyModifiedPropertiesWithoutUndo();
			extra?.Invoke(root);
			return SavePrefab(root, folder, prefabName);
		}

		private static GameObject MakeField(string folder, string prefabName, Material material, float radius, float lifeSeconds, DamageSpec damage, Action<GameObject> extra = null)
		{
			GameObject root = new(prefabName);
			AddBase(root, lifeSeconds);
			AddSphereTrigger(root, radius);
			AddDamage(root, damage);
			CreateVisual(root, PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f), Vector3.zero, new Vector3(radius * 2f, 0.03f, radius * 2f), material);
			extra?.Invoke(root);
			return SavePrefab(root, folder, prefabName);
		}

		private static GameObject MakeBeam(string folder, string prefabName, Material material, float width, float length, float lifeSeconds, DamageSpec damage, Action<GameObject> extra = null)
		{
			GameObject root = new(prefabName);
			AddBase(root, lifeSeconds);
			AddBoxTrigger(root, width, length);
			AddDamage(root, damage);
			CreateVisual(root, PrimitiveType.Cube, new Vector3(0f, VISUAL_HEIGHT, length * 0.5f), Vector3.zero, new Vector3(width, 0.15f, length), material);
			extra?.Invoke(root);
			return SavePrefab(root, folder, prefabName);
		}

		private static GameObject MakeFlyer(string folder, string prefabName, Material material, PrimitiveType primitive, Vector3 visualScale, float duration, float arcHeight, float dropHeight, GameObject explosion)
		{
			GameObject root = new(prefabName);
			AddBase(root, 0f);
			SerializedObject flight = new(root.AddComponent<ArcFlight>());
			Set(flight, "duration", duration);
			Set(flight, "arcHeight", arcHeight);
			Set(flight, "dropHeight", dropHeight);
			flight.ApplyModifiedPropertiesWithoutUndo();
			SerializedObject explode = new(root.AddComponent<ExplodeOnEnd>());
			Set(explode, "explosionPrefab", explosion);
			explode.ApplyModifiedPropertiesWithoutUndo();
			CreateVisual(root, primitive, new Vector3(0f, VISUAL_HEIGHT, 0f), Vector3.zero, visualScale, material);
			return SavePrefab(root, folder, prefabName);
		}

		private static GameObject MakeMine(string folder, string prefabName, Material material, Vector3 visualScale, float lifeSeconds, float triggerRadius, GameObject explosion)
		{
			GameObject root = new(prefabName);
			AddBase(root, lifeSeconds);
			AddSphereTrigger(root, triggerRadius);
			SerializedObject explode = new(root.AddComponent<ExplodeOnEnd>());
			Set(explode, "explosionPrefab", explosion);
			Set(explode, "explodeOnContact", true);
			explode.ApplyModifiedPropertiesWithoutUndo();
			CreateVisual(root, PrimitiveType.Capsule, new Vector3(0f, 0.3f, 0f), Vector3.zero, visualScale, material);
			return SavePrefab(root, folder, prefabName);
		}

		private static GameObject MakeSpawnerObject(WeaponSpec spec, string folder, GameObject spawned, SpawnPositionStrategy position, SpawnAimStrategy aim, float interval, float delay, float range, int baseCount, Action<GameObject> extra = null, bool opposite = false, float spread = 0f, float clockStep = 30f, float scalePerStack = 0.1f)
		{
			GameObject root = new(spec.Key + "Object");
			AddBase(root, 0f);
			AddSpawner(root, spec, spawned, position, aim, interval, delay, range, baseCount, opposite, spread, clockStep, scalePerStack);
			extra?.Invoke(root);
			return SavePrefab(root, folder, spec.Key + "Object");
		}

		private static void AddSpawner(GameObject root, WeaponSpec spec, GameObject spawned, SpawnPositionStrategy position, SpawnAimStrategy aim, float interval, float delay, float range, int baseCount, bool opposite, float spread, float clockStep, float scalePerStack)
		{
			Spawner spawner = root.AddComponent<Spawner>();
			SerializedObject serialized = new(spawner);
			Set(serialized, "prefabToSpawn", spawned);
			Set(serialized, "spawnPositionStrategy", (int)position);
			Set(serialized, "spawnAimStrategy", (int)aim);
			Set(serialized, "<RandomRange>k__BackingField", range);
			Set(serialized, "<SpawnDelay>k__BackingField", delay);
			Set(serialized, "baseInterval", interval);
			Set(serialized, "attackSpeedBonusStatType", (int)Stat(spec, "ATTACK_SPEED_BONUS"));
			Set(serialized, "damageBonusStatType", (int)Stat(spec, "DAMAGE_BONUS"));
			Set(serialized, "scaleBonusStatType", (int)Stat(spec, "SCALE_BONUS"));
			Set(serialized, "scalePerStack", scalePerStack);
			Set(serialized, "spawnOpposite", opposite);
			Set(serialized, "spreadDegrees", spread);
			Set(serialized, "clockStepDegrees", clockStep);
			serialized.ApplyModifiedPropertiesWithoutUndo();

			SerializedObject initializer = new(root.AddComponent<SpawnerInitializer>());
			Set(initializer, "spawner", spawner);
			Set(initializer, "spawnCountStat", (int)Stat(spec, "COUNT"));
			Set(initializer, "baseCount", baseCount);
			initializer.ApplyModifiedPropertiesWithoutUndo();
		}

		private static GameObject MakeAura(WeaponSpec spec, string folder, Material material, float radius, DamageSpec damage, bool requireStill)
		{
			GameObject root = new(spec.Key + "Object");
			AddBase(root, 0f);
			SphereCollider sphere = AddSphereTrigger(root, radius);
			DamagingObject damagingObject = AddDamage(root, damage);
			GameObject visual = CreateVisual(root, PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f), Vector3.zero, new Vector3(radius * 2f, 0.03f, radius * 2f), material);
			SerializedObject aura = new(root.AddComponent<EquipmentAura>());
			Set(aura, "damagingObject", damagingObject);
			Set(aura, "damageCollider", sphere);
			Set(aura, "visual", visual);
			Set(aura, "damageBonusStat", (int)Stat(spec, "DAMAGE_BONUS"));
			Set(aura, "scaleBonusStat", (int)Stat(spec, "SCALE_BONUS"));
			Set(aura, "requireStill", requireStill);
			aura.ApplyModifiedPropertiesWithoutUndo();
			return SavePrefab(root, folder, spec.Key + "Object");
		}

		private static UnitStatType Stat(WeaponSpec spec, string suffix) => (UnitStatType)Enum.Parse(typeof(UnitStatType), spec.Prefix + "_" + suffix);
	}
}
