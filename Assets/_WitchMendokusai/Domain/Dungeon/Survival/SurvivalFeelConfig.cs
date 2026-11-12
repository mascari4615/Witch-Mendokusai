using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 한 순간의 흔들림, 전역 정지, 조각과 고리
	[Serializable]
	public class FeelMoment
	{
		[Min(0f)] public float Shake;
		[Min(0f), Tooltip("실제 시간 초")] public float Hitstop;
		[Range(0.01f, 1f)] public float HitstopScale = 0.05f;
		[Min(0)] public int Shards;
		[Min(0f)] public float ShardSpeed = 4f;
		[Min(0.01f)] public float ShardLife = 0.35f;
		[Min(0.01f)] public float ShardSize = 0.18f;
		[Min(0f)] public float RingSize;
		[Min(0.01f)] public float RingLife = 0.3f;
		public Color Color = Color.white;
	}

	[Serializable]
	public class ExpGemTier
	{
		[Min(0), Tooltip("이 경험치 이상이면 이 등급")] public int MinValue;
		[Min(0.1f)] public float Scale = 1f;
		public Color Color = Color.white;
		[Min(0f)] public float Emission = 3f;
	}

	[Serializable]
	public class SwordPattern
	{
		[Min(0.1f), Tooltip("적을 노리는 최대 거리")] public float TargetRange = 9f;
		[Min(0f), Tooltip("이동 방향 쪽 적을 이 거리만큼 가깝게 취급")] public float ForwardBias = 2.5f;
		[Min(0f), Tooltip("노릴 적이 모자랄 때 벌리는 각도")] public float FanDegrees = 35f;
		[Min(1), Tooltip("진화 뒤 이 횟수마다 전방위 베기")] public int ResonanceBurstEvery = 4;
		[Min(1)] public int ResonanceBurstSlashes = 6;
		[Min(0), Tooltip("전방위 베기 추가 피해")] public int ResonanceBurstDamage = 10;
	}

	[Serializable]
	public class WeaponHitColor
	{
		public int EquipmentId;
		public Color Color = Color.white;
	}

	// 영구 강화 중 레벨 값만으로 정해지지 않는 규칙 수치
	[Serializable]
	public class UpgradeTuning
	{
		[Min(0.1f), Tooltip("체력 재생 주기 초. 주기마다 HP_REGENERATION 만큼 회복")] public float HpRegenInterval = 20f;
		[Range(0.01f, 1f), Tooltip("부활 체력, 최대 체력 대비 비율")] public float ReviveHealthRatio = 0.5f;
		[Min(0f), Tooltip("부활 직후 무적 초")] public float ReviveInvincibility = 3f;
		[Min(1f), Tooltip("INVINCIBLE_TIME 스탯에서 1초에 해당하는 값")] public float InvincibleTimeStatPerSecond = 1000f;
	}

	[CreateAssetMenu(fileName = nameof(SurvivalFeelConfig), menuName = "WM/Variable/" + nameof(SurvivalFeelConfig))]
	public class SurvivalFeelConfig : ScriptableObject
	{
		[Header("Materials")]
		public Material ShardMaterial;
		public Material RingMaterial;

		[Header("Freeze status")]
		public Sprite FreezeStatusIcon;
		public Color FreezeStatusColor = new(.65f, .85f, 1f, .85f);
		[Min(.01f)] public float FreezeMarkerSize = 1.8f;
		[Min(0f)] public float FreezeMarkerHeight = .08f;
		[Min(.01f)] public float FreezeMarkerRefreshLife = .25f;
		public Vector2 FreezeHudOffset = new(0f, -150f);
		public Vector2 FreezeHudSize = new(420f, 36f);
		[Min(1f)] public float FreezeHudFontSize = 22f;
		[Min(1f)] public float FreezeHudIconSize = 30f;
		[Min(0f)] public float FreezeHudIconSpacing = 8f;
		public string FreezeStatusLabel = "시간정지";
		public string FreezeBossLabel = "보스";
		public string FreezeBossReleasedLabel = "보스 해제";

		[Header("Player hit flash")]
		public RuntimeAnimatorController PlayerHitFlashController;
		[Range(0f, 1f), Tooltip("피격 때 화면 전체 빨강의 세기 배율")] public float PlayerHitFlashStrength = 0.3f;

		[Header("Shake limit")]
		[Min(0f), Tooltip("쌓인 흔들림 상한. 대량 적에서 이 이상 쌓이지 않는다")] public float ShakeCap = 0.9f;
		[Min(0f), Tooltip("초당 빠지는 흔들림 양")] public float ShakeDecay = 2.5f;
		[Min(0f), Tooltip("적중과 처치 흔들림의 최소 간격")] public float SmallShakeInterval = 0.12f;

		[Header("Rate limit")]
		[Min(0f), Tooltip("처치 전역 정지 최소 간격")] public float KillHitstopInterval = 1.2f;
		[Min(0), Tooltip("초당 조각 터뜨리기 상한. 넘으면 고리와 조각을 생략")] public int BurstsPerSecond = 30;
		[Min(1)] public int MaxParticles = 800;
		[Min(0.1f), Tooltip("엘리트 주변 조각 주기")] public float EliteAuraInterval = 0.12f;
		[Min(0.1f)] public float EliteRingInterval = 0.9f;
		public float ImpactHeight = 0.6f;
		[Min(0f), Tooltip("조각 시작 위치 흩어짐")] public float ShardOriginSpread = 0.45f;
		[Min(0f), Tooltip("조각 속도 감쇠 (초당). 속도 상한으로 묶지 않음")] public float ShardDrag = 2.5f;
		[Range(0f, 1f), Tooltip("조각 위쪽 성분 최대치. 낮을수록 바닥을 따라 퍼짐")] public float ShardLift = 0.35f;
		[Range(0f, 1f), Tooltip("조각 방향 칸 안 흔들림. 0이면 등간격")] public float ShardAngleJitter = 0.8f;

		[Header("Sound limit")]
		[Min(1)] public int ExpSoundsPerWindow = 3;
		[Min(0.01f)] public float ExpSoundWindow = 0.1f;

		[Header("Moments")]
		public FeelMoment Hit = new() { Shake = 0.02f, Shards = 2, ShardSpeed = 3f, ShardLife = 0.18f, ShardSize = 0.1f };
		public FeelMoment Kill = new() { Shake = 0.05f, Hitstop = 0.03f, HitstopScale = 0.2f, Shards = 6, ShardSpeed = 5f, ShardLife = 0.32f, ShardSize = 0.16f, RingSize = 1.6f, RingLife = 0.22f, Color = new Color(1f, 0.95f, 0.75f, 1f) };
		public FeelMoment EliteAura = new() { Shards = 1, ShardSpeed = 1.2f, ShardLife = 0.5f, ShardSize = 0.16f, RingSize = 3f, RingLife = 0.7f, Color = new Color(1f, 0.72f, 0.3f, 1f) };
		public FeelMoment EliteKill = new() { Shake = 0.45f, Hitstop = 0.1f, HitstopScale = 0.05f, Shards = 40, ShardSpeed = 9f, ShardLife = 0.6f, ShardSize = 0.26f, RingSize = 7f, RingLife = 0.45f, Color = new Color(1f, 0.72f, 0.3f, 1f) };
		public FeelMoment ExpPickup = new() { Shards = 2, ShardSpeed = 2.5f, ShardLife = 0.2f, ShardSize = 0.09f, Color = new Color(1f, 0.85f, 0.35f, 1f) };
		public FeelMoment LevelUp = new() { Shake = 0.25f, Shards = 36, ShardSpeed = 7f, ShardLife = 0.8f, ShardSize = 0.22f, RingSize = 8f, RingLife = 0.7f, Color = new Color(0.65f, 0.9f, 1f, 1f) };
		public FeelMoment SwarmStart = new() { Shake = 0.3f, RingSize = 22f, RingLife = 0.6f, Color = new Color(1f, 0.4f, 0.35f, 1f) };
		public FeelMoment BossSpawn = new() { Shake = 0.8f, Hitstop = 0.14f, HitstopScale = 0.05f, Shards = 60, ShardSpeed = 11f, ShardLife = 0.8f, ShardSize = 0.3f, RingSize = 14f, RingLife = 0.7f, Color = new Color(1f, 0.35f, 0.3f, 1f) };
		public FeelMoment BossKill = new() { Shake = 1.1f, Hitstop = 0.28f, HitstopScale = 0.04f, Shards = 120, ShardSpeed = 14f, ShardLife = 1.2f, ShardSize = 0.34f, RingSize = 20f, RingLife = 0.9f, Color = new Color(1f, 0.9f, 0.55f, 1f) };
		public FeelMoment PlayerDeath = new() { Shake = 0.7f, Shards = 50, ShardSpeed = 8f, ShardLife = 0.9f, ShardSize = 0.26f, RingSize = 8f, RingLife = 0.6f, Color = new Color(1f, 0.3f, 0.3f, 1f) };
		public FeelMoment FreezePick = new() { Shake = 0.1f, Shards = 24, ShardSpeed = 6f, ShardLife = 0.5f, ShardSize = 0.2f, RingSize = 12f, RingLife = 0.5f, Color = new Color(0.45f, 0.75f, 1f, 1f) };
		public FeelMoment ClearPick = new() { Shake = 0.2f, Shards = 40, ShardSpeed = 9f, ShardLife = 0.5f, ShardSize = 0.22f, RingSize = 16f, RingLife = 0.45f, Color = new Color(1f, 0.9f, 0.4f, 1f) };
		public FeelMoment ChestOpen = new() { Shake = 0.3f, Shards = 48, ShardSpeed = 9f, ShardLife = 0.9f, ShardSize = 0.24f, RingSize = 9f, RingLife = 0.7f, Color = new Color(1f, 0.85f, 0.35f, 1f) };

		[Header("Sword")]
		public SwordPattern Sword = new();
		public FeelMoment ResonanceAwakening = new() { Shake = 0.6f, Shards = 80, ShardSpeed = 12f, ShardLife = 0.9f, ShardSize = 0.3f, RingSize = 16f, RingLife = 0.8f, Color = new Color(0.6f, 0.8f, 1f, 1f) };
		public FeelMoment ResonanceBurst = new() { Shake = 0.2f, Shards = 16, ShardSpeed = 8f, ShardLife = 0.4f, ShardSize = 0.2f, RingSize = 9f, RingLife = 0.35f, Color = new Color(0.6f, 0.8f, 1f, 1f) };

		[Header("Clear")]
		[Min(0f), Tooltip("보스 클리어 뒤 결과창까지 실제 시간 초")] public float BossClearResultDelay = 1f;
		[Min(0f), Tooltip("사망 뒤 결과창까지 실제 시간 초")] public float DefeatResultDelay = 1f;

		[Header("Level up pop")]
		[Min(1f)] public float LevelUpPopScale = 1.35f;
		[Min(0.01f)] public float LevelUpPopTime = 0.28f;

		[Header("Weapon hit colors")]
		public Color DefaultHitColor = new(1f, 0.95f, 0.8f, 1f);
		public List<WeaponHitColor> WeaponHitColors = new();

		[Header("Exp gems")]
		[Min(1), Tooltip("바닥 보석 상한. 넘으면 새 보석 대신 기존 보석에 값을 더한다")] public int ExpGemCap = 200;
		public List<ExpGemTier> ExpGemTiers = new()
		{
			new ExpGemTier { MinValue = 0, Scale = 1f, Color = new Color(0.45f, 0.75f, 1f, 1f), Emission = 3f },
			new ExpGemTier { MinValue = 15, Scale = 1.35f, Color = new Color(0.45f, 1f, 0.55f, 1f), Emission = 4f },
			new ExpGemTier { MinValue = 60, Scale = 1.8f, Color = new Color(1f, 0.35f, 0.4f, 1f), Emission = 6f },
		};

		[Header("Exp orb look")]
		[Min(0.01f), Tooltip("등급 1 오브 기준 크기. 이전 구 0.15의 75%")] public float ExpOrbSize = 0.1125f;
		[Range(0.1f, 1.5f), Tooltip("큐브 한 변 비율. 구 지름과 같은 인상이 되게 줄인 값")] public float ExpOrbCubeRatio = 0.85f;
		[Min(0f), Tooltip("등급 발광값에 곱함. HDR 발광과 Bloom의 밝은 테두리 조절")] public float ExpOrbEmissionScale = 1.1f;
		[Tooltip("회전 기울기. Y는 회전 시작 각에 더해짐")] public Vector3 ExpOrbTilt = new(35f, 0f, 20f);
		[Min(0f), Tooltip("초당 회전 각도")] public float ExpOrbSpinSpeed = 90f;
		[Range(0f, 0.5f), Tooltip("맥동 크기 변화 비율")] public float ExpOrbPulseAmplitude = 0.08f;
		[Min(0f), Tooltip("맥동 초당 횟수")] public float ExpOrbPulseSpeed = 1.2f;
		[Min(0f), Tooltip("떠오름 높이. 바닥 위로만 오르내림")] public float ExpOrbBobAmplitude = 0.04f;
		[Min(0f), Tooltip("떠오름 초당 횟수")] public float ExpOrbBobSpeed = 0.8f;

		[Header("Map look")]
		[Tooltip("생존 맵에서 풀과 지면 재질 바꾸기 (사용자 2026-11-12 조명 C안)")] public bool UseMapLook = true;
		[Tooltip("바꿀 원본 풀 재질 (Unlit)")] public Material GrassSourceMaterial;
		[Tooltip("대신 쓸 빛 받는 풀 재질")] public Material GrassLitMaterial;
		[Tooltip("색을 밝힐 원본 지면 재질")] public Material GroundSourceMaterial;
		[Min(0f), Tooltip("지면 색 배율")] public float GroundColorScale = 1.25f;

		[Header("Run sky")]
		[Tooltip("판 동안 하늘을 월드 시계 대신 아래 시간대로 그림. 월드 시계는 계속 흐름")] public bool UseRunSky;
		[Range(0f, 24f), Tooltip("판 시작 때 하늘 시각")] public float RunSkyStartHour = 10f;
		[Range(0f, 24f), Tooltip("판 제한 시간이 끝날 때 하늘 시각")] public float RunSkyEndHour = 16.5f;

		[Header("Low health")]
		[Range(0f, 1f), Tooltip("이 체력 비율 아래에서 화면 가장자리 붉게")] public float LowHealthThreshold = 0.3f;
		[Range(0f, 1f), Tooltip("경계 바로 아래의 최소 세기")] public float LowHealthMinWeight = 0.35f;
		public Color LowHealthColor = new(0.75f, 0.05f, 0.08f, 1f);
		[Range(0f, 1f), Tooltip("가장자리 최대 불투명도")] public float LowHealthMaxAlpha = 0.55f;
		[Range(0f, 1f), Tooltip("이 반경 (모서리 기준 비율) 안쪽은 투명")] public float LowHealthInnerRadius = 0.45f;
		[Min(0.1f), Tooltip("가장자리로 갈수록 짙어지는 곡선")] public float LowHealthEdgePower = 1.6f;
		[Min(0f), Tooltip("맥동 초당 횟수")] public float LowHealthPulseSpeed = 1.1f;
		[Range(0f, 1f), Tooltip("맥동 때 줄어드는 비율")] public float LowHealthPulseDepth = 0.35f;
		[Min(0f), Tooltip("세기 변화 초당 최대량")] public float LowHealthFadeSpeed = 2.5f;
		[Tooltip("화면 위 캔버스 순서. 전투 UI 보다 아래")] public int LowHealthSortingOrder = -5;

		[Header("Upgrade")]
		public UpgradeTuning Upgrade = new();
		public FeelMoment PlayerRevive = new() { Shake = 0.5f, Shards = 60, ShardSpeed = 9f, ShardLife = 0.8f, ShardSize = 0.26f, RingSize = 10f, RingLife = 0.7f, Color = new Color(1f, 0.9f, 0.55f, 1f) };
	}
}
