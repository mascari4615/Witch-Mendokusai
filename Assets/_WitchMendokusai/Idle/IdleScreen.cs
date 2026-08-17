using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Contracts;
using WitchMendokusai.DomainSDK.Idle;
// 네임스페이스를 통째로 들이면 Vector2 가 UnityEngine 것과 겹친다 — 쓸 것만 별칭으로 들인다.
using WitchMendokusai.Presentation;
using BigNumberText = WitchMendokusai.Numerics.BigNumberText;

namespace WitchMendokusai
{
	/// <summary>
	/// 방치 게임 화면 (TASK-WM-406).
	///
	/// ★ 이 파일에 게임 규칙이 한 줄도 없다 — 사진을 받아 그리고, 의도를 보낸다.
	///   에디터 창과 같은 코어·같은 계약이고 다른 것은 그릇뿐이다.
	///
	/// ★ 짜임 (사용자 컨펌 2026-08-16): 위 요약 띠 · 가운데 <b>실황 셋</b>(기지·전투·창고) ·
	///   아래 <b>조작 서랍</b>(한 번에 한 묶음).
	///   전에는 실황이 <b>전투 하나뿐</b>이었고 기지·창고는 버튼 더미였다. 그래서
	///   ① 기지가 도는 게 안 보이고 ② 오른쪽 한 칸에 세 묶음이 쌓여 서로 겹쳤다.
	///   실황과 조작을 <b>층으로 가른다</b> — 보는 것은 위, 만지는 것은 아래.
	///
	/// ★ 보이는 것은 <b>기하학적 도형</b>이다(사용자 방향: 세계관 정하기 전).
	///   규칙 하나로 읽힌다 — <b>변의 수 = 등급</b>. 1등급 삼각형 … 8등급 십각형.
	///   숫자를 안 읽어도 변만 세면 등급을 안다.
	///
	/// ★ 해상도 — 고정 픽셀을 최소로 쓰고 flex 로 늘린다. 좁으면 실황 셋이 접힌다.
	/// </summary>
	[RequireComponent(typeof(UIDocument))]
	public sealed class IdleScreen : MonoBehaviour, IGameView<IdleSnapshot>
	{
		/// <summary>눈으로 셀 수 있는 최대 장단 — 이보다 빠르면 사람 눈엔 그냥 「계속」이다.</summary>
		private const float FASTEST_VISIBLE_BEATS = 8f;

		/// <summary>창고 격자 한 줄에 몇 칸.</summary>
		private const int VAULT_COLUMNS = 8;

		/// <summary>방금 일어난 일을 몇 초나 적어 두나.</summary>
		private const float NOTE_SECONDS = 6f;

		/// <summary>돌아온 인사는 더 오래 — 읽고 나서도 「얼마 벌었지」를 다시 볼 틈이 있어야 한다.</summary>
		private const float RETURN_NOTE_SECONDS = 20f;

		[Header("수치 — 비워 두면 코드 기본값")]
		[SerializeField] private IdleTuningSO tuningAsset;

		[Header("생김새")]
		[SerializeField] private StyleSheet styleSheet;

		[Header("얼마나 자주 적나 (초)")]
		[SerializeField] private float saveIntervalSeconds = 10f;

		[Header("실황 — 눈에 보이는 리듬")]
		[Tooltip("기지가 알갱이를 몇 초에 하나 뱉나")]
		[SerializeField] private float moteEverySeconds = 0.22f;

		[Tooltip("때리는 소리를 이보다 자주 안 낸다 (초)")]
		[SerializeField] private float tickGapSeconds = 0.14f;

		private IdleSession session;
		private float sinceLastSave;
		private long lastKills;
		private int lastBagCount;

		// ── 위 띠 ───────────────────────────────────────────────────────────
		private Label stageLabel;
		private Label resourceLabel;
		private Label topNoteLabel;

		/// <summary>
		/// 지금 할 <b>한 걸음</b> — 판 상태에서 뽑아낸다 (TASK-WM-406).
		///
		/// ★ 기존 지적: 「첫 30분에 뭘 눌러야 하는지가 안 보인다」.
		///   튜토리얼 팝업 대신 <b>지금 상태에서 파생된 한 줄</b>을 띄운다 —
		///   낡지 않고, 조작을 막지 않고, 후반에도 쓸모가 남는다(다음 목표를 계속 가리킨다).
		/// </summary>
		private Label guideLabel;

		// ── 기지 실황 ───────────────────────────────────────────────────────
		private MoteStreamElement baseMotes;
		private readonly List<NgonElement> baseShapes = new List<NgonElement>();
		private NgonElement vaultMark;
		private Label baseLiveLabel;
		private float sinceMote;
		private int moteTurn;

		// ── 전투 실황 ───────────────────────────────────────────────────────
		private NgonElement targetShape;
		private NgonBurstElement burst;
		private readonly List<NgonElement> heroes = new List<NgonElement>();
		private MoteStreamElement bolts;
		private ProgressBar healthBar;
		private readonly List<VisualElement> killDots = new List<VisualElement>();
		private Label arenaCaption;
		private VisualElement arenaBox;
		private GridBackdropElement backdrop;
		private FloatTextLayer floats;

		/// <summary>지나가는 것 — 누르면 폭주한다.</summary>
		private NgonElement visitor;
		private Label surgeLabel;

		/// <summary>때리는 장단이 얼마나 찼나 (1 이 되면 한 대).</summary>
		private float beat;
		private int heroTurn;

		// ── 창고 실황 ───────────────────────────────────────────────────────
		private MoteStreamElement vaultMotes;
		private readonly List<NgonElement> vaultCells = new List<NgonElement>();
		private Label vaultLabel;

		// ── 조작 서랍 ───────────────────────────────────────────────────────
		private readonly List<Button> tabButtons = new List<Button>();
		private readonly List<VisualElement> pages = new List<VisualElement>();

		private VisualElement basePage;
		private readonly List<Button> producerButtons = new List<Button>();
		private readonly List<NgonElement> producerShapes = new List<NgonElement>();
		private Label baseSummary;

		private VisualElement upgradePage;
		private VisualElement gearPage;
		private VisualElement heroPage;
		private VisualElement foldPage;

		private Button pullButton;
		private Label codexLabel;
		private Label pullNote;
		private VisualElement partyRow;
		private readonly List<Button> partyButtons = new List<Button>();
		private readonly List<NgonElement> partyShapes = new List<NgonElement>();
		private VisualElement heroRows;
		private readonly List<Button> heroButtons = new List<Button>();
		private readonly List<NgonElement> heroShapes = new List<NgonElement>();

		/// <summary>지금 어느 자리를 바꾸는 중인가 — -1 이면 고르는 중이 아니다.</summary>
		private int seatBeingFilled = -1;

		/// <summary>
		/// 자리보다 <b>영웅을 먼저</b> 고른 경우 — -1 이면 고른 것이 없다.
		///
		/// ★ 둘 중 아무 쪽을 먼저 눌러도 되게 한다. 한쪽 순서만 되면
		///   다른 순서로 누른 사람에게는 <b>아무 일도 안 일어난다</b> — 그건 고장으로 읽힌다.
		/// </summary>
		private int pendingHeroId = -1;

		/// <summary>목록에 붙은 작은 도형들 — 같이 돌려야 화면이 살아 있다.</summary>
		private readonly List<NgonElement> decor = new List<NgonElement>();

		private Label damageTitle;
		private Label damageValue;
		private Button damageButton;
		private Label speedTitle;
		private Label speedValue;
		private Button speedButton;
		private Button retreatButton;
		private Button holdButton;

		private Label potentialLabel;
		private Label wornLabel;
		private VisualElement mergeRows;
		private readonly List<Button> mergeButtons = new List<Button>();
		private VisualElement bagRows;
		private readonly List<Button> bagButtons = new List<Button>();
		private readonly List<NgonElement> bagShapes = new List<NgonElement>();
		private VisualElement dropRows;
		private readonly List<Button> appraiseButtons = new List<Button>();
		private Label rollNote;

		/// <summary>
		/// 방금 일어난 일을 적는 줄들 — <b>잠시 뒤 사라진다</b>.
		///
		/// ★ 안 사라지면 지난 일이 <b>지금 상태</b>처럼 읽힌다. 다섯 판 전에 뽑은 결과가
		///   아직 붙어 있으면 사람은 그게 방금 일이라고 믿는다.
		/// </summary>
		private readonly List<Label> fadingNotes = new List<Label>();
		private readonly List<float> fadingLeft = new List<float>();

		private Label foldSummary;
		private Button prestigeButton;

		private ProceduralSfx sound;

		/// <summary>지금 화면이 얼마나 흔들리나 (0~1). 잡으면 조금, 합치면 많이.</summary>
		private float shake;

		/// <summary>보여주는 자원 — 실제 값을 <b>따라 굴러간다</b>(뚝뚝 튀지 않게).</summary>
		private double resourceRolling;
		private double resourceShown;
		private float sinceResourcePop;

		/// <summary>
		/// 영웅 셋의 <b>생김새와 성격</b>.
		///
		/// ★ 사용자 지적 (2026-08-16): 「영웅들이 개성이 없다」. 셋이 똑같은 도형이면
		///   그건 셋이 아니라 <b>하나가 세 번 그려진 것</b>이다.
		///   세계관이 없으니 이름·직업 대신 <b>모양·색·움직임</b>으로 가른다 —
		///   삼각(빠르게 자주 찌른다) · 사각(느리고 크게 친다) · 오각(안 다가가고 쏜다).
		/// </summary>
		private static readonly int[] HERO_SIDES = { 3, 4, 5 };
		private static readonly float[] HERO_TURNS = { 0.55f, 0.18f, 0.34f };
		private static readonly Color[] HERO_COLORS =
		{
			new Color(0.93f, 0.62f, 0.36f),
			new Color(0.46f, 0.80f, 0.72f),
			new Color(0.72f, 0.58f, 0.92f),
		};

		public PresentationKind Kind => PresentationKind.UIOnly;

		private void OnEnable()
		{
			IdleTuning tuning = tuningAsset != null ? tuningAsset.ToTuning() : new IdleTuning();

			IdleState state = new IdleState();
			IdleSaveData? saved = IdleSaveStore.Load();
			if (saved.HasValue)
			{
				state.Load(saved.Value);
			}

			session = new IdleSession(tuning, state);
			sound = new ProceduralSfx(gameObject);

			// ★ 화면을 짓기 전에 자리 비운 몫을 쳐준다 — 첫 그림이 이미 받은 뒤의 판이라야 한다.
			session.CatchUp(IdleSaveStore.NowUnixSeconds(), out IdleAwayReport away);

			BuildInterface(away);
			lastKills = session.State.Kills;
			lastBagCount = session.State.Bag.Count;
			resourceShown = session.State.Resource;
			resourceRolling = session.State.Resource;
			Render(session.Capture());
		}

		private void OnDisable()
		{
			WriteDown();
		}

		private void OnApplicationPause(bool paused)
		{
			if (paused)
			{
				WriteDown();
			}
		}

		private void OnApplicationQuit()
		{
			WriteDown();
		}

		private void Update()
		{
			float delta = Time.unscaledDeltaTime;

			session.Advance(delta);
			// 지나가는 것은 <b>보고 있는 동안만</b> 돈다 — 판정(방치 진행)과 다른 층이다.
			session.AdvanceSurge(delta);
			IdleSnapshot snapshot = session.Capture();

			AdvanceBattle(snapshot, delta);
			AdvanceBase(snapshot, delta);
			AdvanceVault(snapshot, delta);

			// 자원은 계속 들어오므로 <b>주기적으로</b> 띄운다 — 매 프레임이면 글자가 폭포가 된다.
			sinceResourcePop += delta;
			if (sinceResourcePop >= 1f)
			{
				double gained = snapshot.Resource - resourceShown;
				resourceShown = snapshot.Resource;
				sinceResourcePop = 0f;

				if (gained > 0d)
				{
					floats.Pop("+" + BigNumberText.Format(gained),
						new Vector2(Random.Range(40f, 110f), 120f), new Color(0.72f, 0.82f, 0.55f));
				}
			}

			FadeNotes(delta);
			floats.Advance(delta);
			backdrop.Advance(delta);
			visitor.Advance(delta, 0.6f);
			AdvanceShake(delta);

			// ★ 자원이 <b>굴러 올라간다</b> — 뚝뚝 튀면 「많이 벌었다」가 안 느껴진다.
			resourceRolling += (snapshot.Resource - resourceRolling) * Mathf.Min(1f, delta * 6f);

			for (int index = 0; index < decor.Count; index++)
			{
				decor[index].Advance(delta, 0.05f);
			}

			Render(snapshot);

			sinceLastSave += delta;
			if (sinceLastSave >= saveIntervalSeconds)
			{
				WriteDown();
			}
		}

		// ── 실황 굴리기 ─────────────────────────────────────────────────────

		/// <summary>
		/// 전투 — <b>때리는 게 보이게</b> 한다.
		///
		/// ★ 장단은 코어의 실제 공격속도(<see cref="IdleSnapshot.AttacksPerSecond"/>)에서 온다.
		///   화면이 제 장단을 지어내면 공격속도를 올려도 <b>빨라진 게 안 보인다</b>.
		/// ★ 다만 눈이 셀 수 있는 데까지만 — 초당 수백 번은 그냥 「계속」이라 세지 못한다.
		/// </summary>
		private void AdvanceBattle(IdleSnapshot snapshot, float delta)
		{
			float beatsPerSecond = Mathf.Min(FASTEST_VISIBLE_BEATS, (float)snapshot.AttacksPerSecond);
			beat += delta * beatsPerSecond;

			// 한 프레임에 여러 대를 몰아 치지 않는다 — 같은 프레임의 두 대는 눈에 한 대다.
			if (beat >= 1f)
			{
				beat -= 1f;
				if (beat > 1f)
				{
					beat = 0f;
				}

				Strike(snapshot, beatsPerSecond);
			}

			if (snapshot.Kills > lastKills)
			{
				long got = snapshot.Kills - lastKills;
				lastKills = snapshot.Kills;

				burst.Fire(SidesFor(snapshot.MaxTierNow), TierColor(snapshot.MaxTierNow));
				targetShape.Hit();
				sound.Blip(snapshot.MaxTierNow);
				Shake(0.25f);

				floats.Pop("+" + BigNumberText.Format(got),
					new Vector2(Random.Range(30f, 110f), 70f), TierColor(snapshot.MaxTierNow));
			}

			targetShape.Advance(delta, 0.08f);
			burst.Advance(delta);
			bolts.Advance(delta);

			for (int index = 0; index < heroes.Count; index++)
			{
				heroes[index].Advance(delta, HERO_TURNS[index % HERO_TURNS.Length]);
			}
		}

		/// <summary>
		/// 사람이 판을 눌렀다 — <b>한 대 더</b>.
		///
		/// ★ 코어가 값을 정한다(지금 공격속도의 몇 초치). 화면은 「눌렸다」만 보여준다 —
		///   여기서 숫자를 만들면 창마다 다른 게임이 된다.
		/// </summary>
		private void OnTapped(PointerDownEvent moment)
		{
			IdleSnapshot before = session.Capture();
			bool handSurging = before.SurgeKind == IdleSurgeKind.HandFrenzy && before.SurgeSecondsLeft > 0d;

			session.Send(new IdleTapIntent());

			// 누른 것이 <b>손에 남게</b> — 차례 상관없이 셋 다 달려든다(사람이 시킨 것이니까).
			for (int index = 0; index < heroes.Count; index++)
			{
				heroes[index].Lunge(Vector2.right);
			}

			targetShape.Hit();
			sound.Tick(0f);

			// ★ 손 폭주는 한 대가 <b>50배</b>다. 그런데 손끝 느낌이 평소와 같으면
			//   그 50배는 숫자로만 존재한다 — 판이 더 크게 흔들리고 파편이 터져야 한다.
			if (handSurging)
			{
				Shake(0.55f);
				burst.Fire(SidesFor(before.MaxTierNow), new Color(0.98f, 0.84f, 0.38f));
				floats.Pop("×50", new Vector2(Random.Range(40f, 100f), 40f),
					new Color(0.98f, 0.84f, 0.38f));
			}
			else
			{
				Shake(0.12f);
				floats.Pop("!", new Vector2(Random.Range(50f, 90f), 40f),
					new Color(0.95f, 0.86f, 0.55f));
			}

			Render(session.Capture());
		}

		/// <summary>한 대 — 차례가 된 영웅이 나선다.</summary>
		private void Strike(IdleSnapshot snapshot, float beatsPerSecond)
		{
			int who = heroTurn % heroes.Count;
			heroTurn++;

			// 오각(원거리)은 안 다가간다 — 대신 쏜다. 그게 이 셋을 다르게 만든다.
			bool ranged = who == 2;

			if (ranged)
			{
				heroes[who].Hit();
				bolts.Send(new Vector2(0.30f, 0.52f), new Vector2(0.78f, 0.5f),
					HERO_COLORS[who], HERO_SIDES[who], 0.22f);
			}
			else
			{
				heroes[who].Lunge(Vector2.right);
				targetShape.Hit();
			}

			// 잦으면 소음이다 — 눈으로 셀 수 있는 빠르기일 때만 귀에도 낸다.
			if (beatsPerSecond <= FASTEST_VISIBLE_BEATS * 0.6f)
			{
				sound.Tick(tickGapSeconds);
			}
		}

		/// <summary>
		/// 기지 — <b>자원이 어디서 오는지</b>를 보여준다.
		///
		/// ★ 사용자 지적: 「방치 강화도 실제로 아무 효과 없는 것 같다」.
		///   숫자만 오르면 산 것이 일하는지 안 하는지 알 길이 없다.
		///   산 생산자가 알갱이를 <b>위로 뱉고</b>, 그게 저장고로 들어가는 걸 눈으로 본다.
		/// </summary>
		private void AdvanceBase(IdleSnapshot snapshot, float delta)
		{
			baseMotes.Advance(delta);

			if (snapshot.IncomePerSecond <= 0d)
			{
				return;
			}

			sinceMote += delta;
			if (sinceMote < moteEverySeconds)
			{
				return;
			}

			sinceMote = 0f;

			// 많이 내는 쪽이 자주 뱉는다 — 몫만큼 차례가 돌아온다.
			int kind = PickProducerToEmit(snapshot);
			if (kind < 0)
			{
				return;
			}

			float from = (kind + 0.5f) / Mathf.Max(1, snapshot.Producers.Length);
			baseMotes.Send(new Vector2(from, 0.86f), new Vector2(0.5f, 0.20f),
				TierColor(kind + 1), SidesFor(kind + 1), 0.7f);
		}

		/// <summary>몫에 따라 차례를 돌린다 — 무작위보다 눈에 고르게 보인다.</summary>
		private int PickProducerToEmit(IdleSnapshot snapshot)
		{
			double total = snapshot.IncomePerSecond;
			double walked = 0d;
			double want = total * ((moteTurn % 16) / 16d);
			moteTurn++;

			for (int kind = 0; kind < snapshot.Producers.Length; kind++)
			{
				walked += snapshot.Producers[kind].OutputTotal;
				if (walked >= want && snapshot.Producers[kind].Owned > 0L)
				{
					return kind;
				}
			}

			for (int kind = snapshot.Producers.Length - 1; kind >= 0; kind--)
			{
				if (snapshot.Producers[kind].Owned > 0L)
				{
					return kind;
				}
			}

			return -1;
		}

		/// <summary>창고 — 떨어진 장비가 <b>쌓이는 게</b> 보인다.</summary>
		private void AdvanceVault(IdleSnapshot snapshot, float delta)
		{
			vaultMotes.Advance(delta);

			if (snapshot.Bag.Length > lastBagCount)
			{
				for (int index = lastBagCount; index < snapshot.Bag.Length; index++)
				{
					Vector2 cell = VaultCellAt(index, snapshot.BagCapacity);
					vaultMotes.Send(new Vector2(cell.x, 0.02f), cell,
						TierColor(snapshot.Bag[index].Tier), SidesFor(snapshot.Bag[index].Tier), 0.45f);
				}
			}

			lastBagCount = snapshot.Bag.Length;
		}

		/// <summary>격자 한 칸이 칸 안에서 차지하는 자리 (0~1) — 떨어지는 장비가 거기로 꽂힌다.</summary>
		private static Vector2 VaultCellAt(int index, int capacity)
		{
			int rows = Mathf.Max(1, Mathf.CeilToInt((float)capacity / VAULT_COLUMNS));
			int column = index % VAULT_COLUMNS;
			int row = index / VAULT_COLUMNS;

			return new Vector2(
				(column + 0.5f) / VAULT_COLUMNS,
				0.30f + (row + 0.5f) / rows * 0.62f);
		}

		/// <summary>방금 일어난 일을 적는다 — 시계를 다시 감는다.</summary>
		private void SayOnce(Label where, string what)
		{
			SayOnce(where, what, NOTE_SECONDS);
		}

		/// <summary>얼마나 오래 남길지 정해서 적는다 — 돌아온 인사는 더 오래 둔다.</summary>
		private void SayOnce(Label where, string what, float seconds)
		{
			where.text = what;
			where.style.opacity = 1f;

			int at = fadingNotes.IndexOf(where);
			if (at < 0)
			{
				fadingNotes.Add(where);
				fadingLeft.Add(seconds);
				return;
			}

			fadingLeft[at] = seconds;
		}

		/// <summary>적어 둔 것을 <b>흐리게 지운다</b> — 끝에서 툭 사라지면 놀란다.</summary>
		private void FadeNotes(float deltaSeconds)
		{
			for (int index = 0; index < fadingNotes.Count; index++)
			{
				if (fadingLeft[index] <= 0f)
				{
					continue;
				}

				fadingLeft[index] -= deltaSeconds;

				if (fadingLeft[index] <= 0f)
				{
					fadingLeft[index] = 0f;
					fadingNotes[index].text = string.Empty;
					continue;
				}

				// 마지막 1초 동안만 흐려진다 — 그 전에는 또렷하게 읽혀야 한다.
				fadingNotes[index].style.opacity = fadingLeft[index] < 1f ? fadingLeft[index] : 1f;
			}
		}

		/// <summary>화면을 흔든다 — 「일이 일어났다」를 몸으로 알려준다.</summary>
		private void Shake(float amount)
		{
			if (amount > shake)
			{
				shake = amount > 1f ? 1f : amount;
			}
		}

		private void AdvanceShake(float deltaSeconds)
		{
			if (shake <= 0f)
			{
				return;
			}

			shake -= deltaSeconds * 3f;
			if (shake < 0f)
			{
				shake = 0f;
			}

			// 판만 흔든다 — 화면 전체를 흔들면 글자가 읽히지 않는다.
			float power = shake * shake * 10f;
			arenaBox.style.translate = new StyleTranslate(new Translate(
				Random.Range(-power, power), Random.Range(-power, power)));
		}

		private void WriteDown()
		{
			if (session == null)
			{
				return;
			}

			sinceLastSave = 0f;
			session.MarkSeen(IdleSaveStore.NowUnixSeconds());
			IdleSaveStore.Save(session.State.Save());
		}

		// ── 짓기 ────────────────────────────────────────────────────────────

		private void BuildInterface(IdleAwayReport away)
		{
			VisualElement root = GetComponent<UIDocument>().rootVisualElement;
			root.Clear();

			if (styleSheet != null)
			{
				root.styleSheets.Add(styleSheet);
			}

			VisualElement shell = new VisualElement();
			shell.AddToClassList("idle-root");
			root.Add(shell);

			BuildTopBar(shell, away);

			// ★ 위층 = <b>보는 것</b>. 셋이 동시에 돈다.
			VisualElement stages = new VisualElement();
			stages.AddToClassList("idle-stages");
			shell.Add(stages);

			BuildBaseLive(stages);
			BuildArena(stages);
			BuildVaultLive(stages);

			// ★ 아래층 = <b>만지는 것</b>. 한 번에 한 묶음만 편다.
			BuildDrawer(shell);
		}

		private void BuildTopBar(VisualElement parent, IdleAwayReport away)
		{
			VisualElement bar = new VisualElement();
			bar.AddToClassList("idle-topbar");
			parent.Add(bar);

			stageLabel = AddLabel(bar, "idle-top-stage");
			resourceLabel = AddLabel(bar, "idle-top-resource");
			topNoteLabel = AddLabel(bar, "idle-top-note");
			guideLabel = AddLabel(parent, "idle-guide");

			// ★ 돌아온 순간이 방치형의 보상이다 — <b>얼마나</b> 벌었는지 말한다.
			//   그리고 상한에 걸려 흘린 시간이 있으면 그것도 말한다(손해는 조용하면 안 된다).
			if (away.HasAnything)
			{
				topNoteLabel.text = string.Format("자리 비운 {0} — 자원 +{1} · {2}마리{3}{4}{5}",
					DescribeSpan(away.CreditedSeconds),
					BigNumberText.Format(away.ResourceGained),
					BigNumberText.Format(away.KillsGained),
					away.StagesGained > 0 ? string.Format(" · {0}단계 나아감", away.StagesGained) : string.Empty,
					away.ItemsGained > 0 ? string.Format(" · 장비 {0}", away.ItemsGained) : string.Empty,
					away.HitCap
						? string.Format("   ⚠ 상한 {0} 을 넘겨 {1} 을 흘렸다 (환생하면 상한이 는다)",
							DescribeSpan(away.CapSeconds), DescribeSpan(away.LostSeconds))
						: string.Empty);

				topNoteLabel.EnableInClassList("idle-warn", away.HitCap);

				// ★ 이것도 <b>지난 일</b>이다 — 한참 뒤에도 「자리 비운 3시간」이 붙어 있으면
				//   지금 상태로 읽힌다. 다만 돌아온 순간의 보상이라 다른 안내보다 오래 남긴다.
				SayOnce(topNoteLabel, topNoteLabel.text, RETURN_NOTE_SECONDS);
			}
		}

		/// <summary>기지 실황 — 생산자가 알갱이를 뱉고 저장고가 받는다.</summary>
		private void BuildBaseLive(VisualElement parent)
		{
			VisualElement live = new VisualElement();
			live.AddToClassList("idle-live");
			parent.Add(live);

			AddLabel(live, "idle-live-title").text = "기지";

			baseMotes = new MoteStreamElement();
			baseMotes.AddToClassList("idle-backdrop");
			live.Add(baseMotes);

			VisualElement top = new VisualElement();
			top.AddToClassList("idle-vault-head");
			live.Add(top);

			vaultMark = new NgonElement();
			vaultMark.AddToClassList("idle-vault-mark");
			vaultMark.Sides = 6;
			vaultMark.Body = new Color(0.72f, 0.82f, 0.55f);
			top.Add(vaultMark);

			baseLiveLabel = AddLabel(top, "idle-live-note");

			VisualElement spacer = new VisualElement();
			spacer.AddToClassList("idle-spacer");
			live.Add(spacer);

			VisualElement strip = new VisualElement();
			strip.AddToClassList("idle-producer-strip");
			live.Add(strip);

			baseShapes.Clear();

			for (int kind = 0; kind < 8; kind++)
			{
				NgonElement shape = new NgonElement();
				shape.AddToClassList("idle-strip-shape");
				shape.Sides = SidesFor(kind + 1);
				shape.Body = TierColor(kind + 1);
				strip.Add(shape);
				baseShapes.Add(shape);
			}
		}

		private void BuildArena(VisualElement parent)
		{
			VisualElement arena = new VisualElement();
			arena.AddToClassList("idle-live");
			arena.AddToClassList("idle-arena");
			parent.Add(arena);

			AddLabel(arena, "idle-live-title").text = "전투";

			// 바닥 격자 — 밋밋한 검정은 「꺼진 화면」처럼 보인다.
			backdrop = new GridBackdropElement();
			backdrop.AddToClassList("idle-backdrop");
			arena.Add(backdrop);

			VisualElement field = new VisualElement();
			field.AddToClassList("idle-field");
			arena.Add(field);

			VisualElement heroRow = new VisualElement();
			heroRow.AddToClassList("idle-hero-row");
			field.Add(heroRow);
			heroes.Clear();

			for (int one = 0; one < HERO_SIDES.Length; one++)
			{
				NgonElement hero = new NgonElement();
				hero.AddToClassList("idle-hero");
				hero.Sides = HERO_SIDES[one];
				hero.Body = HERO_COLORS[one];
				heroRow.Add(hero);
				heroes.Add(hero);
			}

			VisualElement box = new VisualElement();
			box.AddToClassList("idle-stage-box");
			field.Add(box);

			targetShape = new NgonElement();
			targetShape.AddToClassList("idle-shape");
			box.Add(targetShape);

			burst = new NgonBurstElement();
			burst.AddToClassList("idle-shape");
			box.Add(burst);

			// 쏘는 것은 <b>판 전체</b>를 가로지른다 — 판 위에 깔아야 영웅에서 적까지 간다.
			bolts = new MoteStreamElement();
			bolts.AddToClassList("idle-backdrop");
			field.Add(bolts);

			healthBar = new ProgressBar();
			healthBar.lowValue = 0f;
			healthBar.highValue = 1f;
			healthBar.AddToClassList("idle-health");
			arena.Add(healthBar);

			VisualElement dots = new VisualElement();
			dots.AddToClassList("idle-kills-dots");
			arena.Add(dots);
			killDots.Clear();

			// ★ <b>지나가는 것</b> — 판 위를 가로지르고, 누르면 잠시 폭주한다.
			//   조사 1순위(황금 쿠키 자리): 방치형은 기대값이 평탄해서 「지금 볼 이유」가 없다.
			visitor = new NgonElement();
			visitor.AddToClassList("idle-visitor");
			visitor.Sides = 12;
			visitor.Body = new Color(0.98f, 0.84f, 0.38f);
			visitor.style.display = DisplayStyle.None;
			visitor.RegisterCallback<PointerDownEvent>(OnVisitorClicked);
			arena.Add(visitor);

			surgeLabel = AddLabel(arena, "idle-surge");

			arenaCaption = AddLabel(arena, "idle-arena-caption");

			// 튀는 숫자는 판 위에 뜬다 — 담는 칸이 자리를 잡아 준다.
			arenaBox = box;
			floats = new FloatTextLayer(box);

			// ★ <b>판 전체가 누르는 것</b>이다 (사용자 지적: 「전혀 클리커스럽지 않다」).
			//   쿠키 클리커의 심장은 큰 버튼이다. 작은 버튼을 따로 두면 그건 <b>또 하나의 목록</b>이고,
			//   손이 가는 곳(적이 있는 자리)과 누르는 곳이 갈라진다.
			field.RegisterCallback<PointerDownEvent>(OnTapped);
		}

		/// <summary>창고 실황 — 가방이 격자로 보이고, 떨어진 것이 위에서 꽂힌다.</summary>
		private void BuildVaultLive(VisualElement parent)
		{
			VisualElement live = new VisualElement();
			live.AddToClassList("idle-live");
			parent.Add(live);

			AddLabel(live, "idle-live-title").text = "창고";

			vaultMotes = new MoteStreamElement();
			vaultMotes.AddToClassList("idle-backdrop");
			live.Add(vaultMotes);

			vaultLabel = AddLabel(live, "idle-live-note");

			VisualElement grid = new VisualElement();
			grid.AddToClassList("idle-vault-grid");
			live.Add(grid);
			vaultCells.Clear();

			for (int index = 0; index < 40; index++)
			{
				NgonElement cell = new NgonElement();
				cell.AddToClassList("idle-vault-cell");
				grid.Add(cell);
				vaultCells.Add(cell);
			}
		}

		/// <summary>
		/// 조작 서랍 — <b>한 번에 한 묶음</b>.
		///
		/// ★ 전에는 강화·장비·환생 셋을 한 칸에 세로로 쌓았다. 그래서 스크롤 없이 겹쳤고
		///   「뭐가 뭔지 모르겠다」가 됐다. 탭은 화면을 아끼려는 게 아니라
		///   <b>지금 무엇을 하는 중인지</b>를 하나로 만드는 장치다.
		/// </summary>
		private void BuildDrawer(VisualElement parent)
		{
			VisualElement drawer = new VisualElement();
			drawer.AddToClassList("idle-drawer");
			parent.Add(drawer);

			VisualElement tabs = new VisualElement();
			tabs.AddToClassList("idle-tabs");
			drawer.Add(tabs);

			ScrollView body = new ScrollView();
			body.AddToClassList("idle-drawer-body");
			drawer.Add(body);

			basePage = AddPage(body);
			upgradePage = AddPage(body);
			gearPage = AddPage(body);
			heroPage = AddPage(body);
			foldPage = AddPage(body);

			pages.Clear();
			pages.Add(basePage);
			pages.Add(upgradePage);
			pages.Add(gearPage);
			pages.Add(heroPage);
			pages.Add(foldPage);

			string[] names = { "기지", "강화", "장비", "영웅", "환생" };
			tabButtons.Clear();

			for (int index = 0; index < names.Length; index++)
			{
				int captured = index;
				Button tab = AddButton(tabs, "idle-tab", () => ShowTab(captured));
				tab.text = names[index];
				tabButtons.Add(tab);
			}

			BuildBasePage();
			BuildUpgradePage();
			BuildGearPage();
			BuildHeroPage();
			BuildFoldPage();

			ShowTab(0);
		}

		private void ShowTab(int which)
		{
			for (int index = 0; index < pages.Count; index++)
			{
				pages[index].style.display = index == which ? DisplayStyle.Flex : DisplayStyle.None;
				tabButtons[index].EnableInClassList("idle-tab--on", index == which);
			}
		}

		/// <summary>기지 — <b>시간이 자원을 낸다</b>. 이 층이 없으면 감정도 합치기도 강화도 못 한다.</summary>
		private void BuildBasePage()
		{
			baseSummary = AddLabel(basePage, "idle-row-value");

			producerButtons.Clear();
			producerShapes.Clear();

			for (int kind = 0; kind < 8; kind++)
			{
				int captured = kind;
				producerButtons.Add(AddShapeRow(basePage, kind + 1, () => BuyProducer(captured),
					out NgonElement shape));
				producerShapes.Add(shape);
			}
		}

		private void BuildUpgradePage()
		{
			damageTitle = AddLabel(upgradePage, "idle-row-title");
			damageValue = AddLabel(upgradePage, "idle-row-value");
			damageButton = AddButton(upgradePage, "idle-button idle-button--strong",
				() => Send(IdleUpgradeKind.Damage));

			speedTitle = AddLabel(upgradePage, "idle-row-title");
			speedValue = AddLabel(upgradePage, "idle-row-value");
			speedButton = AddButton(upgradePage, "idle-button idle-button--strong",
				() => Send(IdleUpgradeKind.AttackSpeed));

			AddDivider(upgradePage);

			retreatButton = AddButton(upgradePage, "idle-button", Retreat);
			holdButton = AddButton(upgradePage, "idle-button", ToggleHold);
		}

		/// <summary>
		/// 장비 — 모험이 가져온 것. 차고, 합치고, 감정한다.
		///
		/// ★ 셋 다 <b>자원</b>이 든다(감정·합치기). 그게 기지와 모험을 같은 저울에 올리는 자리다.
		/// </summary>
		private void BuildGearPage()
		{
			wornLabel = AddLabel(gearPage, "idle-row-title");
			wornLabel.style.whiteSpace = WhiteSpace.Normal;

			AddDivider(gearPage);
			AddLabel(gearPage, "idle-row-value").text = "가방 — 눌러서 찬다";

			bagRows = new VisualElement();
			gearPage.Add(bagRows);

			AddDivider(gearPage);
			AddLabel(gearPage, "idle-row-value").text = "합치기 — 같은 부위·등급 셋이 한 단계 위로 (잠재는 사라진다)";

			mergeRows = new VisualElement();
			gearPage.Add(mergeRows);

			AddDivider(gearPage);
			potentialLabel = AddLabel(gearPage, "idle-row-title");

			dropRows = new VisualElement();
			gearPage.Add(dropRows);

			rollNote = AddLabel(gearPage, "idle-note");
		}

		/// <summary>
		/// 영웅 — <b>뽑고 · 모으고 · 셋을 고른다</b> (TASK-WM-406, 사용자 결정 2026-08-17).
		///
		/// ★ 한 화면에 셋을 같이 둔다: 뽑기(설렘) · 파티(결정) · 도감(모은 것).
		///   나누면 「뽑았는데 뭐가 달라졌지」를 확인하러 탭을 옮겨 다녀야 한다.
		/// </summary>
		private void BuildHeroPage()
		{
			pullButton = AddButton(heroPage, "idle-button idle-button--strong", Pull);
			pullNote = AddLabel(heroPage, "idle-note");

			AddDivider(heroPage);
			AddLabel(heroPage, "idle-row-value").text = "내보낸 셋 — 눌러서 바꾼다";

			partyRow = new VisualElement();
			partyRow.AddToClassList("idle-party-row");
			heroPage.Add(partyRow);
			partyButtons.Clear();
			partyShapes.Clear();

			for (int slot = 0; slot < 3; slot++)
			{
				int captured = slot;

				VisualElement seat = new VisualElement();
				seat.AddToClassList("idle-seat");
				partyRow.Add(seat);

				NgonElement shape = new NgonElement();
				shape.AddToClassList("idle-seat-shape");
				seat.Add(shape);
				partyShapes.Add(shape);
				decor.Add(shape);

				Button button = new Button(() => BeginSeat(captured));
				button.AddToClassList("idle-button");
				button.AddToClassList("idle-seat-button");
				seat.Add(button);
				partyButtons.Add(button);
			}

			AddDivider(heroPage);
			codexLabel = AddLabel(heroPage, "idle-row-title");

			heroRows = new VisualElement();
			heroPage.Add(heroRows);
		}

		private void BuildFoldPage()
		{
			foldSummary = AddLabel(foldPage, "idle-row-title");
			foldSummary.style.whiteSpace = WhiteSpace.Normal;

			prestigeButton = AddButton(foldPage, "idle-button idle-button--strong", Prestige);
		}

		// ── 그리기 ──────────────────────────────────────────────────────────

		public void Render(IdleSnapshot snapshot)
		{
			if (stageLabel == null)
			{
				return;
			}

			bool atCeiling = snapshot.MaxTierNow >= snapshot.TierCeiling;

			stageLabel.text = string.Format("{0}단계 · 등급 {1}/{2}{3}",
				snapshot.Stage, snapshot.MaxTierNow, snapshot.TierCeiling,
				atCeiling ? " (천장)" : string.Empty);

			resourceLabel.text = string.Format("{0}  ({1}/초)",
				BigNumberText.Format(resourceRolling), BigNumberText.Format(snapshot.IncomePerSecond));

			guideLabel.text = NextStep(snapshot);

			RenderArena(snapshot);
			RenderBaseLive(snapshot);
			RenderVaultLive(snapshot);

			RenderBasePage(snapshot);
			RenderUpgradePage(snapshot);
			RenderGearPage(snapshot);
			RenderHeroPage(snapshot);
			RenderFoldPage(snapshot);
		}

		/// <summary>
		/// 코어가 고른 <b>한 걸음</b>을 사람 말로 옮긴다.
		///
		/// ★ 고르는 것은 규칙이라 코어(<see cref="IdleAdvice"/>)가 한다 — 화면이 고르면
		///   창마다 다른 답을 내고, 그건 같은 판이 다르게 보이는 것이다.
		///   여기서는 <b>말로 옮기는 일</b>만 한다.
		/// </summary>
		private string NextStep(IdleSnapshot snapshot)
		{
			IdleAdviceResult advice = IdleAdvice.NextStep(snapshot);

			switch (advice.Step)
			{
				case IdleStep.CatchVisitor:
					return "▶ 판 위에 뭔가 지나간다 — 누르면 잠시 폭주한다";

				case IdleStep.BagFull:
					return "▶ 가방이 꽉 찼다 — 합치거나 차야 새 장비가 들어온다 (감정용 개수는 계속 쌓인다)";

				case IdleStep.Prestige:
					return string.Format("▶ 환생할 때다 — 지금 환생하면 환생석 {0} (등급 천장도 오른다)",
						(long)advice.Amount);

				case IdleStep.Pull:
					return "▶ 영웅 탭 — 뽑을 수 있다 (안 내보내도 들고만 있으면 세진다)";

				case IdleStep.Merge:
					return "▶ 장비 탭 — 같은 것 셋을 한 단계 위로 합칠 수 있다";

				case IdleStep.BuyProducer:
					return string.Format("▶ 기지 탭 — {0}번 생산자를 살 수 있다 (수입 +{1:P0})",
						advice.Subject + 1, advice.Amount - 1d);

				case IdleStep.Raise:
					return "▶ 강화 탭 — 올릴 수 있다";

				case IdleStep.Tap:
					return "▶ 판을 눌러 때린다 — 지금은 손이 제일 빠르다";

				default:
					return advice.Amount > 0d && double.IsInfinity(advice.Amount) == false
						? string.Format("· 모으는 중 — {0} 뒤에 살 것이 생긴다 (눌러서 앞당길 수 있다)",
							DescribeSpan(advice.Amount))
						: "· 모으는 중 — 눌러서 앞당길 수 있다";
			}
		}

		private void RenderArena(IdleSnapshot snapshot)
		{
			RenderPartyOnField(snapshot);
			RenderVisitor(snapshot);

			targetShape.Sides = SidesFor(snapshot.MaxTierNow);
			targetShape.Body = TierColor(snapshot.MaxTierNow);
			targetShape.Fill = (float)snapshot.TargetHealthRatio;

			healthBar.value = (float)snapshot.TargetHealthRatio;
			healthBar.title = string.Format("{0:P0}", snapshot.TargetHealthRatio);

			DrawKillDots(snapshot);

			// ★ 「지금 얼마나 빨리 치나」를 글자로도 준다 — 올린 게 숫자로도 보여야 한다.
			arenaCaption.text = string.Format("눌러서 한 대 더 · 초당 {0}대 · {1}",
				BigNumberText.Format(snapshot.AttacksPerSecond),
				snapshot.HoldingStage ? "여기 머무는 중 — 많이 떨군다" : "계속 내려가는 중 — 좋은 게 떨어진다");
		}

		/// <summary>
		/// 지나가는 것과 폭주를 그린다.
		///
		/// ★ 떠 있는 동안 <b>판을 가로질러 흐른다</b> — 가만히 있으면 버튼이지 사건이 아니다.
		/// ★ 사라지기 직전에는 <b>깜빡인다</b> — 「놓치겠다」가 보여야 손이 간다.
		/// </summary>
		private void RenderVisitor(IdleSnapshot snapshot)
		{
			bool here = snapshot.VisitorSecondsLeft > 0d;
			visitor.style.display = here ? DisplayStyle.Flex : DisplayStyle.None;

			if (here)
			{
				// 남은 시간이 줄수록 왼쪽에서 오른쪽으로 간다 — 가는 길이 곧 남은 시간이다.
				float stayed = 1f - (float)(snapshot.VisitorSecondsLeft / 13d);
				visitor.style.left = new StyleLength(new Length(6f + stayed * 82f, LengthUnit.Percent));

				bool leaving = snapshot.VisitorSecondsLeft < 4d;
				visitor.SetPulse(leaving ? 0.22f : 0.08f, leaving ? 4f : 1.2f);
			}

			bool surging = snapshot.SurgeKind != IdleSurgeKind.None && snapshot.SurgeSecondsLeft > 0d;

			if (surging)
			{
				surgeLabel.style.display = DisplayStyle.Flex;
				surgeLabel.text = string.Format("{0}!  {1:0.0}초",
					IdleSurge.NameOf(snapshot.SurgeKind), snapshot.SurgeSecondsLeft);
			}
			else
			{
				surgeLabel.style.display = DisplayStyle.None;
			}

			// ★ <b>판 전체가 달라져야</b> 폭주다 — 글자만 바뀌면 그건 알림이지 사건이 아니다.
			//   바닥 격자가 금빛으로 달아오르고 빨리 흐른다. 끝나면 원래대로 돌아온다.
			backdrop.Line = surging
				? new Color(0.98f, 0.84f, 0.38f, 0.16f)
				: new Color(1f, 1f, 1f, 0.035f);
			backdrop.DriftPerSecond = surging ? 26f : 3f;

			// 남은 시간이 짧아질수록 빨리 뛴다 — 끝나 가는 게 몸으로 느껴져야 한다.
			targetShape.SetPulse(surging ? 0.10f : 0f,
				surging ? 1.5f + (float)(1d / (snapshot.SurgeSecondsLeft + 0.5d)) * 6f : 0f);
		}

		/// <summary>지나가는 것을 잡았다.</summary>
		private void OnVisitorClicked(PointerDownEvent moment)
		{
			// 판을 누른 것으로도 세지 않게 막는다 — 잡기와 때리기는 다른 일이다.
			moment.StopPropagation();

			if (session.TryCatchVisitor(out IdleSurgeKind caught) == false)
			{
				return;
			}

			sound.Sweep();
			Shake(0.8f);
			burst.Fire(12, new Color(0.98f, 0.84f, 0.38f));
			floats.Pop(IdleSurge.NameOf(caught) + "!", new Vector2(50f, 50f),
				new Color(0.98f, 0.84f, 0.38f));

			Render(session.Capture());
		}

		private void RenderPartyOnField(IdleSnapshot snapshot)
		{
			for (int slot = 0; slot < heroes.Count; slot++)
			{
				int id = slot < snapshot.Party.Length ? snapshot.Party[slot] : -1;
				IdleHeroView? found = FindHero(snapshot, id);

				if (found.HasValue)
				{
					heroes[slot].Sides = found.Value.Sides;
					heroes[slot].Body = GradeColor(found.Value.Grade);
					heroes[slot].style.opacity = 1f;

					// ★ 이 붙을수록 크게 선다 — 키운 것이 눈에 보여야 키울 맛이 난다.
					float grown = 1f + Mathf.Min(0.6f, found.Value.Stars * 0.12f);
					heroes[slot].style.scale = new StyleScale(new Scale(new Vector2(grown, grown)));
				}
				else
				{
					heroes[slot].Sides = HERO_SIDES[slot % HERO_SIDES.Length];
					heroes[slot].Body = HERO_COLORS[slot % HERO_COLORS.Length];
					heroes[slot].style.opacity = 0.22f;
					heroes[slot].style.scale = new StyleScale(new Scale(Vector2.one));
				}
			}
		}

		private void RenderBaseLive(IdleSnapshot snapshot)
		{
			baseLiveLabel.text = string.Format("저장고 — 초당 {0}",
				BigNumberText.Format(snapshot.IncomePerSecond));

			// 들어오는 게 있으면 저장고가 뛴다.
			vaultMark.SetPulse(snapshot.IncomePerSecond > 0d ? 0.08f : 0f, 1.2f);
			vaultMark.Advance(Time.unscaledDeltaTime, 0.03f);

			for (int kind = 0; kind < baseShapes.Count; kind++)
			{
				if (kind >= snapshot.Producers.Length)
				{
					continue;
				}

				IdleProducerView view = snapshot.Producers[kind];
				bool working = view.Owned > 0L;

				// 안 산 것은 <b>있다는 것만</b> 보인다 — 없으면 다음 목표가 안 보이고,
				// 진하면 산 것과 구별이 안 된다.
				baseShapes[kind].style.opacity = working ? 1f : 0.16f;
				baseShapes[kind].SetPulse(working ? 0.12f : 0f,
					working ? 0.6f + Mathf.Min(2f, (float)view.Owned * 0.08f) : 0f);
				baseShapes[kind].Advance(Time.unscaledDeltaTime, working ? 0.06f : 0f);
			}
		}

		private void RenderVaultLive(IdleSnapshot snapshot)
		{
			int mergeable = IdleAdvice.MergeableCount(snapshot);

			// ★ 가방이 차면 <b>새 장비가 안 들어온다</b>(IdleGear.Stow 가 자리 없으면 0 을 돌려준다).
			//   조용하면 안 된다 — 사람은 잃고 있다는 걸 모른 채 계속 잡는다.
			// ⚠ 다만 <b>감정용 개수</b>(DroppedByTier)는 계속 쌓인다. 처음엔 「버려진다」고 적었는데
			//   그건 과장이었다 — 급하게 보이려고 사실을 부풀리면 그 다음 경고까지 안 믿게 된다.
			bool full = snapshot.Bag.Length >= snapshot.BagCapacity;

			vaultLabel.text = full
				? string.Format("가방 {0}/{1}  ⚠ 꽉 찼다 — 새 장비가 안 들어온다 (감정용 개수는 계속 쌓인다)",
					snapshot.Bag.Length, snapshot.BagCapacity)
				: string.Format("가방 {0}/{1}{2}",
					snapshot.Bag.Length, snapshot.BagCapacity,
					mergeable > 0 ? string.Format(" · 합칠 수 있는 묶음 {0}", mergeable) : string.Empty);

			vaultLabel.EnableInClassList("idle-warn", full);

			for (int index = 0; index < vaultCells.Count; index++)
			{
				NgonElement cell = vaultCells[index];

				if (index >= snapshot.BagCapacity)
				{
					cell.style.display = DisplayStyle.None;
					continue;
				}

				cell.style.display = DisplayStyle.Flex;

				if (index >= snapshot.Bag.Length)
				{
					cell.style.opacity = 0.10f;
					cell.SetPulse(0f, 0f);
					continue;
				}

				IdleItem one = snapshot.Bag[index];
				cell.style.opacity = 1f;
				cell.Sides = SidesFor(one.Tier);
				cell.Body = TierColor(one.Tier);

				// 감정된 것은 <b>뛴다</b> — 가방에서 골라 낼 때 눈에 먼저 든다.
				cell.SetPulse(one.PotentialValue > 0d ? 0.14f : 0f, 1.4f);
				cell.Advance(Time.unscaledDeltaTime, 0.04f);
			}
		}

		private void DrawKillDots(IdleSnapshot snapshot)
		{
			VisualElement dots = killDots.Count > 0 ? killDots[0].parent : null;
			if (dots == null)
			{
				dots = arenaCaption.parent.Q(className: "idle-kills-dots");
			}

			if (dots == null)
			{
				return;
			}

			if (killDots.Count != snapshot.KillsPerStage)
			{
				dots.Clear();
				killDots.Clear();

				for (int one = 0; one < snapshot.KillsPerStage; one++)
				{
					VisualElement dot = new VisualElement();
					dot.AddToClassList("idle-dot");
					dots.Add(dot);
					killDots.Add(dot);
				}
			}

			for (int index = 0; index < killDots.Count; index++)
			{
				killDots[index].EnableInClassList("idle-dot--done", index < snapshot.KillsInStage);
			}
		}

		private void RenderBasePage(IdleSnapshot snapshot)
		{
			baseSummary.text = string.Format("기지가 초당 {0} 를 낸다 — 자원은 여기서만 나온다",
				BigNumberText.Format(snapshot.IncomePerSecond));

			for (int kind = 0; kind < producerButtons.Count; kind++)
			{
				Button button = producerButtons[kind];

				if (kind >= snapshot.Producers.Length)
				{
					button.parent.style.display = DisplayStyle.None;
					continue;
				}

				IdleProducerView view = snapshot.Producers[kind];

				// 아직 이른 것은 숨긴다 — 처음부터 여덟 줄이면 뭘 할지가 안 보인다.
				// 줄 전체(도형 포함)를 숨긴다 — 버튼만 숨기면 도형이 혼자 남는다.
				button.parent.style.display = view.Hidden ? DisplayStyle.None : DisplayStyle.Flex;

				// 「지금 얼마 내나」 옆에 <b>사면 얼마나 좋아지나</b>와 <b>언제 살 수 있나</b>를 같이.
				button.text = string.Format("{0} {1}   x{2}  ·  초당 {3}   —   {4}{5}{6}",
					ShapeMark(kind + 1),
					kind + 1,
					view.Owned,
					BigNumberText.Format(view.OutputTotal),
					BigNumberText.Format(view.NextCost),
					GainMark(view.IncomeGain),
					WaitMark(view.SecondsToAfford));

				button.SetEnabled(view.CanAfford);
				button.EnableInClassList("idle-button--ready", view.CanAfford);

				// ★ 아직 못 사는 <b>다음</b> 것은 회색으로 남는다 — 목표가 안 보이면 모을 이유도 없다.
				button.EnableInClassList("idle-button--locked", view.CanAfford == false && view.Owned <= 0L);

				if (kind < producerShapes.Count)
				{
					bool working = view.Owned > 0L;
					producerShapes[kind].SetPulse(working ? 0.10f : 0f,
						working ? 0.6f + Mathf.Min(2f, (float)view.Owned * 0.08f) : 0f);
				}
			}
		}

		private void RenderUpgradePage(IdleSnapshot snapshot)
		{
			DrawUpgrade(snapshot.Damage, damageTitle, damageValue, damageButton, "공격력", "한 방 {0}");
			DrawUpgrade(snapshot.AttackSpeed, speedTitle, speedValue, speedButton, "공격속도", "초당 {0}회");

			bool canRetreat = snapshot.Stage > snapshot.BestFarmingStage;
			retreatButton.text = canRetreat
				? string.Format("◀ {0}단계로 물러나 번다", snapshot.BestFarmingStage)
				: string.Format("▶ 가장 깊은 {0}단계로", snapshot.BestStage);
			retreatButton.SetEnabled(snapshot.Stage != (canRetreat ? snapshot.BestFarmingStage : snapshot.BestStage));

			holdButton.text = snapshot.HoldingStage ? "⏸ 여기 머무는 중" : "▽ 계속 내려가는 중";
		}

		private void RenderGearPage(IdleSnapshot snapshot)
		{
			RenderWorn(snapshot);
			RenderBag(snapshot);
			RenderMerge(snapshot);

			if (appraiseButtons.Count != snapshot.DroppedByTier.Length)
			{
				RebuildDropRows(snapshot.DroppedByTier.Length);
			}

			potentialLabel.text = snapshot.BestPotentialValue > 0d
				? string.Format("잠재 {0} {1:P1}", NameOf(snapshot.BestPotentialGrade), snapshot.BestPotentialValue)
				: "잠재 없음 — 2등급부터 감정할 수 있다";

			for (int tier = 1; tier <= appraiseButtons.Count; tier++)
			{
				long count = snapshot.DroppedByTier[tier - 1];
				// ★ 감정 값을 <b>버튼에 적는다</b> — 자원이 든다는 게 안 보이면 두 층이 물린 줄 모른다.
				double cost = IdleGear.AppraiseCost(tier, session.Tuning);

				// 못 누르는 이유를 버튼이 직접 말한다 — 회색만 되면 「고장인가」로 읽힌다.
				bool tooLow = tier < 2;
				bool nothingToAppraise = count <= 0L;
				bool tooPoor = snapshot.Resource < cost;

				appraiseButtons[tier - 1].text = tooLow
					? string.Format("{0}{1}  {2}개 — 잠재 없음 (2등급부터)",
						ShapeMark(tier), tier, BigNumberText.Format(count))
					: nothingToAppraise
						? string.Format("{0}{1}  아직 안 떨어졌다", ShapeMark(tier), tier)
						: tooPoor
							? string.Format("{0}{1}  {2}개 — 자원 {3} 이 모자란다", ShapeMark(tier), tier,
								BigNumberText.Format(count), BigNumberText.Format(cost))
							: string.Format("{0}{1}  {2}개 — 감정 {3} ({4})", ShapeMark(tier), tier,
								BigNumberText.Format(count), BigNumberText.Format(cost),
								NameOf(IdlePotentials.GradeFor(tier)));

				appraiseButtons[tier - 1].SetEnabled(tooLow == false && nothingToAppraise == false && tooPoor == false);
			}
		}

		/// <summary>차고 있는 넷 — 부위마다 올리는 축이 다르다.</summary>
		private void RenderWorn(IdleSnapshot snapshot)
		{
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			string[] names = { "머리(공격력)", "몸(기지)", "손(속도)", "발(떨구기)" };

			for (int slot = 0; slot < snapshot.Worn.Length && slot < names.Length; slot++)
			{
				IdleItem one = snapshot.Worn[slot];
				text.Append(names[slot]).Append(" ");

				if (one.IsEmpty)
				{
					text.AppendLine("— 비어 있음");
					continue;
				}

				text.Append(ShapeMark(one.Tier)).Append(one.Tier);
				if (one.PotentialValue > 0d)
				{
					text.AppendFormat("  {0} {1:P1}", NameOf(one.Grade), one.PotentialValue);
				}

				text.AppendLine();
			}

			wornLabel.text = text.ToString().TrimEnd();
		}

		/// <summary>가방 — 눌러서 찬다. 칸이 차면 더 안 들어온다(그게 정리하라는 신호다).</summary>
		private void RenderBag(IdleSnapshot snapshot)
		{
			if (bagButtons.Count != snapshot.Bag.Length)
			{
				bagRows.Clear();
				bagButtons.Clear();
				bagShapes.Clear();

				for (int index = 0; index < snapshot.Bag.Length; index++)
				{
					int captured = index;
					bagButtons.Add(AddShapeRow(bagRows, snapshot.Bag[index].Tier, () => Equip(captured),
						out NgonElement shape));
					bagShapes.Add(shape);
				}
			}

			string[] slots = { "머리", "몸", "손", "발" };

			for (int index = 0; index < bagButtons.Count; index++)
			{
				IdleItem one = snapshot.Bag[index];
				if (index < bagShapes.Count)
				{
					bagShapes[index].Sides = SidesFor(one.Tier);
					bagShapes[index].Body = TierColor(one.Tier);
				}

				bagButtons[index].text = string.Format("{0}{1} {2}{3}",
					ShapeMark(one.Tier), one.Tier,
					slots[(int)one.Slot],
					one.PotentialValue > 0d ? string.Format("  {0:P1}", one.PotentialValue) : string.Empty);
			}
		}

		/// <summary>합칠 수 있는 조합만 보여준다.</summary>
		private void RenderMerge(IdleSnapshot snapshot)
		{
			int[] counts = new int[64];
			string[] slots = { "머리", "몸", "손", "발" };

			for (int index = 0; index < snapshot.Bag.Length; index++)
			{
				IdleItem one = snapshot.Bag[index];
				int key = one.Tier * 4 + (int)one.Slot;
				if (key >= 0 && key < counts.Length)
				{
					counts[key]++;
				}
			}

			List<string> labels = new List<string>();
			List<int> tiers = new List<int>();
			List<IdleItemSlot> which = new List<IdleItemSlot>();

			for (int key = 0; key < counts.Length; key++)
			{
				if (counts[key] < 3)
				{
					continue;
				}

				int tier = key / 4;
				IdleItemSlot slot = (IdleItemSlot)(key % 4);

				labels.Add(string.Format("{0}{1} {2} x{3} → {4}{5}",
					ShapeMark(tier), tier, slots[(int)slot], counts[key], ShapeMark(tier + 1), tier + 1));
				tiers.Add(tier);
				which.Add(slot);
			}

			if (mergeButtons.Count != labels.Count)
			{
				mergeRows.Clear();
				mergeButtons.Clear();

				for (int index = 0; index < labels.Count; index++)
				{
					int tier = tiers[index];
					IdleItemSlot slot = which[index];
					mergeButtons.Add(AddButton(mergeRows, "idle-button", () => Merge(tier, slot)));
				}
			}

			for (int index = 0; index < mergeButtons.Count && index < labels.Count; index++)
			{
				mergeButtons[index].text = labels[index];
			}
		}

		private void RenderHeroPage(IdleSnapshot snapshot)
		{
			// ★ 못 누르는 버튼은 <b>왜</b> 못 누르는지 말해야 한다. 값 둘을 같이 내는 자리라
			//   그냥 회색이면 사람은 「고장인가」로 읽는다 — 어느 쪽이 모자란지 짚는다.
			bool noStones = snapshot.Stones < snapshot.PullStoneCost;

			pullButton.text = snapshot.CanPull
				? string.Format("영웅 뽑기 — 자원 {0} + 환생석 {1}   (가진 돌 {2})",
					BigNumberText.Format(snapshot.PullCost), snapshot.PullStoneCost, snapshot.Stones)
				: noStones
					? string.Format("영웅 뽑기 — 환생석이 없다 (환생하면 생긴다 · 가진 돌 {0})",
						snapshot.Stones)
					: string.Format("영웅 뽑기 — 자원 {0} 이 모자란다 (돌 {1} 개 있음)",
						BigNumberText.Format(snapshot.PullCost), snapshot.Stones);

			pullButton.SetEnabled(snapshot.CanPull);
			pullButton.EnableInClassList("idle-button--ready", snapshot.CanPull);
			pullButton.EnableInClassList("idle-button--locked", snapshot.CanPull == false);

			// 아직 한 명도 없으면 숫자만 늘어놓는 게 아니라 <무엇을 하는 곳인지>를 말한다.
			codexLabel.text = snapshot.Heroes.Length > 0
				? string.Format("도감 {0}점 · 판 전체 x{1:0.00}  ·  천장까지 {2}번{3}",
					snapshot.CodexScore, snapshot.CodexMultiplier, snapshot.PullsToPity, Waiting())
				: string.Format("도감 — 뽑은 영웅은 여기 쌓인다. 들고만 있어도 판이 세지고,"
					+ " 셋만 내보낸다 (천장까지 {0}번)", snapshot.PullsToPity);

			RenderParty(snapshot);
			RenderCodex(snapshot);
		}

		/// <summary>지금 무엇을 기다리는가 — 반쯤 고른 상태를 화면이 말해 준다.</summary>
		private string Waiting()
		{
			if (seatBeingFilled >= 0)
			{
				return "   ▶ 아래에서 누구를 앉힐지 고른다";
			}

			if (pendingHeroId >= 0)
			{
				return "   ▶ 위에서 어느 자리와 바꿀지 고른다";
			}

			return string.Empty;
		}

		/// <summary>내보낸 셋 — 빈 자리는 「비었다」로 두어 채울 것이 있다는 게 보이게.</summary>
		private void RenderParty(IdleSnapshot snapshot)
		{
			for (int slot = 0; slot < partyButtons.Count; slot++)
			{
				int id = slot < snapshot.Party.Length ? snapshot.Party[slot] : -1;
				IdleHeroView? found = FindHero(snapshot, id);

				if (found.HasValue)
				{
					IdleHeroView view = found.Value;
					partyShapes[slot].Sides = view.Sides;
					partyShapes[slot].Body = GradeColor(view.Grade);
					partyShapes[slot].style.opacity = 1f;
					partyButtons[slot].text = string.Format("{0}{1} · {2}",
						view.Name, Stars(view.Stars), IdleHeroes.NameOfAxis(view.Axis));
				}
				else
				{
					partyShapes[slot].style.opacity = 0.15f;

					// ★ 아직 한 명도 없으면 「비었다」는 <막다른 길>이다 — 채우는 길을 알려준다.
					//   영웅은 환생석으로 뽑고, 환생석은 환생에서 나온다.
					partyButtons[slot].text = snapshot.Heroes.Length > 0
						? "비었다"
						: "뽑으면 여기 선다";
				}

				partyButtons[slot].EnableInClassList("idle-button--ready", seatBeingFilled == slot);
			}
		}

		/// <summary>도감 — 가진 얼굴 전부. 내보낸 것은 표시하고, 안 내보낸 것도 몫을 적는다.</summary>
		private void RenderCodex(IdleSnapshot snapshot)
		{
			if (heroButtons.Count != snapshot.Heroes.Length)
			{
				heroRows.Clear();
				heroButtons.Clear();
				heroShapes.Clear();

				for (int index = 0; index < snapshot.Heroes.Length; index++)
				{
					int capturedId = snapshot.Heroes[index].Id;
					heroButtons.Add(AddShapeRow(heroRows, 1, () => ChooseHero(capturedId),
						out NgonElement shape));
					heroShapes.Add(shape);
				}
			}

			for (int index = 0; index < heroButtons.Count && index < snapshot.Heroes.Length; index++)
			{
				IdleHeroView view = snapshot.Heroes[index];

				heroShapes[index].Sides = view.Sides;
				heroShapes[index].Body = GradeColor(view.Grade);

				heroButtons[index].text = string.Format("{0}{1}  {2}·{3}   보유 +{4:P0}   ({5}/{6}){7}",
					view.Name,
					Stars(view.Stars),
					IdleHeroes.NameOfGrade(view.Grade),
					IdleHeroes.NameOfAxis(view.Axis),
					view.OwnedShare,
					view.Copies,
					view.CopiesForNextStar,
					view.InParty ? "  ▶출전" : string.Empty);

				heroButtons[index].EnableInClassList("idle-button--ready",
					seatBeingFilled >= 0 || view.Id == pendingHeroId);
			}
		}

		private static IdleHeroView? FindHero(IdleSnapshot snapshot, int id)
		{
			if (id < 0)
			{
				return null;
			}

			for (int index = 0; index < snapshot.Heroes.Length; index++)
			{
				if (snapshot.Heroes[index].Id == id)
				{
					return snapshot.Heroes[index];
				}
			}

			return null;
		}

		/// <summary>★ 을 글자로 — 숫자보다 눈이 먼저 센다.</summary>
		private static string Stars(int stars)
		{
			if (stars <= 0)
			{
				return string.Empty;
			}

			return " " + new string('★', stars);
		}

		/// <summary>등급 색 — 위 등급일수록 따뜻하게. 변의 수(축)와 겹치지 않는 축이다.</summary>
		private static Color GradeColor(IdleHeroGrade grade)
		{
			switch (grade)
			{
				case IdleHeroGrade.Rare: return new Color(0.46f, 0.80f, 0.72f);
				case IdleHeroGrade.Epic: return new Color(0.72f, 0.58f, 0.92f);
				case IdleHeroGrade.Legend: return new Color(0.95f, 0.72f, 0.36f);
				default: return new Color(0.68f, 0.72f, 0.80f);
			}
		}

		private void RenderFoldPage(IdleSnapshot snapshot)
		{
			// ★ 두 가지를 <b>갈라 부른다</b>. 재화를 가른 뒤에도 화면이 둘 다 「환생석」이라
			//   불러서, 배수 재료와 쓰는 돌이 같은 것처럼 보였다(내가 만든 혼동).
			//     환생 점수 = 여태 가장 깊이 간 자리 → 배수·천장·자리비움. <b>안 줄어든다</b>
			//     환생석    = 쓰는 것 → 영웅 뽑기
			foldSummary.text = string.Format(
				"환생 점수 {0} (배수 {1}) · 가진 환생석 {2}\n자리 비워도 되는 시간 {3}\n\n"
					+ "환생하면 — 단계·자원·강화는 잃고, 점수·환생석·장비·영웅·도감은 남는다.\n"
					+ "점수는 여태 가장 깊이 간 자리라 줄지 않는다 — 돌을 다 써도 판은 안 약해진다.",
				snapshot.PrestigePoints,
				BigNumberText.Format(snapshot.PrestigeMultiplier),
				snapshot.Stones,
				DescribeSpan(snapshot.MaxOfflineSeconds));

			prestigeButton.text = snapshot.PrestigeAward > 0L
				? string.Format("환생한다 — 환생석 {0} 을 받는다", snapshot.PrestigeAward)
				: "환생한다 — 더 내려가야 한다";
			prestigeButton.SetEnabled(snapshot.PrestigeAward > 0L);
		}

		// ── 의도 ────────────────────────────────────────────────────────────

		private void Send(IdleUpgradeKind kind)
		{
			if (session.Send(new IdleRaiseUpgradeIntent(kind)))
			{
				sound.Click();
				Shake(0.3f);

				// 올린 것이 <b>판에서</b> 반응한다 — 목록만 바뀌면 뭐가 세졌는지 안 보인다.
				for (int index = 0; index < heroes.Count; index++)
				{
					heroes[index].Hit();
				}
			}

			Render(session.Capture());
		}

		private void Retreat()
		{
			IdleSnapshot now = session.Capture();
			int target = now.Stage > now.BestFarmingStage ? now.BestFarmingStage : now.BestStage;

			session.Send(new IdleGoToStageIntent(target));
			WriteDown();
			Render(session.Capture());
		}

		private void BuyProducer(int kind)
		{
			if (session.Send(new IdleBuyProducerIntent(kind)))
			{
				sound.Click();

				// 산 것이 <b>반응한다</b> — 눌렀는데 아무 일도 안 일어나면 눌린 줄 모른다.
				if (kind < producerShapes.Count)
				{
					producerShapes[kind].Hit();
				}

				// 실황에서도 같이 튄다 — 목록과 판이 같은 것을 가리켜야 이어진다.
				if (kind < baseShapes.Count)
				{
					baseShapes[kind].Hit();
					baseMotes.Send(new Vector2((kind + 0.5f) / 8f, 0.86f), new Vector2(0.5f, 0.20f),
						TierColor(kind + 1), SidesFor(kind + 1), 0.5f);
				}
			}

			Render(session.Capture());
		}

		private void ToggleHold()
		{
			session.Send(new IdleHoldStageIntent(session.State.HoldingStage == false));
			WriteDown();
			Render(session.Capture());
		}

		private void Appraise(int tier)
		{
			if (session.TryAppraise(tier, out PotentialRoll roll))
			{
				if (roll.Replaced)
				{
					sound.Good();
					Shake(0.5f);
				}
				else
				{
					sound.Click();
				}

				SayOnce(rollNote, string.Format("◆{0} 감정 → {1} {2:P1}{3}",
					roll.Tier, NameOf(roll.Grade), roll.Value, roll.Replaced ? "   ★ 갈아 끼웠다" : string.Empty));
				WriteDown();
			}

			Render(session.Capture());
		}

		private void Equip(int bagIndex)
		{
			session.Send(new IdleEquipIntent(bagIndex));
			WriteDown();
			Render(session.Capture());
		}

		private void Merge(int tier, IdleItemSlot slot)
		{
			if (session.Send(new IdleMergeIntent(tier, slot)))
			{
				burst.Fire(SidesFor(tier + 1), TierColor(tier + 1));
				sound.Good();
				Shake(0.7f);

				SayOnce(rollNote, string.Format("{0}{1} 셋을 합쳐 {2}{3} 하나 — 잠재는 사라졌다",
					ShapeMark(tier), tier, ShapeMark(tier + 1), tier + 1));
				WriteDown();
			}

			Render(session.Capture());
		}

		/// <summary>뽑는다 — 결과에 따라 소리와 흔들림을 다르게 준다.</summary>
		private void Pull()
		{
			if (session.TryPull(out IdleHeroPull got) == false)
			{
				return;
			}

			IdleHeroKind kind = IdleHeroes.KindOf(got.Id);

			burst.Fire(kind.Sides, GradeColor(got.Grade));

			// 큰 것이 나왔을 때만 크게 — 매번 크게 울리면 큰 것이 안 커진다.
			if (got.Grade >= IdleHeroGrade.Epic || got.IsNew)
			{
				sound.Good();
				Shake(got.Grade == IdleHeroGrade.Legend ? 1f : 0.5f);
			}
			else
			{
				sound.Click();
				Shake(0.2f);
			}

			SayOnce(pullNote, string.Format("{0} {1}{2}{3}{4}",
				IdleHeroes.NameOfGrade(got.Grade),
				kind.Name,
				got.IsNew ? "  ★ 처음 본 얼굴" : string.Empty,
				got.StarredUp ? string.Format("  ★ {0}성이 됐다", got.Stars) : string.Empty,
				got.ByPity ? "  (천장)" : string.Empty));

			floats.Pop(kind.Name, new Vector2(Random.Range(40f, 100f), 60f), GradeColor(got.Grade));

			WriteDown();
			Render(session.Capture());
		}

		/// <summary>
		/// 자리를 눌렀다 — 영웅을 <b>이미 골라 뒀으면</b> 바로 앉히고, 아니면 고를 차례로 넘어간다.
		/// </summary>
		private void BeginSeat(int slot)
		{
			sound.Click();

			if (pendingHeroId >= 0)
			{
				Seat(slot, pendingHeroId);
				return;
			}

			// 같은 자리를 다시 누르면 무른다 — 잘못 눌렀을 때 빠져나갈 길이 있어야 한다.
			seatBeingFilled = seatBeingFilled == slot ? -1 : slot;
			Render(session.Capture());
		}

		/// <summary>
		/// 도감에서 영웅을 눌렀다.
		///
		/// ★ 세 갈래 다 <b>무언가는 일어난다</b>: 자리를 고르는 중이면 거기 앉히고,
		///   아니면 빈 자리에 앉히고, 자리가 다 찼으면 「어느 자리와 바꿀까」로 넘어간다.
		///   「아무 일도 안 일어남」이 없어야 고장으로 안 읽힌다.
		/// </summary>
		private void ChooseHero(int id)
		{
			sound.Click();

			if (seatBeingFilled >= 0)
			{
				Seat(seatBeingFilled, id);
				return;
			}

			int empty = FirstEmptySeat();
			if (empty >= 0)
			{
				Seat(empty, id);
				return;
			}

			// 자리가 다 찼다 — 이제 <b>어느 자리를 내보낼지</b>가 결정이다.
			pendingHeroId = pendingHeroId == id ? -1 : id;
			Render(session.Capture());
		}

		private void Seat(int slot, int id)
		{
			if (session.Send(new IdleSetPartyIntent(slot, id)))
			{
				Shake(0.2f);
			}

			seatBeingFilled = -1;
			pendingHeroId = -1;
			WriteDown();
			Render(session.Capture());
		}

		/// <summary>빈 자리 하나 — 없으면 -1.</summary>
		private int FirstEmptySeat()
		{
			IdleSnapshot now = session.Capture();

			for (int slot = 0; slot < now.Party.Length; slot++)
			{
				if (now.Party[slot] < 0)
				{
					return slot;
				}
			}

			return -1;
		}

		private void Prestige()
		{
			if (session.Send(new IdlePrestigeIntent()))
			{
				sound.Sweep();
				Shake(1f);

				lastKills = session.State.Kills;
				lastBagCount = session.State.Bag.Count;
				WriteDown();
			}

			Render(session.Capture());
		}

		// ── 잔손 ────────────────────────────────────────────────────────────

		private void RebuildDropRows(int tierCount)
		{
			dropRows.Clear();
			appraiseButtons.Clear();

			for (int tier = 1; tier <= tierCount; tier++)
			{
				int captured = tier;
				appraiseButtons.Add(AddButton(dropRows, "idle-button", () => Appraise(captured)));
			}
		}

		private static VisualElement AddPage(VisualElement parent)
		{
			VisualElement page = new VisualElement();
			page.AddToClassList("idle-page");
			parent.Add(page);
			return page;
		}

		private static Label AddLabel(VisualElement parent, string className)
		{
			Label label = new Label(string.Empty);
			label.AddToClassList(className);
			parent.Add(label);
			return label;
		}

		/// <summary>
		/// 도형이 붙은 줄 — 왼쪽에 <b>변의 수 = 등급</b>인 도형, 오른쪽에 누를 것.
		///
		/// ★ 글자로만 두면 판에 도는 도형과 목록이 <b>다른 언어</b>가 된다.
		///   같은 규칙을 두 군데서 쓰면 한 번 배우고 계속 읽는다.
		/// </summary>
		private Button AddShapeRow(VisualElement parent, int tier, System.Action action,
			out NgonElement shape)
		{
			VisualElement row = new VisualElement();
			row.AddToClassList("idle-shape-row");
			parent.Add(row);

			shape = new NgonElement();
			shape.AddToClassList("idle-row-shape");
			shape.Sides = SidesFor(tier);
			shape.Body = TierColor(tier);
			row.Add(shape);
			decor.Add(shape);

			Button button = new Button(action);
			button.AddToClassList("idle-button");
			button.AddToClassList("idle-row-button");
			row.Add(button);

			return button;
		}

		private static Button AddButton(VisualElement parent, string classNames, System.Action action)
		{
			Button button = new Button(action);
			foreach (string one in classNames.Split(' '))
			{
				button.AddToClassList(one);
			}

			parent.Add(button);
			return button;
		}

		private static void AddDivider(VisualElement parent)
		{
			VisualElement line = new VisualElement();
			line.AddToClassList("idle-divider");
			parent.Add(line);
		}

		/// <summary>
		/// 올리기 한 줄 — <b>사면 얼마나 좋아지나</b>와 <b>언제 살 수 있나</b>를 같이 적는다.
		///
		/// ★ 조사에서 「이해 지원」으로 꼽힌 자리다: 값만 보이면 누르는 게 도박이 되고,
		///   그러면 다른 시스템(영웅·장비·폭주)의 재미도 <b>체감이 안 된다</b>.
		/// </summary>
		private static void DrawUpgrade(IdleUpgradeView view, Label title, Label value, Button button,
			string name, string valueFormat)
		{
			title.text = string.Format("{0}  Lv.{1}", name, view.Level);

			value.text = view.IsMaxed
				? string.Format(valueFormat, BigNumberText.Format(view.CurrentValue))
				: string.Format(valueFormat + "  →  {1}  (+{2:P0})",
					BigNumberText.Format(view.CurrentValue),
					BigNumberText.Format(view.NextValue),
					view.CurrentValue > 0d ? view.NextValue / view.CurrentValue - 1d : 0d);

			button.text = view.IsMaxed
				? "최대"
				: string.Format("올리기 — {0}{1}",
					BigNumberText.Format(view.NextCost),
					WaitMark(view.SecondsToAfford));
			button.SetEnabled(view.CanAfford);
			button.EnableInClassList("idle-button--ready", view.CanAfford);
			button.EnableInClassList("idle-button--locked", view.CanAfford == false && view.IsMaxed == false);
		}

		/// <summary>사면 판 전체가 몇 배가 되나 — 안 변하면 아무 말도 안 한다.</summary>
		private static string GainMark(double gain)
		{
			if (double.IsInfinity(gain))
			{
				return "   (첫 수입)";
			}

			if (gain <= 1.0001d)
			{
				return string.Empty;
			}

			return string.Format("   (수입 +{0:P0})", gain - 1d);
		}

		/// <summary>
		/// 얼마나 기다려야 하나 — 이미 살 수 있으면 아무 말도 안 한다.
		///
		/// ★ 「언제 살 수 있나」가 보여야 <b>기다릴지 다른 걸 할지</b>가 결정이 된다.
		///   아주 멀면 숫자 대신 「한참」이라고 적는다 — 87,231초는 정보가 아니다.
		/// </summary>
		private static string WaitMark(double seconds)
		{
			if (seconds <= 0d)
			{
				return string.Empty;
			}

			if (double.IsInfinity(seconds) || seconds > 86400d)
			{
				return "   (한참 걸린다)";
			}

			return "   (" + DescribeSpan(seconds) + " 뒤)";
		}

		/// <summary>변의 수로 등급을 적는다 — 도형과 같은 규칙을 글자에도.</summary>
		private static string ShapeMark(int tier)
		{
			switch (tier)
			{
				case 1: return "△";
				case 2: return "◇";
				case 3: return "⬠";
				case 4: return "⬡";
				default: return "◍";
			}
		}

		/// <summary>
		/// 등급을 <b>변의 수</b>로 옮긴다 — 1등급 삼각형 … 8등급 십각형.
		///
		/// ★ 이 뜻은 <b>게임 층 것</b>이다. 그리는 부품(<see cref="NgonElement"/>)은 변의 수만 안다.
		///   같은 부품을 다른 게임이 다른 뜻으로 쓸 수 있어야 한 저장소에 여럿이 산다.
		/// </summary>
		private static int SidesFor(int tier)
		{
			int sides = tier + 2;
			return sides < 3 ? 3 : sides;
		}

		/// <summary>등급마다 색이 달라진다 — 변을 세기 전에 색으로 먼저 눈치챈다.</summary>
		private static Color TierColor(int tier)
		{
			float hue = Mathf.Repeat(0.58f + (tier - 1) * 0.085f, 1f);
			return Color.HSVToRGB(hue, 0.45f, 0.92f);
		}

		private static string NameOf(PotentialGrade grade)
		{
			switch (grade)
			{
				case PotentialGrade.Rare: return "레어";
				case PotentialGrade.Epic: return "에픽";
				case PotentialGrade.Unique: return "유니크";
				case PotentialGrade.Legendary: return "레전드리";
				default: return "없음";
			}
		}

		private static string DescribeSpan(double seconds)
		{
			if (seconds < 60d)
			{
				return string.Format("{0:N0}초", seconds);
			}

			if (seconds < 3600d)
			{
				return string.Format("{0:N0}분", seconds / 60d);
			}

			return string.Format("{0:N1}시간", seconds / 3600d);
		}
	}
}
