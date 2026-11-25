using System.Collections;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 알리사 십자 수리검. 주기마다 머리 위에서 커져 (windup) 적이 몰린 자리로 떨어지고 (flight) 박히면 폭발. 간격과 배율 판정은 CrossShurikenRules
	// 닌자 단계의 십자 수리검과 최종 대형 십자가 같은 부품의 다른 값. 카드는 스탯으로 켬
	public class CrossShurikenThrower : SkillComponent
	{
		[Header("Throw")]
		[SerializeField, Tooltip("이 스탯을 받아야 던짐. NONE 이면 늘")] private UnitStatType gateStat = UnitStatType.NONE;
		[SerializeField, Min(0.1f)] private float intervalSeconds = 4f;
		[SerializeField, Min(0.1f), Tooltip("뇌격 카드의 간격")] private float heavyIntervalSeconds = 6.5f;
		[SerializeField, Range(0f, 1f)] private float reductionPerStack = 0.15f;
		[SerializeField, Range(0.01f, 1f)] private float minIntervalRatio = 0.25f;
		[SerializeField, Min(0f)] private float targetRange = 12f;
		[SerializeField, Min(0.1f)] private float clusterRadius = 3f;

		[Header("Motion")]
		[SerializeField, Tooltip("수리검 그림. 판정 없음")] private GameObject bodyPrefab;
		[SerializeField, Min(0.01f)] private float windupSeconds = 0.4f;
		[SerializeField, Min(0.01f)] private float flightSeconds = 0.22f;
		[SerializeField, Min(0f)] private float raiseHeight = 2.6f;
		[SerializeField] private float spinDegreesPerSecond = 900f;
		[SerializeField, Min(0f), Tooltip("박힌 뒤 그림이 남는 시간")] private float stuckSeconds = 0.35f;

		[Header("Blast")]
		[SerializeField] private GameObject blastPrefab;
		[SerializeField] private GameObject fieldPrefab;
		[SerializeField, Min(0f)] private float echoGapSeconds = 0.35f;
		[SerializeField, Range(0, 200)] private int echoPercent = 50;
		[SerializeField, Min(0f)] private float echoRadius = 0.7f;
		[SerializeField, Range(0, 400)] private int heavyPercent = 160;
		[SerializeField, Min(0f)] private float radiusPerStack = 0.1f;

		[Header("Stat")]
		[SerializeField] private UnitStatType heavyStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType fieldStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType echoStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType repeatPercentStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType radiusStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType damageBonusStat = UnitStatType.SIGNATURE_RANGED_DAMAGE_BONUS;

		private SkillContext context;
		private GameObject body;

		private PlayerProvider playerProvider;
		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager)
		{
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
		}

		private UnitStat PlayerStat => playerProvider.Current.UnitStat;

		public override void InitContext(SkillObject skillObject) => context = skillObject.Context;

		private int Stat(UnitStatType stat) => stat == UnitStatType.NONE ? 0 : PlayerStat[stat];

		private void OnEnable() => StartCoroutine(Loop());

		private void OnDisable()
		{
			StopAllCoroutines();
			if (body != null)
				Destroy(body);
		}

		private IEnumerator Loop()
		{
			while (true)
			{
				if (gateStat != UnitStatType.NONE && PlayerStat[gateStat] <= 0)
				{
					yield return null;
					continue;
				}
				// 커지고 날아가는 시간은 주기 안에서 씀 (간격이 카드 설명과 같게)
				float interval = CrossShurikenRules.ThrowInterval(intervalSeconds, heavyIntervalSeconds, Stat(heavyStat) > 0, 0, PlayerStat[UnitStatType.ATTACK_SPEED_BONUS], reductionPerStack, minIntervalRatio, PlayerStat[UnitStatType.COOLTIME_BONUS]);
				yield return new WaitForSeconds(Mathf.Max(0f, interval - windupSeconds - flightSeconds));
				yield return Throw();
			}
		}

		private IEnumerator Throw()
		{
			if (body == null)
				body = Instantiate(bodyPrefab);
			body.SetActive(true);
			Vector3 fullScale = bodyPrefab.transform.localScale * RadiusMultiplier;
			Vector3 landing = Vector3.zero;
			for (float elapsed = 0f; elapsed < windupSeconds; elapsed += Time.deltaTime)
			{
				float grown = elapsed / windupSeconds;
				Vector3 above = playerProvider.Current.transform.position + Vector3.up * raiseHeight;
				Place(above, fullScale * Mathf.Sin(grown * Mathf.PI * 0.5f));
				landing = LandingPoint();
				yield return null;
			}
			CombatSfx.Play(CombatSfxEvent.WeaponThrow, body.transform.position);
			Vector3 start = body.transform.position;
			for (float elapsed = 0f; elapsed < flightSeconds; elapsed += Time.deltaTime)
			{
				float progress = elapsed / flightSeconds;
				Place(Vector3.Lerp(start, landing, progress * progress), fullScale);
				yield return null;
			}
			body.transform.position = landing;
			StartCoroutine(Explode(landing));
			StartCoroutine(HideBody());
		}

		private void Place(Vector3 position, Vector3 scale)
		{
			body.transform.position = position;
			body.transform.localScale = scale;
			body.transform.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
		}

		private IEnumerator HideBody()
		{
			yield return new WaitForSeconds(Mathf.Min(stuckSeconds, Mathf.Max(0f, intervalSeconds * minIntervalRatio - windupSeconds - flightSeconds)));
			body.SetActive(false);
		}

		private float RadiusMultiplier => 1f + Stat(radiusStat) * radiusPerStack;

		// 적이 몰린 자리. 없으면 바라보는 쪽
		private Vector3 LandingPoint()
		{
			Vector3 origin = playerProvider.Current.transform.position;
			if (DensityTargeting.TryFindDensestMonsters(origin, targetRange, clusterRadius, 1, out Vector3 point, out int _))
				return new Vector3(point.x, origin.y, point.z);
			Vector3 aim = playerProvider.Current.AimDirection;
			aim.y = 0f;
			return origin + aim.normalized * (targetRange * 0.5f);
		}

		private IEnumerator Explode(Vector3 position)
		{
			int blastPercent = CrossShurikenRules.BlastPercent(Stat(heavyStat) > 0, heavyPercent, Stat(repeatPercentStat));
			int count = CrossShurikenRules.BlastCount(Stat(echoStat) > 0);
			if (fieldPrefab != null && Stat(fieldStat) > 0)
				SkillSpawn.Spawn(objectPoolManager, fieldPrefab, position, Quaternion.identity, context, 0).SetActive(true);
			float waited = 0f;
			for (int i = 0; i < count; i++)
			{
				CrossBlast blast = CrossShurikenRules.Blast(i, blastPercent, echoGapSeconds, echoPercent, echoRadius);
				if (blast.DelaySeconds > waited)
					yield return new WaitForSeconds(blast.DelaySeconds - waited);
				waited = blast.DelaySeconds;
				GameObject spawned = SkillSpawn.Spawn(objectPoolManager, blastPrefab, position, Quaternion.identity, context, Stat(damageBonusStat));
				spawned.transform.localScale *= blast.RadiusMultiplier * RadiusMultiplier;
				if (spawned.TryGetComponent(out ScaledDamage scaledDamage))
					scaledDamage.SetPercent(blast.DamagePercent);
				spawned.SetActive(true);
			}
		}
	}
}
