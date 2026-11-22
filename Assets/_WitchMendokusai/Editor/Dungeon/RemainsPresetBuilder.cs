using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 싸움 흔적 (잔해, 자국, 액체, 풀) 자산을 한 번에 만들고 연결. 다시 돌려도 같은 결과 (있으면 갱신)
	/// - 자국 도장 그림, 조각 재질, 액체 종류 4, 몬스터 프로필, Resources/RemainsSettings
	/// - 몬스터 데이터에 프로필, 공용 몬스터 프리팹에 MonsterRemains, 풀 재질 셰이더, 통 종류에 기름, 종이 폭탄에 불붙임, 걷기 먼지
	/// </summary>
	public static class RemainsPresetBuilder
	{
		private const string ROOT = "Assets/_WitchMendokusai/Domain/Dungeon/Remains";
		private const string PRESETS = ROOT + "/Presets";
		private const string STAMP_PATH = ROOT + "/Art/RemainsStamp.png";
		private const string PIECE_MATERIAL_PATH = ROOT + "/RemainsPiece.mat";
		private const string SETTINGS_PATH = ROOT + "/Resources/RemainsSettings.asset";
		private const string MONSTER_BASE_PATH = "Assets/_WitchMendokusai/Domain/Dungeon/Monster/Impl/MonsterBase.prefab";
		private const string GRASS_MATERIAL_PATH = "Assets/_WitchMendokusai/Domain/World/Parts/Grass/Grass.mat";
		private const string PAPER_BOMB_PATH = "Assets/_WitchMendokusai/Domain/Crafting/Equipment/Weapon/Paper/PaperBomb.prefab";
		private const string BREAKABLE_SETTINGS_PATH = "Assets/_WitchMendokusai/Domain/Dungeon/Breakable/Resources/BreakableSettings.asset";
		private const int STAMP_SIZE = 128;
		private const float WALK_DUST_INTERVAL = .3f;

		[MenuItem("WM/Remains/Build Presets")]
		public static void Build()
		{
			Directory.CreateDirectory(PRESETS);
			Directory.CreateDirectory(ROOT + "/Art");
			Directory.CreateDirectory(ROOT + "/Resources");

			Texture2D stamp = BuildStamp();
			Material pieceMaterial = BuildPieceMaterial();
			GoopKind oil = Goop("GoopKind_1_Oil", new Color(.16f, .12f, .08f, .85f), 40f, kind => kind.Flammable = true);
			GoopKind water = Goop("GoopKind_2_Water", new Color(.25f, .42f, .6f, .55f), 25f, kind => kind.Conductive = true);
			Goop("GoopKind_3_Poison", new Color(.35f, .6f, .15f, .7f), 18f, kind =>
			{
				kind.MonsterDamagePerSecond = 4;
				kind.PlayerDamagePerSecond = 1;
			});
			GoopKind slime = Goop("GoopKind_4_SlimeGoo", new Color(.45f, .75f, .35f, .6f), 15f, kind =>
			{
				kind.MoveSpeedMultiplier = .65f;
				kind.Conductive = true;
			});

			RemainsSettings settings = LoadOrCreate<RemainsSettings>(SETTINGS_PATH);
			settings.StampTexture = stamp;
			settings.PieceMaterial = pieceMaterial;
			settings.StainUtilityShader = RequireShader("Hidden/WM/Remains/StainUtility");
			settings.GroundStainShader = RequireShader("WM/Remains/GroundStain");
			settings.GoopShader = RequireShader("WM/Remains/Goop");
			settings.InteractiveGrassShader = RequireShader("WM/Remains/InteractiveGrass");
			EditorUtility.SetDirty(settings);

			Dictionary<string, RemainsProfile> profiles = BuildProfiles(slime);
			AssignMonsters(profiles);
			AttachToMonsterBase();
			SwapGrassShader(settings.InteractiveGrassShader);
			SetBarrelSpills(oil, water);
			SetPaperBombIgnites();
			SetWalkDust();
			AssetDatabase.SaveAssets();
			Debug.Log("[RemainsPresetBuilder] done");
		}

		// 가운데가 진하고 가장자리가 잡음으로 들쭉날쭉한 흰 얼룩
		private static Texture2D BuildStamp()
		{
			Texture2D texture = new(STAMP_SIZE, STAMP_SIZE, TextureFormat.RGBA32, false);
			float half = STAMP_SIZE * .5f;
			for (int y = 0; y < STAMP_SIZE; y++)
			{
				for (int x = 0; x < STAMP_SIZE; x++)
				{
					Vector2 offset = new((x - half) / half, (y - half) / half);
					float angle = Mathf.Atan2(offset.y, offset.x);
					float wobble = Mathf.PerlinNoise(Mathf.Cos(angle) * 2f + 5f, Mathf.Sin(angle) * 2f + 5f) * .35f + Mathf.PerlinNoise(x * .12f, y * .12f) * .15f;
					float distance = offset.magnitude / (.75f + wobble);
					float alpha = Mathf.Clamp01(1f - Mathf.SmoothStep(.55f, 1f, distance));
					alpha *= .75f + .25f * Mathf.PerlinNoise(x * .3f + 11f, y * .3f + 7f);
					texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
				}
			}
			File.WriteAllBytes(STAMP_PATH, texture.EncodeToPNG());
			Object.DestroyImmediate(texture);
			AssetDatabase.ImportAsset(STAMP_PATH);
			TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(STAMP_PATH);
			importer.alphaIsTransparency = true;
			importer.wrapMode = TextureWrapMode.Clamp;
			importer.mipmapEnabled = false;
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.SaveAndReimport();
			return AssetDatabase.LoadAssetAtPath<Texture2D>(STAMP_PATH);
		}

		private static Material BuildPieceMaterial()
		{
			Material material = AssetDatabase.LoadAssetAtPath<Material>(PIECE_MATERIAL_PATH);
			if (material == null)
			{
				material = new Material(RequireShader("Universal Render Pipeline/Lit"));
				AssetDatabase.CreateAsset(material, PIECE_MATERIAL_PATH);
			}
			material.SetColor("_BaseColor", Color.white);
			material.SetFloat("_Smoothness", .15f);
			material.SetFloat("_Metallic", 0f);
			material.enableInstancing = true;
			EditorUtility.SetDirty(material);
			return material;
		}

		private static GoopKind Goop(string name, Color color, float drySeconds, System.Action<GoopKind> configure)
		{
			GoopKind kind = LoadOrCreate<GoopKind>($"{PRESETS}/{name}.asset");
			kind.Color = color;
			kind.DrySeconds = drySeconds;
			configure(kind);
			EditorUtility.SetDirty(kind);
			return kind;
		}

		private static Dictionary<string, RemainsProfile> BuildProfiles(GoopKind slime)
		{
			Dictionary<string, RemainsProfile> profiles = new();
			profiles["Slime"] = Profile("Remains_1_Slime", RemainsPieceShape.Blob, new Vector2Int(3, 5), new Vector2(.12f, .22f), new Color(.3f, .5f, .2f, .45f), profile =>
			{
				profile.Spill = slime;
				profile.SpillChance = .2f;
				profile.SpillRadius = .7f;
				profile.Bounce = .1f;
			});
			profiles["SlimeBoss"] = Profile("Remains_2_SlimeBoss", RemainsPieceShape.Blob, new Vector2Int(14, 20), new Vector2(.25f, .5f), new Color(.3f, .5f, .2f, .6f), profile =>
			{
				profile.Spill = slime;
				profile.SpillChance = 1f;
				profile.SpillRadius = 3f;
				profile.StainRadius = 2.5f;
				profile.Speed = new Vector2(3f, 7f);
				profile.Lift = 6f;
			});
			profiles["Wisp"] = Profile("Remains_3_Wisp", RemainsPieceShape.Shard, new Vector2Int(0, 0), new Vector2(.08f, .12f), new Color(.55f, .75f, 1f, .4f), profile => profile.StainRadius = .7f);
			profiles["Boar"] = Profile("Remains_4_Boar", RemainsPieceShape.Tuft, new Vector2Int(4, 6), new Vector2(.12f, .2f), new Color(.25f, .12f, .08f, .5f), profile =>
			{
				profile.Palette = new[] { new Color(.42f, .28f, .18f), new Color(.32f, .2f, .12f), new Color(.85f, .8f, .7f) };
			});
			profiles["Cream"] = Profile("Remains_5_Cream", RemainsPieceShape.Blob, new Vector2Int(3, 4), new Vector2(.1f, .18f), new Color(.95f, .9f, .82f, .5f), profile =>
			{
				profile.Palette = new[] { new Color(1f, .97f, .9f), new Color(1f, .82f, .85f) };
			});
			profiles["Dust"] = Profile("Remains_6_Dust", RemainsPieceShape.Chip, new Vector2Int(2, 4), new Vector2(.08f, .14f), new Color(.3f, .28f, .25f, .35f), profile => { });
			return profiles;
		}

		private static RemainsProfile Profile(string name, RemainsPieceShape shape, Vector2Int count, Vector2 size, Color stain, System.Action<RemainsProfile> configure)
		{
			RemainsProfile profile = LoadOrCreate<RemainsProfile>($"{PRESETS}/{name}.asset");
			profile.Shape = shape;
			profile.Count = count;
			profile.Size = size;
			profile.StainColor = stain;
			configure(profile);
			EditorUtility.SetDirty(profile);
			return profile;
		}

		// 이름으로 종을 가름. 연습 인형 (Dummy) 은 비움
		private static void AssignMonsters(Dictionary<string, RemainsProfile> profiles)
		{
			foreach (string guid in AssetDatabase.FindAssets("t:Monster"))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				Monster monster = AssetDatabase.LoadAssetAtPath<Monster>(path);
				if (monster == null)
					continue;
				string name = monster.name;
				RemainsProfile profile = null;
				if (name.Contains("Boss"))
					profile = profiles["SlimeBoss"];
				else if (name.Contains("Slime") || name.Contains("슬라임"))
					profile = profiles["Slime"];
				else if (name.Contains("Wisp") || name.Contains("위스프"))
					profile = profiles["Wisp"];
				else if (name.Contains("멧돼지") || name.Contains("Boar") || name.Contains("Beast"))
					profile = profiles["Boar"];
				else if (name.Contains("Cream") || name.Contains("생크림"))
					profile = profiles["Cream"];
				else if (name.Contains("Dust"))
					profile = profiles["Dust"];
				if (profile == null)
					continue;
				SerializedObject serialized = new(monster);
				serialized.FindProperty("<Remains>k__BackingField").objectReferenceValue = profile;
				serialized.ApplyModifiedPropertiesWithoutUndo();
				Debug.Log($"[RemainsPresetBuilder] {name} -> {profile.name}");
			}
		}

		private static void AttachToMonsterBase()
		{
			GameObject root = PrefabUtility.LoadPrefabContents(MONSTER_BASE_PATH);
			try
			{
				MonsterRemains remains = root.GetComponent<MonsterRemains>();
				if (remains == null)
					remains = root.AddComponent<MonsterRemains>();
				SerializedObject lifeFx = new(root.GetComponent<MonsterLifeFx>());
				SerializedObject serialized = new(remains);
				serialized.FindProperty("body").objectReferenceValue = lifeFx.FindProperty("body").objectReferenceValue;
				serialized.FindProperty("monster").objectReferenceValue = root.GetComponent<MonsterObject>();
				serialized.ApplyModifiedPropertiesWithoutUndo();
				PrefabUtility.SaveAsPrefabAsset(root, MONSTER_BASE_PATH);
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(root);
			}
		}

		private static void SwapGrassShader(Shader shader)
		{
			Material grass = AssetDatabase.LoadAssetAtPath<Material>(GRASS_MATERIAL_PATH);
			if (grass.shader == shader)
				return;
			grass.shader = shader;
			grass.enableInstancing = true;
			EditorUtility.SetDirty(grass);
		}

		// 통은 기름, 항아리는 물. 다른 종류는 그대로
		private static void SetBarrelSpills(GoopKind oil, GoopKind water)
		{
			BreakableSettings breakables = AssetDatabase.LoadAssetAtPath<BreakableSettings>(BREAKABLE_SETTINGS_PATH);
			foreach (BreakableKind kind in breakables.Kinds)
			{
				if (kind.Shape == BreakableShape.Barrel)
					kind.Spill = oil;
				else if (kind.Shape == BreakableShape.Pot)
					kind.Spill = water;
			}
			EditorUtility.SetDirty(breakables);
		}

		private static void SetPaperBombIgnites()
		{
			GameObject root = PrefabUtility.LoadPrefabContents(PAPER_BOMB_PATH);
			try
			{
				foreach (ExplodeOnEnd explode in root.GetComponentsInChildren<ExplodeOnEnd>(true))
				{
					SerializedObject serialized = new(explode);
					serialized.FindProperty("igniteGoop").boolValue = true;
					serialized.ApplyModifiedPropertiesWithoutUndo();
				}
				PrefabUtility.SaveAsPrefabAsset(root, PAPER_BOMB_PATH);
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(root);
			}
		}

		// 걸을 때도 발밑 먼지 (지금은 달릴 때만)
		private static void SetWalkDust()
		{
			foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (path.StartsWith("Assets/_WitchMendokusai/") == false)
					continue;
				GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
				PlayerRunDust dust = prefab.GetComponentInChildren<PlayerRunDust>(true);
				if (dust == null || PrefabUtility.IsPartOfPrefabInstance(dust))
					continue;
				SerializedObject serialized = new(dust);
				SerializedProperty interval = serialized.FindProperty("walkEmitInterval");
				if (Mathf.Approximately(interval.floatValue, WALK_DUST_INTERVAL))
					continue;
				interval.floatValue = WALK_DUST_INTERVAL;
				serialized.ApplyModifiedPropertiesWithoutUndo();
				EditorUtility.SetDirty(prefab);
				Debug.Log($"[RemainsPresetBuilder] walk dust {path}");
			}
		}

		private static T LoadOrCreate<T>(string path) where T : ScriptableObject
		{
			T asset = AssetDatabase.LoadAssetAtPath<T>(path);
			if (asset != null)
				return asset;
			asset = ScriptableObject.CreateInstance<T>();
			AssetDatabase.CreateAsset(asset, path);
			return asset;
		}

		private static Shader RequireShader(string name)
		{
			Shader shader = Shader.Find(name);
			if (shader == null)
				throw new System.InvalidOperationException($"셰이더 없음 {name}");
			return shader;
		}
	}
}
