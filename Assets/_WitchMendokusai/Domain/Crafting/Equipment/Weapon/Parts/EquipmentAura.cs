using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class EquipmentAura : SkillComponent
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

		public override void InitContext(SkillObject skillObject)
		{
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
			Apply();
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
