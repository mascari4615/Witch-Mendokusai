using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 한 번의 진행. 시간선 (트랙) 에 효과를 올려 매 프레임 평가
	/// - 실제 시간 트랙 (화면 층, 카메라) 과 게임 시간 트랙 (월드 효과) 을 나눔. 히트스톱 동안 월드 효과는 같이 멈추고 화면 층은 계속 감
	/// - 이 파일: 진행, 트랙, 예고와 충전, 임팩트의 화면과 카메라. 월드 효과는 UltimateFxRun.World.cs
	/// </summary>
	public sealed partial class UltimateFxRun
	{
		private sealed class Track
		{
			public bool Real;
			public float Start;
			public float Duration;
			public Action<float> Apply;
			public Action End;
		}

		private readonly UltimateFx fx;
		private readonly CombatFxContext context;
		private readonly List<Track> tracks = new();
		private readonly List<Track> added = new();
		private readonly List<Material> materials = new();
		private readonly List<UnityEngine.Object> owned = new();
		private readonly List<Action> releases = new();

		private GameObject root;
		private UltimateScreenLayer layer;
		private Vector3 center;
		private Camera mainCamera;
		private UniversalAdditionalCameraData cameraData;
		private CameraOverrideOption previousColorOption;
		private bool disposed;
		private bool impacted;
		private float realTime;
		private float worldTime;
		private float realDelta;
		private float worldDelta;
		private Action releaseChargeZoom;
		private Action stopTelegraph;

		private float ImpactTime => fx.TelegraphSeconds + fx.ChargeSeconds;
		private UltimateFx.Palette Colors => fx.Colors;

		private Vector3 CasterPosition => context.Follow != null ? context.Follow.position : center;

		public UltimateFxRun(UltimateFx fx, CombatFxContext context)
		{
			this.fx = fx;
			this.context = context;
		}

		public IEnumerator Routine()
		{
			Setup();
			float endGuard = ImpactTime + fx.AftermathSeconds + fx.HitStopSeconds + 2f;
			while (realTime < endGuard)
			{
				// 프레임 녹화 중에는 고정 간격 (DevFrameRecorder), 아니면 실제 경과
				realDelta = Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
				worldDelta = Time.deltaTime;
				realTime += realDelta;
				worldTime += worldDelta;
				if (impacted == false && realTime >= ImpactTime)
					Impact();
				EvaluateTracks();
				if (impacted && tracks.Count == 0)
					break;
				yield return null;
			}
		}

		public void Dispose()
		{
			if (disposed)
				return;
			disposed = true;
			releaseChargeZoom?.Invoke();
			stopTelegraph?.Invoke();
			foreach (Action release in releases)
				release();
			if (cameraData != null)
				cameraData.requiresColorOption = previousColorOption;
			layer?.Dispose();
			if (root != null)
				UnityEngine.Object.Destroy(root);
			foreach (Material material in materials)
				UnityEngine.Object.Destroy(material);
			foreach (UnityEngine.Object item in owned)
				UnityEngine.Object.Destroy(item);
		}

		// ---- 트랙 ----
		private void AddTrack(bool real, float start, float duration, Action<float> apply, Action end = null)
			=> added.Add(new Track { Real = real, Start = start, Duration = duration, Apply = apply, End = end });

		private void EvaluateTracks()
		{
			if (added.Count > 0)
			{
				tracks.AddRange(added);
				added.Clear();
			}
			for (int i = tracks.Count - 1; i >= 0; i--)
			{
				Track track = tracks[i];
				float clock = track.Real ? realTime : worldTime;
				if (clock < track.Start)
					continue;
				float t = track.Duration <= 0f ? 1f : Mathf.Min(1f, (clock - track.Start) / track.Duration);
				track.Apply?.Invoke(t);
				if (t >= 1f)
				{
					track.End?.Invoke();
					tracks.Remove(track);
				}
			}
			if (added.Count > 0)
			{
				tracks.AddRange(added);
				added.Clear();
			}
		}

		private static float EaseOut(float t) => 1f - Mathf.Pow(1f - t, 3f);

		private static float EaseIn(float t) => t * t * t;

		private Material NewMaterial(Texture texture, bool additive, Color color)
		{
			Material material = UltimateFxStage.CreateMaterial(fx.SpriteShader, texture, additive, color);
			materials.Add(material);
			return material;
		}

		private Material NewShockwaveMaterial()
		{
			if (fx.ShockwaveShader == null)
				return null;
			Material material = new(fx.ShockwaveShader) { name = "UltimateFx_Shockwave" };
			materials.Add(material);
			return material;
		}

		// ---- 준비 ----
		private void Setup()
		{
			mainCamera = Camera.main;
			center = context.Position;
			center.y = UltimateFxStage.GroundHeight(center);
			root = new GameObject("UltimateFxRun");
			root.transform.position = center;
			layer = new UltimateScreenLayer(fx.SpriteShader, 2400);

			// 충격파 굴절은 카메라의 Opaque Texture 를 읽음. 끝나면 원래대로
			if (mainCamera != null)
			{
				cameraData = mainCamera.GetUniversalAdditionalCameraData();
				previousColorOption = cameraData.requiresColorOption;
				cameraData.requiresColorOption = CameraOverrideOption.On;
			}

			if (fx.SpawnTemporaryFoliage)
				SpawnFoliage();

			BuildTelegraph();
			BuildCharge();
		}

		// ---- 예고 ----
		private void BuildTelegraph()
		{
			if (fx.Telegraph != null)
			{
				stopTelegraph = fx.Telegraph.Play(new CombatFxContext
				{
					Position = center,
					Direction = context.Direction,
					Radius = fx.ImpactRadius,
					Seconds = fx.TelegraphSeconds + fx.ChargeSeconds * 0.92f,
					Title = context.Title,
					Subtitle = context.Subtitle,
				});
			}

			// 지면 룬 마법진. 예고 동안 켜지며 천천히 돌고 임팩트에서 번쩍이고 사라짐
			Material runeMaterial = NewMaterial(UltimateFxTextures.Rune, true, Color.clear);
			GameObject rune = UltimateFxStage.CreateMeshObject("Rune", root.transform, UltimateFxStage.GroundQuad, runeMaterial);
			runeMaterial.renderQueue = 2938;
			rune.transform.position = center + Vector3.up * 0.05f;
			rune.transform.localScale = new Vector3(fx.ImpactRadius * 2.15f, 1f, fx.ImpactRadius * 2.15f);
			AddTrack(true, 0f, ImpactTime + 0.5f, t =>
			{
				float appear = Mathf.Clamp01(realTime / Mathf.Max(0.01f, fx.TelegraphSeconds));
				float charge = Mathf.Clamp01((realTime - fx.TelegraphSeconds) / fx.ChargeSeconds);
				float fade = realTime > ImpactTime ? 1f - Mathf.Clamp01((realTime - ImpactTime) / 0.5f) : 1f;
				float brightness = Mathf.Lerp(0.45f, 1.5f, charge) * EaseOut(appear) * fade;
				Color color = Color.Lerp(Colors.Mid, Colors.Core, charge);
				color.a = 0.9f;
				runeMaterial.SetColor("_Color", color * brightness);
				rune.transform.rotation = Quaternion.Euler(0f, realTime * 14f * (1f + charge * 3f), 0f);
			}, () => UnityEngine.Object.Destroy(rune));
		}

		// ---- 충전 ----
		private void BuildCharge()
		{
			float start = fx.TelegraphSeconds;
			float duration = fx.ChargeSeconds;

			// 화면: 시네마 바, 가장자리 어둠, 마지막에 하얗게 차오름
			AddTrack(true, start, duration, t =>
			{
				layer.SetBars(EaseOut(Mathf.Clamp01(t * 3f)), fx.BarRatio);
				layer.SetVignette(fx.ChargeDarken * EaseOut(t), Colors.Shade);
				float build = Mathf.Pow(t, 4f);
				layer.SetWash(build * 0.55f, Colors.Mid);
			});

			// 카메라 줌인: 임팩트까지 머묾
			AddTrack(true, start, 0f, _ =>
			{
				if (CameraManager.TryGetExistingInstance(out CameraManager cameraManager))
					releaseChargeZoom = cameraManager.PunchZoom(fx.ChargeZoomScale, fx.ChargeFieldOfView, fx.ChargeSeconds * 0.9f, float.PositiveInfinity, 0.25f, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f));
			});

			// 떨림: 시간이 갈수록 커짐
			float nextTremor = start;
			AddTrack(true, start, duration, t =>
			{
				if (realTime < nextTremor)
					return;
				nextTremor = realTime + 0.09f;
				if (CameraManager.TryGetExistingInstance(out CameraManager cameraManager))
					cameraManager.GenerateCameraImpulse(Mathf.Lerp(0.04f, 1f, t * t) * fx.ChargeTremor);
			});

			BuildChargeParticles(start, duration);
		}

		// ---- 임팩트: 화면과 카메라 ----
		private void Impact()
		{
			impacted = true;
			releaseChargeZoom?.Invoke();
			releaseChargeZoom = null;
			float impactReal = realTime;

			if (TimeManager.TryGetExistingInstance(out TimeManager timeManager))
				timeManager.HitStop(fx.HitStopSeconds, fx.HitStopScale);

			// 섬광: 한 프레임에 정점, 잠깐 유지, 감쇠. 정점 뒤 반감 약 0.2초
			AddTrack(true, impactReal, fx.FlashHoldSeconds + fx.FlashDecaySeconds, t =>
			{
				float elapsed = t * (fx.FlashHoldSeconds + fx.FlashDecaySeconds);
				float amount = elapsed < fx.FlashHoldSeconds ? 1f : Mathf.Pow(1f - (elapsed - fx.FlashHoldSeconds) / fx.FlashDecaySeconds, 2.4f);
				layer.SetFlash(amount * fx.FlashPeak, Color.Lerp(Color.white, Colors.Core, 0.2f));
			}, () => layer.SetFlash(0f, Color.white));

			// 색 번짐과 가장자리 어둠: 빠르게 들어와 오래 머물고 서서히 빠짐
			AddTrack(true, impactReal, fx.WashSeconds, t =>
			{
				float envelope = t < 0.06f ? t / 0.06f : Mathf.Pow(1f - (t - 0.06f) / 0.94f, 1.6f);
				layer.SetWash(envelope * fx.WashPeak, Color.Lerp(Colors.Mid, Colors.Core, 0.45f));
				layer.SetVignette(Mathf.Lerp(fx.ImpactVignette, 0f, EaseOut(t)) * (1f - Mathf.Clamp01(t * 0.2f)), Colors.Shade);
			}, () =>
			{
				layer.SetWash(0f, Colors.Mid);
				layer.SetVignette(0f, Colors.Shade);
			});

			// 시네마 바: 임팩트 뒤 잠시 있다가 걷힘
			AddTrack(true, impactReal + 0.5f, 0.55f, t => layer.SetBars(1f - EaseIn(t), fx.BarRatio), () => layer.SetBars(0f));

			// 방사 속도선: 짧게 번쩍이며 천천히 돎
			AddTrack(true, impactReal, fx.StreakSeconds + fx.HitStopSeconds, t =>
				layer.SetStreaks(Mathf.Pow(1f - t, 1.6f) * fx.StreakAlpha, Color.Lerp(Color.white, Colors.Core, 0.5f), t * 40f, Mathf.Lerp(1.05f, 2.3f, EaseOut(t))),
				() => layer.SetStreaks(0f, Color.white, 0f));

			// 줌 킥: 히트스톱이 끝난 뒤 한 번 밀려났다 돌아옴
			AddTrack(true, impactReal + fx.HitStopSeconds * 0.5f, 0f, _ =>
			{
				if (CameraManager.TryGetExistingInstance(out CameraManager cameraManager))
				{
					Action release = cameraManager.PunchZoom(fx.KickDistanceScale, fx.KickFieldOfView, 0.05f, 0.1f, 0.7f, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f));
					AddTrack(true, realTime + 0.16f, 0f, __ => release?.Invoke());
				}
			});

			// 흔들림: 감쇠하는 연속 충격
			float amplitude = fx.ShakeAmplitude;
			for (int i = 0; i < fx.ShakeCount; i++)
			{
				float strength = amplitude;
				AddTrack(true, impactReal + fx.HitStopSeconds * 0.4f + i * fx.ShakeInterval, 0f, _ =>
				{
					if (CameraManager.TryGetExistingInstance(out CameraManager cameraManager))
						cameraManager.GenerateCameraImpulse(strength);
				});
				amplitude *= fx.ShakeDecay;
			}

			BuildImpactWorld();
			BuildAftermath();
		}
	}
}
