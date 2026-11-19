using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 태양 광선 궁극기. 시전자 앞 빛 구슬 충전 -> 화면이 노랑의 보색 (청보라) 으로 물들고 표적까지 경로 위에 타원 고리가 하나씩 생김
	/// -> 백색 세 장 -> 굵은 노란 광선 3.4초 유지 (가장자리 무지개 번짐, 고리가 광선을 따라 흐름) -> 끝 폭발과 고리 -> 가는 잔선
	/// - 실루엣: 화면 왼쪽 끝까지 곧게 뻗는 굵은 띠와 세운 타원 고리 줄. 배경 장악: 청보라 보색 물들임
	/// - 시간 구조는 참고 영상 15fps 실측 (충전 0.6초, 물들임 1.6초, 백색 3장, 유지 3.4초, 끝 폭발 3장, 잔선 0.27초)
	/// </summary>
	[CreateAssetMenu(fileName = "Ultimate_06_SunBeam", menuName = "WM/CombatFx/Ultimate/" + nameof(SunBeamUltimateFx))]
	public sealed class SunBeamUltimateFx : UltimateFx
	{
		[Header("색")]
		[SerializeField] private Color complementTint = new(0.62f, 0.5f, 1f, 1f);
		[SerializeField, ColorUsage(false, true)] private Color beamColor = new(1.6f, 1.45f, 0.9f);
		[SerializeField, ColorUsage(false, true)] private Color orbColor = new(2f, 1.7f, 0.7f);
		[SerializeField, ColorUsage(false, true)] private Color ringColor = new(2.4f, 2.2f, 1.5f);

		[Header("시간 (초)")]
		[SerializeField, Min(0.1f), Tooltip("실측 0.6초")] private float chargeSeconds = 0.6f;
		[SerializeField, Min(0.1f), Tooltip("실측 1.6초")] private float tintSeconds = 1.6f;
		[SerializeField, Min(0.01f), Tooltip("실측 3장")] private float whiteSeconds = 0.2f;
		[SerializeField, Min(0.1f), Tooltip("실측 3.4초")] private float beamSeconds = 3.4f;
		[SerializeField, Min(0.05f), Tooltip("실측 3장")] private float burstSeconds = 0.2f;
		[SerializeField, Min(0.05f), Tooltip("실측 0.27초")] private float residueSeconds = 0.4f;

		[Header("크기 (월드 단위)")]
		[SerializeField, Min(2f), Tooltip("광선 길이. 화면 끝까지")] private float beamLength = 15f;
		[SerializeField, Min(0.2f), Tooltip("광선 두께. 캐릭터 키의 약 1.4배")] private float beamWidth = 1.8f;
		[SerializeField, Min(0.1f)] private float beamHeight = 0.9f;
		[SerializeField, Min(1)] private int pathRings = 6;
		[SerializeField, Min(0.5f)] private float pathRingSize = 4.6f;
		[SerializeField, Min(0.5f)] private float orbSize = 2.4f;
		[SerializeField, Min(1f)] private float burstRadius = 6f;

		[Header("화면과 카메라")]
		[SerializeField, Range(0f, 1f)] private float tintAmount = 0.7f;
		[SerializeField, Range(0f, 1f)] private float tintDarken = 0f;
		[SerializeField, Range(0f, 1f)] private float whitePeak = 0.85f;
		[SerializeField, Min(0f)] private float beamShake = 0.35f;
		[SerializeField, Min(0f)] private float lightIntensity = 26f;
		[SerializeField, Min(0), Tooltip("광선 유지 동안 광선을 따라 튀는 불티 (초당)")] private int beamSparksPerSecond = 160;

		private float TintStart => chargeSeconds;
		private float WhiteStart => TintStart + tintSeconds;
		private float BeamStart => WhiteStart + whiteSeconds;
		private float BurstStart => BeamStart + beamSeconds;
		private float ResidueStart => BurstStart + burstSeconds;

		public override float DurationSeconds => ResidueStart + residueSeconds;

		public override void Compose(UltimateFxRun run)
		{
			Vector3 direction = -run.ScreenRight;
			Vector3 muzzle = run.Caster + Vector3.up * beamHeight + direction * 0.8f;
			Vector3 end = muzzle + direction * beamLength;

			// 충전 구슬
			Material orbMaterial = run.Additive(UltimateFxTextures.Soft, Color.clear);
			GameObject orb = run.Billboard("Orb", orbMaterial, muzzle, 0.1f, 0.1f, false, true, 9);
			Material glintMaterial = run.Additive(UltimateFxTextures.CrossGlint, Color.clear);
			GameObject glint = run.Billboard("OrbGlint", glintMaterial, muzzle, 0.1f, 0.1f, false, true, 10);
			Light light = run.PointLight("SunLight", beamColor, 18f);
			light.transform.position = muzzle;
			run.Track(0f, BeamStart, progress =>
			{
				float grow = UltimateFxRun.EaseOut(run.Now / chargeSeconds);
				float pulse = 1f + 0.1f * Mathf.Sin(run.Now * 35f);
				orb.transform.localScale = Vector3.one * orbSize * grow * pulse;
				glint.transform.localScale = Vector3.one * orbSize * 2.4f * grow;
				orbMaterial.SetColor("_Color", new Color(orbColor.r, orbColor.g, orbColor.b, grow));
				glintMaterial.SetColor("_Color", new Color(orbColor.r, orbColor.g, orbColor.b, grow * 0.8f));
				glint.GetComponent<UltimateBillboard>().Roll = run.Now * 40f;
				light.intensity = lightIntensity * 0.4f * grow;
			});

			// 보색 물들임과 경로 고리: 표적까지 세운 타원 고리가 하나씩
			run.Track(TintStart, tintSeconds * 0.3f, progress => Tint(run, progress));
			List<GameObject> rings = new();
			Material ringMaterial = run.Additive(UltimateFxTextures.ThinRing, ringColor);
			for (int i = 0; i < pathRings; i++)
			{
				int index = i;
				run.At(TintStart + tintSeconds * 0.85f * index / pathRings, () =>
				{
					Vector3 position = muzzle + direction * (beamLength * (index + 1f) / (pathRings + 1f));
					GameObject ring = UltimateFxStage.CreateMeshObject("PathRing" + index, run.Root, UltimateFxStage.CenterQuad, ringMaterial);
					ringMaterial.renderQueue = 3020;
					ring.transform.position = position;
					ring.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
					ring.transform.localScale = new Vector3(pathRingSize * 0.75f, pathRingSize, 1f);
					rings.Add(ring);
				});
			}

			// 백색 세 장
			run.Track(WhiteStart, whiteSeconds, _ => run.Flash(whitePeak, Color.white), () => run.Flash(0f, Color.white));

			// 광선: 카메라를 향하는 띠. 두께가 고동치고 고리가 광선을 따라 흐름
			LineRenderer beam = null;
			ParticleSystem beamSparks = run.Particles("BeamSparks", run.Additive(UltimateFxTextures.Spark, Color.white), 900, 0.5f, true);
			UltimateFxStage.SetLifetimeLook(beamSparks, UltimateFxStage.FadeGradient(Color.white, 0.02f, 0.5f), null);
			float sparkCarry = 0f;
			run.Track(BeamStart, beamSeconds, progress =>
			{
				if (beam == null)
				{
					run.Destroy(orb);
					run.Destroy(glint);
					beam = CreateBeam(run, run.Additive(UltimateFxTextures.Beam, beamColor), muzzle, end);
				}
				float swell = UltimateFxRun.EaseOut(progress * 12f) * (1f - UltimateFxRun.Span(progress, 0.9f, 1f) * 0.6f);
				float pulse = 1f + 0.08f * Mathf.Sin(run.Now * 40f) + 0.05f * Mathf.Sin(run.Now * 17f);
				beam.widthMultiplier = beamWidth * swell * pulse;
				beam.sharedMaterial.mainTextureOffset = new Vector2(-run.Now * 3f, 0f);
				for (int i = 0; i < rings.Count; i++)
				{
					float along = Mathf.Repeat((i + 1f) / (pathRings + 1f) + progress * 0.6f, 1f);
					rings[i].transform.position = muzzle + direction * beamLength * along;
					float size = pathRingSize * (1f + 0.15f * Mathf.Sin(run.Now * 10f + i));
					rings[i].transform.localScale = new Vector3(size * 0.75f, size, 1f);
				}
				light.transform.position = Vector3.Lerp(muzzle, end, 0.3f);
				light.intensity = lightIntensity * swell;
				run.Layer.SetStreaks(0.25f * swell, beamColor, 0f, 1.8f);
				sparkCarry += beamSparksPerSecond * run.Delta;
				while (sparkCarry >= 1f)
				{
					sparkCarry -= 1f;
					Vector3 along = Vector3.Lerp(muzzle, end, Random.value);
					Vector3 velocity = direction * Random.Range(4f, 14f) + Random.insideUnitSphere * 4f + Vector3.up * 2f;
					UltimateFxStage.Emit(beamSparks, along, velocity, Random.Range(0.12f, 0.3f), Random.Range(0.3f, 0.7f), Color.Lerp(beamColor, orbColor, Random.value));
				}
			}, () => run.Layer.SetStreaks(0f, Color.white, 0f));
			run.ShakeSeries(BeamStart, 24, 0.14f, beamShake, 0.97f);
			run.At(BeamStart, () => run.React(muzzle, 14f, beamLength * 0.7f, false));

			// 끝 폭발과 고리
			Material burstMaterial = run.Additive(UltimateFxTextures.Soft, Color.clear);
			GameObject burst = null;
			Vector3 burstPoint = muzzle + direction * beamLength * 0.55f;
			run.Track(BurstStart, burstSeconds * 3f, progress =>
			{
				if (burst == null)
				{
					burst = run.Billboard("Burst", burstMaterial, burstPoint, 1f, 1f, false, true, 11);
					foreach (GameObject ring in rings)
						run.Destroy(ring);
					beam.enabled = false;
				}
				burst.transform.localScale = Vector3.one * burstRadius * 2f * UltimateFxRun.EaseOut(progress * 2f);
				burstMaterial.SetColor("_Color", new Color(orbColor.r, orbColor.g, orbColor.b, 1f - progress));
				run.Flash(0.3f * (1f - progress), Color.white);
			}, () =>
			{
				run.Destroy(burst);
				run.Flash(0f, Color.white);
				light.intensity = 0f;
			});
			Vector3 burstGround = burstPoint - Vector3.up * beamHeight;
			run.ThinRing(BurstStart, burstGround, 1f, burstRadius * 1.4f, 0.8f, ringColor, 8);
			run.ThinRing(BurstStart + 0.1f, burstGround, 1f, burstRadius, 0.8f, ringColor, 9);
			run.At(BurstStart, () => run.SparkBurst(burstPoint, 90, new Vector2(8f, 22f), new Vector2(0.4f, 1f), new Vector2(0.15f, 0.4f), beamColor, orbColor, 0.6f));

			// 가는 잔선과 물들임 해제
			run.Track(ResidueStart, residueSeconds, progress =>
			{
				beam.enabled = true;
				beam.widthMultiplier = beamWidth * 0.08f * (1f - progress);
				Tint(run, 1f - progress);
			}, () => Tint(run, 0f));
		}

		private void Tint(UltimateFxRun run, float amount)
		{
			run.Grade.SetTint(tintAmount * amount, complementTint);
			run.Grade.Darken = tintDarken * amount;
		}

		private static LineRenderer CreateBeam(UltimateFxRun run, Material material, Vector3 start, Vector3 end)
		{
			GameObject beamObject = new("Beam");
			beamObject.transform.SetParent(run.Root, false);
			LineRenderer line = beamObject.AddComponent<LineRenderer>();
			line.sharedMaterial = material;
			material.renderQueue = 3015;
			material.mainTexture.wrapMode = TextureWrapMode.Repeat;
			line.useWorldSpace = true;
			line.positionCount = 2;
			line.SetPosition(0, start);
			line.SetPosition(1, end);
			line.alignment = LineAlignment.View;
			line.textureMode = LineTextureMode.Tile;
			line.numCapVertices = 6;
			line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			line.receiveShadows = false;
			line.widthMultiplier = 0f;
			return line;
		}
	}
}
