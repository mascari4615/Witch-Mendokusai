using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai.Tests
{
	// 빌보드 그림 빛 반응 (2026-11-21). 셰이더가 UniversalForward 로 그려져 물체당 추가 광원을 받고, 하늘 설정 값이 전역으로 들어가는지
	public class SpriteLightingTest
	{
		private const string SHADER_NAME = "WitchMendokusai/BillboardShader";
		private const string MATERIAL_PATH = "Assets/_WitchMendokusai/Domain/Unit/_Common/UnitBase/Unlit_Billboard.mat";

		[Test]
		public void Billboard_shader_draws_in_universal_forward_with_additional_lights()
		{
			Shader shader = Shader.Find(SHADER_NAME);
			Assert.IsNotNull(shader, SHADER_NAME);
			Assert.AreEqual(1, shader.passCount);
			Assert.AreEqual("UniversalForward", shader.FindPassTagValue(0, new ShaderTagId("LightMode")).name);
			Assert.IsTrue(shader.keywordSpace.FindKeyword("_ADDITIONAL_LIGHTS").isValid, "_ADDITIONAL_LIGHTS 키워드 없음");
			Assert.AreEqual((int)RenderQueue.Transparent, shader.renderQueue);
		}

		[Test]
		public void Billboard_material_responds_to_light_by_default()
		{
			Material material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
			Assert.IsNotNull(material, MATERIAL_PATH);
			Assert.AreEqual(SHADER_NAME, material.shader.name);
			Assert.AreEqual(1f, material.GetFloat("_LightResponse"));
		}

		[Test]
		public void Apply_writes_preset_values_to_shader_globals()
		{
			float savedResponse = Shader.GetGlobalFloat(SpriteLighting.RESPONSE_ID);
			try
			{
				SpriteLightingSettings settings = new();
				SpriteLighting.Apply(settings);
				Assert.AreEqual(settings.Response, Shader.GetGlobalFloat(SpriteLighting.RESPONSE_ID));
				Assert.AreEqual(settings.Floor, Shader.GetGlobalFloat(SpriteLighting.FLOOR_ID));
				Assert.AreEqual(settings.MaxAdditionalLights, Shader.GetGlobalFloat(SpriteLighting.MAX_COUNT_ID));
				Assert.Greater(settings.Floor, 0f, "바닥값 0 이면 밤 그림이 검게 묻힘");
			}
			finally
			{
				Shader.SetGlobalFloat(SpriteLighting.RESPONSE_ID, savedResponse);
			}
		}
	}
}
