using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class EquipmentAura : SkillComponent, IEquipmentCooldown
	{
		[SerializeField] private DamagingObject damagingObject;
		[SerializeField] private Collider damageCollider;
		[SerializeField] private GameObject visual;

		[Header("Stat")]
		[SerializeField] private UnitStatType damageBonusStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType scaleBonusStat = UnitStatType.NONE;
		[SerializeField, Min(0f)] private float scalePerStack = 0.1f;

		[Header("Still Reward")]
		[SerializeField] private bool requireStill = false;
		[SerializeField, Min(0f)] private float stillDelay = 0.4f;
		[SerializeField, Min(0f)] private float chargeSeconds = 4f;
		[SerializeField, Min(0)] private int fullChargeDamageBonus = 12;
		[SerializeField, Range(0.05f, 1f)] private float emptyChargeScale = 0.5f;
		[SerializeField, Min(0f)] private float stillSpeedThreshold = 0.2f;

		private Vector3 lastPosition;
		private float stillSeconds;
		private Vector3 baseScale;

		private PlayerProvider playerProvider;

		[Inject]
		public void Construct(PlayerProvider playerProvider)
		{
			this.playerProvider = playerProvider;
		}

		private UnitStat PlayerStat => playerProvider.Current.UnitStat;

		public float StillSeconds => stillSeconds;
		// 스킬창 장비 칸 표시용 (EquipmentCooldowns). 멈춰 서야 차는 오라만 등록, 상시 오라는 상시 장비로 보임
		public CooldownState Cooldown => CooldownState.FromCharge(AttackCadence.ChargeRatio(stillSeconds - stillDelay, chargeSeconds));
		private SkillObject owner;

		public override void InitContext(SkillObject skillObject)
		{
			owner = skillObject;
			stillSeconds = 0f;
		}

		private void Awake()
		{
			baseScale = transform.localScale;
		}

		private void OnEnable()
		{
			lastPosition = playerProvider.Current.transform.position;
			stillSeconds = 0f;
			if (requireStill)
				EquipmentCooldowns.Register(owner.Context.UsedEquipment, this);
			Apply();
		}

		private void OnDisable()
		{
			EquipmentCooldowns.Unregister(owner.Context.UsedEquipment, this);
		}

		private void Update()
		{
			Vector3 position = playerProvider.Current.transform.position;
			transform.position = position;

			if (requireStill && Time.deltaTime > 0f)
			{
				Vector3 delta = position - lastPosition;
				delta.y = 0f;
				bool isStill = delta.magnitude / Time.deltaTime <= stillSpeedThreshold;
				stillSeconds = isStill ? stillSeconds + Time.deltaTime : 0f;
			}
			lastPosition = position;
			Apply();
		}

		private void Apply()
		{
			int damageBonus = damageBonusStat != UnitStatType.NONE ? PlayerStat[damageBonusStat] : 0;
			float scale = 1f + (scaleBonusStat != UnitStatType.NONE ? PlayerStat[scaleBonusStat] : 0) * scalePerStack;
			scale *= PlayerStat.PercentMultiplier(UnitStatType.PROJECTILE_SCALE_BONUS);

			if (requireStill)
			{
				float charged = stillSeconds - stillDelay;
				float ratio = AttackCadence.ChargeRatio(charged, chargeSeconds);
				damageBonus += Mathf.RoundToInt(fullChargeDamageBonus * ratio);
				scale *= Mathf.Lerp(emptyChargeScale, 1f, ratio);
				SetEffectActive(charged > 0f);
			}

			damagingObject.SetDamageBonus(damageBonus);
			transform.localScale = baseScale * scale;
		}

		private void SetEffectActive(bool isActive)
		{
			if (damageCollider != null && damageCollider.enabled != isActive)
				damageCollider.enabled = isActive;
			if (visual != null && visual.activeSelf != isActive)
				visual.SetActive(isActive);
		}
	}
}
