using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 3D 판 프리셋. 공통 프리셋 생성 (UltimateFxPresetBuilder) 뒤 3D 판에만 있는 연결 (검 모델, 바위 메시, 셰이더) 을 넣음
	/// 메뉴: WM/CombatFx/Build Ultimate 3D Presets. 재생은 개발 창 fx ultimate ult3d
	/// </summary>
	public static class UltimateFx3DPresetBuilder
	{
		private const string PRESET_PATH = "Assets/_WitchMendokusai/Domain/Dungeon/Fx/Presets/Ult3D_01_SkySword.asset";
		private const string SWORD_PATH = "Assets/_WitchMendokusai/Domain/Dungeon/Fx/Ultimate/World3D/Meshes/SkySwordClaymore.obj";
		private const string ROCK_PATH = "Assets/_WitchMendokusai/Domain/World/Parts/Gravel/Gravel.fbx";

		[MenuItem("WM/CombatFx/Build Ultimate 3D Presets")]
		public static void Build()
		{
			UltimateFxPresetBuilder.Build();
			SkySwordWorldUltimateFx fx = AssetDatabase.LoadAssetAtPath<SkySwordWorldUltimateFx>(PRESET_PATH);
			if (fx == null)
				throw new InvalidOperationException($"프리셋 없음: {PRESET_PATH}");
			GameObject sword = AssetDatabase.LoadAssetAtPath<GameObject>(SWORD_PATH);
			Mesh rock = AssetDatabase.LoadAllAssetsAtPath(ROCK_PATH).OfType<Mesh>().FirstOrDefault();
			Shader meshGlow = Shader.Find("WM/Fx/UltimateMeshGlow");
			Shader litParticle = Shader.Find("WM/Fx/UltimateLitParticle");
			if (sword == null || rock == null || meshGlow == null || litParticle == null)
				throw new InvalidOperationException($"연결 누락 sword={sword != null} rock={rock != null} meshGlow={meshGlow != null} litParticle={litParticle != null}");

			SerializedObject serialized = new(fx);
			serialized.FindProperty("swordModel").objectReferenceValue = sword;
			serialized.FindProperty("rockMesh").objectReferenceValue = rock;
			serialized.FindProperty("meshGlowShader").objectReferenceValue = meshGlow;
			serialized.FindProperty("litParticleShader").objectReferenceValue = litParticle;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(fx);
			AssetDatabase.SaveAssets();
			Debug.Log("[UltimateFx] 3D 프리셋 연결 갱신");
		}
	}
}
