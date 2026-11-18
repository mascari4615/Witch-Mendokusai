using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 처치 순간의 파편과 발밑 고리
	// - 파편: 재질마다 공유 ParticleSystem 하나. 몬스터마다 오브젝트 없이 Emit 만. 상한은 MaxParticles, 넘으면 엔진이 생략
	// - 고리: 등장 고리와 같은 판과 셰이더, 목록과 상한만 따로
	// - 파편 모양 값 (감속, 늘림, 크기 곡선) 은 시스템 공용이라 마지막에 쏜 스타일 값. 스타일이 같은 재질을 나눠 쓰면 서로 덮음
	public partial class MonsterLifeFxDriver
	{
		private const string SPARK_KEY = "MonsterDeathSparks";

		private sealed class SparkEmitter
		{
			public ParticleSystem Particles;
			public ParticleSystemRenderer Renderer;
		}

		private readonly Dictionary<Material, SparkEmitter> sparkEmitters = new();
		private readonly List<Puff> rings = new();

		public int RingCount => rings.Count;

		/// <summary> 처치 고리 하나 추가. 상한 (MaxRings) 을 넘으면 NONE </summary>
		public int AddRing(Vector3 groundPosition, MonsterDeathStyle style, float sizeMultiplier) => AddGroundPuff(rings, groundPosition, style, sizeMultiplier);

		/// <summary> 처치 파편을 몸 가운데에서 사방으로 쏨. 색은 스타일 색과 몸 색을 섞음 </summary>
		public void EmitSparks(in MonsterDeathGhostPose pose, MonsterDeathStyle style)
		{
			SparkEmitter emitter = GetSparkEmitter(style.SparkMaterial);
			ConfigureSparks(emitter, style);

			Vector3 origin = pose.Position;
			if (pose.Sprite != null)
				origin += Vector3.up * (pose.Sprite.bounds.center.y * pose.Scale.y);

			Color32 sparkColor = SparkColorFor(pose, style);
			Color32 coreColor = style.SparkCoreColor;
			bool hasCore = style.SparkCoreSizeRatio > 0f;
			float angleStep = 360f / style.SparkCount;
			float baseAngle = UnityEngine.Random.Range(0f, 360f);
			ParticleSystem.EmitParams emitParams = new() { position = origin };

			// 고른 간격에 흔들림. 완전 무작위면 한쪽으로 몰려 보임
			for (int i = 0; i < style.SparkCount; i++)
			{
				float jitter = UnityEngine.Random.Range(-0.5f, 0.5f) * style.SparkAngleJitter;
				float angle = (baseAngle + (i + jitter) * angleStep) * Mathf.Deg2Rad;
				float upward = UnityEngine.Random.Range(style.SparkUpwardMin, style.SparkUpwardMax);
				Vector3 direction = new Vector3(Mathf.Cos(angle), upward, Mathf.Sin(angle)).normalized;
				float size = UnityEngine.Random.Range(style.SparkSizeMin, style.SparkSizeMax);

				emitParams.velocity = direction * UnityEngine.Random.Range(style.SparkSpeedMin, style.SparkSpeedMax);
				emitParams.startLifetime = UnityEngine.Random.Range(style.SparkLifetimeMin, style.SparkLifetimeMax);
				emitParams.startSize = size;
				emitParams.startColor = sparkColor;
				emitter.Particles.Emit(emitParams, 1);

				// 흰 심. 같은 자리, 같은 속도의 작은 입자를 겹침
				if (hasCore == false)
					continue;
				emitParams.startSize = size * style.SparkCoreSizeRatio;
				emitParams.startColor = coreColor;
				emitter.Particles.Emit(emitParams, 1);
			}
		}

		// 몸 색을 가장 밝은 채널 1 로 끌어올리고 흰색 쪽으로 밝힌 뒤 스타일 색과 섞음
		private static Color SparkColorFor(in MonsterDeathGhostPose pose, MonsterDeathStyle style)
		{
			Color body = pose.Color;
			if (pose.Sprite != null && SpriteColorSampler.TryGet(pose.Sprite, out Color spriteColor))
				body *= spriteColor;

			float brightest = Mathf.Max(body.r, Mathf.Max(body.g, body.b));
			body = brightest > 0f ? body / brightest : Color.white;
			body = Color.Lerp(body, Color.white, style.SparkBodyColorLift);

			Color spark = Color.Lerp(style.SparkColor, body, style.SparkBodyColorWeight);
			spark.a = style.SparkColor.a;
			return spark;
		}

		// Play 중 에셋을 고쳐도 다음 처치부터 반영되게 매번 넣음
		private static void ConfigureSparks(SparkEmitter emitter, MonsterDeathStyle style)
		{
			ParticleSystem.MainModule main = emitter.Particles.main;
			main.maxParticles = style.SparkMaxParticles;

			ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = emitter.Particles.limitVelocityOverLifetime;
			limitVelocity.drag = style.SparkDrag;

			ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = emitter.Particles.sizeOverLifetime;
			sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, style.SparkSizeCurve);

			emitter.Renderer.velocityScale = style.SparkStretch;
			emitter.Renderer.lengthScale = style.SparkLengthScale;
		}

		private SparkEmitter GetSparkEmitter(Material material)
		{
			if (sparkEmitters.TryGetValue(material, out SparkEmitter existing))
				return existing;

			GameObject sparkObject = new(SPARK_KEY);
			sparkObject.transform.SetParent(transform, false);
			ParticleSystem system = sparkObject.AddComponent<ParticleSystem>();
			system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

			ParticleSystem.MainModule main = system.main;
			main.loop = true;
			main.playOnAwake = false;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.startSpeed = 0f;
			main.gravityModifier = 0f;
			main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

			// 쏘는 것은 Emit 으로만
			ParticleSystem.EmissionModule emission = system.emission;
			emission.enabled = false;
			ParticleSystem.ShapeModule shape = system.shape;
			shape.enabled = false;

			// 속도 상한 없이 감속 (drag) 만. 속도에 비례해 줄어 빠르게 날다 멈춤
			ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = system.limitVelocityOverLifetime;
			limitVelocity.enabled = true;
			limitVelocity.dampen = 0f;
			limitVelocity.multiplyDragByParticleSize = false;
			limitVelocity.multiplyDragByParticleVelocity = false;

			ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
			sizeOverLifetime.enabled = true;

			// 속도 방향으로 늘린 판. 느려지면 짧아짐
			ParticleSystemRenderer sparkRenderer = sparkObject.GetComponent<ParticleSystemRenderer>();
			sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch;
			sparkRenderer.sharedMaterial = material;
			sparkRenderer.shadowCastingMode = ShadowCastingMode.Off;
			sparkRenderer.receiveShadows = false;
			sparkRenderer.sortMode = ParticleSystemSortMode.None;

			system.Play();

			SparkEmitter emitter = new() { Particles = system, Renderer = sparkRenderer };
			sparkEmitters.Add(material, emitter);
			return emitter;
		}
	}
}
