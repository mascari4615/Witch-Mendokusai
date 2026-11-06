using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace WitchMendokusai
{
	public static partial class SurvivalEquipmentBuilder
	{
		private const string FX_SHADER = WEAPON_ROOT + "/Parts/SurvivalWeaponFx.shader";
		private const string FEEL_CONFIG = "Assets/_WitchMendokusai/Domain/Dungeon/Survival/SurvivalFeelConfig.asset";
		private const string HIT_FLASH_CONTROLLER = "Assets/_WitchMendokusai/Core/UI/Effect/Hit/Hit.controller";
		private const string SPRITE_ROOT = "Assets/_WitchMendokusai/Domain/Item/Assets/_Common/Sprite/";
		private const int FIELD_QUEUE = 2950;
		private const int BEAM_QUEUE = 3010;
		private const float PLAYER_HIT_FLASH_STRENGTH = 0.3f;

		private readonly struct SpriteSpec
		{
			public readonly Sprite Sprite;
			public readonly Color Tint;
			public readonly float Size;
			public readonly bool Flat;
			public readonly float Height;
			public readonly float Roll;

			public SpriteSpec(Sprite sprite, Color tint, float size, bool flat = false, float height = VISUAL_HEIGHT, float roll = 0f)
			{
				Sprite = sprite;
				Tint = tint;
				Size = size;
				Flat = flat;
				Height = height;
				Roll = roll;
			}
		}

		private readonly struct AmbientSpec
		{
			public readonly Color Color;
			public readonly float Rate;
			public readonly float Radius;
			public readonly float Life;
			public readonly float Size;
			public readonly float Rise;

			public AmbientSpec(Color color, float rate, float radius, float life, float size, float rise)
			{
				Color = color;
				Rate = rate;
				Radius = radius;
				Life = life;
				Size = size;
				Rise = rise;
			}
		}

		private static Color Muted(float red, float green, float blue, float alpha = 1f) => new(red, green, blue, alpha);

		private static Sprite IconSprite(string iconName) => LoadRequired<Sprite>(ICON_ROOT + "/" + iconName + ".png");

		private static Sprite ItemSprite(string assetPath) => LoadRequired<ItemData>(assetPath).Sprite;

		private static Sprite SheetTile(string sheetPath, int column, int rowFromTop, int tileSize)
		{
			string path = SPRITE_ROOT + sheetPath;
			Texture2D texture = LoadRequired<Texture2D>(path);
			Vector2 corner = new(column * tileSize, texture.height - (rowFromTop + 1) * tileSize);
			foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
			{
				if (asset is Sprite sprite && sprite.rect.position == corner)
					return sprite;
			}
			throw new InvalidOperationException("스프라이트 칸 없음 " + sheetPath + " " + corner);
		}

		private static Shader FxShader => LoadRequired<Shader>(FX_SHADER);

		private static Material EnsureFxMaterial(string folder, string materialName)
		{
			string path = folder + "/" + materialName + ".mat";
			Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
			if (material == null)
			{
				material = new Material(FxShader);
				AssetDatabase.CreateAsset(material, path);
			}
			material.shader = FxShader;
			material.shaderKeywords = Array.Empty<string>();
			return material;
		}

		private static Material EnsureFieldMaterial(string folder, string materialName, Color color, float fillAlpha, float ringAlpha, float ringWidth, float pulseSpeed = 0f, float pulseAmount = 0f)
		{
			Material material = EnsureFxMaterial(folder, materialName);
			material.SetColor("_Color", color);
			material.SetFloat("_Shape", 0f);
			material.SetFloat("_FillAlpha", fillAlpha);
			material.SetFloat("_RingAlpha", ringAlpha);
			material.SetFloat("_RingWidth", ringWidth);
			material.SetFloat("_PulseSpeed", pulseSpeed);
			material.SetFloat("_PulseAmount", pulseAmount);
			material.SetFloat("_Alpha", 1f);
			material.renderQueue = FIELD_QUEUE;
			EditorUtility.SetDirty(material);
			return material;
		}

		private static Material EnsureBeamMaterial(string folder, string materialName, Color color, float coreAlpha, float centerAlpha)
		{
			Material material = EnsureFxMaterial(folder, materialName);
			material.SetColor("_Color", color);
			material.SetFloat("_Shape", 1f);
			material.SetFloat("_FillAlpha", centerAlpha);
			material.SetFloat("_RingAlpha", coreAlpha);
			material.SetFloat("_Alpha", 1f);
			material.renderQueue = BEAM_QUEUE;
			EditorUtility.SetDirty(material);
			return material;
		}

		private static Material EnsureFlatMaterial(string folder, string materialName)
		{
			Material material = EnsureFxMaterial(folder, materialName);
			material.SetColor("_Color", Color.white);
			material.SetFloat("_Shape", 2f);
			material.SetFloat("_RingAlpha", 1f);
			material.SetFloat("_Alpha", 1f);
			material.renderQueue = BEAM_QUEUE;
			EditorUtility.SetDirty(material);
			return material;
		}

		private static GameObject CreateSprite(GameObject parent, SpriteSpec spec)
		{
			GameObject visual = new("Visual");
			visual.transform.SetParent(parent.transform, false);
			visual.transform.localPosition = new Vector3(0f, spec.Height, 0f);
			visual.transform.localEulerAngles = spec.Flat ? new Vector3(90f, 0f, spec.Roll) : new Vector3(0f, 0f, spec.Roll);
			float scale = spec.Size / Mathf.Max(spec.Sprite.bounds.size.x, spec.Sprite.bounds.size.y);
			visual.transform.localScale = Vector3.one * scale;
			SpriteRenderer spriteRenderer = visual.AddComponent<SpriteRenderer>();
			spriteRenderer.sprite = spec.Sprite;
			spriteRenderer.color = spec.Tint;
			spriteRenderer.shadowCastingMode = ShadowCastingMode.Off;
			spriteRenderer.receiveShadows = false;
			return visual;
		}

		private static GameObject CreateQuad(GameObject parent, string name, Material material)
		{
			GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
			Object.DestroyImmediate(quad.GetComponent<Collider>());
			quad.name = name;
			quad.transform.SetParent(parent.transform, false);
			quad.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
			MeshRenderer meshRenderer = quad.GetComponent<MeshRenderer>();
			meshRenderer.sharedMaterial = material;
			meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			meshRenderer.receiveShadows = false;
			return quad;
		}

		private static GameObject CreateGroundQuad(GameObject parent, Material material, float radius)
		{
			GameObject quad = CreateQuad(parent, "Ground", material);
			quad.transform.localPosition = new Vector3(0f, 0.06f, 0f);
			quad.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
			return quad;
		}

		private static GameObject CreateBeamQuad(GameObject parent, Material material, float width, float length)
		{
			GameObject quad = CreateQuad(parent, "Beam", material);
			quad.transform.localPosition = new Vector3(0f, 0.3f, length * 0.5f);
			quad.transform.localScale = new Vector3(width, length, 1f);
			return quad;
		}

		private static GameObject CreateExpandingRing(GameObject parent, Material material, float startRadius, float endRadius, float seconds)
		{
			GameObject quad = CreateGroundQuad(parent, material, endRadius);
			SerializedObject scale = new(quad.AddComponent<ScaleOverLife>());
			Set(scale, "duration", seconds);
			RequireProperty(scale, "startScale").vector3Value = new Vector3(startRadius * 2f, startRadius * 2f, 1f);
			RequireProperty(scale, "endScale").vector3Value = new Vector3(endRadius * 2f, endRadius * 2f, 1f);
			scale.ApplyModifiedPropertiesWithoutUndo();
			AddFade(quad, seconds);
			return quad;
		}

		private static void AddFade(GameObject target, float seconds)
		{
			SerializedObject fade = new(target.AddComponent<FadeOverLife>());
			Set(fade, "duration", seconds);
			fade.ApplyModifiedPropertiesWithoutUndo();
		}

		private static void AddTrail(GameObject target, string folder, Color color, float seconds, float width)
		{
			TrailRenderer trail = target.AddComponent<TrailRenderer>();
			trail.time = seconds;
			trail.startWidth = width;
			trail.endWidth = 0f;
			trail.minVertexDistance = 0.15f;
			trail.numCapVertices = 0;
			trail.startColor = color;
			trail.endColor = new Color(color.r, color.g, color.b, 0f);
			trail.shadowCastingMode = ShadowCastingMode.Off;
			trail.receiveShadows = false;
			trail.sharedMaterial = EnsureFlatMaterial(folder, "Trail");
			target.AddComponent<TrailResetOnEnable>();
		}

		private static void AddBurst(GameObject target, FeelMoment moment)
		{
			SerializedObject serialized = new(target.AddComponent<SurvivalFeelBurst>());
			SerializedProperty property = RequireProperty(serialized, "moment");
			property.FindPropertyRelative("Shake").floatValue = moment.Shake;
			property.FindPropertyRelative("Shards").intValue = moment.Shards;
			property.FindPropertyRelative("ShardSpeed").floatValue = moment.ShardSpeed;
			property.FindPropertyRelative("ShardLife").floatValue = moment.ShardLife;
			property.FindPropertyRelative("ShardSize").floatValue = moment.ShardSize;
			property.FindPropertyRelative("RingSize").floatValue = moment.RingSize;
			property.FindPropertyRelative("RingLife").floatValue = moment.RingLife;
			property.FindPropertyRelative("Color").colorValue = moment.Color;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		private static FeelMoment Burst(Color color, int shards, float speed = 4f, float life = 0.3f, float size = 0.14f, float shake = 0f, float ringSize = 0f)
		{
			return new FeelMoment { Shake = shake, Shards = shards, ShardSpeed = speed, ShardLife = life, ShardSize = size, RingSize = ringSize, RingLife = 0.25f, Color = color };
		}

		private static void AddAmbient(GameObject parent, AmbientSpec spec)
		{
			GameObject host = new("Ambient");
			host.SetActive(false);
			host.transform.SetParent(parent.transform, false);
			host.transform.localPosition = new Vector3(0f, 0.1f, 0f);
			host.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
			ParticleSystem system = host.AddComponent<ParticleSystem>();
			ParticleSystem.MainModule main = system.main;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.scalingMode = ParticleSystemScalingMode.Hierarchy;
			main.loop = true;
			main.playOnAwake = true;
			main.startLifetime = spec.Life;
			main.startSize = new ParticleSystem.MinMaxCurve(spec.Size * 0.6f, spec.Size);
			main.startSpeed = 0f;
			main.startColor = spec.Color;
			main.maxParticles = Mathf.CeilToInt(spec.Rate * spec.Life) + 2;
			ParticleSystem.EmissionModule emission = system.emission;
			emission.rateOverTime = spec.Rate;
			ParticleSystem.ShapeModule shape = system.shape;
			shape.shapeType = ParticleSystemShapeType.Circle;
			shape.radius = spec.Radius;
			shape.radiusThickness = 1f;
			ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
			velocity.enabled = true;
			velocity.space = ParticleSystemSimulationSpace.World;
			velocity.x = 0f;
			velocity.y = spec.Rise;
			velocity.z = 0f;
			ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
			color.enabled = true;
			Gradient fade = new();
			fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
				new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
			color.color = fade;
			ParticleSystemRenderer systemRenderer = host.GetComponent<ParticleSystemRenderer>();
			systemRenderer.sharedMaterial = LoadRequired<SurvivalFeelConfig>(FEEL_CONFIG).ShardMaterial;
			systemRenderer.shadowCastingMode = ShadowCastingMode.Off;
			systemRenderer.receiveShadows = false;
			host.SetActive(true);
		}

		private static void ApplyFeelConfig(List<WeaponSpec> specs)
		{
			SurvivalFeelConfig config = LoadRequired<SurvivalFeelConfig>(FEEL_CONFIG);
			SerializedObject serialized = new(config);
			Set(serialized, "PlayerHitFlashController", LoadRequired<RuntimeAnimatorController>(HIT_FLASH_CONTROLLER));
			Set(serialized, "PlayerHitFlashStrength", PLAYER_HIT_FLASH_STRENGTH);
			serialized.ApplyModifiedPropertiesWithoutUndo();
			foreach (WeaponSpec spec in specs)
			{
				if (spec.HitColor.a <= 0f)
					continue;
				WeaponHitColor existing = config.WeaponHitColors.Find(entry => entry.EquipmentId == spec.Id);
				if (existing == null)
				{
					existing = new WeaponHitColor { EquipmentId = spec.Id };
					config.WeaponHitColors.Add(existing);
				}
				existing.Color = spec.HitColor;
			}
			EditorUtility.SetDirty(config);
		}

		// 기존 큐브와 폭탄에 궤적
		private static void AddLegacyTrails()
		{
			AddLegacyTrail(WEAPON_ROOT + "/Spawner/Cube.prefab", Muted(0.55f, 0.42f, 0.72f, 0.5f), 0.22f, 0.45f);
			AddLegacyTrail(WEAPON_ROOT + "/Spawner/Bomb.prefab", Muted(0.68f, 0.42f, 0.32f, 0.5f), 0.28f, 0.35f);
		}

		private static void AddLegacyTrail(string prefabPath, Color color, float seconds, float width)
		{
			GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
			MeshRenderer mesh = contents.GetComponentInChildren<MeshRenderer>(true);
			if (mesh == null)
				throw new InvalidOperationException("메시 없음 " + prefabPath);
			if (mesh.GetComponent<TrailRenderer>() == null)
				AddTrail(mesh.gameObject, WEAPON_ROOT + "/Spawner", color, seconds, width);
			PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
			PrefabUtility.UnloadPrefabContents(contents);
		}

		// 빠진 그림 참조 수정. 포탑 아이콘 파일이 저장소에 없음
		private static void FixMissingSprites()
		{
			Sprite turretIcon = IconSprite("arrow-cluster");
			SetItemSprite(WEAPON_ROOT + "/I_20000005_포탑.asset", turretIcon);
			foreach (string guid in AssetDatabase.FindAssets("C_30 t:CardData", new[] { CARD_ROOT }))
			{
				CardData card = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
				if (card.Sprite == null)
				{
					SetItemSprite(AssetDatabase.GUIDToAssetPath(guid), turretIcon);
					log.AppendLine("sprite fix " + card.name);
				}
			}
		}

		private static void SetItemSprite(string assetPath, Sprite sprite)
		{
			Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
			SerializedObject serialized = new(asset);
			Set(serialized, "<Sprite>k__BackingField", sprite);
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(asset);
		}

		private static void DeleteStaleMaterials()
		{
			foreach (string stale in new[] { "Needle/NeedleLine", "Bolt/BoltLight", "Beam/BeamLight", "Flask/FlaskBottle", "Broom/BroomBody", "Pin/PinBody", "Potion/PotionBody", "Candy/CandyBody", "Rune/RuneBody", "Thread/ThreadLine", "Ink/InkField", "Ink/InkDrop", "Page/PageBody", "Paper/PaperBody", "ClockHand/ClockHandLight" })
			{
				string path = WEAPON_ROOT + "/" + stale + ".mat";
				if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
				{
					AssetDatabase.DeleteAsset(path);
					log.AppendLine("deleted " + stale);
				}
			}
		}
	}
}
