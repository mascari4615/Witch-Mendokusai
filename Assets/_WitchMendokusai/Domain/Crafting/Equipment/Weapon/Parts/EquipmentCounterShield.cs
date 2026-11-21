using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 피격 반격, 재사용 대기 중 고리 흐림
	public class EquipmentCounterShield : SkillComponent, IEquipmentCooldown
	{
		private static readonly int ALPHA_ID = Shader.PropertyToID("_Alpha");

		[SerializeField] private GameObject counterPrefab;
		[SerializeField] private Renderer readyRing;
		[SerializeField, Min(0.01f)] private float baseCooldown = 2.5f;
		[SerializeField, Range(0f, 1f)] private float cooldownReductionPerStack = 0.15f;
		[SerializeField, Range(0.01f, 1f)] private float minCooldownRatio = 0.25f;
		[SerializeField, Range(0f, 1f)] private float coolingRingAlpha = 0.25f;
		[SerializeField, Min(0f)] private float scalePerStack = 0.1f;

		[Header("Stat")]
		[SerializeField] private UnitStatType cooldownStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType damageBonusStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType scaleBonusStat = UnitStatType.NONE;

		private SkillObject owner;
		private PlayerObject subscribedPlayer;
		private MaterialPropertyBlock properties;
		private float readyTime;
		private float readyInterval;
		// 스킬창 장비 칸의 대기 표시용 (EquipmentCooldowns). 맞기 전까지는 늘 준비됨
		public CooldownState Cooldown => CooldownState.FromReadyTime(readyTime, readyInterval);

		private PlayerProvider playerProvider;
		private ObjectPoolManager objectPoolManager;

		public int CounterCount { get; private set; }

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager)
		{
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
		}

		private UnitStat PlayerStat => playerProvider.Current.UnitStat;

		public override void InitContext(SkillObject skillObject)
		{
			owner = skillObject;
			readyTime = 0f;
			CounterCount = 0;
		}

		private void OnEnable()
		{
			properties ??= new MaterialPropertyBlock();
			subscribedPlayer = playerProvider.CurrentObject;
			subscribedPlayer.Health.OnTakeDamage += HandlePlayerDamaged;
			EquipmentCooldowns.Register(owner.Context.UsedEquipment, this);
		}

		private void OnDisable()
		{
			subscribedPlayer.Health.OnTakeDamage -= HandlePlayerDamaged;
			EquipmentCooldowns.Unregister(owner.Context.UsedEquipment, this);
		}

		private void Update()
		{
			transform.position = playerProvider.Current.transform.position;
			properties.SetFloat(ALPHA_ID, Time.time >= readyTime ? 1f : coolingRingAlpha);
			readyRing.SetPropertyBlock(properties);
		}

		private void HandlePlayerDamaged(DamageInfo damageInfo)
		{
			if (Time.time < readyTime)
				return;

			readyInterval = AttackCadence.WeaponInterval(baseCooldown, PlayerStat[cooldownStat], PlayerStat[UnitStatType.ATTACK_SPEED_BONUS],
				cooldownReductionPerStack, minCooldownRatio, PlayerStat[UnitStatType.COOLTIME_BONUS]);
			readyTime = Time.time + readyInterval;
			CounterCount++;

			GameObject counter = objectPoolManager.Spawn(counterPrefab, subscribedPlayer.transform.position, Quaternion.identity);
			if (counter.TryGetComponent(out SkillObject skillObject))
				skillObject.InitAttack(owner.Context);
			counter.transform.localScale *= 1f + PlayerStat[scaleBonusStat] * scalePerStack;
			counter.GetComponentInChildren<DamagingObject>(true).SetDamageBonus(PlayerStat[damageBonusStat]);
			counter.SetActive(true);
		}
	}
}
