using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	/// <summary>
	/// 인물 형태 확정 순간의 변신 연출 (알리사 화력). 가슴 빛에서 무기를 뽑고, 손 위로 빛 막대가 뻗고, 날개와 빛기둥과 고리가 터짐
	/// - 흐름: 모임 (어두워짐, 금빛 실이 가슴으로) -> 뽑음과 뻗음 (한 프레임) -> 터짐 (날개, 조금 뒤 기둥, 더 뒤 고리) -> 막대가 끝부터 부서져 완드로 -> 깃이 흩어짐
	/// - 나타나는 것은 전부 한 프레임, 순서 차이가 풍성함을 만듦. 사라짐은 길고 방향이 있음 (레퍼런스 실측 memo wm/design/alisa-confirm-moment-20261123)
	/// - 시전자를 따라다님. 정지, 느려짐, 줌, 흔들림은 화면 박자표 (FeelUltimateFx) 몫
	/// - 이 파일: 값, 시간선, 좌표, 모임, 화면 빛. 무기는 .Weapon.cs, 날개와 기둥과 고리와 발밑은 .Ascend.cs
	/// </summary>
	[CreateAssetMenu(fileName = "FormFx_New", menuName = "WM/CombatFx/" + nameof(FormTransformFx))]
	public sealed partial class FormTransformFx : WorldUltimateFx
	{
		private static readonly int CORE_COLOR_ID = Shader.PropertyToID("_CoreColor");
		private static readonly int BODY_COLOR_ID = Shader.PropertyToID("_BodyColor");
		private static readonly int TIP_COLOR_ID = Shader.PropertyToID("_TipColor");
		private static readonly int ALPHA_ID = Shader.PropertyToID("_Alpha");
		private static readonly int FLASH_ID = Shader.PropertyToID("_Flash");
		private static readonly int REVEAL_ID = Shader.PropertyToID("_Reveal");
		private static readonly int SEED_ID = Shader.PropertyToID("_Seed");
		private static readonly int INTENSITY_ID = Shader.PropertyToID("_Intensity");
		// 인물 그림 (큐 3000) 뒤와 앞
		private const int BEHIND_BODY_QUEUE = 2962;
		private const int FRONT_QUEUE = 3012;

		[Header("셰이더")]
		[SerializeField] private Shader energyShader;
		[SerializeField] private Shader beamShader;

		[Header("색")]
		[SerializeField, ColorUsage(false, true)] private Color coreColor = new(2f, 1.75f, 1.15f);
		[SerializeField, ColorUsage(false, true)] private Color bodyColor = new(1.35f, 0.78f, 0.16f);
		[SerializeField, ColorUsage(false, true)] private Color tipColor = new(1.2f, 0.45f, 0.06f);
		[SerializeField] private Color lightColor = new(1f, 0.7f, 0.36f, 1f);

		[Header("시간 (연출 초)")]
		[SerializeField, Min(0.05f), Tooltip("모임. 이 끝에서 뽑음")] private float gatherSeconds = 0.34f;
		[SerializeField, Min(0.02f), Tooltip("뽑은 막대가 가슴에서 손 위로 올라가는 길이")] private float raiseSeconds = 0.16f;
		[SerializeField, Min(0.05f), Tooltip("뽑음에서 터짐까지")] private float impactDelay = 0.3f;
		[SerializeField, Min(0f), Tooltip("터짐 뒤 빛기둥이 솟는 시각")] private float pillarDelay = 0.04f;
		[SerializeField, Min(0f), Tooltip("터짐 뒤 머리 위 고리 시각")] private float haloDelay = 0.24f;
		[SerializeField, Min(0.1f), Tooltip("터짐 뒤 막대가 부서지기 시작")] private float barBreakDelay = 0.14f;
		[SerializeField, Min(0.1f)] private float barBreakSeconds = 0.4f;
		[SerializeField, Min(0.2f), Tooltip("터짐 뒤 날개가 흩어지기 시작")] private float wingHold = 0.95f;
		[SerializeField, Min(0.1f)] private float wingBreakSeconds = 0.5f;
		[SerializeField, Min(0.2f), Tooltip("터짐 뒤 빛기둥이 남는 길이")] private float pillarHold = 0.75f;
		[SerializeField, Min(0.1f)] private float pillarFade = 0.3f;
		[SerializeField, Min(0.2f)] private float haloHold = 0.6f;
		[SerializeField, Min(0.2f), Tooltip("터짐 뒤 완드가 떠 있는 길이")] private float wandHold = 2.9f;

		[SerializeField, Range(0.01f, 1f), Tooltip("모임과 뽑음 동안 게임 시간 배율 (적이 멈춤). 터진 뒤 느려짐은 화면 박자표")] private float gatherTimeScale = 0.08f;

		[Header("크기 (월드)")]
		[SerializeField, Min(0.1f)] private float chestHeight = 0.46f;
		[SerializeField, Min(0.1f)] private float headHeight = 1.75f;
		[SerializeField, Tooltip("손 자리 (화면 오른쪽, 위)")] private Vector2 handOffset = new(0.3f, 0.34f);
		[SerializeField, Range(0f, 1f), Tooltip("날개 판을 카메라 쪽으로 눕히는 정도")] private float faceCamera = 0.55f;

		[Header("모임")]
		[SerializeField, Min(0)] private int threadCount = 18;
		[SerializeField] private Vector2 threadRadius = new(1.4f, 2.9f);
		[SerializeField] private Vector2 threadHeight = new(0.05f, 2f);
		[SerializeField, Min(0.005f)] private float threadWidth = 0.06f;
		[SerializeField, Min(0.1f)] private float orbSize = 0.15f;
		[SerializeField, Min(0.1f)] private float orbGlowSize = 0.55f;

		[Header("빛")]
		[SerializeField, Range(0f, 1f), Tooltip("모임에서 해 세기 배율")] private float sunDim = 0.22f;
		[SerializeField, Range(0f, 1f)] private float ambientDim = 0.3f;
		[SerializeField, Min(0f)] private float gatherLight = 5f;
		[SerializeField, Min(0f)] private float impactLight = 9f;
		[SerializeField, Min(1f)] private float lightRange = 9f;
		[SerializeField, Min(0f)] private float bloom = 0.65f;
		[SerializeField, Min(0.1f), Tooltip("장면 빛이 돌아오는 길이")] private float restoreSeconds = 0.7f;

		private float DrawTime => gatherSeconds;
		private float ImpactTime => gatherSeconds + impactDelay;
		private float RestoreTime => ImpactTime + wingHold - 0.3f;

		/// <summary> 터짐 시각 (연출 초). 화면 박자표의 타격 시각과 맞춤 </summary>
		public float ImpactSeconds => ImpactTime;
		public override float DurationSeconds => ImpactTime + Mathf.Max(wingHold + wingBreakSeconds, wandHold);

		/// <summary> 매 프레임 시전자와 카메라에서 다시 계산하는 자리 </summary>
		private readonly struct Pose
		{
			public readonly Vector3 Foot;
			public readonly Vector3 Right;
			public readonly Vector3 Up;
			public readonly Vector3 Away;

			public Pose(UltimateFxRun run, float faceCamera)
			{
				Transform cameraTransform = run.MainCamera.transform;
				Foot = run.Caster;
				Right = cameraTransform.right;
				Up = Vector3.Slerp(Vector3.up, cameraTransform.up, faceCamera).normalized;
				Away = Vector3.Cross(Right, Up).normalized;
			}

			public Quaternion Facing => Quaternion.LookRotation(Away, Up);
		}

		private Pose PoseNow(UltimateFxRun run) => new(run, faceCamera);
		private Vector3 Chest(in Pose pose) => pose.Foot + Vector3.up * chestHeight;
		private Vector3 Hand(in Pose pose) => pose.Foot + pose.Right * handOffset.x + pose.Up * handOffset.y - pose.Away * 0.12f;

		protected override void ComposeWorld(Stage stage)
		{
			if (energyShader == null || beamShader == null)
				throw new InvalidOperationException($"{name}: energyShader 또는 beamShader 미연결");
			stage.Run.At(0f, () => stage.Run.HitStop(ImpactTime, gatherTimeScale));
			ComposeGather(stage);
			ComposeBar(stage);
			ComposeWand(stage);
			ComposeWings(stage);
			ComposePillar(stage);
			ComposeHalo(stage);
			ComposeGround(stage);
			ComposeLights(stage);
			ComposeGrade(stage);
		}

		private Material Energy(UltimateFxRun run, int queue)
		{
			Material material = new(energyShader) { name = "FormFx_Energy", renderQueue = queue };
			material.SetColor(CORE_COLOR_ID, coreColor);
			material.SetColor(BODY_COLOR_ID, bodyColor);
			material.SetColor(TIP_COLOR_ID, tipColor);
			run.OnRelease(() => UnityEngine.Object.Destroy(material));
			return material;
		}

		// ---- 모임: 금빛 실이 나선으로 가슴에 빨려듦. 가슴 빛이 커지고 뽑기 직전 한 번 더 부풂 ----
		private void ComposeGather(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			const int POINTS = 14;
			Material threadMaterial = Energy(run, FRONT_QUEUE);
			threadMaterial.SetFloat("_Softness", 1f);
			threadMaterial.SetFloat("_CoreWidth", 0.5f);
			threadMaterial.SetFloat("_EdgeAmount", 0f);
			threadMaterial.SetFloat(INTENSITY_ID, 1.5f);
			List<LineRenderer> lines = new();
			List<Vector4> seeds = new();
			for (int i = 0; i < threadCount; i++)
			{
				GameObject lineObject = new("FormThread");
				lineObject.transform.SetParent(run.Root, false);
				LineRenderer line = lineObject.AddComponent<LineRenderer>();
				line.sharedMaterial = threadMaterial;
				line.positionCount = POINTS;
				line.widthMultiplier = threadWidth * Random.Range(0.6f, 1.2f);
				line.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0.2f));
				line.alignment = LineAlignment.View;
				line.enabled = false;
				lines.Add(line);
				seeds.Add(new Vector4(i / (float)threadCount * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f), Random.Range(threadRadius.x, threadRadius.y), Random.Range(threadHeight.x, threadHeight.y), Random.Range(0f, 0.35f)));
			}
			run.Track(0f, gatherSeconds + 0.06f, _ =>
			{
				Pose pose = PoseNow(run);
				for (int i = 0; i < lines.Count; i++)
				{
					Vector4 seed = seeds[i];
					float start = seed.w * gatherSeconds;
					float head = UltimateFxRun.EaseIn(UltimateFxRun.Span(run.Now, start, gatherSeconds * 0.92f));
					float tail = UltimateFxRun.EaseIn(UltimateFxRun.Span(run.Now, start + gatherSeconds * 0.18f, gatherSeconds + 0.05f));
					lines[i].enabled = head > 0.001f && tail < 0.999f;
					for (int point = 0; point < POINTS; point++)
					{
						float travel = Mathf.Lerp(tail, head, point / (POINTS - 1f));
						float angle = seed.x + travel * 2.2f;
						float radius = seed.y * Mathf.Pow(1f - travel, 1.15f);
						Vector3 position = pose.Foot + new Vector3(Mathf.Cos(angle) * radius, Mathf.Lerp(seed.z, chestHeight, travel), Mathf.Sin(angle) * radius);
						lines[i].SetPosition(point, position - pose.Away * 0.15f * travel);
					}
				}
			}, () =>
			{
				foreach (LineRenderer line in lines)
					line.enabled = false;
			});

			Material orbMaterial = Glow(run, new Color(1f, 0.9f, 0.6f, 1f), coreColor * 1.4f, coreColor, false);
			GameObject orb = UltimateFxStage.CreateMeshObject("FormOrb", run.Root, Keep(run, UltimateMeshFactory.Sphere(12, 18)), orbMaterial);
			Material haloMaterial = run.Additive(UltimateFxTextures.Soft, Color.clear);
			GameObject glow = run.Billboard("FormOrbGlow", haloMaterial, run.Caster, 1f, 1f, false, false, 14);
			run.Track(0f, ImpactTime + 0.25f, _ =>
			{
				Pose pose = PoseNow(run);
				float now = run.Now;
				float grow = UltimateFxRun.EaseOut(UltimateFxRun.Span(now, 0.05f, gatherSeconds)) * 0.75f;
				float swell = Spike(now, DrawTime - 0.08f, 0.08f, 0.07f);
				float fade = 1f - UltimateFxRun.Span(now, DrawTime + 0.05f, ImpactTime + 0.2f);
				float size = (grow + swell * 0.8f) * fade;
				Vector3 position = Chest(pose) - pose.Away * 0.16f;
				orb.transform.position = position;
				orb.transform.localScale = Vector3.one * orbSize * size;
				glow.transform.position = position;
				glow.transform.localScale = new Vector3(orbGlowSize * (0.4f + size), orbGlowSize * (0.4f + size), 1f);
				haloMaterial.SetColor("_Color", lightColor * (0.65f * size));
			}, () =>
			{
				run.Destroy(orb);
				run.Destroy(glow);
			});
		}

		// ---- 광원: 가슴 점광 (모임에서 커지고 터짐에 최고) 하나, 머리 위 점광 (기둥) 하나 ----
		private void ComposeLights(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			Light chest = WorldLight(stage, "FormChestLight", LightType.Point, lightColor, lightRange);
			Light above = WorldLight(stage, "FormAboveLight", LightType.Point, lightColor, lightRange * 1.3f);
			run.Track(0f, DurationSeconds, _ =>
			{
				Pose pose = PoseNow(run);
				float now = run.Now;
				chest.transform.position = Chest(pose) + Vector3.up * 0.5f - pose.Away * 0.45f;
				float gather = gatherLight * UltimateFxRun.EaseIn(UltimateFxRun.Span(now, 0f, gatherSeconds)) * (1f - UltimateFxRun.Span(now, ImpactTime, ImpactTime + 0.4f));
				chest.intensity = gather + gatherLight * 2f * Spike(now, DrawTime, 0.02f, 0.06f) + impactLight * Spike(now, ImpactTime, 0.02f, 0.28f);
				above.transform.position = pose.Foot + Vector3.up * 2.6f;
				above.intensity = impactLight * 0.6f * Plateau(now, ImpactTime + pillarDelay, 0.05f, ImpactTime + pillarHold, pillarFade);
			}, () =>
			{
				chest.enabled = false;
				above.enabled = false;
			});
		}

		private void ComposeGrade(Stage stage)
		{
			stage.BloomTint = new Color(1f, 0.9f, 0.65f);
			stage.BloomThreshold = 1.15f;
			float Dim(float now) => Plateau(now, 0f, gatherSeconds * 0.7f, RestoreTime, restoreSeconds);
			stage.SunScale = now => Mathf.Lerp(1f, sunDim, Dim(now));
			stage.AmbientScale = now => Mathf.Lerp(1f, ambientDim, Dim(now));
			stage.Vignette = now => 0.3f * Dim(now);
			stage.Filter = now => Color.Lerp(Color.white, new Color(0.9f, 0.92f, 1f), Dim(now) * 0.7f);
			stage.Bloom = now => bloom * Mathf.Max(0.5f * Dim(now), Plateau(now, DrawTime, 0.04f, RestoreTime, restoreSeconds));
		}
	}
}
