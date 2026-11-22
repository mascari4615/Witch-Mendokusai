using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class SurvivalFeel : MonoBehaviour
	{
		public static SurvivalFeel Instance { get; private set; }

		private ResourceManager resourceManager;
		private CameraManager cameraManager;
		private TimeManager timeManager;
		private GameEventManager gameEventManager;
		private PlayerProvider playerProvider;

		private ParticleSystem shards;
		private ParticleSystem rings;
		private ParticleSystem unscaledShards;
		private ParticleSystem unscaledRings;
		private readonly List<MonsterObject> elites = new();

		private float shakeEnergy;
		private float nextSmallShakeTime;
		private float nextKillHitstopTime;
		private float nextAuraTime;
		private float nextEliteRingTime;
		private float burstWindowEnd;
		private int burstCount;
		private UnityEngine.UI.Image hitFlashImage;
		private PlayerHitFeel playerHitFeel;
		private readonly Dictionary<string, int> lastSoundFrames = new();
		private int killChainStep;
		private float lastKillChainTime = float.NegativeInfinity;

		private SurvivalFeelConfig Config => resourceManager.SurvivalFeel;

		[Inject]
		public void Construct(ResourceManager resourceManager, CameraManager cameraManager, TimeManager timeManager, GameEventManager gameEventManager, PlayerProvider playerProvider, InputManager inputManager)
		{
			this.resourceManager = resourceManager;
			this.cameraManager = cameraManager;
			this.timeManager = timeManager;
			this.gameEventManager = gameEventManager;
			this.playerProvider = playerProvider;
			Instance = this;
			BuildSystems();
			SurvivalLowHealthVignette edge = gameObject.AddComponent<SurvivalLowHealthVignette>();
			edge.Init(resourceManager, playerProvider);
			playerHitFeel = gameObject.AddComponent<PlayerHitFeel>();
			playerHitFeel.Init(resourceManager, timeManager, edge);
			gameObject.AddComponent<SurvivalOffscreenMarkers>().Init(resourceManager, playerProvider, this);
			gameObject.AddComponent<SurvivalMinimap>().Init(resourceManager, playerProvider, this, inputManager);
			gameEventManager.RegisterCallback(GameEventType.OnLevelUp, OnLevelUp);
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
			gameEventManager?.UnregisterCallback(GameEventType.OnLevelUp, OnLevelUp);
		}

		private void BuildSystems()
		{
			shards = BuildSystem("Shards", Config.ShardMaterial, false, ParticleSystemRenderMode.Stretch);
			rings = BuildSystem("Rings", Config.RingMaterial, false, ParticleSystemRenderMode.HorizontalBillboard);
			unscaledShards = BuildSystem("UnscaledShards", Config.ShardMaterial, true, ParticleSystemRenderMode.Stretch);
			unscaledRings = BuildSystem("UnscaledRings", Config.RingMaterial, true, ParticleSystemRenderMode.HorizontalBillboard);
		}

		private ParticleSystem BuildSystem(string systemName, Material material, bool unscaled, ParticleSystemRenderMode renderMode)
		{
			GameObject host = new(systemName);
			host.SetActive(false);
			host.transform.SetParent(transform, false);
			ParticleSystem system = host.AddComponent<ParticleSystem>();
			ParticleSystem.MainModule main = system.main;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.useUnscaledTime = unscaled;
			main.playOnAwake = false;
			main.loop = false;
			main.maxParticles = Config.MaxParticles;
			main.gravityModifier = 0f;
			ParticleSystem.EmissionModule emission = system.emission;
			emission.enabled = false;
			ParticleSystem.ShapeModule shape = system.shape;
			shape.enabled = false;

			bool ring = renderMode == ParticleSystemRenderMode.HorizontalBillboard;
			ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
			size.enabled = true;
			size.size = new ParticleSystem.MinMaxCurve(1f, ring
				? AnimationCurve.EaseInOut(0f, 0.15f, 1f, 1f)
				: AnimationCurve.Linear(0f, 1f, 1f, 0f));
			ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
			color.enabled = true;
			Gradient fade = new();
			fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
				new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(ring ? 0.6f : 1f, 0.5f), new GradientAlphaKey(0f, 1f) });
			color.color = fade;
			if (ring == false)
			{
				// 속도 상한 감쇠는 조각을 출발점 근처에 묶어 한 점처럼 보임. 상한 없이 감쇠만
				ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
				limit.enabled = true;
				limit.dampen = 0f;
				limit.drag = Config.ShardDrag;
				limit.multiplyDragByParticleSize = false;
				limit.multiplyDragByParticleVelocity = false;
			}

			ParticleSystemRenderer systemRenderer = host.GetComponent<ParticleSystemRenderer>();
			systemRenderer.sharedMaterial = material;
			systemRenderer.renderMode = renderMode;
			systemRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			systemRenderer.receiveShadows = false;
			if (ring == false)
			{
				systemRenderer.velocityScale = 0.06f;
				systemRenderer.lengthScale = 1.2f;
			}
			host.SetActive(true);
			system.Play();
			return system;
		}

		private void Update()
		{
			shakeEnergy = Mathf.Max(0f, shakeEnergy - Config.ShakeDecay * Time.unscaledDeltaTime);
			UpdateEliteAura();
			ExpObject.TickMotion(Config, Time.time);
		}

		private void UpdateEliteAura()
		{
			if (elites.Count == 0 || Time.time < nextAuraTime)
				return;
			SurvivalFeelConfig config = Config;
			nextAuraTime = Time.time + config.EliteAuraInterval;
			bool ring = Time.time >= nextEliteRingTime;
			if (ring)
				nextEliteRingTime = Time.time + config.EliteRingInterval;
			for (int i = elites.Count - 1; i >= 0; i--)
			{
				MonsterObject elite = elites[i];
				EmitShards(shards, elite.transform.position, config.EliteAura, config.EliteAura.Color);
				if (ring)
					EmitRing(rings, elite.transform.position, config.EliteAura, config.EliteAura.Color);
			}
		}

		public IReadOnlyList<MonsterObject> Elites => elites;
		public void RegisterElite(MonsterObject elite) => elites.Add(elite);
		public void UnregisterElite(MonsterObject elite) => elites.Remove(elite);

		public void OnMonsterHit(Vector3 position, DamageInfo damageInfo)
		{
			SurvivalFeelConfig config = Config;
			RequestSmallShake(config.Hit.Shake);
			if (TakeBurst() == false)
				return;
			EmitShards(shards, position, config.Hit, HitColor(damageInfo.equipmentDataId));
		}

		public void OnMonsterKilled(Vector3 position)
		{
			SurvivalFeelConfig config = Config;
			RequestSmallShake(config.Kill.Shake);
			if (config.Kill.Hitstop > 0f && Time.unscaledTime >= nextKillHitstopTime)
			{
				nextKillHitstopTime = Time.unscaledTime + config.KillHitstopInterval;
				timeManager.HitStop(config.Kill.Hitstop, config.Kill.HitstopScale);
			}
			if (TakeBurst() == false)
				return;
			EmitShards(shards, position, config.Kill, config.Kill.Color);
			EmitRing(rings, position, config.Kill, config.Kill.Color);
		}

		public void OnEliteKilled(Vector3 position) => PlayMoment(Config.EliteKill, position, false);
		public void OnSwarmStarted(Vector3 position) => PlayMoment(Config.SwarmStart, position, false);
		public void OnBossSpawned(Vector3 position) => PlayMoment(Config.BossSpawn, position, false);
		public void OnBossKilled(Vector3 position) => PlayMoment(Config.BossKill, position, false);
		public void OnPlayerDied(Vector3 position) => PlayMoment(Config.PlayerDeath, position, true);
		public void OnPlayerRevived(Vector3 position) => PlayMoment(Config.PlayerRevive, position, true);

		public void OnResonanceAwakening(Vector3 position) => PlayMoment(Config.ResonanceAwakening, position, true);
		public void OnResonanceBurst(Vector3 position) => PlayMoment(Config.ResonanceBurst, position, false);
		public void OnFreezePicked(Vector3 position) => PlayMoment(Config.FreezePick, position, false);
		public void OnClearPicked(Vector3 position) => PlayMoment(Config.ClearPick, position, false);
		public void OnChestOpened(Vector3 position) => PlayMoment(Config.ChestOpen, position, true);

		public void OnExpPicked(Vector3 position)
		{
			if (TakeBurst())
				EmitShards(shards, position, Config.ExpPickup, Config.ExpPickup.Color);
		}

		// 같은 소리는 SameSoundFrameInterval 프레임에 한 번. 줍기 소리가 한 프레임에 겹쳐 커지는 것 방지
		public bool TakeSound(string soundKey)
		{
			int frame = Time.frameCount;
			if (lastSoundFrames.TryGetValue(soundKey, out int lastFrame) && frame - lastFrame < Config.SameSoundFrameInterval)
				return false;
			lastSoundFrames[soundKey] = frame;
			return true;
		}

		// 처치음 (MonsterPop) 음높이 배수. 연달아 처치할수록 음계를 따라 오름 (사운드 조사 S11). 실시간 기준이라 정지 연출에 안 끊김
		// 들여다보기만 하고 칸은 안 올림. 소리가 실제로 났을 때 CommitKillChain 으로 올려야 간격 제한에 걸러진 처치가 음계를 건너뛰지 않음
		public float PeekKillChainPitch()
			=> KillChainPitch.Multiplier(NextKillChainStep(), Config.KillChainScale);

		public void CommitKillChain()
		{
			killChainStep = NextKillChainStep();
			lastKillChainTime = Time.unscaledTime;
		}

		private int NextKillChainStep()
			=> KillChainPitch.NextStep(killChainStep, lastKillChainTime, Time.unscaledTime, Config.KillChainWindowSeconds, Config.KillChainMaxStep);

		public void OnWeaponBurst(Vector3 position, FeelMoment moment)
		{
			RequestSmallShake(moment.Shake);
			if (TakeBurst() == false)
				return;
			EmitShards(shards, position, moment, moment.Color);
			EmitRing(rings, position, moment, moment.Color);
		}

		// 무적 시간이 아닌 피격만 옴 (PlayerObject 가 거름). 화면 전체 빨강은 애니메이션 색 알파에 곱셈, 나머지 화면 연출은 PlayerHitFeel
		public void OnPlayerHit(PlayerObject player, int damage, bool statusTick)
		{
			if (hitFlashImage == null)
				hitFlashImage = FindHitFlashImage();
			hitFlashImage.canvasRenderer.SetAlpha(Config.PlayerHitFlashStrength);
			if (statusTick == false)
				playerHitFeel.Play(player, damage);
		}

		private UnityEngine.UI.Image FindHitFlashImage()
		{
			foreach (Animator animator in FindObjectsByType<Animator>())
				if (animator.runtimeAnimatorController == Config.PlayerHitFlashController)
					return animator.GetComponentInChildren<UnityEngine.UI.Image>(true);
			throw new System.InvalidOperationException("피격 화면 효과 없음");
		}

		private void OnLevelUp()
		{
			if (DungeonManagerBridge.IsDungeon == false)
				return;
			SurvivalFeelConfig config = Config;
			PlayerObject player = playerProvider.CurrentObject;
			PlayMoment(config.LevelUp, player.transform.position, true);
			player.Health.PunchScale(config.LevelUpPopScale, config.LevelUpPopTime);
		}

		private void PlayMoment(FeelMoment moment, Vector3 position, bool unscaled)
		{
			RequestShake(moment.Shake);
			if (moment.Hitstop > 0f)
				timeManager.HitStop(moment.Hitstop, moment.HitstopScale);
			EmitShards(unscaled ? unscaledShards : shards, position, moment, moment.Color);
			EmitRing(unscaled ? unscaledRings : rings, position, moment, moment.Color);
		}

		private void RequestSmallShake(float amplitude)
		{
			if (amplitude <= 0f || Time.unscaledTime < nextSmallShakeTime)
				return;
			nextSmallShakeTime = Time.unscaledTime + Config.SmallShakeInterval;
			RequestShake(amplitude);
		}

		private void RequestShake(float amplitude)
		{
			float allowed = Mathf.Min(amplitude, Config.ShakeCap - shakeEnergy);
			if (allowed <= 0f)
				return;
			shakeEnergy += allowed;
			cameraManager.GenerateCameraImpulse(allowed);
		}

		private bool TakeBurst()
		{
			if (Time.unscaledTime >= burstWindowEnd)
			{
				burstWindowEnd = Time.unscaledTime + 1f;
				burstCount = 0;
			}
			burstCount++;
			return burstCount <= Config.BurstsPerSecond;
		}

		private Color HitColor(int equipmentId)
		{
			SurvivalFeelConfig config = Config;
			for (int i = 0; i < config.WeaponHitColors.Count; i++)
				if (config.WeaponHitColors[i].EquipmentId == equipmentId)
					return config.WeaponHitColors[i].Color;
			return config.DefaultHitColor;
		}

		private void EmitShards(ParticleSystem system, Vector3 position, FeelMoment moment, Color color)
		{
			if (moment.Shards <= 0)
				return;
			ParticleSystem.EmitParams emitParams = new()
			{
				startLifetime = moment.ShardLife,
				startColor = color,
				applyShapeToPosition = false,
			};
			SurvivalFeelConfig config = Config;
			Vector3 origin = position + Vector3.up * config.ImpactHeight;
			float burstTurn = Random.value;
			for (int i = 0; i < moment.Shards; i++)
			{
				Vector3 direction = ShardSpread.Direction(i, moment.Shards, burstTurn, Random.value * config.ShardAngleJitter, Random.value * config.ShardLift);
				emitParams.position = origin + direction * (config.ShardOriginSpread * Random.value);
				emitParams.velocity = direction * (moment.ShardSpeed * Random.Range(0.5f, 1f));
				emitParams.startSize = moment.ShardSize * Random.Range(0.6f, 1.2f);
				system.Emit(emitParams, 1);
			}
		}

		private void EmitRing(ParticleSystem system, Vector3 position, FeelMoment moment, Color color)
		{
			if (moment.RingSize <= 0f)
				return;
			ParticleSystem.EmitParams emitParams = new()
			{
				position = position + Vector3.up * 0.08f,
				velocity = Vector3.zero,
				startSize = moment.RingSize,
				startLifetime = moment.RingLife,
				startColor = color,
				applyShapeToPosition = false,
			};
			system.Emit(emitParams, 1);
		}
	}
}
