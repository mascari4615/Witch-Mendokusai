using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// ★ 이 파일의 좌표는 「판정 쪽」이다 (TASK-WM-214).
//   개척 판의 셈은 거의 전부 시뮬이고(Vector3 118 · Vector2Int 27 · Vector3Int 13),
//   엔진을 실제로 만지는 자리는 스무 곳 남짓((Vector3)transform.position 등)이다.
//   그래서 이 파일에서 Vector* 는 SDK 타입을 뜻하고, 엔진으로 나갈 때만 자동으로 변환된다.
//   반대로 엔진 값을 받아올 때는 캐스트가 필요하다 — 그 자리가 곧 경계다.
using Vector2 = WitchMendokusai.Numerics.Vector2;
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;

namespace WitchMendokusai
{
	/// <summary>
	/// 특수시공 개척(TD) 매치 오케스트레이터 — ArenaMatch 와 동형 셸(맵 생성 → 유닛 스폰(기존 풀, 자동 DI)
	/// → MatchCombatant/TacticDriver 부착 → TargetingSystem 등록 → TimeManager 틱으로 TowerDefenseCore 폴).
	/// 규칙 판단은 전부 순수 코어(TowerDefenseCore)에 있고 본 셸은 그 신호(TowerDefenseSignal)를 받아
	/// 스폰/자원차감/정리 같은 actuation 만 수행 — Arena 아키텍처의 "코어=브레인, 셸=손발" 원칙 그대로 재사용.
	/// 배치 UI/입력전략/게임모드진입/카메라는 별도 증분(본 셸은 매치 진행 자체만 담당).
	/// </summary>
	public partial class TowerDefenseMatch : MonoBehaviour
	{
		private const int DEFENDER_TEAM = 0; // 코어/타워/채집건물 소속 팀.
		private const int ATTACKER_TEAM = 1; // 웨이브 적 소속 팀.

		[field: Header("_" + nameof(TowerDefenseMatch))]
		[SerializeField] private TowerDefenseStageSO stage;
		[SerializeField] private Transform stageRoot;

		private ObjectPoolManager pool;
		private TimeManager timeManager;
		private TargetingSystem targeting;
		private TowerDefenseCore core;
		private MatchCombatant coreCombatant;
		private int nextCombatantId;
		private bool started;
		private bool ticking;
		private bool matchEndedFired;

		// ★ 몇 번째 판인가. 인형을 세우는 일은 한 프레임 쉬었다 이어지는데, 그 사이에 판이 통째로
		//   갈릴 수 있다(다시 시작). 그때 「판이 사라졌나」만 보면 *새 판이 이미 서 있어서* 검사를
		//   통과하고, 지난 판이 부른 인형이 새 판에 세워진다.
		//   실측: 아무것도 안 지은 무방비 판에 지난 판 영웅이 서서 세 웨이브를 막았고, 코어가 안 죽어
		//   승리도 패배도 없는 판이 됐다. 「사라졌나」가 아니라 **「그 판이 맞나」**를 물어야 한다.
		private int matchGeneration;
		private readonly List<ICombatant> registeredCombatants = new();
		private readonly List<TacticDriver> drivers = new();
		// 판을 *그리는* 층 — 바닥·암반·길 표시·표식. 규칙과 그림을 갈라둔다.
		private readonly TowerDefenseTerrainView terrainView = new();

		// ★ 코어도 자란다(사용자 지시: "코어 건물 자체의 레벨도 있어서 그것도 선택지 있으면 좋을듯").
		//   새 선택지 체계를 하나 더 만들지 않고 *이미 있는 드래프트 카드*를 코어 레벨업에 붙였다 —
		//   웨이브가 부르던 카드를 코어 성장이 부르게 바꾼 것뿐이다. 체계가 둘로 갈리면 같은 선택이
		//   두 곳에서 다른 규칙으로 살게 된다.
		//   성장 곡선은 스테이지가 정한다(Begin 에서 세운다) — 코드에 박아 두면 스테이지에서 아무리
		//   만져도 코어만 옛 속도로 자란다.
		private TowerDefenseBuildingProgress coreProgress;
		private TowerDefenseRing highlightedRing;

		private TowerDefenseMapLayout mapLayout;
		private TowerDefenseFlowField flowField;
		private ITacticNavigator flowNavigator;
		private Vector3 activeCorePosition;

		public event Action<TowerDefenseOutcome> MatchEnded = delegate { };

		/// <summary>
		/// 이번 판의 난이도 — 다음 판에도 유지된다(매번 다시 고르게 하면 그건 설정이 아니라 잔소리다).
		/// 판이 도는 중에 바꿔도 이미 시작한 판에는 안 걸린다(시작 조건이므로).
		/// </summary>
		public TowerDefenseDifficultyKind Difficulty { get; set; } = TowerDefenseDifficultyKind.Normal;

		private TowerDefenseDifficulty difficulty = TowerDefenseDifficulty.For(TowerDefenseDifficultyKind.Normal);

		/// <summary>
		/// 진행 중인 스테이지 데이터(읽기 전용) — 검증 하네스가 좌표·수치를 **정본에서 읽게** 한다.
		/// 하네스에 좌표를 박아두면 레이아웃을 옮기는 순간 검사가 조용히 무의미해진다(항상 거절만 확인).
		/// </summary>
		public TowerDefenseStageSO Stage => stage;

		/// <summary> 코어 참가자(진단용) — 적이 코어를 실제로 때리고 있는지 체력으로 확인한다. </summary>
		public MatchCombatant CoreCombatant => coreCombatant;

		/// <summary> 매치에 등록된 전 참가자(진단용) — 수비 유닛 생존 여부 확인. </summary>
		public IReadOnlyList<ICombatant> RegisteredCombatants => registeredCombatants;
		public TowerDefensePhase Phase => core != null ? core.Phase : TowerDefensePhase.Prepare;
		public TowerDefenseOutcome Outcome => core != null ? core.Outcome : TowerDefenseOutcome.InProgress;
		public float PrepareRemaining => core != null ? core.PrepareRemaining : 0f;

		/// <summary> 프로그래매틱 시작(런처/모드 진입용) — stage·stageRoot 주입 후 Begin. </summary>
		/// <summary> 의존 배선. 모드 컨트롤러 프리팹의 자식이라 씬 스코프가 못 보므로 컨트롤러가 Construct 에서 넘긴다 (2026-09-05, .Instance 제거) </summary>
		public void Construct(ObjectPoolManager objectPoolManager, TimeManager timeManagerDependency)
		{
			pool = objectPoolManager;
			timeManager = timeManagerDependency;
		}

		/// <summary>
		/// 신호를 받고 있는 중계(발전 인형) 수 — **컨트롤넷의 핵심 약속이 실제로 서는지**의 유일한 증거.
		/// 0 이면 사슬이 한 칸도 안 뻗은 것이고, 그러면 전기는 「코어 반경 안」이 전부다.
		/// </summary>
		public int FedRelayCount
		{
			get
			{
				int fed = 0;
				// 0 번은 코어(스스로 낸다) — 중계만 센다.
				for (int index = 1; index < powerGrid.Field.NodeCount; index++)
				{
					if (powerGrid.Field.IsFed(index))
						fed++;
				}
				return fed;
			}
		}

		/// <summary> 판 크기(칸) — 상한이 판에 비해 충분한지 함께 봐야 판단이 된다. </summary>
		public int MapCellCount => mapLayout != null ? mapLayout.Width * mapLayout.Length : 0;

		/// <summary>
		/// 내 건물 하나를 코어에서 *가장 먼* 것으로 골라 없앤다 — 검사 전용.
		///
		/// ★ 왜 필요한가: 「뚫린 자리가 다음 파도를 끌어당긴다」는 건물을 잃어야만 확인된다. 그런데
		///   하네스는 마수가 내 건물을 부술 때까지 기다릴 수밖에 없고, 그건 판마다 오거나 안 온다
		///   (적응 검사에서 이미 다섯 사이클을 그렇게 날렸다). 재는 쪽이 사건을 일으킬 수 있어야 한다.
		/// ★ 왜 가장 먼 것인가: 코어 바로 옆을 없애면 방향이 거의 안 바뀌어 「끌렸다」를 못 가른다.
		///   멀수록 각이 뚜렷해 참·거짓이 갈린다.
		/// ★ 없애는 방법은 마수가 부수는 것과 같은 문(오브젝트 소멸)이다 — 다른 문으로 들어가면
		///   *검사만 통과하는* 길이 생긴다.
		/// </summary>
		/// <summary> 이보다 가까운 것을 없애면 방향이 안 나온다 — 재는 의미가 없다. </summary>
		private const float MIN_VERIFY_LOSS_DISTANCE = 6f;

		/// <summary> 무대 루트 — 화면 표시가 로컬 좌표를 월드로 옮길 때 쓴다. </summary>
		public Transform StageRoot => stageRoot;

		/// <summary> 그 대상이 코어인가 — 화면이 「연구」 패널을 띄울지 정한다. </summary>
		public bool IsCore(MatchCombatant combatant) => combatant != null && combatant == coreCombatant;

		public int BuiltCount { get; private set; }
		public int LostCount { get; private set; }
		public int KilledCount { get; private set; }
		public int PeakEnemies { get; private set; }

		// ── 판 도중 저장 ──────────────────────────────────────────────────────────
		// ★ 「장면 통째」가 아니라 *다시 지을 수 있는 최소 정보*만 담는다 — 판은 씨앗에서 다시 태어나고
		//   내가 한 일은 「무엇을 어디에 세웠나」로 전부 적힌다. 그러면 프리팹이 바뀌어도 저장이 살아남는다.
		// ★ 걷고 있는 마수는 저장하지 않는다 — 되살리는 것보다 *다시 몰려오게* 두는 편이 규칙이 단순하고,
		//   불러온 직후의 짧은 숨돌릴 틈이 오히려 자연스럽다.

		/// <summary>
		/// 확인 도구 전용 — 값만 채운다. **배치 규칙은 우회하지 않는다**(보급·암반·점유 그대로).
		/// 값이 모자라 확인 자체를 못 하던 것들(전초기지·바깥 채집)을 라이브로 보기 위한 최소 통로.
		/// </summary>
		public void GrantForVerification(int resource, int essence)
		{
			if (core == null)
				return;

			core.AddResource(resource);
			core.AddEssence(essence);
		}
		public int CorePendingChoices => coreProgress.PendingChoices;

		/// <summary> 이번 판의 씨앗 — 같은 값이면 같은 판이 나온다(재현·신고용). 고정 판이면 0. </summary>
		/// <summary> 이번 판의 배치도 — 지도가 지형을 그리려면 이게 있어야 한다(읽기 전용). </summary>
		public TowerDefenseMapLayout MapLayout => mapLayout;

		public int MapSeed => mapLayout != null ? mapLayout.Seed : 0;

		private int? nextMatchSeed;

		/// <summary>
		/// 다음 판에 쓸 씨앗을 지정한다 — 남이 준 씨앗으로 *같은 땅*을 여는 유일한 문.
		/// 다음 판 하나에만 걸린다(계속 걸리면 그건 공유가 아니라 고정이다).
		/// </summary>
		public void SetNextMatchSeed(int seed)
		{
			nextMatchSeed = seed;
		}

		/// <summary>
		/// 지금의 포탑 피해 배수. 포탑이 매 발사 때 *읽어가므로* 나중에 세운 연구 인형이
		/// 이미 서 있던 포탑에도 즉시 반영된다(세운 뒤에야 효과가 오면 강화가 아니라 벌칙이다).
		/// </summary>
		// 연구(판 안 건물)와 드래프트(웨이브 사이 선택)는 서로 다른 층이라 곱해진다 — 둘 다 쌓은 판이
		// 눈에 띄게 세지는 것이 「이 판은 화력으로 갔다」의 실체다.
		/// <summary> 연구로 늘어난 포탑 사거리 배수 — 무기가 사거리를 물을 때마다 읽는다. </summary>
		/// <summary>
		/// 이 대상을 때릴 때의 피해 배수 — 「둥지에 더 아프게」 카드가 여기서 걸린다.
		/// 카드는 뽑히는데 걸릴 자리가 없으면 화면엔 「둥지↑」라 적히고 실제로는 똑같이 때린다.
		/// </summary>
		private float DamageMultiplierFor(ICombatant target)
		{
			float multiplier = TowerDamageMultiplier;
			if (target is MatchCombatant combatant && IsNest(combatant))
				multiplier *= boons.NestDamageMultiplier;
			return multiplier;
		}

		/// <summary>
		/// 지금까지 각 수단을 몇 번 썼나 — 적응이 0 일 때 「안 쐈다」와 「골고루 썼다」를 가르는 유일한 값.
		///
		/// ★ 적응은 총량이 아니라 *편중*으로 붙는다(한 수단이 1/3 을 넘게 차지해야 저항이 생긴다).
		///   그래서 「둔화 포탑을 세웠는데 저항이 0」은 결함일 수도, 규칙대로일 수도 있다 —
		///   이 숫자 없이는 그 둘을 못 가른다(실측에서 멀쩡한 것을 두 번 실패로 찍었다).
		/// </summary>
		public (int Slow, int Splash, int Pierce) AdaptationUseCounts
		{
			get
			{
				int slowUses = 0;
				int splashHits = 0;
				int pierceHits = 0;
				foreach (GameObject unit in spawnedUnits)
				{
					if (unit == null)
						continue;
					TowerDefenseWeapon weapon = unit.GetComponent<TowerDefenseWeapon>();
					if (weapon == null)
						continue;
					slowUses += weapon.SlowApplied;
					splashHits += weapon.SplashHits;
					pierceHits += weapon.PierceHits;
				}
				return (slowUses, splashHits, pierceHits);
			}
		}

		// 유출 지점은 코어만이 아니다 — 전초기지도 지켜야 할 곳이다(넓힌 만큼 늘어난다).
		private bool IsAtAnyGoal(Vector3 position, float radiusSqr)
		{
			if ((position - coreCombatant.Position).sqrMagnitude <= radiusSqr)
				return true;

			foreach (Transform outpost in outposts)
			{
				if (outpost != null && (position - outpost.position.ToSim()).sqrMagnitude <= radiusSqr)
					return true;
			}
			return false;
		}

		/// <summary>
		/// 이 판에서 마수가 굳었던 *자리* 수 — 「지형에 막힘 / 서로 막음」으로 갈라 센다.
		///
		/// ★ 왜 자리로 세나: 경고 줄 수는 같은 마수가 4초마다 다시 찍혀 부풀고 판 길이에 휘둘린다.
		///   자리 수는 「판의 어디가 막히는가」를 세므로 판끼리 견줄 수 있다(이 값 없이 한 판씩 비교하다
		///   두 번 헛짚었다 — 좋아진 줄 알았던 것이 그냥 다른 판이었다).
		/// </summary>
		public (int Total, int ByTerrain, int ByUnit) StuckCellSummary =>
			(stuckCells.Count, stuckByTerrainCells.Count, stuckByUnitCells.Count);

		/// <summary>
		/// 격파 보상 지급 — 마수가 죽은 것을 처음 본 틱에 1회. 「잡는 맛」이 이 경로 하나에 달려 있다:
		/// 웨이브 정산만 있으면 교전 20초 동안 화면에서 아무 일도 안 일어나고, 잘 맞췄는지도 알 수 없다.
		/// 이탈(무대 밖) 제거는 목록에서 먼저 빠지므로 보상 대상이 아니다 — 사고에 상을 주지 않는다.
		/// </summary>
		private void PayKillBounties()
		{
			if (core == null)
				return;

			foreach (MatchCombatant enemy in waveEnemies)
			{
				if (enemy == null || enemy.IsAlive)
					continue;
				if (bountyPaidEnemyIds.Add(enemy.CombatantId) == false)
					continue;

				int bounty = enemyBountyById.TryGetValue(enemy.CombatantId, out int recorded)
					? recorded
					: core.BountyPerKill;
				bounty = Mathf.RoundToInt(bounty * boons.BountyMultiplier); // 드래프트로 고른 「사냥의 값」.
				if (bounty <= 0)
					continue;

				KilledCount++;
				core.AddResource(bounty);
				PopWorldText("+" + bounty, enemy.Position, TextType.Exp);
				AwardKillExperience(enemy.Position);
				AwardCoreExperience(Mathf.RoundToInt(stage.KillExperience * boons.EnemyRewardMultiplier)); // 코어도 판이 잘 굴러가는 만큼 자란다.

				// 죽은 자리에 잔해 — 많이 죽인 곳이 저절로 늪이 되어 다음 무리가 느려진다.
				TowerDefenseDebris.Spawn(stageRoot, enemy.Position.ToUnity(), waveEnemies,
					stage.DebrisSeconds, stage.DebrisSlowFactor, stage.GroundCellSize * 0.8f, stage.DebrisTint);
			}
		}

		/// <summary>
		/// 웨이브를 넘길 때마다 내 편(코어·인형·영웅)을 최대 체력의 일정 비율만큼 회복시킨다(사용자 요청).
		///
		/// ★ 왜 필요한가: 지금은 한 번 긁힌 인형이 판이 끝날 때까지 그 체력으로 산다. 그러면 「버텼다」의
		///   보상이 없고, 앞줄에 세운 인형은 필연적으로 죽으니 앞에 세우는 선택 자체가 손해가 된다.
		///   웨이브 사이 회복이 있으면 「이번엔 버틸 수 있나」가 매 웨이브의 계산이 된다.
		/// ★ 완전 회복이 아닌 이유: 그러면 피해가 아무 의미가 없어져 방어선의 소모전이 사라진다.
		/// </summary>
		private void HealDefenders()
		{
			if (stage == null || stage.DefenderHealPerWave <= 0f)
				return;

			foreach (ICombatant combatant in registeredCombatants)
			{
				if (combatant is not MatchCombatant defender || defender.IsAlive == false)
					continue;
				if (defender.TeamId != DEFENDER_TEAM || defender.UnitObject == null)
					continue;

				UnitHealth health = defender.UnitObject.GetComponent<UnitHealth>();
				if (health == null)
					continue;

				int maxHp = defender.UnitObject.UnitStat[UnitStatType.HP_MAX];
				int currentHp = defender.UnitObject.UnitStat[UnitStatType.HP_CUR];
				if (currentHp >= maxHp)
					continue;

				int healAmount = Mathf.Max(1, Mathf.RoundToInt(maxHp * stage.DefenderHealPerWave));
				health.ReceiveHeal(healAmount);
				PopWorldText("+" + Mathf.Min(healAmount, maxHp - currentHp), defender.Position, TextType.Heal);
			}
		}
	}
}
