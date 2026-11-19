using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 3D 판 프리셋. 공통 프리셋 생성 (UltimateFxPresetBuilder) 뒤 3D 판에만 있는 연결 (검 모델, 바위 메시, 셰이더) 을 넣음
	/// - 약한 검 (Ult3D_01_SkySword, 개발 키 ult3d) 과 강한 검 (Ult3D_02_SkySwordHeavy, 개발 키 ult3d_heavy) 은 같은 클래스의 독립 에셋. 어느 쪽이든 단독 사용
	/// - 강한 검 에셋이 없을 때만 약한 검 에셋을 복사해 강화 값을 한 번 넣음. 이미 있으면 연결만 갱신 (인스펙터에서 바꾼 값 보존)
	/// 메뉴: WM/CombatFx/Build Ultimate 3D Presets. 재생은 개발 창 fx ultimate ult3d, fx ultimate ult3d_heavy, 비교는 fx ultimate ult3d+ult3d_heavy
	/// </summary>
	public static class UltimateFx3DPresetBuilder
	{
		private const string PRESET_PATH = "Assets/_WitchMendokusai/Domain/Dungeon/Fx/Presets/Ult3D_01_SkySword.asset";
		private const string HEAVY_PRESET_PATH = "Assets/_WitchMendokusai/Domain/Dungeon/Fx/Presets/Ult3D_02_SkySwordHeavy.asset";
		private const string SWORD_PATH = "Assets/_WitchMendokusai/Domain/Dungeon/Fx/Ultimate/World3D/Meshes/SkySwordClaymore.obj";
		private const string ROCK_PATH = "Assets/_WitchMendokusai/Domain/World/Parts/Gravel/Gravel.fbx";

		[MenuItem("WM/CombatFx/Build Ultimate 3D Presets")]
		public static void Build()
		{
			UltimateFxPresetBuilder.Build();
			SkySwordWorldUltimateFx light = AssetDatabase.LoadAssetAtPath<SkySwordWorldUltimateFx>(PRESET_PATH);
			if (light == null)
				throw new InvalidOperationException($"프리셋 없음: {PRESET_PATH}");
			bool created = false;
			if (AssetDatabase.LoadAssetAtPath<SkySwordWorldUltimateFx>(HEAVY_PRESET_PATH) == null)
			{
				if (AssetDatabase.CopyAsset(PRESET_PATH, HEAVY_PRESET_PATH) == false)
					throw new InvalidOperationException($"강한 검 프리셋 복사 실패: {HEAVY_PRESET_PATH}");
				created = true;
			}
			SkySwordWorldUltimateFx heavy = AssetDatabase.LoadAssetAtPath<SkySwordWorldUltimateFx>(HEAVY_PRESET_PATH);

			Connect(light, "ult3d", false);
			Connect(heavy, "ult3d_heavy", true);
			if (created)
				ApplyHeavyValues(heavy);
			AssetDatabase.SaveAssets();
			Debug.Log($"[UltimateFx] 3D 프리셋 연결 갱신 (강한 검 새로 만듦={created})");
		}

		private static void Connect(SkySwordWorldUltimateFx fx, string devKey, bool heavyLayers)
		{
			GameObject sword = AssetDatabase.LoadAssetAtPath<GameObject>(SWORD_PATH);
			Mesh rock = AssetDatabase.LoadAllAssetsAtPath(ROCK_PATH).OfType<Mesh>().FirstOrDefault();
			Shader meshGlow = Shader.Find("WM/Fx/UltimateMeshGlow");
			Shader litParticle = Shader.Find("WM/Fx/UltimateLitParticle");
			Shader beam = Shader.Find("WM/Fx/UltimateBeamVolume");
			Shader decal = Shader.Find("Shader Graphs/Decal");
			if (sword == null || rock == null || meshGlow == null || litParticle == null || beam == null || decal == null)
				throw new InvalidOperationException($"연결 누락 sword={sword != null} rock={rock != null} meshGlow={meshGlow != null} litParticle={litParticle != null} beam={beam != null} decal={decal != null}");

			SerializedObject serialized = new(fx);
			serialized.FindProperty("devKey").stringValue = devKey;
			serialized.FindProperty("swordModel").objectReferenceValue = sword;
			serialized.FindProperty("rockMesh").objectReferenceValue = rock;
			serialized.FindProperty("meshGlowShader").objectReferenceValue = meshGlow;
			serialized.FindProperty("litParticleShader").objectReferenceValue = litParticle;
			serialized.FindProperty("beamShader").objectReferenceValue = beam;
			serialized.FindProperty("decalShader").objectReferenceValue = decal;
			serialized.FindProperty("heavyLayers").boolValue = heavyLayers;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(fx);
		}

		// 강한 검 첫 값. 근거는 memo wm/design/ult3d-heavy-20261120 (약한 검 대비 실측 차이). 이후 정본은 에셋
		private static void ApplyHeavyValues(SkySwordWorldUltimateFx heavy)
		{
			SerializedObject serialized = new(heavy);
			void Set(string property, float value) => serialized.FindProperty(property).floatValue = value;
			void SetInt(string property, int value) => serialized.FindProperty(property).intValue = value;
			void SetColor(string property, Color value) => serialized.FindProperty(property).colorValue = value;
			void SetVector(string property, Vector2 value) => serialized.FindProperty(property).vector2Value = value;

			SetColor("bladeColor", new Color(0.92f, 0.88f, 0.75f, 1f));
			SetColor("bladeGlow", new Color(0.45f, 0.32f, 0.1f));
			SetColor("bladeRim", new Color(2.8f, 1.9f, 0.6f));
			SetColor("hiltColor", new Color(0.78f, 0.6f, 0.28f, 1f));
			SetColor("swordLightColor", new Color(1f, 0.84f, 0.45f, 1f));
			SetColor("impactLightColor", new Color(1f, 0.7f, 0.3f, 1f));
			Set("swordLength", 8.5f);
			Set("swordWidthScale", 3f);
			Set("swordThicknessScale", 1.6f);
			Set("targetDistance", 2.2f);
			Set("spawnHeight", 30f);
			Set("hangHeight", 1.8f);
			Set("pullBack", 1.55f);
			Set("sunDim", 0.35f);
			Set("ambientDim", 0.45f);
			Set("swordLightIntensity", 20f);
			Set("spotIntensity", 40f);
			Set("impactLightIntensity", 40f);
			Set("impactLightRange", 18f);
			Set("impactHalfLife", 0.9f);
			SetInt("upheavalCount", 16);
			SetVector("upheavalRadius", new Vector2(1.6f, 4.2f));
			SetVector("upheavalScale", new Vector2(1.6f, 2.8f));
			SetInt("debrisCount", 18);
			Set("debrisSpeed", 12f);
			SetInt("rayCount", 140);
			SetInt("dustCount", 60);
			Set("dustRadius", 8f);
			SetInt("emberCount", 140);
			SetInt("trailCount", 60);
			SetInt("gatherCount", 60);
			Set("decalSize", 12f);
			Set("shakeStrength", 2.6f);
			SetInt("shakeCount", 12);
			Set("exposureFlash", 1.8f);
			Set("bloomImpact", 3f);
			Set("chromaticImpact", 0.45f);
			Set("vignetteSummon", 0.4f);
			Set("beamTelegraphRadius", 0.09f);
			Set("beamTelegraphIntensity", 0.3f);
			SetColor("beamColor", new Color(3f, 1.8f, 0.45f));
			SetColor("beamCoreColor", new Color(2.6f, 1.9f, 0.7f));
			Set("beamImpactBoost", 1.1f);
			Set("beamHeight", 16f);
			Set("beamFullRadius", 0.75f);
			Set("beamThickSeconds", 0.2f);
			Set("goldRingRadius", 5.5f);
			Set("shockRadius", 8f);
			Set("scorchLingerSeconds", 3f);
			SetColor("rockColor", new Color(0.36f, 0.33f, 0.3f, 1f));
			serialized.FindProperty("projectorDecal").boolValue = true;
			serialized.FindProperty("m_Name").stringValue = "Ult3D_02_SkySwordHeavy";
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(heavy);
		}
	}
}
