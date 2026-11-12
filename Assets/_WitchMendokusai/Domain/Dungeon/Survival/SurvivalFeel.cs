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
		private ParticleSystem freezeMarkers;
		private ParticleSystem.Particle[] freezeParticles = System.Array.Empty<ParticleSystem.Particle>();
		private readonly Plane[] freezePlanes = new Plane[6];
		private readonly List<MonsterObject> elites = new();

		private float shakeEnergy;
		private float nextSmallShakeTime;
		private float nextKillHitstopTime;
		private float nextAuraTime;
		private float nextEliteRingTime;
		private float burstWindowEnd;
		private int burstCount;
		private float expSoundWindowEnd;
		private UnityEngine.UI.Image hitFlashImage;
		private int expSoundCount;

		private SurvivalFeelConfig Config => resourceManager.SurvivalFeel;

		private void OnEnable() => UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += RenderFreezeMarkers;

		private void OnDisable()
		{
			UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= RenderFreezeMarkers;
			if (freezeMarkers != null)
				freezeMarkers.Clear();
		}

		[Inject]
		public void Construct(ResourceManager resourceManager, CameraManager cameraManager, TimeManager timeManager, GameEventManager gameEventManager, PlayerProvider playerProvider)
		{
			this.resourceManager = resourceManager;
			this.cameraManager = cameraManager;
			this.timeManager = timeManager;
			this.gameEventManager = gameEventManager;
			this.playerProvider = playerProvider;
			Instance = this;
			BuildSystems();
			gameObject.AddComponent<SurvivalLowHealthVignette>().Init(resourceManager, playerProvider);
			gameEventManager.RegisterCallback(GameEventType.OnLevelUp, OnLevelUp);
			gameEventManager.RegisterCallback(GameEventType.OnPlayerHit, OnPlayerHit);
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
			gameEventManager?.UnregisterCallback(GameEventType.OnLevelUp, OnLevelUp);
			gameEventManager?.UnregisterCallback(GameEventType.OnPlayerHit, OnPlayerHit);
		}

		private void BuildSystems()
		{
			shards = BuildSystem("Shards", Config.ShardMaterial, false, ParticleSystemRenderMode.Stretch);
			rings = BuildSystem("Rings", Config.RingMaterial, false, ParticleSystemRenderMode.HorizontalBillboard);
			unscaledShards = BuildSystem("UnscaledShards", Config.ShardMaterial, true, ParticleSystemRenderMode.Stretch);
			unscaledRings = BuildSystem("UnscaledRings", Config.RingMaterial, true, ParticleSystemRenderMode.HorizontalBillboard);
			freezeMarkers = BuildSystem("Freeze markers", Config.RingMaterial, false, ParticleSystemRenderMode.HorizontalBillboard);
			ParticleSystem.SizeOverLifetimeModule size = freezeMarkers.sizeOverLifetime;
			size.enabled = false;
			ParticleSystem.ColorOverLifetimeModule color = freezeMarkers.colorOverLifetime;
			color.enabled = false;
		}

		private void RenderFreezeMarkers(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
		{
			if (freezeMarkers == null || camera.cameraType != CameraType.Game || camera.CompareTag("MainCamera") == false)
				return;
			if (EnemyFreezeState.NormalRemainingSeconds <= 0f && EnemyFreezeState.BossRemainingSeconds <= 0f)
			{
				freezeMarkers.SetParticles(freezeParticles, 0);
				return;
			}
			List<GameObject> monsters = ObjectBufferManager.GetObjects(ObjectType.Monster);
			if (freezeParticles.Length < monsters.Count)
			{
				System.Array.Resize(ref freezeParticles, Mathf.NextPowerOfTwo(monsters.Count));
				ParticleSystem.MainModule main = freezeMarkers.main;
				main.maxParticles = Mathf.Max(main.maxParticles, freezeParticles.Length);
			}
			GeometryUtility.CalculateFrustumPlanes(camera, freezePlanes);
			SurvivalFeelConfig config = Config;
			int count = 0;
			foreach (GameObject monsterObject in monsters)
			{
				if (monsterObject == null || monsterObject.TryGetComponent(out MonsterObject monster) == false || monster.isActiveAndEnabled == false || monster.IsActionSuspended == false)
					continue;
				SpriteRenderer renderer = monster.SpriteRenderer;
				if (renderer == null || renderer.enabled == false || renderer.gameObject.activeInHierarchy == false || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0 || GeometryUtility.TestPlanesAABB(freezePlanes, renderer.bounds) == false)
					continue;
				float scale = Mathf.Max(Mathf.Abs(monster.transform.lossyScale.x), Mathf.Abs(monster.transform.lossyScale.z));
				freezeParticles[count++] = new ParticleSystem.Particle
				{
					position = monster.transform.position + Vector3.up * config.FreezeMarkerHeight,
					velocity = Vector3.zero,
					startColor = config.FreezeStatusColor,
					startSize = config.FreezeMarkerSize * scale,
					startLifetime = config.FreezeMarkerRefreshLife,
					remainingLifetime = config.FreezeMarkerRefreshLife
				};
			}
			freezeMarkers.SetParticles(freezeParticles, count);
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

		public bool TakeExpSound()
		{
			SurvivalFeelConfig config = Config;
			if (Time.unscaledTime >= expSoundWindowEnd)
			{
				expSoundWindowEnd = Time.unscaledTime + config.ExpSoundWindow;
				expSoundCount = 0;
			}
			expSoundCount++;
			return expSoundCount <= config.ExpSoundsPerWindow;
		}

		public void OnWeaponBurst(Vector3 position, FeelMoment moment)
		{
			RequestSmallShake(moment.Shake);
			if (TakeBurst() == false)
				return;
			EmitShards(shards, position, moment, moment.Color);
			EmitRing(rings, position, moment, moment.Color);
		}

		// 화면 전체 피격 효과 세기, 애니메이션 색 알파에 곱셈
		private void OnPlayerHit()
		{
			if (hitFlashImage == null)
				hitFlashImage = FindHitFlashImage();
			hitFlashImage.canvasRenderer.SetAlpha(Config.PlayerHitFlashStrength);
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
