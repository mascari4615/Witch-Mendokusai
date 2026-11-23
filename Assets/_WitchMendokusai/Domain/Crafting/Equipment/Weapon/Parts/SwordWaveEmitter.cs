using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 알리사 빛의 검. 같은 오브젝트의 베기 생성기 (Spawner) 가 한 번 벨 때마다 검기를 냄
	// 베기 간격이 문턱을 넘을 때마다 검기가 한 단계씩 바뀜 (1 두 갈래, 2 크기, 3 끝에서 터짐). 판정 AttackSpeedTierRules
	// 축복 카드: 검기 적중마다 잠깐 쿨감 (처치면 더 오래), 적중 수마다 수선 한 번, 주기마다 날개 검기 폭발
	[RequireComponent(typeof(Spawner))]
	public class SwordWaveEmitter : SkillComponent
	{
		public static SwordWaveEmitter Current { get; private set; }

		[Header("Wave")]
		[SerializeField] private GameObject wavePrefab;
		[SerializeField, Tooltip("단계 3 에서 검기 끝에 터지는 것")] private GameObject tipBurstPrefab;
		[SerializeField, Tooltip("검기 프리팹의 길이 (월드). 끝 폭발 자리 계산")] private float waveLength = 7f;
		[SerializeField, Tooltip("단계 1, 2, 3 의 베기 간격 문턱 (초)")] private float[] tierThresholds = { 0.95f, 0.65f, 0.45f };
		[SerializeField] private float splitDegrees = 12f;
		[SerializeField, Min(1f)] private float tierTwoScale = 1.3f;
		[SerializeField, Range(0, 200), Tooltip("검기 고정 피해 보너스는 베기 보너스의 이만큼")] private int bonusSharePercent = 50;
		[SerializeField, Min(0f)] private float lengthPerStack = 0.1f;
		[SerializeField, Min(0f)] private float widthPerStack = 0.1f;
		[SerializeField, Min(1f), Tooltip("천상 마무리: 최상 단계 검기 길이 배율 (화면 끝)")] private float screenEdgeMultiplier = 3.6f;

		[Header("Blessing")]
		[SerializeField] private UnitStatType hasteTargetStat = UnitStatType.COOLTIME_BONUS;
		[SerializeField, Min(0f)] private float hasteSeconds = 2f;
		[SerializeField, Min(0f)] private float hasteKillSeconds = 4f;
		[SerializeField, Min(0)] private int hasteCap = 15;
		[SerializeField, Min(1)] private int hitsPerMend = 200;
		[SerializeField, Min(0.1f)] private float wingsIntervalSeconds = 10f;
		[SerializeField, Min(1)] private int wingsCount = 8;
		[SerializeField, Min(0f), Tooltip("날개 검기 길이 (월드)")] private float wingsRadius = 4f;
		[SerializeField, Range(0, 500)] private int wingsDamagePercent = 200;

		[Header("Stat")]
		[SerializeField] private UnitStatType meleeDamageBonusStat = UnitStatType.SIGNATURE_MELEE_DAMAGE_BONUS;
		[SerializeField] private UnitStatType lengthStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType widthStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType screenEdgeStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType hasteStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType hasteKillStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType mendStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType wingsStat = UnitStatType.NONE;

		private Spawner spawner;
		private SkillContext context;
		private readonly List<float> hasteExpiries = new();
		private int appliedHaste;
		private int waveHits;
		private float nextWingsTime;

		private PlayerProvider playerProvider;
		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager)
		{
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
		}

		private UnitStat PlayerStat => playerProvider.Current.UnitStat;

		public int Tier => AttackSpeedTierRules.Tier(spawner.CurrentInterval, tierThresholds);

		public override void InitContext(SkillObject skillObject) => context = skillObject.Context;

		private void Awake() => spawner = GetComponent<Spawner>();

		private void OnEnable()
		{
			Current = this;
			spawner.Shot += HandleSlash;
			hasteExpiries.Clear();
			appliedHaste = 0;
			waveHits = 0;
			nextWingsTime = Time.time + wingsIntervalSeconds;
		}

		private void OnDisable()
		{
			spawner.Shot -= HandleSlash;
			if (Current == this)
				Current = null;
			if (appliedHaste != 0 && playerProvider.Current != null)
				PlayerStat[hasteTargetStat] -= appliedHaste;
			appliedHaste = 0;
		}

		private void Update()
		{
			UpdateHaste();
			if (PlayerStat[wingsStat] > 0 && Time.time >= nextWingsTime)
			{
				nextWingsTime = Time.time + wingsIntervalSeconds;
				SpawnWings();
			}
		}

		private void HandleSlash(GameObject slash, Vector3 direction)
		{
			if (direction == Vector3.zero)
				return;
			int tier = Tier;
			float scale = tier >= 2 ? tierTwoScale : 1f;
			float length = scale * AttackSpeedTierRules.LengthMultiplier(PlayerStat[lengthStat], lengthPerStack, PlayerStat[screenEdgeStat] > 0, screenEdgeMultiplier, tier, tierThresholds.Length);
			float width = scale * (1f + Mathf.Max(0, PlayerStat[widthStat]) * widthPerStack);
			Vector3 origin = playerProvider.Current.transform.position;
			if (tier >= 1)
			{
				SpawnWave(origin, Quaternion.Euler(0f, splitDegrees, 0f) * direction, length, width, tier);
				SpawnWave(origin, Quaternion.Euler(0f, -splitDegrees, 0f) * direction, length, width, tier);
			}
			else
				SpawnWave(origin, direction, length, width, tier);
		}

		private void SpawnWave(Vector3 origin, Vector3 direction, float length, float width, int tier)
		{
			GameObject wave = SpawnWaveObject(origin, direction, length, width, 100);
			if (tier >= tierThresholds.Length && tipBurstPrefab != null)
				SkillSpawn.Spawn(objectPoolManager, tipBurstPrefab, origin + direction * waveLength * wave.transform.localScale.z, Quaternion.identity, context, 0).SetActive(true);
		}

		private GameObject SpawnWaveObject(Vector3 origin, Vector3 direction, float length, float width, int damagePercent)
		{
			int bonus = PlayerStat[meleeDamageBonusStat] * bonusSharePercent / 100;
			GameObject wave = SkillSpawn.Spawn(objectPoolManager, wavePrefab, origin, Quaternion.LookRotation(direction, Vector3.up), context, bonus);
			Vector3 baseScale = wave.transform.localScale;
			wave.transform.localScale = new Vector3(baseScale.x * width, baseScale.y, baseScale.z * length);
			if (wave.TryGetComponent(out ScaledDamage scaledDamage))
				scaledDamage.SetPercent(damagePercent);
			wave.SetActive(true);
			return wave;
		}

		private void SpawnWings()
		{
			Vector3 origin = playerProvider.Current.transform.position;
			for (int i = 0; i < wingsCount; i++)
			{
				Vector3 direction = Quaternion.Euler(0f, 360f * i / wingsCount, 0f) * Vector3.forward;
				SpawnWaveObject(origin, direction, wingsRadius / waveLength, 1f, wingsDamagePercent);
			}
		}

		public void OnWaveHit(UnitObject victim)
		{
			if (PlayerStat[hasteStat] > 0)
			{
				float seconds = PlayerStat[hasteKillStat] > 0 && victim.IsAlive == false ? hasteKillSeconds : hasteSeconds;
				hasteExpiries.Add(Time.time + seconds);
			}
			if (PlayerStat[mendStat] > 0 && ++waveHits % hitsPerMend == 0 && MendRunner.Current != null)
				MendRunner.Current.MendNow();
		}

		// 살아 있는 가속 쌓임만큼 대상 스탯을 맞춤 (상한 hasteCap)
		private void UpdateHaste()
		{
			float now = Time.time;
			hasteExpiries.RemoveAll(expiry => expiry <= now);
			int live = AttackSpeedTierRules.ClampStacks(hasteExpiries.Count, hasteCap);
			if (live == appliedHaste)
				return;
			PlayerStat[hasteTargetStat] += live - appliedHaste;
			appliedHaste = live;
		}
	}
}
