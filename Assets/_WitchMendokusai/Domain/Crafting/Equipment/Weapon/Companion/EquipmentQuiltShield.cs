using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 한 번 막는 방패. 있는 동안 피해를 막고 버프, 깨질 때 주변을 밀어냄, 맞으면 재충전 쿨이 앞당겨짐
	public class EquipmentQuiltShield : SkillComponent, IDamageAbsorber
	{
		private static readonly int ALPHA_ID = Shader.PropertyToID("_Alpha");

		[SerializeField] private GameObject burstPrefab;
		[SerializeField] private Renderer shieldRing;
		[SerializeField, Min(1)] private int maxCharges = 1;
		[SerializeField, Min(0.1f)] private float rechargeCooldown = 20f;
		// 실제로 피해를 입을 때 재충전이 앞당겨지는 시간
		[SerializeField, Min(0f)] private float advanceOnHit = 4f;
		// 깨진 직후 짧은 무적. 한 번에 여러 겹이 깨지지 않게
		[SerializeField, Min(0f)] private float breakInvincibleSeconds = 0.7f;
		[SerializeField, Range(0f, 1f)] private float brokenRingAlpha = 0.1f;
		[SerializeField, Range(0f, 1f)] private float cooldownReductionPerStack = 0.15f;
		[SerializeField, Range(0.01f, 1f)] private float minCooldownRatio = 0.25f;
		[SerializeField] private UnitStatType buffStat = UnitStatType.DAMAGE_BONUS;
		[SerializeField] private int buffAmountPerCharge = 15;

		private SkillObject owner;
		private PlayerObject subscribedPlayer;
		private ShieldState state;
		private MaterialPropertyBlock properties;
		private int appliedBuff;

		private PlayerProvider playerProvider;
		private ObjectPoolManager objectPoolManager;

		public ShieldState State => state;

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager)
		{
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
		}

		public override void InitContext(SkillObject skillObject)
		{
			owner = skillObject;
			state = new ShieldState(maxCharges, rechargeCooldown);
			appliedBuff = 0;
		}

		private void OnEnable()
		{
			properties ??= new MaterialPropertyBlock();
			state ??= new ShieldState(maxCharges, rechargeCooldown);
			subscribedPlayer = playerProvider.CurrentObject;
			subscribedPlayer.AddAbsorber(this);
			subscribedPlayer.Health.OnTakeDamage += HandleDamageTaken;
			RefreshBuff();
		}

		private void OnDisable()
		{
			if (subscribedPlayer == null)
				return;
			subscribedPlayer.RemoveAbsorber(this);
			subscribedPlayer.Health.OnTakeDamage -= HandleDamageTaken;
			SetBuff(0);
			subscribedPlayer = null;
		}

		private void Update()
		{
			transform.position = playerProvider.Current.transform.position;
			state.Tick(Time.deltaTime * CompanionTargeting.CooldownRate(playerProvider.Current.UnitStat, cooldownReductionPerStack, minCooldownRatio));
			RefreshBuff();
			if (shieldRing != null)
			{
				properties.SetFloat(ALPHA_ID, state.HasShield ? 1f : brokenRingAlpha);
				shieldRing.SetPropertyBlock(properties);
			}
		}

		public bool TryAbsorb(DamageInfo damageInfo)
		{
			if (state.TryAbsorb() == false)
				return false;

			subscribedPlayer.GrantInvincibility(breakInvincibleSeconds);
			SpawnBurst();
			RefreshBuff();
			return true;
		}

		// 방패가 없을 때 실제로 맞으면 재충전이 빨라짐
		private void HandleDamageTaken(DamageInfo damageInfo)
		{
			if (damageInfo.type != DamageType.StatusTick)
				state.Advance(advanceOnHit);
		}

		private void SpawnBurst()
		{
			if (burstPrefab == null)
				return;
			GameObject burst = objectPoolManager.Spawn(burstPrefab, subscribedPlayer.transform.position, Quaternion.identity);
			if (burst.TryGetComponent(out SkillObject skillObject))
				skillObject.InitAttack(owner.Context);
			burst.SetActive(true);
		}

		private void RefreshBuff() => SetBuff(state.Charges * buffAmountPerCharge);

		private void SetBuff(int target)
		{
			if (subscribedPlayer == null || target == appliedBuff)
				return;
			subscribedPlayer.UnitStat[buffStat] += target - appliedBuff;
			appliedBuff = target;
		}
	}
}
