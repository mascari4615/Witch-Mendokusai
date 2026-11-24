using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	/// <summary>
	/// 변신 연출의 터짐. 등 뒤 금빛 날개 (한 프레임에 나와 넘치게 펼쳐졌다 멈춤), 빛기둥, 머리 위 고리, 발밑 불꽃과 충격
	/// - 꺼지는 순서는 기둥, 고리, 날개. 날개는 끝부터 부서지며 깃이 흩어짐
	/// </summary>
	public sealed partial class FormTransformFx
	{
		[System.Serializable]
		private struct FeatherLayer
		{
			[Min(1)] public int Count;
			[Tooltip("가장 바깥 깃과 가장 안쪽 깃의 각도 (옆 0, 위 90)")] public Vector2 Angles;
			[Tooltip("가장 바깥 깃과 가장 안쪽 깃의 길이")] public Vector2 Lengths;
			[Tooltip("뿌리가 어깨에서 떨어진 거리 (바깥, 안쪽)")] public Vector2 Roots;
			[Min(0.02f)] public float Width;
			[Tooltip("카메라 쪽으로 당김")] public float Forward;
			[Range(0f, 1f)] public float BodyAlpha;
			[Min(0f)] public float Intensity;
		}

		[Header("날개")]
		[SerializeField] private FeatherLayer[] layers =
		{
			new() { Count = 8, Angles = new Vector2(70f, -14f), Lengths = new Vector2(1.8f, 0.95f), Roots = new Vector2(0.5f, 0.12f), Width = 0.26f, Forward = 0f, BodyAlpha = 0.58f, Intensity = 0.95f },
			new() { Count = 7, Angles = new Vector2(62f, -8f), Lengths = new Vector2(1.1f, 0.55f), Roots = new Vector2(0.42f, 0.1f), Width = 0.24f, Forward = 0.05f, BodyAlpha = 0.5f, Intensity = 0.9f },
			new() { Count = 6, Angles = new Vector2(54f, 2f), Lengths = new Vector2(0.62f, 0.34f), Roots = new Vector2(0.32f, 0.08f), Width = 0.2f, Forward = 0.09f, BodyAlpha = 0.5f, Intensity = 0.95f },
		};
		[SerializeField] private Vector3 featherShape = new(0.16f, 0.22f, 0.5f);
		[SerializeField, Min(0.1f)] private float wingScale = 1.7f;
		[SerializeField, Min(0f), Tooltip("어깨가 몸 가운데에서 벌어진 거리")] private float shoulderGap = 0.16f;
		[SerializeField, Range(0f, 60f), Tooltip("날개 끝이 뒤로 젖혀진 각도 (깊이)")] private float wingYaw = 10f;
		[SerializeField, Min(0f)] private float wingDepth = 0.2f;
		[SerializeField, Min(0)] private int driftFeathers = 26;

		[Header("기둥과 고리")]
		[SerializeField, Min(0.1f)] private float pillarRadius = 0.85f;
		[SerializeField, Min(1f)] private float pillarHeight = 9f;
		[SerializeField, Min(0)] private int pillarMotesPerSecond = 90;
		[SerializeField, Min(0f)] private float pillarIntensity = 0.3f;
		[SerializeField] private Vector2 haloRadius = new(0.25f, 0.62f);
		[SerializeField] private Vector2 haloTilt = new(6f, 18f);

		[Header("발밑")]
		[SerializeField, Min(0)] private int petalCount = 0;
		[SerializeField] private Vector2 petalLength = new(0.6f, 1.1f);
		[SerializeField, Min(0.5f)] private float shockRadius = 4.2f;

		private sealed class Feather
		{
			public Transform Transform;
			public Renderer Renderer;
			public float Side;
			public float Fraction;
			public float Angle;
			public float Root;
			public float Length;
			public float Forward;
			public float Delay;
			public float Seed;
		}

		private void ComposeWings(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			float end = ImpactTime + wingHold + wingBreakSeconds;
			GameObject wings = new("FormWings");
			wings.transform.SetParent(run.Root, false);
			wings.SetActive(false);
			List<Feather> feathers = new();
			Mesh particleMesh = null;
			for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
			{
				FeatherLayer layer = layers[layerIndex];
				Material material = Energy(run, FRONT_QUEUE + layerIndex);
				material.SetFloat("_BodyAlpha", layer.BodyAlpha);
				material.SetFloat(INTENSITY_ID, layer.Intensity);
				material.SetFloat("_CoreWidth", 0.16f);
				material.SetFloat("_EdgeWidth", 0.3f);
				material.SetFloat("_EdgeAmount", 0.3f);
				material.SetFloat("_Cover", 0.95f);
				material.SetFloat("_RootFade", 0.35f);
				Mesh mesh = Keep(run, FormMeshes.Feather(14, layer.Width, featherShape.x, featherShape.y, featherShape.z));
				particleMesh = mesh;
				for (int side = -1; side <= 1; side += 2)
				for (int i = 0; i < layer.Count; i++)
				{
					float fraction = layer.Count <= 1 ? 0f : i / (float)(layer.Count - 1);
					GameObject featherObject = UltimateFxStage.CreateMeshObject("FormFeather", wings.transform, mesh, material);
					feathers.Add(new Feather
					{
						Transform = featherObject.transform,
						Renderer = featherObject.GetComponent<MeshRenderer>(),
						Side = side,
						Fraction = fraction,
						Angle = Mathf.Lerp(layer.Angles.x, layer.Angles.y, fraction) + Random.Range(-1.5f, 1.5f),
						Root = Mathf.Lerp(layer.Roots.x, layer.Roots.y, fraction),
						Length = Mathf.Lerp(layer.Lengths.x, layer.Lengths.y, Mathf.Pow(fraction, 0.85f)) * Random.Range(0.95f, 1.05f),
						Forward = layer.Forward,
						Delay = layerIndex * 0.012f + fraction * 0.02f,
						Seed = Random.value * 10f,
					});
				}
			}
			run.At(ImpactTime, () => wings.SetActive(true));
			MaterialPropertyBlock block = new();
			run.Track(ImpactTime, end - ImpactTime, _ =>
			{
				Pose pose = PoseNow(run);
				float now = run.Now;
				float age = now - ImpactTime;
				wings.transform.SetPositionAndRotation(Chest(pose) + pose.Up * 0.1f + pose.Away * wingDepth, pose.Facing);
				wings.transform.localScale = Vector3.one * wingScale;
				float breakProgress = UltimateFxRun.Span(now, ImpactTime + wingHold, end);
				foreach (Feather feather in feathers)
				{
					float open = OverShoot(UltimateFxRun.Span(age, feather.Delay, feather.Delay + 0.13f), 0.16f);
					float settle = 2.2f * Mathf.Sin(age * 4.2f - feather.Fraction * 1.4f) * Mathf.Clamp01(age * 3f) + 5f * breakProgress * (1f - feather.Fraction);
					float angle = Mathf.Lerp(82f, feather.Angle, 0.45f + 0.55f * open) + settle;
					Quaternion spin = Quaternion.Euler(0f, 0f, angle);
					Vector3 root = spin * Vector3.right * feather.Root;
					// 왼쪽 날개는 좌우를 뒤집은 같은 깃
					Quaternion yaw = Quaternion.Euler(0f, -wingYaw * feather.Side, 0f);
					feather.Transform.localPosition = yaw * new Vector3((shoulderGap + root.x) * feather.Side, root.y, root.z - feather.Forward);
					feather.Transform.localRotation = yaw * Quaternion.Euler(0f, 0f, angle * feather.Side);
					float length = feather.Length * (0.62f + 0.38f * open);
					feather.Transform.localScale = new Vector3(length * feather.Side, 1f, 1f);
					float own = Mathf.Clamp01(breakProgress * 1.35f - feather.Fraction * 0.35f);
					block.SetFloat(DISSOLVE_ID, own);
					block.SetFloat(ALPHA_ID, 1f - UltimateFxRun.EaseInOut(UltimateFxRun.Span(own, 0.35f, 0.95f)));
					block.SetFloat(SEED_ID, feather.Seed);
					block.SetFloat(FLASH_ID, 0.5f * Spike(age, 0f, 0.001f, 0.05f));
					feather.Renderer.SetPropertyBlock(block);
				}
			}, () => run.Destroy(wings));

			// 흩어지는 깃과 빛 알갱이
			Material driftMaterial = Energy(run, FRONT_QUEUE);
			ParticleSystem drift = MeshParticles(run, "FormDrift", particleMesh, driftMaterial, driftFeathers * 2 + 8, 0.06f, false, 0f, 0f, false);
			UltimateFxStage.SetLifetimeLook(drift, UltimateFxStage.FadeGradient(Color.white, 0.05f, 0.5f), AnimationCurve.Linear(0f, 1f, 1f, 0.6f));
			ParticleSystem motes = Motes(run, "FormWingMotes", 160, -0.05f, 0.5f);
			void Shed(int featherCount, int moteCount, float speed)
			{
				Pose pose = PoseNow(run);
				for (int i = 0; i < featherCount + moteCount; i++)
				{
					Feather feather = feathers[Random.Range(0, feathers.Count)];
					Vector3 from = feather.Transform.TransformPoint(new Vector3(Random.Range(0.35f, 0.95f), 0f, 0f));
					Vector3 outward = (from - Chest(pose)).normalized;
					Vector3 velocity = outward * Random.Range(0.4f, 1f) * speed + Vector3.up * Random.Range(0.2f, 0.9f);
					if (i < featherCount)
						EmitMesh(drift, from, velocity, Random.Range(0.22f, 0.42f), Random.Range(0.8f, 1.3f), Color.white, Random.insideUnitSphere * 180f, Random.insideUnitSphere * 240f);
					else
						UltimateFxStage.Emit(motes, from, velocity, Random.Range(0.04f, 0.1f), Random.Range(0.6f, 1.2f), new Color(1f, 0.86f, 0.5f, 1f));
				}
			}
			run.At(ImpactTime + 0.03f, () => Shed(driftFeathers / 3, 30, 3.2f));
			for (int i = 0; i < 5; i++)
			{
				float time = ImpactTime + wingHold + wingBreakSeconds * i / 5f;
				run.At(time, () => Shed(driftFeathers / 5, 16, 1.3f));
			}
		}

		// 위에서 내려와 몸을 감싸는 빛기둥. 심과 겉 두 겹
		private void ComposePillar(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			float start = ImpactTime + pillarDelay;
			float end = ImpactTime + pillarHold + pillarFade;
			Mesh mesh = Keep(run, FormMeshes.Column(28));
			List<(GameObject column, Material material, float radius)> columns = new();
			for (int i = 0; i < 2; i++)
			{
				Material material = new(beamShader) { name = "FormFx_Pillar", renderQueue = BEHIND_BODY_QUEUE - 4 + i };
				run.OnRelease(() => Object.Destroy(material));
				material.SetColor("_Color", bodyColor * (i == 0 ? 1f : 1.4f));
				material.SetColor("_CoreColor", i == 0 ? bodyColor * 1.4f : coreColor);
				material.SetFloat("_EdgePower", i == 0 ? 3.2f : 2.6f);
				material.SetFloat("_StreakAmount", 0.75f);
				material.SetFloat("_SoftDistance", 0.4f);
				GameObject column = UltimateFxStage.CreateMeshObject("FormPillar", run.Root, mesh, material);
				column.SetActive(false);
				columns.Add((column, material, i == 0 ? pillarRadius : pillarRadius * 0.42f));
			}
			ParticleSystem rise = Motes(run, "FormPillarMotes", 140, -0.1f, 0.15f);
			float carry = 0f;
			run.Track(start, pillarHold, _ =>
			{
				Pose pose = PoseNow(run);
				for (carry += pillarMotesPerSecond * run.Delta; carry >= 1f; carry -= 1f)
				{
					Vector3 from = pose.Foot + UltimateFxStage.RandomRingPoint(0.1f, pillarRadius * 1.1f) + Vector3.up * Random.Range(0f, 0.4f);
					UltimateFxStage.Emit(rise, from, Vector3.up * Random.Range(2.5f, 6f), Random.Range(0.05f, 0.12f), Random.Range(0.5f, 0.9f), new Color(1f, 0.85f, 0.45f, 1f));
				}
			});
			run.Track(start, end - start, _ =>
			{
				Pose pose = PoseNow(run);
				float now = run.Now;
				float open = OverShoot(UltimateFxRun.Span(now, start, start + 0.12f), 0.25f);
				float fade = 1f - UltimateFxRun.EaseInOut(UltimateFxRun.Span(now, ImpactTime + pillarHold, end));
				foreach ((GameObject column, Material material, float radius) in columns)
				{
					column.SetActive(true);
					column.transform.position = pose.Foot + pose.Away * 0.12f + Vector3.up * (pillarHeight * 0.5f);
					float width = radius * 2f * (0.5f + 0.5f * open) * Mathf.Lerp(0.25f, 1f, fade);
					column.transform.localScale = new Vector3(width, pillarHeight * 0.5f, width);
					material.SetFloat(INTENSITY_ID, pillarIntensity * fade * (1f + 0.9f * Spike(now, start, 0.02f, 0.1f)));
				}
			}, () =>
			{
				foreach ((GameObject column, Material _, float _) in columns)
					run.Destroy(column);
			});
		}

		// 머리 위 고리. 작게 나와 커지며 기울고 점선으로 부서짐
		private void ComposeHalo(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			float start = ImpactTime + haloDelay;
			float end = start + haloHold + 0.35f;
			Material material = Energy(run, FRONT_QUEUE);
			material.SetFloat("_BodyAlpha", 0.95f);
			material.SetFloat("_EdgeAmount", 0f);
			material.SetFloat("_CoreWidth", 0.4f);
			material.SetVector("_NoiseScale", new Vector4(26f, 2f, 0f, 0f));
			material.SetFloat("_FlowAmount", 0.2f);
			material.SetColor(TIP_COLOR_ID, bodyColor);
			GameObject ring = UltimateFxStage.CreateMeshObject("FormHalo", run.Root, Keep(run, UltimateMeshFactory.Torus(72, 10, 0.085f)), material);
			ring.SetActive(false);
			run.Track(start, end - start, _ =>
			{
				Pose pose = PoseNow(run);
				float now = run.Now;
				ring.SetActive(true);
				float grow = UltimateFxRun.EaseOut(UltimateFxRun.Span(now, start, start + haloHold));
				float broken = UltimateFxRun.Span(now, start + haloHold * 0.7f, end);
				float radius = Mathf.Lerp(haloRadius.x, haloRadius.y, grow);
				Quaternion tilt = Quaternion.AngleAxis(Mathf.Lerp(haloTilt.x, haloTilt.y, grow), pose.Away) * Quaternion.AngleAxis(-12f, pose.Right);
				ring.transform.position = pose.Foot + Vector3.up * headHeight;
				ring.transform.rotation = tilt * Quaternion.AngleAxis(now * 70f, Vector3.up);
				ring.transform.localScale = new Vector3(radius, radius, radius);
				material.SetFloat(DISSOLVE_ID, broken);
				material.SetFloat(FLASH_ID, 0.7f * Spike(now, start, 0.001f, 0.06f));
				material.SetFloat(ALPHA_ID, 1f - UltimateFxRun.Span(now, end - 0.2f, end));
			}, () => run.Destroy(ring));
		}

		// 발밑: 바깥으로 누운 불꽃 잎, 충격파, 먼지, 가로로 퍼지는 빛 줄
		private void ComposeGround(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			float end = ImpactTime + pillarHold + pillarFade;
			Material material = Energy(run, BEHIND_BODY_QUEUE - 8);
			material.SetFloat("_BodyAlpha", 0.35f);
			material.SetFloat(INTENSITY_ID, 0.7f);
			material.SetFloat("_FlowAmount", 0.6f);
			material.SetFloat("_Flow", 3f);
			Mesh mesh = Keep(run, FormMeshes.Feather(12, 0.34f, -0.18f, 0f, 0.3f));
			List<(Transform petal, float angle, float length, float seed)> petals = new();
			for (int i = 0; i < petalCount; i++)
			{
				GameObject petal = UltimateFxStage.CreateMeshObject("FormPetal", run.Root, mesh, material);
				petal.SetActive(false);
				petals.Add((petal.transform, (i + Random.Range(-0.3f, 0.3f)) / petalCount * 360f, Random.Range(petalLength.x, petalLength.y), Random.value * 10f));
			}
			run.Track(ImpactTime, end - ImpactTime, _ =>
			{
				Pose pose = PoseNow(run);
				float now = run.Now;
				float pop = 0.75f + 0.25f * OverShoot(UltimateFxRun.Span(now, ImpactTime, ImpactTime + 0.12f), 0.3f);
				float broken = UltimateFxRun.Span(now, ImpactTime + 0.12f, ImpactTime + 0.5f);
				foreach ((Transform petal, float angle, float length, float seed) in petals)
				{
					petal.gameObject.SetActive(true);
					float lick = 1f + 0.16f * Mathf.Sin(now * 17f + seed * 3f);
					Quaternion rotation = Quaternion.Euler(0f, angle, 0f) * Quaternion.Euler(-72f, 0f, 0f);
					petal.SetPositionAndRotation(pose.Foot + Vector3.up * 0.05f + Quaternion.Euler(0f, angle, 0f) * Vector3.right * 0.18f, rotation);
					petal.localScale = new Vector3(length * pop * lick, pop, 1f);
				}
				material.SetFloat(DISSOLVE_ID, broken);
				material.SetFloat(FLASH_ID, 0.6f * Spike(now, ImpactTime, 0.001f, 0.06f));
			}, () =>
			{
				foreach ((Transform petal, float _, float _, float _) in petals)
					run.Destroy(petal.gameObject);
			});

			ParticleSystem streaks = Streaks(run, "FormRingStreaks", 90, 0.05f);
			run.At(ImpactTime, () =>
			{
				Pose pose = PoseNow(run);
				Vector3 foot = pose.Foot;
				Vector3 waist = foot + Vector3.up * (chestHeight * 0.8f);
				for (int i = 0; i < 46; i++)
				{
					Vector3 direction = UltimateFxStage.RandomRingPoint(0.99f, 1f);
					direction.y = Random.Range(-0.05f, 0.22f);
					UltimateFxStage.Emit(streaks, waist + direction * 0.4f, direction * Random.Range(5f, 12f), Random.Range(0.05f, 0.1f), Random.Range(0.2f, 0.42f), new Color(1f, 0.84f, 0.45f, 1f));
				}
				run.ThinRing(run.Now, foot, 0.6f, shockRadius, 0.55f, new Color(1f, 0.78f, 0.35f, 0.8f), 6);
				run.React(foot, shockRadius * 2f, shockRadius, false);
				DustRing(run, foot, 22, shockRadius * 0.5f, new Color(0.62f, 0.55f, 0.42f, 0.5f), 1f);
				GroundGlow(run, foot, 5f, bodyColor * 0.16f, run.Now, 0.02f, 0.35f, 1.4f);
			});
		}
	}
}
