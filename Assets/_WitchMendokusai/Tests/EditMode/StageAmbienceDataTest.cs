using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 떠도는 먼지는 스테이지가 정한다 (사용자 2026-11-27 "플레이어에 붙은게 애초에 이상함, 스테이지에 따라야지").
	/// 전에는 Player.prefab 과 ArenaDoll.prefab 의 자식 "Particle System" 이라 실내에서도 먼지가 돌았음
	/// </summary>
	public sealed class StageAmbienceDataTest
	{
		private const string DUST_MATERIAL_PATH = "Assets/_WitchMendokusai/Domain/Doll/_Common/Dust.mat";
		private const string AMBIENT_DUST_PATH = "Assets/_WitchMendokusai/Domain/World/Stage/Ambient/AmbientDust.prefab";
		private const string STAGE_ROOT = "Assets/_WitchMendokusai/Domain/World/Stage/";

		[TestCase("Assets/_WitchMendokusai/Domain/Doll/_Common/Player.prefab")]
		[TestCase("Assets/_WitchMendokusai/Domain/Doll/_Common/ArenaDoll.prefab")]
		public void 인형에는_떠도는_먼지가_안_붙는다(string prefabPath)
		{
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
			Assert.IsNotNull(prefab, prefabPath);
			Material dust = AssetDatabase.LoadAssetAtPath<Material>(DUST_MATERIAL_PATH);
			foreach (ParticleSystemRenderer particleRenderer in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
				Assert.AreNotEqual(dust, particleRenderer.sharedMaterial, $"{prefabPath} 의 {particleRenderer.name} 가 먼지 재질을 씀. 먼지는 Stage.AmbientParticles 로");
		}

		[Test]
		public void 먼지_prefab_은_먼지_재질의_입자다()
		{
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AMBIENT_DUST_PATH);
			Assert.IsNotNull(prefab);
			ParticleSystemRenderer particleRenderer = prefab.GetComponent<ParticleSystemRenderer>();
			Assert.IsNotNull(particleRenderer);
			Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Material>(DUST_MATERIAL_PATH), particleRenderer.sharedMaterial);
		}

		[TestCase("0006_Village/WS_6_Village.asset")]
		[TestCase("0000_Home/WS_0_Home.asset")]
		public void 바깥_스테이지는_먼지가_있다(string stagePath)
		{
			WorldStage stage = AssetDatabase.LoadAssetAtPath<WorldStage>(STAGE_ROOT + stagePath);
			Assert.IsNotNull(stage, stagePath);
			Assert.AreEqual(AssetDatabase.LoadAssetAtPath<GameObject>(AMBIENT_DUST_PATH), stage.AmbientParticles);
		}

		[TestCase("0001_HomeInside/WS_1_HomeInside.asset")]
		[TestCase("0004_Lab/WS_4_Lab.asset")]
		[TestCase("0007_Museum/WS_7_Museum.asset")]
		public void 실내_스테이지는_먼지가_없다(string stagePath)
		{
			WorldStage stage = AssetDatabase.LoadAssetAtPath<WorldStage>(STAGE_ROOT + stagePath);
			Assert.IsNotNull(stage, stagePath);
			Assert.IsNull(stage.AmbientParticles);
		}
	}
}
