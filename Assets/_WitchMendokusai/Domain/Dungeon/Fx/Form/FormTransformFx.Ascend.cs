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
			[Min(0f)] public float Intensity;
		}

		[Header("날개")]
		[SerializeField] private FeatherLayer[] layers =
		{
			new() { Count = 6, Angles = new Vector2(70f, -14f), Lengths = new Vector2(1.8f, 0.95f), Roots = new Vector2(0.5f, 0.12f), Width = 0.36f, Forward = 0f, Intensity = 1f },
			new() { Count = 5, Angles = new Vector2(62f, -8f), Lengths = new Vector2(1.1f, 0.55f), Roots = new Vector2(0.42f, 0.1f), Width = 0.32f, Forward = 0.05f, Intensity = 1f },
			new() { Count = 4, Angles = new Vector2(54f, 2f), Lengths = new Vector2(0.62f, 0.34f), Roots = new Vector2(0.32f, 0.08f), Width = 0.27f, Forward = 0.09f, Intensity = 1f },
		};
		[SerializeField] private Vector3 featherShape = new(0.16f, 0.22f, 0.5f);
		[SerializeField, Min(0.1f)] private float wingScale = 1.7f;
		[SerializeField, Min(0f), Tooltip("어깨가 몸 가운데에서 벌어진 거리")] private float shoulderGap = 0.16f;
		[SerializeField, Range(0f, 60f), Tooltip("날개 끝이 뒤로 젖혀진 각도 (깊이)")] private float wingYaw = 10f;
		[SerializeField, Range(-0.5f, 0.5f), Tooltip("날개 색 단을 낮춤. 무기와 얼굴보다 어둡게")] private float wingShade = -0.16f;
		[SerializeField, Min(0f)] private float wingDepth = 0.2f;
		[SerializeField, Min(0)] private int driftFeathers = 26;

		[Header("기둥과 고리")]
		[SerializeField, Min(0.1f)] private float pillarRadius = 0.85f;
		[SerializeField, Min(1f)] private float pillarHeight = 9f;
		[SerializeField, Min(0)] private int pillarMotesPerSecond = 90;
		[SerializeField, Range(0f, 1f)] private float pillarIntensity = 0.8f;
		[SerializeField] private Vector2 haloRadius = new(0.4f, 0.44f);
		[SerializeField] private Vector2 haloTilt = new(4f, 9f);

		[Header("발밑")]
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
			public float TexelsAcross;
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
				material.SetFloat(SHAPE_ID, FEATHER);
				material.SetFloat(RIM_AMOUNT_ID, layerIndex == 0 ? 1f : 0.35f);
				material.SetFloat(SHADE_ID, wingShade);
				material.SetFloat(INTENSITY_ID, layer.Intensity);
				material.SetFloat("_RootFade", 0.16f);
				Mesh mesh = Keep(run, FormMeshes.Feather(7, layer.Width, featherShape.x, featherShape.y, featherShape.z));
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
						TexelsAcross = Mathf.Round(layer.Width * wingScale * texelsPerUnit),
					});
				}
			}
			run.At(ImpactTime, () => wings.SetActive(true));
			MaterialPropertyBlock block = new();
			run.Track(ImpactTime, end - ImpactTime, _ =>
			{
				Pose pose = PoseNow(run);
				float now = Stepped(run.Now);
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
					block.SetVector(TEXELS_ID, new Vector4(Mathf.Round(feather.Length * wingScale * texelsPerUnit), feather.TexelsAcross, 0f, 0f));
					block.SetFloat(SEED_ID, feather.Seed);
					block.SetFloat(FLASH_ID, 0.5f * Spike(age, 0f, 0.001f, 0.05f));
					feather.Renderer.SetPropertyBlock(block);
				}
			}, () => run.Destroy(wings));

			// 흩어지는 깃과 빛 알갱이
			Material driftMaterial = Energy(run, FRONT_QUEUE);
			driftMaterial.SetFloat(SHAPE_ID, FEATHER);
			driftMaterial.SetVector(TEXELS_ID, new Vector4(14f, 6f, 0f, 0f));
			ParticleSystem drift = MeshParticles(run, "FormDrift", particleMesh, driftMaterial, driftFeathers * 2 + 8, 0.06f, false, 0f, 0f, false);
			UltimateFxStage.SetLifetimeLook(drift, null, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 0.9f), new Keyframe(1f, 0f)));
			ParticleSystem motes = PixelMotes(run, "FormWingMotes", 160, -0.05f, 0.5f);
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
			Material material = new(columnShader) { name = "FormFx_Column", renderQueue = BEHIND_BODY_QUEUE - 4 };
			run.OnRelease(() => Object.Destroy(material));
			material.SetColor(CORE_COLOR_ID, coreColor);
			material.SetColor(BODY_COLOR_ID, bodyColor);
			material.SetColor(TIP_COLOR_ID, tipColor);
			GameObject column = UltimateFxStage.CreateMeshObject("FormPillar", run.Root, Keep(run, FormMeshes.Bounds()), material);
			column.SetActive(false);
			ParticleSystem rise = PixelMotes(run, "FormPillarMotes", 140, -0.1f, 0.15f);
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
				float open = OverShoot(UltimateFxRun.Span(Stepped(now), start, start + 0.12f), 0.25f);
				float fade = 1f - UltimateFxRun.EaseInOut(UltimateFxRun.Span(Stepped(now), ImpactTime + pillarHold, end));
				column.SetActive(true);
				column.transform.position = pose.Foot + pose.Away * 0.12f;
				float thin = Mathf.Lerp(1f, 0.55f, UltimateFxRun.EaseInOut(UltimateFxRun.Span(Stepped(now), start + 0.2f, start + 0.5f)));
				float width = pillarRadius * 2f * (0.5f + 0.5f * open) * fade * thin;
				column.transform.localScale = new Vector3(width, pillarHeight, width);
				material.SetFloat(ALPHA_ID, pillarIntensity * (1f + 0.4f * Spike(now, start, 0.02f, 0.1f)));
			}, () => run.Destroy(column));
		}

		// 머리 위 고리. 작게 나와 커지며 기울고 점선으로 부서짐
		private void ComposeHalo(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			float start = ImpactTime + haloDelay;
			float end = start + haloHold + 0.35f;
			Material material = Energy(run, FRONT_QUEUE);
			material.SetVector("_NoiseScale", new Vector4(26f, 2f, 0f, 0f));
			material.SetVector(TEXELS_ID, new Vector4(96f, 6f, 0f, 0f));
			GameObject ring = UltimateFxStage.CreateMeshObject("FormHalo", run.Root, Keep(run, UltimateMeshFactory.Torus(18, 5, 0.17f)), material);
			ring.SetActive(false);
			run.Track(start, end - start, _ =>
			{
				Pose pose = PoseNow(run);
				float now = run.Now;
				ring.SetActive(true);
				now = Stepped(now);
				float grow = UltimateFxRun.EaseOut(UltimateFxRun.Span(now, start, start + haloHold));
				float broken = UltimateFxRun.Span(now, start + haloHold * 0.7f, end);
				float radius = Mathf.Lerp(haloRadius.x, haloRadius.y, grow);
				Quaternion tilt = Quaternion.AngleAxis(Mathf.Lerp(haloTilt.x, haloTilt.y, grow), pose.Away) * Quaternion.AngleAxis(-12f, pose.Right);
				ring.transform.position = pose.Foot + Vector3.up * headHeight;
				ring.transform.rotation = tilt * Quaternion.AngleAxis(now * 70f, Vector3.up);
				ring.transform.localScale = new Vector3(radius, radius, radius);
				material.SetFloat(DISSOLVE_ID, broken > 0f ? 0.3f + 0.7f * broken : 0f);
				material.SetFloat(FLASH_ID, 0.7f * Spike(now, start, 0.001f, 0.06f));
			}, () => run.Destroy(ring));
		}

		// 발밑: 퍼지는 고리, 풀 바람, 가로로 튀는 빛 줄
		private void ComposeGround(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			// 바닥 고리: 저폴리 납작 고리가 퍼지며 텍셀째 부서짐
			const float RING_SECONDS = 0.6f;
			Material material = Energy(run, BEHIND_BODY_QUEUE - 8);
			material.SetVector("_NoiseScale", new Vector4(60f, 2f, 0f, 0f));
			material.SetFloat("_DissolveFromTip", 0f);
			material.SetVector(TEXELS_ID, new Vector4(180f, 3f, 0f, 0f));
			GameObject ring = UltimateFxStage.CreateMeshObject("FormGroundRing", run.Root, Keep(run, FormMeshes.Ring(28, 0.93f)), material);
			ring.SetActive(false);
			run.Track(ImpactTime, RING_SECONDS, progress =>
			{
				Pose pose = PoseNow(run);
				float stepped = Mathf.Clamp01((Stepped(run.Now) - ImpactTime) / RING_SECONDS);
				ring.SetActive(true);
				float radius = Mathf.Lerp(0.5f, shockRadius, UltimateFxRun.EaseOut(stepped));
				ring.transform.position = pose.Foot + Vector3.up * 0.06f;
				ring.transform.localScale = new Vector3(radius, 1f, radius);
				material.SetFloat(DISSOLVE_ID, UltimateFxRun.Span(stepped, 0.35f, 1f));
			}, () => run.Destroy(ring));

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
				run.React(foot, shockRadius * 2f, shockRadius, false);
			});
		}
	}
}
