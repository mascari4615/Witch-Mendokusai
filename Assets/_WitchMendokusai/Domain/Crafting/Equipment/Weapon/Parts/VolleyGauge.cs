using System.Collections;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 알리사 일제 사격. 무기 탄의 적중 (GaugeOnHit) 이 게이지를 채우고, 다 차면 무기를 잔뜩 꺼내 바라보는 쪽에 부채꼴로 포화
	// 판정 VolleyGaugeRules. 카드는 스탯으로 켬 (장전, 나눠 쏘기, 집중, 끝 폭발, 무기 수, 유도, 빛줄기, 판 안 누적)
	public class VolleyGauge : SkillComponent
	{
		public static VolleyGauge Current { get; private set; }

		[Header("Gauge")]
		[SerializeField, Min(1f)] private float gaugeSize = 120f;
		[SerializeField, Min(0f)] private float normalCharge = 1f;
		[SerializeField, Min(0f), Tooltip("정예와 보스 적중 하나의 충전")] private float strongCharge = 3f;
		[SerializeField, Min(0f)] private float minIntervalSeconds = 6f;

		[Header("Burst")]
		[SerializeField] private GameObject shotPrefab;
		[SerializeField, Tooltip("유도 카드가 섞는 탄. 맞을 적을 향해 나감")] private GameObject homingShotPrefab;
		[SerializeField, Range(0, 100)] private int homingPercent = 25;
		[SerializeField] private GameObject beamPrefab;
		[SerializeField, Min(0)] private int beamCount = 2;
		[SerializeField] private GameObject blastPrefab;
		[SerializeField, Min(0f), Tooltip("포화가 끝난 자리. 앞쪽 거리")] private float blastDistance = 6f;
		[SerializeField, Min(0)] private int baseWeapons = 8;
		[SerializeField, Min(1)] private int shotsPerWeapon = 2;
		[SerializeField] private float fanDegrees = 50f;
		[SerializeField] private float focusFanDegrees = 20f;
		[SerializeField, Min(0f)] private float burstSeconds = 1f;
		[SerializeField, Min(0f), Tooltip("연속 포화의 두 번째까지 쉬는 시간")] private float splitGapSeconds = 0.4f;
		[SerializeField, Min(0f), Tooltip("포화 탄이 나오는 자리의 옆 폭")] private float weaponSpread = 1.2f;
		[SerializeField, Range(0, 200)] private int splitPercent = 60;
		[SerializeField, Range(0, 300)] private int focusPercent = 140;
		[SerializeField] private string readyText = "일제 사격!";
		[SerializeField, Tooltip("꺼낸 무기와 줄기 그림. 다 나온 뒤 첫 발")] private VolleyArsenal arsenal;

		[Header("Stat")]
		[SerializeField] private UnitStatType chargePercentStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType bonusWeaponsStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType splitStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType focusStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType blastStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType homingStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType beamStat = UnitStatType.NONE;
		[SerializeField, Tooltip("판 안 누적 피해 퍼센트 (무기고 마무리가 레벨업마다 올림)")] private UnitStatType growthPercentStat = UnitStatType.NONE;
		[SerializeField, Tooltip("포화 탄이 더하는 고정 피해. 빛탄 투자가 이어짐")] private UnitStatType damageBonusStat = UnitStatType.NONE;

		private float gauge;
		private float lastFireTime;
		private bool firing;
		private SkillContext context;

		private PlayerProvider playerProvider;
		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager)
		{
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
		}

		public float Fill01 => gauge / gaugeSize;

		private UnitStat PlayerStat => playerProvider.Current.UnitStat;

		public override void InitContext(SkillObject skillObject) => context = skillObject.Context;

		private void OnEnable()
		{
			Current = this;
			gauge = 0f;
			firing = false;
			lastFireTime = Time.time;
		}

		private void OnDisable()
		{
			if (Current == this)
				Current = null;
		}

		public void OnWeaponHit(UnitObject victim)
		{
			bool isStrongFoe = victim is MonsterObject monster && (monster.IsElite || monster.UnitData.Type == MonsterType.Boss);
			gauge = VolleyGaugeRules.Add(gauge, VolleyGaugeRules.Charge(isStrongFoe, normalCharge, strongCharge, PlayerStat[chargePercentStat]), gaugeSize);
			if (firing == false && VolleyGaugeRules.CanFire(gauge, gaugeSize, Time.time - lastFireTime, minIntervalSeconds))
				StartCoroutine(Fire());
		}

		private IEnumerator Fire()
		{
			firing = true;
			gauge = 0f;
			if (UIManager.TryGetExistingInstance(out UIManager uiManager))
				uiManager.PopText(readyText, TextType.Normal, playerProvider.Current.transform.position);

			bool split = PlayerStat[splitStat] > 0;
			bool focus = PlayerStat[focusStat] > 0;
			int percent = VolleyGaugeRules.DamagePercent(PlayerStat[growthPercentStat], split, splitPercent, focus, focusPercent);
			int waves = split ? 2 : 1;
			// 무기를 꺼내는 길이는 화력 주기에 안 셈 (첫 발만 늦음). 나눠 쏘기의 두 번째는 쉬는 시간에 꺼냄
			float raiseDelay = 0f;
			for (int wave = 0; wave < waves; wave++)
			{
				float gap = wave == 0 ? 0f : splitGapSeconds;
				float wait = Mathf.Max(gap, arsenal.WindupSeconds);
				raiseDelay += wait - gap;
				int count = VolleyGaugeRules.ShotCount(baseWeapons, PlayerStat[bonusWeaponsStat], shotsPerWeapon);
				if (count > 0)
					arsenal.Raise(playerProvider.Current.transform, AimDirection, count);
				yield return new WaitForSeconds(wait);
				yield return Burst(count, focus ? focusFanDegrees : fanDegrees, percent);
			}
			if (PlayerStat[blastStat] > 0)
				SpawnBlast();
			lastFireTime = Time.time - raiseDelay;
			firing = false;
		}

		private IEnumerator Burst(int count, float fan, int percent)
		{
			bool homing = PlayerStat[homingStat] > 0;
			int beams = PlayerStat[beamStat] > 0 ? beamCount : 0;
			float step = count > 0 ? burstSeconds / count : 0f;
			for (int i = 0; i < count; i++)
			{
				GameObject prefab = homing && Random.Range(0, 100) < homingPercent ? homingShotPrefab : shotPrefab;
				arsenal.Fire(i, SpawnShot(prefab, VolleyGaugeRules.FanAngle(i, count, fan), percent));
				if (i < beams)
					SpawnShot(beamPrefab, VolleyGaugeRules.FanAngle(i, beams, fan), percent);
				if (step > 0f)
					yield return new WaitForSeconds(step);
			}
		}

		private Vector3 AimDirection()
		{
			Vector3 aim = playerProvider.Current.AimDirection;
			aim.y = 0f;
			return aim.sqrMagnitude < 0.0001f ? playerProvider.Current.transform.forward : aim.normalized;
		}

		private GameObject SpawnShot(GameObject prefab, float angle, int percent)
		{
			Transform player = playerProvider.Current.transform;
			Vector3 aim = AimDirection();
			Vector3 direction = Quaternion.Euler(0f, angle, 0f) * aim;
			Vector3 side = Vector3.Cross(Vector3.up, aim) * Random.Range(-weaponSpread, weaponSpread);
			GameObject spawned = SkillSpawn.Spawn(objectPoolManager, prefab, player.position + side, Quaternion.LookRotation(direction, Vector3.up), context, PlayerStat[damageBonusStat]);
			// 유도 탄은 판정 초기화 때 가까운 적을 향해 방향을 정함. 덮어쓰지 않음
			if (prefab != homingShotPrefab && spawned.TryGetComponent(out BulletMovement bulletMovement))
				bulletMovement.SetMoveDirection(direction);
			if (spawned.TryGetComponent(out ScaledDamage scaledDamage))
				scaledDamage.SetPercent(percent);
			spawned.SetActive(true);
			return spawned;
		}

		private void SpawnBlast()
		{
			Vector3 position = playerProvider.Current.transform.position + AimDirection() * blastDistance;
			SkillSpawn.Spawn(objectPoolManager, blastPrefab, position, Quaternion.identity, context, 0).SetActive(true);
		}
	}
}
