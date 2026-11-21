using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 태양 광선 강한 판 (강력 스킬용). 머리 위에 태양 구가 차오르고 경로에 금빛 고리가 서며, 굵은 광선이 땅을 긁어 녹은 줄을 그린 뒤 끝에서 폭발
	/// - 흐름: 태양 구 충전 (빨려드는 빛 알갱이) -> 연보라 물들임과 경로 고리 다섯 -> 발사 (흰 3장은 화면 연출) -> 쓸기 2.8초 (닿는 점이 앞으로 나가며 좌우로, 고리를 차례로 깨뜨림)
	///   -> 끝 폭발 -> 녹은 줄 잔류
	/// - 광선은 태양 구에서 땅의 닿는 점까지 원기둥 세 겹 (흰 심, 노랑, 주황 겉). 연출 광원은 태양 구 점광과 닿는 점 점광 둘
	/// - 흔들림, 정지, 섬광은 화면 연출 박자표 (FeelUltimateFx) 몫. 쓸기 동안은 흔들지 않음 (멀미), 대신 광선 밝기 박동
	/// - 설계 memo wm/design/ult3d-all-20261120/06_sun-beam.md, 제작 기록 ult3d-batch2-20261121
	/// - 재생: 개발 창 fx ultimate SunBeamHeavy
	/// </summary>
	[CreateAssetMenu(fileName = "Ult3D_SunBeamHeavy", menuName = "WM/CombatFx/Ultimate/" + nameof(SunBeamHeavyUltimateFx))]
	public sealed class SunBeamHeavyUltimateFx : WorldUltimateFx
	{
		[Header("색")]
		[SerializeField, ColorUsage(false, true)] private Color sunColor = new(3.2f, 2.6f, 1.2f);
		[SerializeField, ColorUsage(false, true)] private Color beamCore = new(3.4f, 3.2f, 2.6f);
		[SerializeField, ColorUsage(false, true)] private Color beamMiddle = new(2.6f, 1.9f, 0.5f);
		[SerializeField, ColorUsage(false, true)] private Color beamOuter = new(1.8f, 0.7f, 0.15f);
		[SerializeField] private Color sunLightColor = new(1f, 0.9f, 0.6f, 1f);
		[SerializeField] private Color contactLightColor = new(1f, 0.6f, 0.25f, 1f);
		[SerializeField] private Color ringBase = new(0.85f, 0.65f, 0.3f, 1f);
		[SerializeField, ColorUsage(false, true)] private Color ringGlow = new(1.6f, 1.1f, 0.35f);
		[SerializeField, ColorUsage(false, true)] private Color meltColor = new(2.2f, 0.8f, 0.2f);
		[SerializeField] private Color steamColor = new(0.75f, 0.7f, 0.68f, 0.6f);
		[SerializeField] private Color tintColor = new(0.86f, 0.82f, 1f);

		[Header("방향과 크기")]
		[SerializeField, Range(-60f, 60f), Tooltip("화면 오른쪽 기준 아래 (카메라 쪽) 로 꺾는 각도")] private float aimDown = 8f;
		[SerializeField, Min(0.3f)] private float sunDiameter = 1.5f;
		[SerializeField, Min(0.5f), Tooltip("태양 구 높이 (시전자 위)")] private float sunHeight = 2.4f;
		[SerializeField, Min(0.05f), Tooltip("광선 세 겹 반지름 (심, 노랑, 주황)")] private Vector3 beamRadii = new(0.35f, 0.7f, 1.1f);
		[SerializeField, Tooltip("닿는 점이 나가는 거리 (시작, 끝, 시전자 기준)")] private Vector2 sweepReach = new(2.8f, 11f);
		[SerializeField, Range(0f, 40f), Tooltip("좌우 쓸기 각도")] private float sweepAngle = 15f;
		[SerializeField, Min(0.3f), Tooltip("좌우 쓸기 주기 (초)")] private float sweepPeriod = 2.8f;

		[Header("고리")]
		[SerializeField, Min(0)] private int ringCount = 5;
		[SerializeField, Min(0.2f)] private float ringRadius = 1.2f;
		[SerializeField, Min(0.01f)] private float ringTube = 0.13f;

		[Header("시간 (초)")]
		[SerializeField, Min(0.1f), Tooltip("태양 구 충전 (m06 0.6s)")] private float chargeSeconds = 0.6f;
		[SerializeField, Min(0.1f), Tooltip("물들임과 고리 예고 (m06 1.6s)")] private float omenSeconds = 1.6f;
		[SerializeField, Min(0.5f), Tooltip("쓸기 (m06 유지 3.4s)")] private float sweepSeconds = 2.8f;
		[SerializeField, Min(0.1f)] private float meltLinger = 3.2f;
		[SerializeField, Min(0.1f)] private float restoreSeconds = 1.2f;

		[Header("빛")]
		[SerializeField, Range(0f, 1f)] private float sunDim = 0.55f;
		[SerializeField, Range(0f, 1f)] private float ambientDim = 0.6f;
		[SerializeField, Min(0f)] private float sunIntensity = 14f;
		[SerializeField, Min(1f)] private float sunRange = 9f;
		[SerializeField, Min(0f)] private float contactIntensity = 26f;
		[SerializeField, Min(0f)] private float blastIntensity = 42f;
		[SerializeField, Min(1f)] private float contactRange = 11f;

		[Header("후처리")]
		[SerializeField, Min(0f)] private float bloom = 1.6f;
		[SerializeField, Range(0f, 1f)] private float vignette = 0.25f;

		[Header("반응")]
		[SerializeField, Min(0)] private int gatherCount = 80;
		[SerializeField, Min(0)] private int flowPerSecond = 180;
		[SerializeField, Min(0)] private int sparkPerSecond = 150;
		[SerializeField, Min(0)] private int steamPerSecond = 22;
		[SerializeField, Min(0.05f), Tooltip("녹은 줄 점 간격")] private float meltSpacing = 0.22f;
		[SerializeField, Min(0.2f)] private float meltWidth = 1.3f;
		[SerializeField, Min(0)] private int blastPuffs = 44;
		[SerializeField, Min(0.5f)] private float blastRadius = 3.2f;
		[SerializeField, Min(1f)] private float windSpeed = 12f;

		// ScaleWorld 배율. 실행용 복제본에서만 1 이 아님 (리터럴 크기 값에 곱함)
		[System.NonSerialized] private float sizeScale = 1f;

		private float FireTime => chargeSeconds + omenSeconds;
		private float BlastTime => FireTime + sweepSeconds;
		private float RestoreTime => BlastTime + 0.6f;

		public override float DurationSeconds => BlastTime + meltLinger;

		public override void ScaleWorld(float scale)
		{
			sizeScale = scale;
			sunDiameter *= scale;
			sunHeight *= Mathf.Pow(scale, 0.5f);
			beamRadii *= scale;
			sweepReach *= scale;
			ringRadius *= scale;
			ringTube *= scale;
			sunRange *= scale;
			contactRange *= scale;
			meltWidth *= scale;
			blastRadius *= scale;
			gatherCount = Mathf.RoundToInt(gatherCount * scale);
			blastPuffs = Mathf.RoundToInt(blastPuffs * scale);
		}

		protected override void ComposeWorld(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			float angle = aimDown * Mathf.Deg2Rad;
			Vector3 direction = (run.ScreenRight * Mathf.Cos(angle) - run.ScreenForward * Mathf.Sin(angle)).normalized;
			// 시전 순간 자리에 고정. 시전자가 움직여도 태양 구, 고리, 닿는 점 경로가 어긋나지 않게
			Vector3 origin = run.Caster;
			Vector3 sun = origin + Vector3.up * sunHeight;

			// 닿는 점 경로: 앞으로 나가며 좌우로 쓸기 (사인). 시각 -> 위치
			Vector3 Contact(float now)
			{
				float progress = UltimateFxRun.Span(now, FireTime, BlastTime);
				float reach = Mathf.Lerp(sweepReach.x, sweepReach.y, UltimateFxRun.EaseInOut(progress));
				float yaw = sweepAngle * Mathf.Sin(progress * sweepSeconds / sweepPeriod * Mathf.PI * 2f);
				Vector3 point = origin + Quaternion.Euler(0f, yaw, 0f) * direction * reach;
				point.y = UltimateFxStage.GroundHeight(point);
				return point;
			}

			ComposeSun(stage, sun);
			List<(Transform ring, float time)> rings = ComposeRings(stage, direction, Contact);
			ComposeBeam(stage, sun, Contact, rings);
			ComposeBlast(stage, Contact(BlastTime));
			ComposeGrade(stage);
		}

		// 태양 구: 발광 구가 차오르고 겉에 옅은 빛 껍질, 둘레 빛 알갱이가 빨려듦. 태양 구 점광
		private void ComposeSun(Stage stage, Vector3 sun)
		{
			UltimateFxRun run = stage.Run;
			Mesh sphere = Keep(run, UltimateMeshFactory.Sphere(20, 32));
			Material coreMaterial = Glow(run, new Color(1f, 0.85f, 0.5f, 1f), sunColor * 0.6f, sunColor, false);
			coreMaterial.SetFloat(RIM_POWER_ID, 1.6f);
			GameObject core = UltimateFxStage.CreateMeshObject("SunCore", run.Root, sphere, coreMaterial);
			Solid(core, false);
			core.transform.position = sun;
			Material shellMaterial = run.Additive(UltimateFxTextures.Soft, Color.clear);
			GameObject shell = run.Billboard("SunShell", shellMaterial, sun, 1f, 1f, false, false, 2);
			run.Track(0f, DurationSeconds, _ =>
			{
				float now = run.Now;
				float grow = UltimateFxRun.EaseOut(UltimateFxRun.Span(now, 0f, chargeSeconds)) * (0.75f + 0.25f * UltimateFxRun.Span(now, chargeSeconds, FireTime));
				float spend = 1f - 0.35f * UltimateFxRun.Span(now, FireTime, BlastTime);
				float fade = 1f - UltimateFxRun.EaseIn(UltimateFxRun.Span(now, BlastTime, BlastTime + 0.5f));
				float pulse = 1f + 0.05f * Mathf.Sin(now * 21f);
				float size = sunDiameter * grow * spend * fade * pulse;
				core.transform.localScale = Vector3.one * size;
				core.transform.rotation = Quaternion.Euler(0f, now * 40f, 0f);
				shell.transform.localScale = new Vector3(size * 3.2f, size * 3.2f, 1f);
				shellMaterial.SetColor("_Color", sunColor * 0.35f * fade);
			}, () =>
			{
				run.Destroy(core);
				run.Destroy(shell);
			});

			ParticleSystem gather = Motes(run, "SunGather", gatherCount + 10, 0f, 0.2f);
			for (int i = 0; i < gatherCount; i++)
			{
				float time = Mathf.Lerp(0f, FireTime - 0.3f, i / (float)Mathf.Max(1, gatherCount - 1));
				run.At(time, () =>
				{
					Vector3 from = sun + Random.onUnitSphere * Random.Range(2.5f, 4.5f) * sizeScale;
					from.y = Mathf.Max(from.y, sun.y - 1.6f * sizeScale);
					float life = Random.Range(0.4f, 0.6f);
					UltimateFxStage.Emit(gather, from, (sun - from) / life, Random.Range(0.07f, 0.14f), life, new Color(1f, 0.85f, 0.45f, 1f));
				});
			}

			Light light = WorldLight(stage, "SunLight", LightType.Point, sunLightColor, sunRange);
			light.transform.position = sun + stage.ToCamera * 0.6f;
			run.Track(0f, BlastTime + 0.5f, _ =>
			{
				float now = run.Now;
				float charge = UltimateFxRun.EaseIn(UltimateFxRun.Span(now, 0f, FireTime));
				float firing = Mathf.Lerp(1f, 0.45f, UltimateFxRun.Span(now, FireTime, FireTime + 0.3f));
				float fade = 1f - UltimateFxRun.Span(now, BlastTime, BlastTime + 0.5f);
				light.intensity = sunIntensity * charge * firing * fade;
			}, () => light.enabled = false);
		}

		// 경로 고리: 닿는 점이 지나갈 자리에 세운 금빛 고리 (광선이 지나가는 방향을 향함). 광선이 닿으면 깨짐
		private List<(Transform ring, float time)> ComposeRings(Stage stage, Vector3 direction, System.Func<float, Vector3> contact)
		{
			UltimateFxRun run = stage.Run;
			Mesh torus = Keep(run, UltimateMeshFactory.Torus(48, 10, ringTube / ringRadius));
			Material material = Glow(run, ringBase, ringGlow * 0.5f, ringGlow, false);
			material.SetFloat(SPECULAR_ID, 0.8f);
			material.SetColor(DISSOLVE_EDGE_ID, ringGlow);
			List<(Transform ring, float time)> rings = new();
			for (int i = 0; i < ringCount; i++)
			{
				float hit = Mathf.Lerp(FireTime + sweepSeconds * 0.12f, BlastTime - sweepSeconds * 0.12f, i / (float)Mathf.Max(1, ringCount - 1));
				Vector3 spot = contact(hit);
				Vector3 next = contact(hit + 0.05f);
				Vector3 travel = next - spot;
				travel.y = 0f;
				Vector3 facing = travel.sqrMagnitude > 0.0001f ? travel.normalized : direction;
				GameObject ring = UltimateFxStage.CreateMeshObject("SunPathRing", run.Root, torus, material);
				Solid(ring, true);
				// 고리 면이 진행 방향을 향해 서고, 아래 4분의 1 이 묻힘
				ring.transform.rotation = Quaternion.FromToRotation(Vector3.up, facing);
				Vector3 buried = spot - Vector3.up * ringRadius * 2.2f;
				Vector3 standing = spot + Vector3.up * ringRadius * 0.75f;
				ring.transform.position = buried;
				ring.transform.localScale = Vector3.one * ringRadius;
				float rise = chargeSeconds + i * 0.3f;
				run.Track(rise, 0.25f, progress => ring.transform.position = Vector3.LerpUnclamped(buried, standing, OverShoot(progress, 0.12f)));
				run.At(rise + 0.2f, () => Puff(run, spot));
				rings.Add((ring.transform, hit));
			}
			run.Track(chargeSeconds, omenSeconds, progress => material.SetColor(EMISSION_ID, ringGlow * Mathf.Lerp(0.3f, 0.9f, progress)));
			return rings;
		}

		private void Puff(UltimateFxRun run, Vector3 spot) => run.Dust(spot, 0.8f * sizeScale, 6, steamColor);

		// 광선: 원기둥 세 겹이 태양 구에서 닿는 점까지. 표면을 따라 흐르는 빛, 닿는 점 불티와 증기, 녹은 줄, 닿는 점 점광, 고리 깨짐
		private void ComposeBeam(Stage stage, Vector3 sun, System.Func<float, Vector3> contact, List<(Transform ring, float time)> rings)
		{
			UltimateFxRun run = stage.Run;
			Mesh cylinder = Keep(run, UltimateMeshFactory.OpenCylinder(24, 1f, 1f));
			Color[] colors = { beamCore, beamMiddle, beamOuter };
			float[] radii = { beamRadii.x, beamRadii.y, beamRadii.z };
			float[] alpha = { 1f, 0.55f, 0.28f };
			List<(GameObject layer, Material material)> layers = new();
			for (int i = 0; i < 3; i++)
			{
				Material material = run.Additive(i == 0 ? UltimateFxTextures.White : UltimateFxTextures.Beam, Color.clear);
				GameObject layer = UltimateFxStage.CreateMeshObject("SunBeamLayer", run.Root, cylinder, material);
				layer.SetActive(false);
				layers.Add((layer, material));
			}
			run.At(FireTime, () =>
			{
				foreach ((GameObject layer, Material _) in layers)
					layer.SetActive(true);
			});
			run.Track(FireTime, sweepSeconds + 0.15f, _ =>
			{
				float now = run.Now;
				Vector3 end = contact(now);
				Vector3 axis = end - sun;
				float open = UltimateFxRun.EaseOut(UltimateFxRun.Span(now, FireTime, FireTime + 0.1f));
				float close = 1f - UltimateFxRun.Span(now, BlastTime, BlastTime + 0.15f);
				float pulse = 1f + 0.15f * Mathf.Sin(now / 0.18f * Mathf.PI * 2f);
				Quaternion rotation = Quaternion.FromToRotation(Vector3.up, axis.normalized);
				for (int i = 0; i < 3; i++)
				{
					(GameObject layer, Material material) = layers[i];
					float radius = radii[i] * 2f * open * Mathf.Lerp(0.4f, 1f, close) * (i == 0 ? 1f : pulse);
					layer.transform.position = sun;
					layer.transform.rotation = rotation;
					layer.transform.localScale = new Vector3(radius, axis.magnitude + radii[i], radius);
					material.mainTextureOffset = new Vector2(now * 0.3f * (i + 1), -now * 4f);
					material.SetColor("_Color", colors[i] * alpha[i] * close * (i == 0 ? 1f : pulse));
				}
			}, () =>
			{
				foreach ((GameObject layer, Material _) in layers)
					run.Destroy(layer);
			});

			ParticleSystem flow = Motes(run, "SunBeamFlow", flowPerSecond + 40, 0f, 0f);
			ParticleSystem sparks = run.Particles("SunBeamSparks", run.Additive(UltimateFxTextures.Spark, Color.white), sparkPerSecond + 60, 1.2f, true);
			UltimateFxStage.SetLifetimeLook(sparks, UltimateFxStage.FadeGradient(Color.white, 0.02f, 0.5f), AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
			ParticleSystem steam = run.Particles("SunBeamSteam", LitParticle(run, UltimateFxTextures.Smoke, 0.8f), steamPerSecond * 3 + 20, -0.08f);
			UltimateFxStage.SetLifetimeLook(steam, UltimateFxStage.FadeGradient(Color.white, 0.1f, 0.5f), AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2f));
			ParticleSystem melt = MeltParticles(run, "SunBeamMelt", true);
			ParticleSystem soot = MeltParticles(run, "SunBeamChar", false);
			Light light = WorldLight(stage, "SunContact", LightType.Point, contactLightColor, contactRange);
			float flowCarry = 0f;
			float sparkCarry = 0f;
			float steamCarry = 0f;
			Vector3 lastMelt = contact(FireTime);
			int nextRing = 0;
			run.Track(FireTime, sweepSeconds, _ =>
			{
				float now = run.Now;
				float delta = run.Delta;
				Vector3 end = contact(now);
				Vector3 axis = end - sun;
				flowCarry += flowPerSecond * delta;
				sparkCarry += sparkPerSecond * delta;
				steamCarry += steamPerSecond * delta;
				for (; flowCarry >= 1f; flowCarry -= 1f)
				{
					Vector3 position = sun + axis * Random.Range(0f, 0.4f) + Random.insideUnitSphere * beamRadii.y;
					UltimateFxStage.Emit(flow, position, axis.normalized * Random.Range(14f, 22f), Random.Range(0.08f, 0.18f), axis.magnitude / 18f, new Color(1f, 0.92f, 0.6f, 1f));
				}
				for (; sparkCarry >= 1f; sparkCarry -= 1f)
				{
					Vector3 velocity = (Random.onUnitSphere + Vector3.up * 1.3f).normalized * Random.Range(4f, 11f);
					UltimateFxStage.Emit(sparks, end + Vector3.up * 0.1f, velocity, Random.Range(0.05f, 0.1f), Random.Range(0.3f, 0.7f), new Color(1f, 0.75f, 0.3f, 1f));
				}
				for (; steamCarry >= 1f; steamCarry -= 1f)
				{
					Color tone = steamColor;
					tone.a *= Random.Range(0.7f, 1f);
					UltimateFxStage.Emit(steam, end + UltimateFxStage.RandomRingPoint(0f, 1f) + Vector3.up * 0.3f, Vector3.up * Random.Range(1.5f, 3f) + UltimateFxStage.RandomRingPoint(0f, 1.5f), Random.Range(1.4f, 2.4f), Random.Range(0.9f, 1.4f), tone, Random.value * 360f, Random.Range(-40f, 40f));
				}
				// 녹은 줄: 닿는 점이 지나간 자리마다 바닥에 누운 점 (주황 발광 -> 짙은 빨강 -> 사라짐) 과 그을음
				for (float walked = Vector3.Distance(lastMelt, end); walked >= meltSpacing; walked -= meltSpacing)
				{
					lastMelt = Vector3.MoveTowards(lastMelt, end, meltSpacing);
					UltimateFxStage.Emit(melt, lastMelt + Vector3.up * 0.06f, Vector3.zero, meltWidth * Random.Range(0.85f, 1.15f), meltLinger + (BlastTime - now), Color.white, Random.value * 360f);
					UltimateFxStage.Emit(soot, lastMelt + Vector3.up * 0.04f, Vector3.zero, meltWidth * 1.7f, meltLinger + (BlastTime - now) + 0.6f, new Color(0.05f, 0.03f, 0.02f, 0.75f), Random.value * 360f);
				}
				light.enabled = true;
				light.transform.position = end + Vector3.up * 1.1f + stage.ToCamera * 0.5f;
				float open = UltimateFxRun.EaseOut(UltimateFxRun.Span(now, FireTime, FireTime + 0.1f));
				light.intensity = contactIntensity * open * (1f + 0.12f * Mathf.Sin(now / 0.18f * Mathf.PI * 2f));
				while (nextRing < rings.Count && now >= rings[nextRing].time)
				{
					BreakRing(run, rings[nextRing].ring);
					nextRing++;
				}
			});
			// 풀 반응은 발사와 고리 깨짐마다 한 번씩. React 는 모든 풀의 시작 시각을 되돌려 매 프레임 부르면 풀이 멈춤 (독립 검토)
			run.At(FireTime, () => run.React(contact(FireTime), windSpeed, 6f * sizeScale, false));
			GroundGlow(run, contact(FireTime), contactRange * 0.7f, beamOuter * 0.4f, FireTime, 0.05f, 0.3f, 0.8f);
			run.Track(BlastTime, 1.2f, _ => light.intensity = blastIntensity * Spike(run.Now, BlastTime, 0.02f, 0.5f), () => light.enabled = false);
		}

		// 바닥에 누운 점 (수평 빌보드). 녹은 줄은 가산 주황, 그을음은 알파 검정
		private ParticleSystem MeltParticles(UltimateFxRun run, string name, bool glow)
		{
			Material material = glow ? run.Additive(UltimateFxTextures.Soft, meltColor) : run.Alpha(UltimateFxTextures.Smoke, Color.white);
			material.renderQueue = glow ? 2952 : 2948;
			ParticleSystem particles = run.Particles(name, material, Mathf.CeilToInt((sweepReach.y + 10f) / meltSpacing * 2f) + 40, 0f);
			ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
			particleRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
			if (glow)
			{
				Gradient gradient = new();
				gradient.SetKeys(
					new[] { new GradientColorKey(new Color(1f, 0.85f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 0.2f), new GradientColorKey(new Color(0.5f, 0.05f, 0.02f), 0.7f), new GradientColorKey(Color.black, 1f) },
					new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) });
				UltimateFxStage.SetLifetimeLook(particles, gradient, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.7f));
			}
			else
				UltimateFxStage.SetLifetimeLook(particles, UltimateFxStage.FadeGradient(Color.white, 0.02f, 0.75f), null);
			return particles;
		}

		// 고리가 광선에 닿으면 금빛 조각으로 깨지며 사라짐
		private void BreakRing(UltimateFxRun run, Transform ring)
		{
			if (ring == null)
				return;
			Vector3 position = ring.position;
			run.SparkBurst(position, 40, new Vector2(5f, 12f), new Vector2(0.3f, 0.7f), new Vector2(0.08f, 0.16f), new Color(1f, 0.85f, 0.4f, 1f), Color.white, 1f);
			run.Debris(position, ringRadius, 8, 7f * sizeScale, ringBase, new Color(1f, 0.8f, 0.4f, 1f), 0.8f);
			run.React(position, windSpeed, 4f * sizeScale, false);
			run.Destroy(ring.gameObject);
		}

		// 끝 폭발: 닿는 점에서 불덩이, 연기, 흙 파편, 먼지, 풀 바람
		private void ComposeBlast(Stage stage, Vector3 spot)
		{
			UltimateFxRun run = stage.Run;
			ParticleSystem fire = run.Particles("SunBlastFire", run.Additive(UltimateFxTextures.FlamePuff, beamMiddle), blastPuffs + 10, -0.2f);
			Gradient gradient = new();
			gradient.SetKeys(
				new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.5f, 0.15f), 0.4f), new GradientColorKey(new Color(0.3f, 0.08f, 0.04f), 1f) },
				new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
			UltimateFxStage.SetLifetimeLook(fire, gradient, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2.4f));
			ParticleSystem smoke = run.Particles("SunBlastSmoke", LitParticle(run, UltimateFxTextures.Smoke, 1f), blastPuffs + 10, -0.06f);
			UltimateFxStage.SetLifetimeLook(smoke, UltimateFxStage.FadeGradient(Color.white, 0.1f, 0.6f), AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2.4f));
			run.At(BlastTime, () =>
			{
				for (int i = 0; i < blastPuffs; i++)
				{
					Vector3 velocity = (Random.onUnitSphere + Vector3.up * 1.1f).normalized * Random.Range(3f, 8f) * blastRadius / 3f;
					UltimateFxStage.Emit(fire, spot + Vector3.up * 0.5f, velocity, Random.Range(1f, 1.8f) * blastRadius / 3f, Random.Range(0.5f, 0.9f), Color.white, Random.value * 360f, Random.Range(-120f, 120f));
					Color tone = new(0.3f, 0.27f, 0.25f, 0.8f);
					UltimateFxStage.Emit(smoke, spot + Vector3.up * 0.8f, velocity * 0.45f + Vector3.up * 1.5f, Random.Range(1.6f, 2.6f) * blastRadius / 3f, Random.Range(1.4f, 2.2f), tone, Random.value * 360f, Random.Range(-40f, 40f));
				}
				run.Debris(spot, blastRadius * 0.5f, 22, 11f, new Color(0.33f, 0.3f, 0.27f, 1f), new Color(1f, 0.6f, 0.2f, 1f), 0.5f);
				DustRing(run, spot, 40, blastRadius, new Color(0.45f, 0.4f, 0.36f, 0.8f), 1.8f);
				run.SparkBurst(spot + Vector3.up * 0.4f, 90, new Vector2(8f, 18f), new Vector2(0.3f, 0.7f), new Vector2(0.08f, 0.18f), new Color(1f, 0.8f, 0.4f, 1f), Color.white, 1f);
				run.React(spot, windSpeed, blastRadius * 3f, true);
			});
			Texture2D scorch = ScorchTexture(run, 66, new Color(0.06f, 0.04f, 0.03f, 0.85f), new Color(1f, 0.5f, 0.15f, 1f), 0.8f);
			GroundDecal(run, scorch, spot, blastRadius * 2.4f, BlastTime, 0.05f, DurationSeconds - 1.2f, 1.2f, 2);
			GroundGlow(run, spot, blastRadius * 3f, beamMiddle * 0.8f, BlastTime, 0.02f, 0.4f, 1.6f);
		}

		private void ComposeGrade(Stage stage)
		{
			float restore = RestoreTime;
			stage.BloomTint = new Color(1f, 0.85f, 0.55f);
			stage.SunScale = now => Mathf.Lerp(1f, sunDim, Plateau(now, chargeSeconds, omenSeconds, restore, restoreSeconds));
			stage.AmbientScale = now => Mathf.Lerp(1f, ambientDim, Plateau(now, chargeSeconds, omenSeconds, restore, restoreSeconds));
			stage.Vignette = now => vignette * Plateau(now, chargeSeconds, omenSeconds, restore, restoreSeconds);
			// 노랑 광선에 보색 연보라 물들임 (m06)
			stage.Filter = now => Color.Lerp(Color.white, tintColor, Plateau(now, chargeSeconds, omenSeconds, restore, restoreSeconds));
			stage.Bloom = now => Mathf.Max(0.5f * UltimateFxRun.Span(now, 0f, FireTime), bloom * Plateau(now, FireTime, 0.08f, restore, restoreSeconds)) * (1f - UltimateFxRun.Span(now, restore, restore + restoreSeconds));
		}
	}
}
