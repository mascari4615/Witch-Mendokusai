using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 플레이어 피격 화면 연출. 사용자 2026-11-21 지적: 피격 이펙트 약함, 화면 흔들림과 가장자리 빨강 없음
	/// - 흔들림: 궁극기와 같은 트라우마 흔들림 (TraumaShake) 을 같은 카메라 손 (UltimateFeelCamera) 으로 그리기 직전에만 얹음. 궁극기와 겹치면 더해짐
	/// - 정지: TimeManager.HitStop 한 번 (1~3장)
	/// - 가장자리 빨강: SurvivalLowHealthVignette 의 피격 이미지
	/// - 몸 번쩍임은 PlayerObject 무적 시간 시작에서 (PlayerBodyFlash)
	/// - 연속 피격: 트라우마 상한, MinInterval 안에서는 정지와 가장자리를 새로 시작하지 않음. 무적 시간 중 피격은 PlayerObject 가 걸러 여기 안 옴
	/// - 세기 줄이기: 흔들림 끄기 설정과 UltFeel_Comfort 흔들림, 정지는 Comfort 시간. 가장자리는 위험 신호라 줄이지 않음
	/// </summary>
	public class PlayerHitFeel : MonoBehaviour
	{
		private const float NOISE_SEED = 73.1f;

		private ResourceManager resourceManager;
		private TimeManager timeManager;
		private SurvivalLowHealthVignette edge;
		private readonly TraumaShake shake = new(NOISE_SEED);
		private UltimateFeelCamera feelCamera;
		private Camera boundCamera;
		private Transform focus;
		private float lastPlayTime = float.NegativeInfinity;

		private SurvivalFeelConfig Config => resourceManager.SurvivalFeel;
		private float ComfortShake => Config.Comfort != null ? Config.Comfort.Shake : 1f;
		private float ComfortTimeWarp => Config.Comfort != null ? Config.Comfort.TimeWarp : 1f;

		/// <summary> 녹화와 시험용. 지금 트라우마 </summary>
		public float Trauma => shake.Trauma;

		public void Init(ResourceManager resourceManager, TimeManager timeManager, SurvivalLowHealthVignette edge)
		{
			this.resourceManager = resourceManager;
			this.timeManager = timeManager;
			this.edge = edge;
		}

		public void Play(PlayerObject player, int damage)
		{
			PlayerHitFeelSettings settings = Config.PlayerHit;
			float strength = settings.StrengthOf(damage, player.UnitStat[UnitStatType.HP_MAX]);
			focus = player.transform;
			shake.Add(Mathf.Lerp(settings.TraumaAtMin, settings.TraumaAtMax, strength), settings.TraumaCap);

			float now = Time.unscaledTime;
			if (now - lastPlayTime < settings.MinInterval)
				return;
			lastPlayTime = now;
			float hitStop = Mathf.Lerp(settings.HitStopAtMin, settings.HitStopAtMax, strength) * ComfortTimeWarp;
			if (hitStop > 0f)
				timeManager.HitStop(hitStop, settings.HitStopScale);
			edge.Hit(Mathf.Lerp(settings.EdgeAlphaAtMin, settings.EdgeAlphaAtMax, strength));
		}

		// 카메라 계산 (Cinemachine) 뒤, 그리기 전
		private void LateUpdate()
		{
			if (resourceManager == null)
				return;
			if (shake.Trauma <= 0f)
			{
				feelCamera?.Set(Vector3.zero, Vector2.zero, 0f, 0f, 0f);
				return;
			}
			BindCamera();
			float strength = CombatEffectSettings.ScreenShake ? ComfortShake : 0f;
			shake.Step(Config.PlayerHit.Trauma, Time.unscaledTime, Time.unscaledDeltaTime, 0f, strength, out Vector2 offset, out float roll);
			Vector3 target = focus != null ? focus.position : Vector3.zero;
			feelCamera?.Set(target, offset, roll, 0f, 0f);
		}

		// 씬마다 주 카메라가 바뀔 수 있어 쓸 때 다시 잡음
		private void BindCamera()
		{
			Camera main = Camera.main;
			if (main == boundCamera && feelCamera != null)
				return;
			feelCamera?.Dispose();
			boundCamera = main;
			feelCamera = main != null ? new UltimateFeelCamera(main, false) : null;
		}

		private void OnDestroy()
		{
			feelCamera?.Dispose();
			feelCamera = null;
		}
	}
}
