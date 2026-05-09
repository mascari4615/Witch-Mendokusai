using UnityEngine;

namespace WitchMendokusai
{
	// 단일 날씨의 데이터·게임플레이 flag·페이드 시간·시각 풀세트 노브.
	// E2-fix (2026-05-09) — visual 노브 (Material / 색 / 모양) 도 SO 정본화. WeatherDirector 가 spawn + 매 frame 반영.
	// prefab 은 empty ParticleSystem shell — 모든 visual 데이터는 본 SO 가 정본.
	// 룰 「수치 노출 / 런타임 tweak」 정합 — 인스펙터 슬라이더로 in-play tweak 즉시 반영.
	[CreateAssetMenu(fileName = nameof(WeatherSO), menuName = "WM/Weather/WeatherSO")]
	public class WeatherSO : DataSO
	{
		[field: Header("_" + nameof(WeatherSO))]
		[field: Tooltip("WeatherType enum 항목 (Bootstrap 이 자동 박음).")]
		[field: SerializeField] public WeatherType Type { get; private set; }

		[field: Tooltip("이 날씨로 페이드 인 시간 (게임분). 30 default = 자연 전이.")]
		[field: SerializeField, Range(1f, 120f)] public float FadeMinutes { get; private set; } = 30f;

		[field: Tooltip("ground 가 젖은 상태 (sub-F 농사 자동 물주기 / sub-E Wet shader hook 용).")]
		[field: SerializeField] public bool IsWet { get; private set; }

		[field: Tooltip("AudioManager 호출 키 (sub-E WeatherDirector 가 발동 시 사용).")]
		[field: SerializeField] public string SfxKey { get; private set; } = "";

		[field: Tooltip("디버그 HUD 표시 색 (sub-D D4).")]
		[field: SerializeField] public Color DebugTint { get; private set; } = Color.white;

		[field: Tooltip("시각 풀세트 prefab — empty ParticleSystem shell. WeatherDirector 가 OnWeatherChanged 시 Instantiate 후 본 SO 의 노브로 모든 ParticleSystem 모듈 set.")]
		[field: SerializeField] public GameObject VisualPrefab { get; private set; }

		// ─── Particle visual 노브 (E2-fix). WeatherDirector.ApplyCurrentSOToParticle 가 spawn 시 + 매 frame 적용 ───

		[field: Header("Particle Visual")]
		[field: Tooltip("ParticleSystemRenderer.sharedMaterial. URP Particle Unlit shader 사용. null = 핑크 fallback.")]
		[field: SerializeField] public Material ParticleMaterial { get; private set; }

		[field: Tooltip("ParticleSystem.main.startColor.")]
		[field: SerializeField] public Color ParticleColor { get; private set; } = Color.white;

		[field: SerializeField, Range(0.01f, 10f)] public float ParticleStartSize { get; private set; } = 0.05f;
		[field: SerializeField, Range(0f, 50f)] public float ParticleStartSpeed { get; private set; } = 18f;
		[field: SerializeField, Range(0.1f, 30f)] public float ParticleStartLifetime { get; private set; } = 1.5f;
		[field: SerializeField, Range(0f, 2000f)] public float ParticleEmissionRate { get; private set; } = 400f;
		[field: SerializeField, Range(-2f, 2f)] public float ParticleGravityModifier { get; private set; } = 0.5f;

		[field: Tooltip("ShapeModule.shapeType — Box (Rain/Snow/Storm) 또는 Sphere (Fog).")]
		[field: SerializeField] public ParticleSystemShapeType ParticleShapeType { get; private set; } = ParticleSystemShapeType.Box;

		[field: SerializeField] public Vector3 ParticleShapeScale { get; private set; } = new Vector3(40f, 0.1f, 40f);
		[field: SerializeField] public Vector3 ParticleShapePosition { get; private set; } = new Vector3(0f, 18f, 0f);
		[field: SerializeField] public Vector3 ParticleShapeRotation { get; private set; } = Vector3.zero;
		[field: SerializeField, Range(0.1f, 30f)] public float ParticleShapeRadius { get; private set; } = 12f;
	}
}
