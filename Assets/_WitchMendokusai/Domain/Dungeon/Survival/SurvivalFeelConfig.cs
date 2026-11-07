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

		[Header("Upgrade")]
		public UpgradeTuning Upgrade = new();
		public FeelMoment PlayerRevive = new() { Shake = 0.5f, Shards = 60, ShardSpeed = 9f, ShardLife = 0.8f, ShardSize = 0.26f, RingSize = 10f, RingLife = 0.7f, Color = new Color(1f, 0.9f, 0.55f, 1f) };
	}
}
