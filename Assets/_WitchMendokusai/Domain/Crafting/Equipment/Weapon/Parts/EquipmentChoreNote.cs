using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 움직인 거리만큼 쌓였다가 멈추는 순간 터지는 구역
	public class EquipmentChoreNote : SkillComponent
	{
		[SerializeField] private GameObject burstPrefab;
		[SerializeField] private Transform chargeRing;

		[Header("Charge")]
		[SerializeField, Min(0.01f)] private float metersToFull = 12f;
		[SerializeField, Range(0f, 1f)] private float metersReductionPerStack = 0.1f;
		[SerializeField, Range(0.01f, 1f)] private float minMetersRatio = 0.4f;
		[SerializeField, Range(0.01f, 1f)] private float minChargeToBurst = 0.25f;
		[SerializeField, Min(0f)] private float stillDelay = 0.15f;
		[SerializeField, Min(0f)] private float stillSpeedThreshold = 0.2f;

		[Header("Burst")]
		[SerializeField, Min(0)] private int fullChargeDamageBonus = 30;
		[SerializeField, Min(0.01f)] private float emptyChargeScale = 0.6f;
		[SerializeField, Min(0.01f)] private float fullChargeScale = 1.4f;
		[SerializeField, Min(0f)] private float scalePerStack = 0.1f;

		[Header("Stat")]
		[SerializeField] private UnitStatType chargeSpeedStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType damageBonusStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType scaleBonusStat = UnitStatType.NONE;

		private SkillObject owner;
		private Vector3 lastPosition;
		private float charge;
		private float stillSeconds;
		private Vector3 ringBaseScale;

		private PlayerProvider playerProvider;
		private ObjectPoolManager objectPoolManager;

		public float Charge => charge;
		public int BurstCount { get; private set; }

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
			charge = 0f;
			stillSeconds = 0f;
			BurstCount = 0;
		}

		private void Awake()
		{
			ringBaseScale = chargeRing.localScale;
		}

		private void OnEnable()
		{
			lastPosition = playerProvider.Current.transform.position;
		}

		private void Update()
		{
			Vector3 position = playerProvider.Current.transform.position;
			transform.position = position;
			Vector3 delta = position - lastPosition;
			delta.y = 0f;
			lastPosition = position;

			if (Time.deltaTime <= 0f)
				return;

			if (delta.magnitude / Time.deltaTime <= stillSpeedThreshold)
			{
				stillSeconds += Time.deltaTime;
				if (stillSeconds >= stillDelay && charge >= minChargeToBurst)
					Burst();
			}
			else
			{
				stillSeconds = 0f;
				float meters = metersToFull * Mathf.Max(minMetersRatio, 1f - PlayerStat[chargeSpeedStat] * metersReductionPerStack);
				charge = Mathf.Clamp01(charge + delta.magnitude / meters);
			}

			chargeRing.localScale = ringBaseScale * charge;
		}

		private void Burst()
		{
			GameObject burst = objectPoolManager.Spawn(burstPrefab, transform.position, Quaternion.identity);
			if (burst.TryGetComponent(out SkillObject skillObject))
				skillObject.InitAttack(owner.Context);
			burst.transform.localScale *= Mathf.Lerp(emptyChargeScale, fullChargeScale, charge) * (1f + PlayerStat[scaleBonusStat] * scalePerStack);
			int bonus = PlayerStat[damageBonusStat] + Mathf.RoundToInt(fullChargeDamageBonus * charge);
			burst.GetComponentInChildren<DamagingObject>(true).SetDamageBonus(bonus);
			burst.SetActive(true);
			charge = 0f;
			BurstCount++;
		}
	}
}
