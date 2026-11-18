using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	/// <summary>
	/// 달리는 동안 발밑에서 작은 먼지 구름이 퐁퐁 남 (사용자 2026-11-18 "달릴 때 뽈뽈뽈 하고 연기")
	/// - 공유 ParticleSystem 하나에 Emit 만. 먼지마다 오브젝트 없음
	/// - 땅에 닿아 있고 실제로 움직일 때만. 달리기는 IS_SPRINTING 스탯으로 판정
	/// - 먼지는 이동 반대쪽으로 조금 밀려 남고, 커지면서 옅어짐
	/// - 걸을 때는 walkEmitInterval 이 0 이면 없음
	/// </summary>
	[RequireComponent(typeof(PlayerObject))]
	public class PlayerRunDust : MonoBehaviour
	{
		private const string DUST_OBJECT_NAME = "RunDust";

		[Tooltip("먼지 재질. 알파 합성 파티클 재질 (가산이면 먼지가 빛처럼 보임)")]
		[SerializeField] private Material dustMaterial;
		[Tooltip("한 번에 남아 있을 수 있는 먼지 수. 넘치면 그 먼지는 생략")]
		[SerializeField, Min(1)] private int maxParticles = 48;

		[Header("Timing")]
		[Tooltip("달릴 때 먼지 사이 간격 (초)")]
		[SerializeField, Min(0.01f)] private float runEmitInterval = 0.09f;
		[Tooltip("걸을 때 먼지 사이 간격 (초). 0 이면 걸을 때 먼지 없음")]
		[SerializeField, Min(0f)] private float walkEmitInterval = 0f;
		[Tooltip("걸을 때 먼지 크기와 진하기 배율")]
		[SerializeField, Range(0f, 1f)] private float walkStrength = 0.5f;
		[Tooltip("이 속도 (초당) 이상으로 움직일 때만 먼지")]
		[SerializeField, Min(0f)] private float minMoveSpeed = 0.5f;

		[Header("Spawn")]
		[Tooltip("발밑 높이. 캐릭터 기준점에서 위로")]
		[SerializeField] private float footHeight = 0.05f;
		[Tooltip("발 뒤로 얼마나 떨어져 나오나")]
		[SerializeField] private float behindOffset = 0.15f;
		[Tooltip("발생 자리 좌우 흔들림 폭")]
		[SerializeField, Min(0f)] private float sideJitter = 0.12f;
		[Tooltip("한 번 발생 먼지 수 (최소, 최대)")]
		[SerializeField] private Vector2Int countRange = new(1, 2);

		[Header("Motion")]
		[Tooltip("이동 반대쪽으로 밀려나는 속도 (최소, 최대)")]
		[SerializeField] private Vector2 backSpeedRange = new(0.4f, 0.9f);
		[Tooltip("위로 뜨는 속도 (최소, 최대)")]
		[SerializeField] private Vector2 upSpeedRange = new(0.15f, 0.4f);
		[Tooltip("좌우로 퍼지는 속도. 밀려나는 속도에 곱함")]
		[SerializeField, Min(0f)] private float sideSpread = 0.5f;
		[Tooltip("감속. 클수록 빨리 멈춤")]
		[SerializeField, Min(0f)] private float drag = 3f;

		[Header("Look")]
		[Tooltip("먼지 수명 (최소, 최대, 초)")]
		[SerializeField] private Vector2 lifetimeRange = new(0.35f, 0.55f);
		[Tooltip("처음 크기 (최소, 최대)")]
		[SerializeField] private Vector2 startSizeRange = new(0.18f, 0.28f);
		[Tooltip("수명 동안 크기 배율. 가로축 진행도 0 ~ 1")]
		[SerializeField] private AnimationCurve sizeOverLifetime = AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1f);
		[Tooltip("수명 동안 크기 최대 배율. 위 곡선 값에 곱함")]
		[SerializeField, Min(0f)] private float sizeGrowth = 2.2f;
		[Tooltip("먼지 색")]
		[SerializeField] private Color dustColor = new(0.86f, 0.81f, 0.72f, 0.55f);
		[Tooltip("수명 동안 진하기. 끝으로 갈수록 옅어짐")]
		[SerializeField] private Gradient fadeOverLifetime = CreateDefaultFade();

		private PlayerObject playerObject;
		private ParticleSystem dust;
		private float emitTimer;

		private void Awake()
		{
			playerObject = GetComponent<PlayerObject>();
		}

		private void OnDestroy()
		{
			if (dust != null)
				Destroy(dust.gameObject);
		}

#if UNITY_EDITOR
		// 에디터에서 인스펙터 값을 Play 중 바꾸면 다음 먼지부터 반영
		private void OnValidate()
		{
			if (dust != null)
				ConfigureDust(dust);
		}
#endif

		private void Update()
		{
			float interval = CurrentInterval(out float strength);
			if (interval <= 0f)
			{
				// 다시 달리기 시작하면 첫 먼지가 바로 나오게
				emitTimer = runEmitInterval;
				return;
			}

			emitTimer += Time.deltaTime;
			if (emitTimer < interval)
				return;

			// 프레임이 크게 끊겨도 한 번에 몰아 쏘지 않음
			emitTimer = Mathf.Min(emitTimer - interval, interval);
			Emit(strength);
		}

		// 지금 먼지를 낼 간격. 0 이면 안 냄
		private float CurrentInterval(out float strength)
		{
			strength = 0f;
			// 이동 부품은 Init 에서 붙음. 그 전 프레임은 건너뜀
			if (playerObject.UnitMovement == null)
				return 0f;
			if (playerObject.UnitStat[UnitStatType.DEAD] > 0)
				return 0f;
			if (playerObject.UnitMovement.IsGrounded() == false)
				return 0f;

			Vector3 velocity = playerObject.UnitMovement.Velocity;
			velocity.y = 0f;
			if (velocity.magnitude < minMoveSpeed)
				return 0f;

			if (playerObject.UnitStat[UnitStatType.IS_SPRINTING] > 0)
			{
				strength = 1f;
				return runEmitInterval;
			}

			strength = walkStrength;
			return walkEmitInterval;
		}

		private void Emit(float strength)
		{
			ParticleSystem system = EnsureDust();

			int count = UnityEngine.Random.Range(countRange.x, countRange.y + 1);
			if (system.particleCount + count > maxParticles)
				return;

			Vector3 velocity = playerObject.UnitMovement.Velocity;
			velocity.y = 0f;
			Vector3 back = -velocity.normalized;
			Vector3 side = Vector3.Cross(Vector3.up, back);
			Vector3 foot = transform.position + Vector3.up * footHeight + back * behindOffset;

			Color color = dustColor;
			color.a *= strength;
			ParticleSystem.EmitParams emitParams = new() { startColor = color };
			for (int i = 0; i < count; i++)
			{
				float sideAmount = UnityEngine.Random.Range(-1f, 1f);
				emitParams.position = foot + side * (sideAmount * sideJitter);
				emitParams.velocity = back * UnityEngine.Random.Range(backSpeedRange.x, backSpeedRange.y)
					+ side * (sideAmount * UnityEngine.Random.Range(backSpeedRange.x, backSpeedRange.y) * sideSpread)
					+ Vector3.up * UnityEngine.Random.Range(upSpeedRange.x, upSpeedRange.y);
				emitParams.startLifetime = UnityEngine.Random.Range(lifetimeRange.x, lifetimeRange.y);
				emitParams.startSize = UnityEngine.Random.Range(startSizeRange.x, startSizeRange.y) * strength;
				emitParams.rotation = UnityEngine.Random.Range(0f, 360f);
				system.Emit(emitParams, 1);
			}
		}

		// 인스펙터 값으로 정하는 모듈 값. 생성 때 한 번, 에디터에서는 OnValidate 때 다시
		private void ConfigureDust(ParticleSystem system)
		{
			ParticleSystem.MainModule main = system.main;
			main.maxParticles = maxParticles;

			ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = system.limitVelocityOverLifetime;
			limitVelocity.drag = drag;

			ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
			size.size = new ParticleSystem.MinMaxCurve(sizeGrowth, sizeOverLifetime);

			ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
			colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fadeOverLifetime);

			ParticleSystemRenderer dustRenderer = system.GetComponent<ParticleSystemRenderer>();
			dustRenderer.sharedMaterial = dustMaterial;
		}

		private ParticleSystem EnsureDust()
		{
			if (dust != null)
				return dust;

			// 캐릭터 밑에 두지 않음. 캐릭터가 꺼지거나 순간이동해도 남은 먼지가 같이 끌려가지 않게
			GameObject dustObject = new(DUST_OBJECT_NAME);
			ParticleSystem system = dustObject.AddComponent<ParticleSystem>();
			system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

			ParticleSystem.MainModule main = system.main;
			main.loop = true;
			main.playOnAwake = false;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.startSpeed = 0f;
			main.gravityModifier = 0f;
			main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

			// 쏘는 것은 Emit 으로만
			ParticleSystem.EmissionModule emission = system.emission;
			emission.enabled = false;
			ParticleSystem.ShapeModule shape = system.shape;
			shape.enabled = false;

			ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = system.limitVelocityOverLifetime;
			limitVelocity.enabled = true;
			limitVelocity.dampen = 0f;
			limitVelocity.multiplyDragByParticleSize = false;
			limitVelocity.multiplyDragByParticleVelocity = false;

			ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
			size.enabled = true;
			ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
			colorOverLifetime.enabled = true;

			ParticleSystemRenderer dustRenderer = dustObject.GetComponent<ParticleSystemRenderer>();
			dustRenderer.renderMode = ParticleSystemRenderMode.Billboard;
			dustRenderer.shadowCastingMode = ShadowCastingMode.Off;
			dustRenderer.receiveShadows = false;
			dustRenderer.sortMode = ParticleSystemSortMode.Distance;

			ConfigureDust(system);
			system.Play();
			dust = system;
			return dust;
		}

		private static Gradient CreateDefaultFade()
		{
			Gradient gradient = new();
			gradient.SetKeys(
				new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
				new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.4f), new GradientAlphaKey(0f, 1f) });
			return gradient;
		}
	}
}
