using System;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 3D 배치 연출 (WorldUltimateFx 파생: 창 왕관, 카드, 달의 약한 판과 강한 판) 프리셋에 3D 셰이더를 연결
	/// - 공통 프리셋 생성 (UltimateFxPresetBuilder) 이 클래스마다 에셋을 만든 뒤, 여기서 메시 셰이더와 빛 받는 입자 셰이더만 넣음. 값은 그대로 (인스펙터 정본)
	/// - 주제 메시는 코드 생성 (UltimateMeshFactory) 이라 연결할 모델 없음
	/// - 별밭 창 셰이더 칸 (starWindowShader) 이 있는 연출 (밤의 날) 은 그것도 연결
	/// 메뉴: WM/CombatFx/Build Ultimate 3D Batch Presets
	/// </summary>
	public static class UltimateFx3DBatchPresetBuilder
	{
		[MenuItem("WM/CombatFx/Build Ultimate 3D Batch Presets")]
		public static void Build()
		{
			UltimateFxPresetBuilder.Build();
			Shader meshGlow = Shader.Find("WM/Fx/UltimateMeshGlow");
			Shader litParticle = Shader.Find("WM/Fx/UltimateLitParticle");
			Shader starWindow = Shader.Find("WM/Fx/UltimateStarWindow");
			if (meshGlow == null || litParticle == null || starWindow == null)
				throw new InvalidOperationException($"연결 누락 meshGlow={meshGlow != null} litParticle={litParticle != null} starWindow={starWindow != null}");
			int count = 0;
			foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(WorldUltimateFx)))
			{
				WorldUltimateFx fx = AssetDatabase.LoadAssetAtPath<WorldUltimateFx>(AssetDatabase.GUIDToAssetPath(guid));
				if (fx == null)
					continue;
				SerializedObject serialized = new(fx);
				serialized.FindProperty("meshGlowShader").objectReferenceValue = meshGlow;
				serialized.FindProperty("litParticleShader").objectReferenceValue = litParticle;
				SerializedProperty starWindowProperty = serialized.FindProperty("starWindowShader");
				if (starWindowProperty != null)
					starWindowProperty.objectReferenceValue = starWindow;
				serialized.ApplyModifiedPropertiesWithoutUndo();
				EditorUtility.SetDirty(fx);
				count++;
			}
			AssetDatabase.SaveAssets();
			Debug.Log($"[UltimateFx] 3D 배치 프리셋 {count}개 셰이더 연결");
		}
	}
}
