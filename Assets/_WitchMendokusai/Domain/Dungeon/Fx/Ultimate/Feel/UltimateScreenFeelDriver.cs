using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 화면 연출 구동기. 박자표 (UltimateScreenFeel) 하나를 진행기 (UltimateFxRun) 하나 위에서 매 프레임 구동. 강한 판 넷이 같이 씀
	/// - 시간: 정지 한 박과 느려짐을 진행기 시계 배율 (주제 메시와 연출 트랙) 과 게임 시간 배율 (입자, 인물, 적) 에 같은 값으로 넣음. 화면 장치 곡선은 실제 시간이라 정지 중에도 흐름
	/// - 카메라: 시야각 펀치, 전진, 트라우마 흔들림, 궁극기 동안 물림. 그리기 직전에만 얹고 그린 뒤 되돌림 (UltimateFeelCamera)
	/// - 후처리: 아래 Volume 값에 더하는 방식 (UltimateFeelVolume). 기존 연출의 장면 빛 곡선을 지우지 않음
	/// - 화면 왜곡: 방사 흐림, 굴절 고리, 임팩트 프레임 (UltimateImpactScreen, 렌더러 기능)
	/// - 끝나면 (도중 중단 포함) 시간 배율, 카메라, Volume, 화면 왜곡 전부 되돌림
	/// - 동시에 하나만. 새 구동기가 시작하면 앞 구동기를 끝냄 (시간 배율, Volume 더하기, 화면 왜곡이 전역 하나라 겹치면 서로 덮음)
	/// </summary>
	public sealed class UltimateScreenFeelDriver
	{
		private const float TRIGGER_EPSILON = 0.0005f;

		private static UltimateScreenFeelDriver current;

		private readonly UltimateFxRun run;
		private readonly UltimateScreenFeel feel;
		private readonly Vector3 target;
		private readonly float pullBackEnd;
		private readonly UltimateFeelCamera feelCamera;
		private readonly UltimateFeelVolume feelVolume;
		private readonly float[] impactReal;
		private readonly float[] swellEndReal;
		private readonly bool[] traumaGiven;
		private readonly TraumaShake shake;
		private bool disposed;

		/// <param name="pullBackEnd"> 물림이 0 으로 돌아오는 연출 시간 (보통 body 길이) </param>
		public UltimateScreenFeelDriver(UltimateFxRun run, UltimateScreenFeel feel, Vector3 target, float pullBackEnd)
		{
			this.run = run;
			this.feel = feel;
			this.target = target;
			this.pullBackEnd = pullBackEnd;
			impactReal = new float[feel.Beats.Length];
			swellEndReal = new float[feel.Swells.Length];
			traumaGiven = new bool[feel.Beats.Length];
			for (int i = 0; i < impactReal.Length; i++)
				impactReal[i] = float.NaN;
			for (int i = 0; i < swellEndReal.Length; i++)
				swellEndReal[i] = float.NaN;
			// 녹화마다 같은 흔들림이 나오게 에셋 이름으로 고정
			shake = new TraumaShake(Mathf.Abs(feel.name.GetHashCode() % 1000) * 0.1f);
			current?.Dispose();
			current = this;
			feelCamera = new UltimateFeelCamera(run.MainCamera);
			UltimateImpactScreen.TargetCamera = run.MainCamera;
			feelVolume = new UltimateFeelVolume();
			run.OnRelease(Dispose);
		}

		private float ComfortFlash => feel.Comfort != null ? feel.Comfort.Flash : 1f;
		private float ComfortShake => feel.Comfort != null ? feel.Comfort.Shake : 1f;
		private float ComfortZoom => feel.Comfort != null ? feel.Comfort.Zoom : 1f;
		private float ComfortDistortion => feel.Comfort != null ? feel.Comfort.Distortion : 1f;
		private float ComfortTimeWarp => feel.Comfort != null ? feel.Comfort.TimeWarp : 1f;
		private float ComfortImpactFrame => feel.Comfort != null ? feel.Comfort.ImpactFrame : 1f;
		private float ImpactFrameMaxSeconds => feel.Comfort != null ? feel.Comfort.ImpactFrameMaxSeconds : 0.09f;

		/// <summary> 매 프레임 (진행기 트랙, 연출 트랙 뒤) 부름 </summary>
		public void Update()
		{
			if (disposed)
				return;
			float realNow = run.RealNow;
			float fxNow = run.Now;
			TriggerBeats(realNow, fxNow);
			MarkSwellEnds(realNow, fxNow);
			ApplyTime(realNow, fxNow);
			ApplyCamera(realNow, fxNow);
			ApplyVolume(realNow, fxNow);
			ApplyScreen(realNow);
		}

		public void Dispose()
		{
			if (disposed)
				return;
			disposed = true;
			if (current == this)
				current = null;
			run.ClockScale = 1f;
			SetGameTimeScale(1f);
			feelCamera.Dispose();
			feelVolume.Dispose();
			UltimateImpactScreen.Clear();
		}

		// ---- 박자 ----

		private void TriggerBeats(float realNow, float fxNow)
		{
			UltimateScreenFeel.Beat[] beats = feel.Beats;
			for (int i = 0; i < beats.Length; i++)
			{
				if (float.IsNaN(impactReal[i]) == false || fxNow < beats[i].FxTime - TRIGGER_EPSILON)
					continue;
				float hold = beats[i].HoldSeconds * ComfortTimeWarp;
				impactReal[i] = realNow + hold;
			}
		}

		private void MarkSwellEnds(float realNow, float fxNow)
		{
			UltimateScreenFeel.Swell[] swells = feel.Swells;
			for (int i = 0; i < swells.Length; i++)
			{
				if (float.IsNaN(swellEndReal[i]) && fxNow > swells[i].FxEnd + TRIGGER_EPSILON)
					swellEndReal[i] = realNow;
			}
		}

		/// <summary> 고조 구간 세기 0~1. 끝까지는 연출 시간으로 오르고, 끝을 지난 뒤는 실제 시간으로 풀림 </summary>
		private float SwellWeight(int index, float realNow, float fxNow)
		{
			UltimateScreenFeel.Swell swell = feel.Swells[index];
			if (fxNow < swell.FxStart)
				return 0f;
			float progress = UltimateFxRun.Span(fxNow, swell.FxStart, swell.FxEnd);
			float rise = swell.Shape != null && swell.Shape.length > 0 ? Mathf.Clamp01(swell.Shape.Evaluate(progress)) : UltimateFxRun.EaseIn(progress);
			if (float.IsNaN(swellEndReal[index]))
				return rise;
			float release = Mathf.Clamp01((realNow - swellEndReal[index]) / Mathf.Max(0.0001f, swell.ReleaseSeconds));
			return rise * (1f - UltimateFxRun.EaseOut(release));
		}

		/// <summary> 타격 나이 (임팩트 뒤 실제 초). 정지 중이거나 시작 전이면 음수 </summary>
		private float BeatAge(int index, float realNow) => float.IsNaN(impactReal[index]) ? -1f : realNow - impactReal[index];

		private bool IsHolding(int index, float realNow) => float.IsNaN(impactReal[index]) == false && realNow < impactReal[index];

		// ---- 시간 ----

		private void ApplyTime(float realNow, float fxNow)
		{
			float scale = 1f;
			for (int i = 0; i < feel.Swells.Length; i++)
				scale = Mathf.Min(scale, Mathf.Lerp(1f, feel.Swells[i].TimeScale, SwellWeight(i, realNow, fxNow)));
			UltimateScreenFeel.Beat[] beats = feel.Beats;
			for (int i = 0; i < beats.Length; i++)
			{
				if (IsHolding(i, realNow))
				{
					scale = Mathf.Min(scale, beats[i].HoldTimeScale);
					continue;
				}
				float age = BeatAge(i, realNow);
				if (age < 0f || age >= beats[i].SlowSeconds)
					continue;
				float progress = age / Mathf.Max(0.0001f, beats[i].SlowSeconds);
				float recovery = beats[i].SlowRecovery != null && beats[i].SlowRecovery.length > 0 ? Mathf.Clamp01(beats[i].SlowRecovery.Evaluate(progress)) : progress;
				scale = Mathf.Min(scale, Mathf.Lerp(beats[i].SlowScale, 1f, recovery));
			}
			scale = Mathf.Lerp(1f, scale, ComfortTimeWarp);
			SetGameTimeScale(Mathf.Max(scale, 0.0001f));

			// 정지 박자 시각을 지나치지 않게: 다음 프레임에 연출 시간이 정확히 그 시각에 닿도록 주제 연출 시계만 깎음 (게임 시간은 그대로)
			float step = Mathf.Max(0.0001f, run.RealDelta);
			float clock = scale;
			for (int i = 0; i < beats.Length; i++)
			{
				if (float.IsNaN(impactReal[i]) == false || fxNow >= beats[i].FxTime || beats[i].HoldSeconds * ComfortTimeWarp <= 0f)
					continue;
				float remaining = beats[i].FxTime - fxNow;
				if (step * clock > remaining)
					clock = remaining / step;
			}
			run.ClockScale = clock;
		}

		private static void SetGameTimeScale(float scale)
		{
			if (TimeManager.TryGetExistingInstance(out TimeManager timeManager))
				timeManager.SetEffectScale(scale);
		}

		// ---- 카메라 ----

		private void ApplyCamera(float realNow, float fxNow)
		{
			float zoom = 0f;
			float dolly = 0f;
			float rumble = 0f;
			for (int i = 0; i < feel.Swells.Length; i++)
			{
				float weight = SwellWeight(i, realNow, fxNow);
				zoom += feel.Swells[i].Zoom * weight;
				dolly += feel.Swells[i].Dolly * weight;
				rumble = Mathf.Max(rumble, feel.Swells[i].Rumble * weight);
			}
			UltimateScreenFeel.Beat[] beats = feel.Beats;
			for (int i = 0; i < beats.Length; i++)
			{
				float age = BeatAge(i, realNow);
				if (age < 0f)
					continue;
				// 임팩트 순간 트라우마를 한 번 더함
				if (traumaGiven[i] == false)
				{
					traumaGiven[i] = true;
					shake.Add(beats[i].Trauma);
				}
				zoom += beats[i].Zoom.Evaluate(age);
				dolly += beats[i].Dolly.Evaluate(age);
			}
			shake.Step(feel.Trauma, realNow, run.RealDelta, rumble, ComfortShake, out Vector2 offset, out float roll);
			// 물림은 움직임을 줄이는 쪽이라 세기 줄이기 대상 아님. 연출 시간 기준이라 정지 동안 그대로 머묾
			float pullBack = feel.PullBack.Amount * feel.PullBack.Evaluate(fxNow, pullBackEnd);
			UltimateScreenFeel.CameraPath path = feel.Path;
			Vector2 orbit = new Vector2(UltimateScreenFeel.CameraPath.At(path.Pitch, fxNow), UltimateScreenFeel.CameraPath.At(path.Yaw, fxNow)) * ComfortZoom;
			roll += UltimateScreenFeel.CameraPath.At(path.Roll, fxNow) * ComfortZoom;
			zoom += UltimateScreenFeel.CameraPath.At(path.Zoom, fxNow);
			feelCamera.Set(target, offset, roll, zoom * ComfortZoom, dolly * ComfortZoom - pullBack, orbit);
		}

		// ---- 후처리 ----

		private void ApplyVolume(float realNow, float fxNow)
		{
			float exposure = 0f;
			float saturation = 0f;
			float contrast = 0f;
			float vignette = 0f;
			for (int i = 0; i < feel.Swells.Length; i++)
			{
				float weight = SwellWeight(i, realNow, fxNow);
				exposure += feel.Swells[i].Exposure * weight;
				saturation += feel.Swells[i].Saturation * weight;
				contrast += feel.Swells[i].Contrast * weight;
				vignette += feel.Swells[i].Vignette * weight;
			}
			float bloom = 0f;
			float chromatic = 0f;
			float lens = 0f;
			float flash = 0f;
			UltimateScreenFeel.Beat[] beats = feel.Beats;
			for (int i = 0; i < beats.Length; i++)
			{
				float age = BeatAge(i, realNow);
				if (age < 0f)
					continue;
				// 임팩트 프레임 동안은 섬광을 미룸 (흑백과 실루엣이 하얗게 날아가 눈부심, 독립 검토 4건 공통). 프레임이 끝난 뒤부터 섬광 곡선 시작
				float frameSeconds = beats[i].ImpactFrame == UltimateScreenFeel.ImpactFrameKind.None ? 0f : Mathf.Min(beats[i].ImpactFrameSeconds, ImpactFrameMaxSeconds) * ComfortImpactFrame;
				float flashAge = age - frameSeconds;
				if (flashAge >= 0f)
				{
					flash += beats[i].Exposure.Evaluate(flashAge);
					bloom += beats[i].Bloom.Evaluate(flashAge);
				}
				saturation += beats[i].Saturation.Evaluate(age);
				contrast += beats[i].Contrast.Evaluate(age);
				chromatic += beats[i].Chromatic.Evaluate(age);
				lens += beats[i].LensDistortion.Evaluate(age);
				vignette += beats[i].Vignette.Evaluate(age);
			}
			// 밝아지는 섬광만 줄이기 대상. 어두워지는 고조는 그대로
			exposure += flash > 0f ? flash * ComfortFlash : flash;
			feelVolume.Apply(exposure, bloom * ComfortFlash, saturation, contrast, chromatic, lens * ComfortDistortion, feel.LensScaleCompensation, vignette);
		}

		// ---- 화면 왜곡 ----

		private void ApplyScreen(float realNow)
		{
			float radial = 0f;
			float ripple = 0f;
			float rippleRadius = 0f;
			UltimateScreenFeel.ImpactFrameKind frameKind = UltimateScreenFeel.ImpactFrameKind.None;
			float frameStrength = 0f;
			UltimateScreenFeel.Beat[] beats = feel.Beats;
			for (int i = 0; i < beats.Length; i++)
			{
				float age = BeatAge(i, realNow);
				if (age < 0f)
					continue;
				radial += beats[i].RadialBlur.Evaluate(age);
				float rippleNow = beats[i].Ripple.Evaluate(age);
				if (rippleNow > ripple)
				{
					ripple = rippleNow;
					rippleRadius = age * beats[i].RippleSpeed;
				}
				float frameSeconds = Mathf.Min(beats[i].ImpactFrameSeconds, ImpactFrameMaxSeconds);
				if (beats[i].ImpactFrame != UltimateScreenFeel.ImpactFrameKind.None && age < frameSeconds)
				{
					frameKind = beats[i].ImpactFrame;
					frameStrength = beats[i].ImpactFrameStrength * ComfortImpactFrame;
				}
			}
			// 중심은 카메라 손이 그리기 직전 (줌, 흔들림을 얹은 뒤) 다시 넣음
			Camera camera = run.MainCamera;
			float aspect = camera != null ? camera.aspect : 16f / 9f;
			UltimateImpactScreen.Set(run.Viewport(target), aspect, radial * ComfortDistortion, feel.RadialBlurReach, rippleRadius, ripple * ComfortDistortion, feel.RippleWidth, frameKind, frameStrength);
		}
	}
}
