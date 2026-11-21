using UnityEngine;

namespace WitchMendokusai
{
	/// <summary> 밤의 날 강한 판의 틈, 번지는 밤, 별빛 흡수, 둘째 베기의 땅 자국 </summary>
	public sealed partial class NightBladeHeavyUltimateFx
	{
		private Vector3 RiftCenter(Frame frame) => frame.Pivot + frame.Aim(riftAngle) * frame.Length * riftReach;

		// 시각 -> 틈 폭. 검은 사선, 열림 (살짝 넘쳤다 자리 잡음), 둘째 베기에 닫힘
		private float RiftWidth(float now)
		{
			float slit = riftSlitWidth * UltimateFxRun.EaseOut(UltimateFxRun.Span(now, SlashTime, SlashTime + 0.06f));
			float open = Mathf.LerpUnclamped(slit, riftWidth, OverShoot(UltimateFxRun.Span(now, OpenStart, OpenEnd), 0.15f) * UltimateFxRun.Span(now, OpenStart, OpenStart + 0.02f));
			float breathe = 1f + 0.04f * Mathf.Sin(now * 5f) * UltimateFxRun.Span(now, OpenEnd, OpenEnd + 0.3f);
			float close = 1f - UltimateFxRun.EaseIn(UltimateFxRun.Span(now, ChopTime - 0.03f, ChopTime + 0.1f));
			return (now < OpenStart ? slit : open * breathe) * close;
		}

		// 틈: 휘두름 면에 선 방추형 판 (별밭 창, 가장자리 빛). 가장자리에서 빛 알갱이가 떨어지고 틈 점광이 땅을 비춤. 아래 땅에서 밤이 번짐
		private void ComposeRift(Stage stage, Frame frame, Light light)
		{
			UltimateFxRun run = stage.Run;
			Vector3 center = RiftCenter(frame);
			// 틈의 길이 축은 칼이 지나간 방향 (놓인 각도에 직각)
			Vector3 along = frame.Aim(riftAngle + 90f);
			Vector3 across = frame.Aim(riftAngle);
			Material material = StarWindow(run, 0f, riftEdge);
			material.SetFloat("_EdgePower", riftEdgePower);
			material.renderQueue = 2985;
			GameObject rift = UltimateFxStage.CreateMeshObject("NightRift", run.Root, Keep(run, UltimateMeshFactory.Rift(808, 40, riftJagged)), material);
			rift.transform.SetPositionAndRotation(center, Quaternion.LookRotation(frame.Normal, across));
			rift.SetActive(false);
			run.At(SlashTime, () => rift.SetActive(true));
			run.Track(SlashTime, ChopTime + 0.1f - SlashTime, _ =>
			{
				float now = run.Now;
				float width = RiftWidth(now);
				float grow = UltimateFxRun.EaseOut(UltimateFxRun.Span(now, SlashTime, SlashTime + 0.08f));
				rift.transform.localScale = new Vector3(riftLength * grow, Mathf.Max(0.001f, width), 1f);
				material.SetFloat(ALPHA_ID, 1f);
				// 벤 순간, 열리는 순간, 닫히는 순간 가장자리가 밝음
				material.SetFloat(EDGE_AMOUNT_ID, riftEdge * (1f + 2f * Spike(now, SlashTime, 0.02f, 0.12f) + 1.2f * Spike(now, OpenStart, 0.05f, 0.25f) + 3f * Spike(now, ChopTime - 0.03f, 0.03f, 0.1f)));
			}, () => run.Destroy(rift));

			ParticleSystem motes = Motes(run, "NightRiftMotes", Mathf.CeilToInt(edgeMotesPerSecond * 1.6f) + 30, 0.25f, 0.25f);
			float carry = 0f;
			run.Track(SlashTime, ChopTime - SlashTime, _ =>
			{
				float now = run.Now;
				float width = RiftWidth(now);
				carry += edgeMotesPerSecond * Mathf.Lerp(0.3f, 1f, UltimateFxRun.Span(now, OpenStart, OpenEnd)) * run.Delta;
				for (; carry >= 1f; carry -= 1f)
				{
					float position = Random.Range(-0.46f, 0.46f);
					// 방추형이라 끝으로 갈수록 좁음
					float half = width * 0.5f * Mathf.Pow(Mathf.Cos(position * Mathf.PI), 0.7f);
					Vector3 point = center + along * position * riftLength + across * half * (Random.value < 0.5f ? 1f : -1f);
					UltimateFxStage.Emit(motes, point, Random.insideUnitSphere * 0.4f * sizeScale, Random.Range(0.07f, 0.15f) * sizeScale, Random.Range(0.6f, 1.2f), new Color(0.8f, 0.7f, 1f, 1f));
				}
			});

			light.transform.position = center + stage.ToCamera * 1f - Vector3.up * 0.3f;
			run.Track(SlashTime, ChopTime + 0.15f - SlashTime, _ =>
			{
				float now = run.Now;
				float slit = 0.25f * UltimateFxRun.Span(now, SlashTime, SlashTime + 0.06f);
				float open = UltimateFxRun.EaseOut(UltimateFxRun.Span(now, OpenStart, OpenEnd));
				float close = 1f - UltimateFxRun.Span(now, ChopTime, ChopTime + 0.15f);
				light.intensity = riftLightIntensity * Mathf.Max(slit, open) * close * (0.92f + 0.08f * Mathf.Sin(now * 11f));
			}, () => light.enabled = false);

			ComposeNight(stage, center);
		}

		// 번지는 밤: 틈 아래 땅에서 어두운 원이 퍼지고, 가장자리를 따라 보라 빛 알갱이가 피어오름. 둘째 베기에 걷힘
		private void ComposeNight(Stage stage, Vector3 riftCenter)
		{
			UltimateFxRun run = stage.Run;
			Vector3 ground = riftCenter;
			ground.y = UltimateFxStage.GroundHeight(ground);
			Material material = run.Alpha(UltimateFxTextures.Soft, new Color(0f, 0f, 0f, 0f));
			GameObject shade = run.Plate("NightShade", material, ground + Vector3.up * 0.06f, 0.01f, 4);
			ParticleSystem motes = Motes(run, "NightShadeMotes", 260, -0.12f, 0.2f);
			float spreadSeconds = openSeconds + 0.2f;
			float carry = 0f;
			run.Track(OpenStart, ChopTime + 0.25f - OpenStart, _ =>
			{
				float now = run.Now;
				float spread = UltimateFxRun.EaseOut(UltimateFxRun.Span(now, OpenStart, OpenStart + spreadSeconds));
				float lift = 1f - UltimateFxRun.Span(now, ChopTime, ChopTime + 0.25f);
				float radius = nightRadius * spread;
				// Soft 그림은 가장자리가 옅어 판을 반지름보다 넉넉하게
				shade.transform.localScale = new Vector3(radius * 2.6f, 1f, radius * 2.6f);
				material.SetColor("_Color", new Color(0.02f, 0.01f, 0.06f, nightDarkness * spread * lift));
				if (now >= ChopTime)
					return;
				carry += 110f * run.Delta;
				for (; carry >= 1f; carry -= 1f)
				{
					float angle = Random.value * Mathf.PI * 2f;
					Vector3 point = ground + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius * Random.Range(0.85f, 1f);
					point.y = UltimateFxStage.GroundHeight(point) + 0.1f;
					UltimateFxStage.Emit(motes, point, Vector3.up * Random.Range(0.4f, 1.4f), Random.Range(0.08f, 0.18f) * sizeScale, Random.Range(0.6f, 1.2f), new Color(0.65f, 0.5f, 1f, 1f));
				}
			}, () => run.Destroy(shade));
			run.At(OpenStart, () => run.React(ground, windSpeed * 0.6f, windReach, false));
		}

		// 별빛 흡수: 틈과 밤 위의 별 입자가 치켜든 칼날로 빨려듦. 뒤로 갈수록 많아짐
		private void ComposeAbsorb(Stage stage, Frame frame)
		{
			UltimateFxRun run = stage.Run;
			ParticleSystem stars = Streaks(run, "NightAbsorb", absorbCount + 40, 0.035f);
			Vector3 riftCenter = RiftCenter(frame);
			Vector3 along = frame.Aim(riftAngle + 90f);
			Vector3 ground = riftCenter;
			ground.y = UltimateFxStage.GroundHeight(ground);
			float seconds = AbsorbEnd - AbsorbStart;
			float carry = 0f;
			run.Track(AbsorbStart, seconds, progress =>
			{
				// 흡수량은 곡선 2 x progress (적분 1): 끝으로 갈수록 몰림
				carry += absorbCount * 2f * progress / Mathf.Max(0.01f, seconds) * run.Delta;
				Vector3 bladeBase = frame.Pivot;
				Vector3 bladeAxis = frame.Aim(SwordAngle(frame, run.Now)) * frame.Length;
				for (; carry >= 1f; carry -= 1f)
				{
					// 절반은 틈에서, 절반은 밤이 덮은 땅 위에서
					Vector3 from = Random.value < 0.5f
						? riftCenter + along * Random.Range(-0.45f, 0.45f) * riftLength + frame.Aim(riftAngle) * Random.Range(-0.4f, 0.4f) * riftWidth
						: ground + UltimateFxStage.RandomRingPoint(nightRadius * 0.2f, nightRadius * 0.9f) + Vector3.up * Random.Range(0.2f, 1.6f);
					Vector3 to = bladeBase + bladeAxis * Random.Range(0.15f, 0.95f);
					float life = Random.Range(0.26f, 0.42f);
					UltimateFxStage.Emit(stars, from, (to - from) / life, Random.Range(0.07f, 0.15f) * sizeScale, life, new Color(0.85f, 0.85f, 1f, 1f));
				}
			});
		}

		// 둘째 베기: 흰 날이 땅을 긋고 틈이 닫힘. 땅에 빛나는 긴 자국과 그을음, 흙 파편, 먼지, 풀 바람
		private void ComposeChop(Stage stage, Frame frame, Vector3 origin)
		{
			UltimateFxRun run = stage.Run;
			Vector3 direction = frame.Aim(GroundAngle(frame));
			direction.y = 0f;
			direction.Normalize();
			float reach = frame.Length * 1.1f;
			Vector3 start = origin + direction * frame.Length * 0.12f;

			Material glowMaterial = run.Additive(UltimateFxTextures.Soft, scarColor);
			glowMaterial.renderQueue = 2952;
			Material sootMaterial = run.Alpha(UltimateFxTextures.Smoke, Color.white);
			sootMaterial.renderQueue = 2948;
			int capacity = Mathf.CeilToInt(reach / scarSpacing) + 20;
			ParticleSystem glow = run.Particles("NightScarGlow", glowMaterial, capacity, 0f);
			glow.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
			Gradient gradient = new();
			gradient.SetKeys(
				new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.8f, 0.6f, 1f), 0.25f), new GradientColorKey(new Color(0.25f, 0.1f, 0.5f), 0.75f), new GradientColorKey(Color.black, 1f) },
				new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) });
			UltimateFxStage.SetLifetimeLook(glow, gradient, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.6f));
			ParticleSystem soot = run.Particles("NightScarSoot", sootMaterial, capacity, 0f);
			soot.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
			UltimateFxStage.SetLifetimeLook(soot, UltimateFxStage.FadeGradient(Color.white, 0.02f, 0.75f), null);
			ParticleSystem risers = Streaks(run, "NightScarRisers", 160, 0.05f);

			float sootSeconds = DurationSeconds - ChopTime;
			run.At(ChopTime, () =>
			{
				for (float walked = 0f; walked <= reach; walked += scarSpacing)
				{
					Vector3 point = start + direction * walked;
					point.y = UltimateFxStage.GroundHeight(point);
					// 가운데가 굵고 양 끝이 가는 자국
					float taper = Mathf.Lerp(0.45f, 1f, Mathf.Sin(walked / reach * Mathf.PI));
					UltimateFxStage.Emit(glow, point + Vector3.up * 0.06f, Vector3.zero, scarWidth * taper * Random.Range(0.85f, 1.15f), scarSeconds * Random.Range(0.85f, 1.15f), Color.white, Random.value * 360f);
					UltimateFxStage.Emit(soot, point + Vector3.up * 0.04f, Vector3.zero, scarWidth * 1.6f * taper, sootSeconds, new Color(0.04f, 0.03f, 0.07f, 0.8f), Random.value * 360f);
					if (Random.value < 0.6f)
						UltimateFxStage.Emit(risers, point + Vector3.up * 0.1f, Vector3.up * Random.Range(6f, 16f) + Random.insideUnitSphere * 1.5f, Random.Range(0.08f, 0.16f) * sizeScale, Random.Range(0.2f, 0.45f), new Color(0.9f, 0.85f, 1f, 1f));
				}
				for (int i = 1; i <= 3; i++)
				{
					Vector3 point = start + direction * reach * i / 4f;
					point.y = UltimateFxStage.GroundHeight(point);
					run.Debris(point, 1.2f * sizeScale, 8, 9f * sizeScale, new Color(0.3f, 0.28f, 0.33f, 1f), new Color(0.85f, 0.75f, 1f, 1f), 0.45f);
					run.Dust(point, 2.2f * sizeScale, 7, dustColor);
				}
				Vector3 middle = start + direction * reach * 0.5f;
				run.SparkBurst(middle + Vector3.up * 0.3f, 110, new Vector2(7f, 18f), new Vector2(0.3f, 0.7f), new Vector2(0.07f, 0.16f), Color.white, new Color(0.75f, 0.6f, 1f, 1f), 0.8f);
				run.React(middle, windSpeed, windReach, true);
			});
			Vector3 glowSpot = start + direction * reach * 0.5f;
			glowSpot.y = UltimateFxStage.GroundHeight(glowSpot);
			GroundGlow(run, glowSpot, reach * 1.6f, scarColor * 0.9f, ChopTime, 0.02f, 0.35f, 1.5f);
			// 첫 베기는 풀만 눕힘 (허공을 벰)
			run.At(SlashTime, () => run.React(origin, windSpeed, windReach * 0.8f, false));
		}
	}
}
