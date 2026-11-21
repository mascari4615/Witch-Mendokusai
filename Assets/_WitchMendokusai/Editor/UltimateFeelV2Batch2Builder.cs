using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 강한 판 V2 배치 2 넷 (번개, 화염, 태양 광선, 블랙홀) 프리셋 생성
	/// - body: 3D 강한 판 에셋 (Ult3D_&lt;이름&gt;Heavy) 을 그대로 씀. 처음부터 화면 연출과 짝으로 만든 연출이라 흔들림과 섬광 값이 body 에 없음 (1차 넷처럼 복사해 줄이지 않음)
	/// - feel: 박자표. 처음 값은 UltimateFeelV2Batch2Seeds. 공통 세기 줄이기 에셋 (UltFeel_Comfort) 을 1차 넷과 같이 씀
	/// - 래퍼: FeelUltimateFx, 크기 배율 1.45 (1차 넷과 같은 기본, 사용자 판정 2026-11-21 "작아")
	/// - 이미 있는 에셋 값은 그대로 (인스펙터 정본). 처음 값을 다시 넣는 것은 Reseed 메뉴만
	/// 메뉴: WM/CombatFx/Build Ultimate Feel V2 Batch2, WM/CombatFx/Reseed Ultimate Feel V2 Batch2
	/// </summary>
	public static class UltimateFeelV2Batch2Builder
	{
		private const string PRESET_FOLDER = "Assets/_WitchMendokusai/Domain/Dungeon/Fx/Presets";
		private const string FEEL_FOLDER = PRESET_FOLDER + "/FeelV2";
		private const string COMFORT_PATH = FEEL_FOLDER + "/UltFeel_Comfort.asset";
		private const float WORLD_SIZE = 1.45f;

		private sealed class Entry
		{
			public string Name;
			public string DevKey;
			public string Body;
			// 표적의 화면 위쪽 거리. 박자표 왜곡 중심과 body 표적에 같은 값 (크기 1.45 에서 위 끝이 덜 잘리게 1차 넷처럼 당김)
			public float TargetForward;
			public Func<UltimateScreenFeel.Swell[]> Swells;
			public Func<UltimateScreenFeel.Beat[]> Beats;
			public UltimateScreenFeel.TraumaSettings Trauma;
		}

		private static Entry[] Entries() => new[]
		{
			new Entry
			{
				Name = "ThunderHeavyV2", DevKey = "thunder_heavy_v2", Body = "Ult3D_ThunderHeavy", TargetForward = 1.4f,
				Swells = UltimateFeelV2Batch2Seeds.ThunderSwells, Beats = UltimateFeelV2Batch2Seeds.ThunderBeats,
				Trauma = UltimateFeelV2Seeds.Trauma(0.024f, 18f, 2.6f, 0.6f),
			},
			new Entry
			{
				Name = "FlameHeavyV2", DevKey = "flame_heavy_v2", Body = "Ult3D_FlameHeavy", TargetForward = 1.6f,
				Swells = UltimateFeelV2Batch2Seeds.FlameSwells, Beats = UltimateFeelV2Batch2Seeds.FlameBeats,
				Trauma = UltimateFeelV2Seeds.Trauma(0.02f, 13f, 1.8f, 0.55f),
			},
			new Entry
			{
				Name = "SunBeamHeavyV2", DevKey = "sunbeam_heavy_v2", Body = "Ult3D_SunBeamHeavy", TargetForward = 1.6f,
				Swells = UltimateFeelV2Batch2Seeds.SunBeamSwells, Beats = UltimateFeelV2Batch2Seeds.SunBeamBeats,
				Trauma = UltimateFeelV2Seeds.Trauma(0.022f, 15f, 2.2f, 0.7f),
			},
			new Entry
			{
				Name = "BlackHoleHeavyV2", DevKey = "blackhole_heavy_v2", Body = "Ult3D_BlackHoleHeavy", TargetForward = 1f,
				Swells = UltimateFeelV2Batch2Seeds.BlackHoleSwells, Beats = UltimateFeelV2Batch2Seeds.BlackHoleBeats,
				Trauma = UltimateFeelV2Seeds.Trauma(0.024f, 12f, 1.8f, 0.5f),
			},
		};

		[MenuItem("WM/CombatFx/Build Ultimate Feel V2 Batch2")]
		public static void Build() => Run(false);

		[MenuItem("WM/CombatFx/Reseed Ultimate Feel V2 Batch2")]
		public static void Reseed() => Run(true);

		private static void Run(bool reseed)
		{
			// body 에셋 생성과 3D 셰이더 연결 (클래스마다 에셋 하나)
			UltimateFx3DBatchPresetBuilder.Build();
			if (AssetDatabase.IsValidFolder(FEEL_FOLDER) == false)
				AssetDatabase.CreateFolder(PRESET_FOLDER, "FeelV2");
			UltimateFeelComfort comfort = LoadOrCreate<UltimateFeelComfort>(COMFORT_PATH, out _);
			foreach (Entry entry in Entries())
				BuildEntry(entry, comfort, reseed);
			AssetDatabase.SaveAssets();
			Debug.Log($"[UltimateFx] 강한 판 V2 배치 2 {Entries().Length}개 {(reseed ? "처음 값 다시 넣음" : "갱신")}");
		}

		private static void BuildEntry(Entry entry, UltimateFeelComfort comfort, bool reseed)
		{
			string bodyPath = PRESET_FOLDER + "/" + entry.Body + ".asset";
			UltimateFx body = AssetDatabase.LoadAssetAtPath<UltimateFx>(bodyPath);
			if (body == null)
				throw new InvalidOperationException($"3D 강한 판 없음: {bodyPath}");

			string feelPath = FEEL_FOLDER + "/UltFeel_" + entry.Name + ".asset";
			// 처음 만들 때와 다시 넣기 때만 body 표적 거리를 박자표와 같게
			if (reseed || AssetDatabase.LoadAssetAtPath<UltimateScreenFeel>(feelPath) == null)
			{
				SerializedObject bodyTune = new(body);
				Property(bodyTune, "targetDistance").floatValue = entry.TargetForward;
				bodyTune.ApplyModifiedPropertiesWithoutUndo();
				EditorUtility.SetDirty(body);
			}

			UltimateScreenFeel feel = LoadOrCreate<UltimateScreenFeel>(feelPath, out bool feelCreated);
			if (feelCreated || reseed)
			{
				Seed(feel, comfort, entry);
				EditorUtility.SetDirty(feel);
			}

			string wrapperPath = PRESET_FOLDER + "/Ult3D_" + entry.Name + ".asset";
			FeelUltimateFx wrapper = LoadOrCreate<FeelUltimateFx>(wrapperPath, out bool wrapperCreated);
			SerializedObject wrapperSerialized = new(wrapper);
			SerializedObject bodySerialized = new(body);
			bool copyCommon = wrapperCreated || reseed || wrapper.Body == null;
			if (copyCommon)
			{
				foreach (string field in new[] { "spriteShader", "shockwaveShader", "gradeShader", "overlayFlashCap", "backgroundFlashCap", "spawnTemporaryFoliage", "foliageAreaRadius", "foliage", "foliageResponse", "tailSeconds", "screenSortingOrder" })
					wrapperSerialized.CopyFromSerializedProperty(Property(bodySerialized, field));
				Property(wrapperSerialized, "worldSize").floatValue = WORLD_SIZE;
			}
			Property(wrapperSerialized, "devKey").stringValue = entry.DevKey;
			Property(wrapperSerialized, "body").objectReferenceValue = body;
			Property(wrapperSerialized, "feel").objectReferenceValue = feel;
			wrapperSerialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(wrapper);
		}

		/// <summary>
		/// 박자표 처음 값을 칸 이름으로 넣음. EditorSeed 의 매개변수가 바뀌는 중 (카메라 물림 칸 추가) 이라 시그니처에 묶이지 않게
		/// - 없는 칸 이름은 예외 (조용히 건너뛰지 않음). 물림 같은 나머지 칸은 클래스 기본값
		/// </summary>
		private static void Seed(UltimateScreenFeel feel, UltimateFeelComfort comfort, Entry entry)
		{
			SetField(feel, "comfort", comfort);
			SetField(feel, "trauma", entry.Trauma);
			SetField(feel, "swells", entry.Swells());
			SetField(feel, "beats", entry.Beats());
			SetField(feel, "targetForward", entry.TargetForward);
		}

		private static void SetField(UltimateScreenFeel feel, string name, object value)
		{
			FieldInfo field = typeof(UltimateScreenFeel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if (field == null)
				throw new InvalidOperationException($"{nameof(UltimateScreenFeel)}: 칸 {name} 없음");
			field.SetValue(feel, value);
		}

		private static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
		{
			T asset = AssetDatabase.LoadAssetAtPath<T>(path);
			created = asset == null;
			if (created)
			{
				asset = ScriptableObject.CreateInstance<T>();
				AssetDatabase.CreateAsset(asset, path);
			}
			return asset;
		}

		private static SerializedProperty Property(SerializedObject target, string name)
		{
			SerializedProperty property = target.FindProperty(name);
			if (property == null)
				throw new InvalidOperationException($"{target.targetObject.name}: 칸 {name} 없음");
			return property;
		}
	}
}
