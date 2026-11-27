using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 연출의 월드 쪽 공통 도구. 재질, 바닥판과 세로판 메시, 입자계 생성, 바닥 높이
	/// 연출 종류와 무관. 연출마다 다른 진행은 UltimateFx 의 파생 클래스가 짬
	/// </summary>
	public static class UltimateFxStage
	{
		private static Mesh groundQuad;
		private static Mesh standingQuad;
		private static Mesh centerQuad;

		/// <summary> 빛 판 재질. additive 면 더하기, 아니면 알파 합성 </summary>
		public static Material CreateMaterial(Shader shader, Texture texture, bool additive, Color color)
		{
			if (shader == null)
				throw new System.InvalidOperationException($"{nameof(UltimateFx)}.{nameof(UltimateFx.SpriteShader)} 미연결");
			Material material = new(shader) { name = "UltimateFx_" + (additive ? "Add" : "Alpha"), mainTexture = texture };
			material.SetColor("_Color", color);
			material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
			material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
			material.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
			return material;
		}

		/// <summary> 바닥과 나란한 정사각 (XZ, 한 변 1, 중심 원점, UV 0~1) </summary>
		public static Mesh GroundQuad
		{
			get
			{
				if (groundQuad != null)
					return groundQuad;
				groundQuad = new Mesh { name = "UltimateGroundQuad" };
				groundQuad.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
				groundQuad.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
				groundQuad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
				groundQuad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
				groundQuad.RecalculateBounds();
				return groundQuad;
			}
		}

		/// <summary> 세운 정사각 (XY, 가로 1, 높이 1, 발 중심 원점, UV 0~1). 빛기둥용 </summary>
		public static Mesh StandingQuad
		{
			get
			{
				if (standingQuad != null)
					return standingQuad;
				standingQuad = new Mesh { name = "UltimateStandingQuad" };
				standingQuad.vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f), new Vector3(0.5f, 0f, 0f) };
				standingQuad.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
				standingQuad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
				standingQuad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
				standingQuad.RecalculateBounds();
				return standingQuad;
			}
		}

		/// <summary> 가운데가 원점인 세운 정사각 (XY, 한 변 1, UV 0~1). 하늘의 달, 카드, 블랙홀, 화면 덮개 판 </summary>
		public static Mesh CenterQuad
		{
			get
			{
				if (centerQuad != null)
					return centerQuad;
				centerQuad = new Mesh { name = "UltimateCenterQuad" };
				centerQuad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f) };
				centerQuad.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
				centerQuad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
				centerQuad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
				centerQuad.RecalculateBounds();
				return centerQuad;
			}
		}

		public static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material material, int sortingOrder = 0)
		{
			GameObject meshObject = new(name, typeof(MeshFilter), typeof(MeshRenderer));
			meshObject.transform.SetParent(parent, false);
			meshObject.GetComponent<MeshFilter>().sharedMesh = mesh;
			MeshRenderer meshRenderer = meshObject.GetComponent<MeshRenderer>();
			meshRenderer.sharedMaterial = material;
			meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			meshRenderer.receiveShadows = false;
			meshRenderer.sortingOrder = sortingOrder;
			return meshObject;
		}

		/// <summary> 코드로 직접 내보내는 입자계. 방출, 모양, 색 변화는 호출하는 쪽이 정함 </summary>
		public static ParticleSystem CreateParticles(string name, Transform parent, Material material, int maxParticles, float gravity, bool stretched = false)
		{
			GameObject particleObject = new(name);
			particleObject.transform.SetParent(parent, false);
			ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
			particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			ParticleSystem.MainModule main = particles.main;
			main.playOnAwake = false;
			main.loop = false;
			main.maxParticles = maxParticles;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.gravityModifier = gravity;
			main.startLifetime = 1f;
			main.startSpeed = 0f;
			ParticleSystem.EmissionModule emission = particles.emission;
			emission.enabled = false;
			ParticleSystem.ShapeModule shape = particles.shape;
			shape.enabled = false;
			ParticleSystemRenderer particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
			particleRenderer.sharedMaterial = material;
			particleRenderer.renderMode = stretched ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
			if (stretched)
			{
				particleRenderer.velocityScale = 0.06f;
				particleRenderer.lengthScale = 2.2f;
			}
			particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
			particleRenderer.receiveShadows = false;
			return particles;
		}

		/// <summary> 수명 동안 알파가 사라지는 색 변화와 (있으면) 크기 변화 </summary>
		public static void SetLifetimeLook(ParticleSystem particles, Gradient colorOverLifetime, AnimationCurve sizeOverLifetime)
		{
			ParticleSystem.ColorOverLifetimeModule colorModule = particles.colorOverLifetime;
			colorModule.enabled = colorOverLifetime != null;
			if (colorOverLifetime != null)
				colorModule.color = colorOverLifetime;
			ParticleSystem.SizeOverLifetimeModule sizeModule = particles.sizeOverLifetime;
			sizeModule.enabled = sizeOverLifetime != null;
			if (sizeOverLifetime != null)
				sizeModule.size = new ParticleSystem.MinMaxCurve(1f, sizeOverLifetime);
		}

		public static Gradient FadeGradient(Color color, float fadeInEnd, float fadeOutStart)
		{
			Gradient gradient = new();
			gradient.SetKeys(
				new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
				new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(color.a, fadeInEnd), new GradientAlphaKey(color.a, fadeOutStart), new GradientAlphaKey(0f, 1f) });
			return gradient;
		}

		public static void Emit(ParticleSystem particles, Vector3 position, Vector3 velocity, float size, float lifetime, Color color, float rotation = 0f, float angularVelocity = 0f)
		{
			ParticleSystem.EmitParams emitParams = new()
			{
				position = position,
				velocity = velocity,
				startSize = size,
				startLifetime = lifetime,
				startColor = color,
				rotation = rotation,
				angularVelocity = angularVelocity,
			};
			particles.Emit(emitParams, 1);
		}

		/// <summary> 이 자리 높이에 가까운 층의 바닥 높이. 바닥 질의는 스폰 자리 공급자 한 곳 (유닛 몸, 트리거 제외). 못 찾으면 기본 높이 </summary>
		public static float GroundHeight(Vector3 position) =>
			SpawnSites.TryNearHeight(position, position.y, out SpawnSite site) ? site.Ground.y : position.y;

		public static Vector3 RandomRingPoint(float minRadius, float maxRadius)
		{
			float angle = Random.value * Mathf.PI * 2f;
			float radius = Mathf.Sqrt(Mathf.Lerp(minRadius * minRadius, maxRadius * maxRadius, Random.value));
			return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
		}
	}
}
