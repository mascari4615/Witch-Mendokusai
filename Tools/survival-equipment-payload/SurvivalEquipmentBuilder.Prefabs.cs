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

		private static GameObject SavePrefab(GameObject root, string folder, string prefabName)
		{
			root.name = prefabName;
			GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/" + prefabName + ".prefab");
			Object.DestroyImmediate(root);
			return prefab;
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

		private static GameObject MakeChainLink(string folder, string prefabName, Color color)
		{
			GameObject root = new(prefabName);
			root.AddComponent<PoolingObject>();
			SerializedObject life = new(root.AddComponent<ObjectWithTime>());
			Set(life, "duration", 0.12f);
			life.ApplyModifiedPropertiesWithoutUndo();
			LineRenderer lineRenderer = root.AddComponent<LineRenderer>();
			lineRenderer.useWorldSpace = true;
			lineRenderer.widthMultiplier = 0.07f;
			lineRenderer.startColor = color;
			lineRenderer.endColor = color;
			lineRenderer.sharedMaterial = EnsureFlatMaterial(folder, prefabName + "Line");
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

		private static GameObject MakeBullet(string folder, string prefabName, SpriteSpec sprite, float speed, bool autoAim, float lifeSeconds, float radius, DamageSpec damage, Color trailColor, Action<GameObject> extra = null)
		{
			GameObject root = new(prefabName);
			AddBase(root, lifeSeconds);
			AddSphereTrigger(root, radius);
			AddDamage(root, damage);
			SerializedObject movement = new(root.AddComponent<BulletMovement>());
			Set(movement, "moveSpeed", speed);
			Set(movement, "useAutoAim", autoAim);
			movement.ApplyModifiedPropertiesWithoutUndo();
			GameObject visual = CreateSprite(root, sprite);
			if (trailColor.a > 0f)
				AddTrail(visual, folder, trailColor, 0.18f, sprite.Size * 0.35f);
			extra?.Invoke(root);
			return SavePrefab(root, folder, prefabName);
		}

		private static GameObject MakeBlast(string folder, string prefabName, Color color, float radius, float lifeSeconds, DamageSpec damage, FeelMoment burst, Action<GameObject> extra = null)
		{
			GameObject root = new(prefabName);
			AddBase(root, lifeSeconds);
			AddSphereTrigger(root, radius);
			AddDamage(root, damage);
			AddBurst(root, burst);
			Material material = EnsureFieldMaterial(folder, prefabName, color, 0.1f, 0.55f, 0.08f);
			CreateExpandingRing(root, material, radius * 0.5f, radius, lifeSeconds);
			extra?.Invoke(root);
			return SavePrefab(root, folder, prefabName);
		}

		private static GameObject MakeField(string folder, string prefabName, Color color, float radius, float lifeSeconds, DamageSpec damage, Action<GameObject> extra = null)
		{
			GameObject root = new(prefabName);
			AddBase(root, lifeSeconds);
			AddSphereTrigger(root, radius);
			AddDamage(root, damage);
			Material material = EnsureFieldMaterial(folder, prefabName, color, 0.1f, 0.4f, 0.05f, 3f, 0.25f);
			CreateGroundQuad(root, material, radius);
			extra?.Invoke(root);
			return SavePrefab(root, folder, prefabName);
		}

		private static GameObject MakeBeam(string folder, string prefabName, Color color, float width, float length, float lifeSeconds, DamageSpec damage, Action<GameObject> extra = null)
		{
			GameObject root = new(prefabName);
			AddBase(root, lifeSeconds);
			AddBoxTrigger(root, width, length);
			AddDamage(root, damage);
			Material material = EnsureBeamMaterial(folder, prefabName, color, 0.5f, 0.25f);
			GameObject quad = CreateBeamQuad(root, material, width, length);
			AddFade(quad, lifeSeconds);
			extra?.Invoke(root);
			return SavePrefab(root, folder, prefabName);
		}

		private static GameObject MakeFlyer(string folder, string prefabName, SpriteSpec sprite, float duration, float arcHeight, float dropHeight, GameObject explosion, Color trailColor)
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
			GameObject visual = CreateSprite(root, sprite);
			if (trailColor.a > 0f)
				AddTrail(visual, folder, trailColor, 0.25f, sprite.Size * 0.3f);
			return SavePrefab(root, folder, prefabName);
		}

		private static GameObject MakeMine(string folder, string prefabName, SpriteSpec sprite, Color ringColor, float lifeSeconds, float triggerRadius, GameObject explosion)
		{
			GameObject root = new(prefabName);
			AddBase(root, lifeSeconds);
			AddSphereTrigger(root, triggerRadius);
			SerializedObject explode = new(root.AddComponent<ExplodeOnEnd>());
			Set(explode, "explosionPrefab", explosion);
			Set(explode, "explodeOnContact", true);
			explode.ApplyModifiedPropertiesWithoutUndo();
			CreateSprite(root, sprite);
			Material material = EnsureFieldMaterial(folder, prefabName + "Ring", ringColor, 0.04f, 0.3f, 0.1f);
			CreateGroundQuad(root, material, triggerRadius);
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

		private static GameObject MakeAura(WeaponSpec spec, string folder, Color color, float radius, DamageSpec damage, bool requireStill, float fillAlpha, float ringAlpha, AmbientSpec ambient)
		{
			GameObject root = new(spec.Key + "Object");
			AddBase(root, 0f);
			SphereCollider sphere = AddSphereTrigger(root, radius);
			DamagingObject damagingObject = AddDamage(root, damage);
			GameObject visual = new("Visual");
			visual.transform.SetParent(root.transform, false);
			Material material = EnsureFieldMaterial(folder, spec.Key + "Field", color, fillAlpha, ringAlpha, 0.04f, 1.5f, 0.3f);
			CreateGroundQuad(visual, material, radius);
			AddAmbient(visual, ambient);
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

		private static GameObject MakeCounterShield(WeaponSpec spec, string folder, Color color, float ringRadius, GameObject counter)
		{
			GameObject root = new(spec.Key + "Object");
			AddBase(root, 0f);
			Material material = EnsureFieldMaterial(folder, spec.Key + "Ring", color, 0.02f, 0.4f, 0.06f, 2f, 0.2f);
			GameObject ring = CreateGroundQuad(root, material, ringRadius);
			SerializedObject shield = new(root.AddComponent<EquipmentCounterShield>());
			Set(shield, "counterPrefab", counter);
			Set(shield, "readyRing", ring.GetComponent<Renderer>());
			Set(shield, "cooldownStat", (int)Stat(spec, "ATTACK_SPEED_BONUS"));
			Set(shield, "damageBonusStat", (int)Stat(spec, "DAMAGE_BONUS"));
			Set(shield, "scaleBonusStat", (int)Stat(spec, "SCALE_BONUS"));
			shield.ApplyModifiedPropertiesWithoutUndo();
			return SavePrefab(root, folder, spec.Key + "Object");
		}

		private static GameObject MakeChoreNote(WeaponSpec spec, string folder, Color color, float ringRadius, GameObject burst)
		{
			GameObject root = new(spec.Key + "Object");
			AddBase(root, 0f);
			Material material = EnsureFieldMaterial(folder, spec.Key + "Ring", color, 0.05f, 0.4f, 0.06f);
			GameObject ring = CreateGroundQuad(root, material, ringRadius);
			SerializedObject note = new(root.AddComponent<EquipmentChoreNote>());
			Set(note, "burstPrefab", burst);
			Set(note, "chargeRing", ring.transform);
			Set(note, "chargeSpeedStat", (int)Stat(spec, "ATTACK_SPEED_BONUS"));
			Set(note, "damageBonusStat", (int)Stat(spec, "DAMAGE_BONUS"));
			Set(note, "scaleBonusStat", (int)Stat(spec, "SCALE_BONUS"));
			note.ApplyModifiedPropertiesWithoutUndo();
			return SavePrefab(root, folder, spec.Key + "Object");
		}

		private static UnitStatType Stat(WeaponSpec spec, string suffix) => (UnitStatType)Enum.Parse(typeof(UnitStatType), spec.Prefix + "_" + suffix);
	}
}
