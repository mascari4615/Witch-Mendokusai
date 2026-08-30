using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>전투 피격 시각 효과의 생성 책임.</summary>
	internal static class IdleBattleFx
	{
		public static void SpawnImpact(Transform holder, Vector3 position, Color color)
		{
			GameObject effectObject = new GameObject("Impact");
			effectObject.transform.SetParent(holder, false);
			effectObject.transform.position = position;
			ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
			ParticleSystem.MainModule main = particles.main;
			main.duration = 0.16f;
			main.startLifetime = 0.16f;
			main.startSpeed = 1.2f;
			main.startSize = 0.045f;
			main.startColor = color;
			main.maxParticles = 6;
			ParticleSystem.EmissionModule emission = particles.emission;
			emission.rateOverTime = 0f;
			emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 4) });
			ParticleSystem.ShapeModule shape = particles.shape;
			shape.shapeType = ParticleSystemShapeType.Sphere;
			shape.radius = 0.05f;
			ParticleSystemRenderer renderer = effectObject.GetComponent<ParticleSystemRenderer>();
			renderer.renderMode = ParticleSystemRenderMode.Mesh;
			renderer.mesh = IdleBattleVisualFactory.BuildImpactMesh();
			renderer.sharedMaterial = IdleBattleVisualFactory.MakeMaterial(color);
			particles.Play();
			Object.Destroy(effectObject, 0.35f);
		}
	}
}
