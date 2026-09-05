using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using WitchMendokusai.Net;

namespace WitchMendokusai
{
	/// <summary>
	/// 세운 판(빌드)이 <b>세계에 붙어 서로를 보는지</b> 스스로 적어 두는 파수꾼 (TASK-WM-217 단계 4).
	///
	/// ★ 왜 필요한가: FishNet 을 지우려면 그것이 지키던 관문(2-peer 스모크)을 <b>먼저</b> 대신해야 한다.
	///   서버 시험(WS)은 서버 쪽만 본다 — 「진짜 실행 파일이 진짜로 붙어서 남을 본다」는 여기서만 증명된다.
	///
	/// 켜는 법 = 환경변수 <c>WM_WORLD_SMOKE_RESULT</c> 에 적을 파일 경로를 준다(없으면 아무 일도 안 함).
	/// 적는 것 = <c>result=pass|fail</c> · <c>dolls=N</c> · <c>local=true|false</c> · <c>reason=…</c>.
	/// 판정(둘 다 서로를 봤나)은 이 파일 둘을 읽는 러너가 한다 — 창은 <b>본 것만</b> 적는다.
	/// </summary>
	public sealed partial class WorldSmokeSentinel : MonoBehaviour
	{
		/// <summary>
		/// 이만큼 걸려도 한 바퀴를 못 돌면 실패로 적는다 (초).
		///
		/// ★ 60 이었을 때 (실측 2026-08-10): 모을 나무를 9로 올리자 <b>줍는 데만 마감을 다 썼다</b>
		///   (<c>reason=could not gather</c>) — 들판이 넓어 노드 다섯 곳을 걸어 다녀야 한다.
		///   그래서 솥·제작은 <b>시작도 못 해 본 채</b> 「안 됐다」로 적혔고, 나는 그걸 결함으로 읽을 뻔했다.
		///   관문은 「놀 수 있는가」를 재는 것이지 「빨리 노나」를 재는 게 아니다.
		/// </summary>
		private const float DEADLINE_SECONDS = 180f;

		/// <summary>줍기까지 걸어갈 때 한 번에 보내는 걸음 사이 간격 (초).</summary>
		private const float STEP_SECONDS = 0.1f;

		/// <summary>다 놀았어도 이만큼은 세계에 머문다 — 늦게 온 사람과 겹칠 시간(초).</summary>
		private const float LINGER_SECONDS = 12f;

		private string resultPath;

		// 이름을 정했나 · 만들어 봤나 (TASK-WM-217/218) — 관문이 새 기능까지 진짜 판으로 잰다.
		private bool named;
		private bool askedCraft;
		private int craftedItemId;
		private string myName = string.Empty;
		private float waited;
		private bool finished;

		// ── 「놀 수 있나」 재기 (TASK-WM-217) ──────────────────────────────
		// 남을 본 뒤에도 끝내지 않고 <b>루프 한 바퀴</b>를 돌아 본다: 걸어가 줍고 → 넣고 → 완성.
		// 붙는 것만 재면 「접속은 되는데 아무것도 못 하는」 세계를 초록으로 통과시킨다.
		/// <summary>한 번이라도 같이 있던 사람 수(나 포함) — 「둘이 만났나」는 러너가 이 수로 본다.</summary>
		private int mostPeersSeen;
		private int gatheredItemId;

		/// <summary>주웠나 — <b>번호로 판단하지 않는다</b>: 게임의 나무가 0번이라 「못 주웠다」로 읽힌다.</summary>
		private bool gathered;

		/// <summary>
		/// 모을 나무 — 한 바퀴에 드는 것을 <b>다 더한 값</b>이다 (실측 2026-08-10).
		/// 상자 2 + 솥 2 + 솥에 넣을 1 + 제작 한 줄 3 = 8. 여기에 여유 1.
		///
		/// ★ 6 이었을 때: 조리까지는 됐는데 <b>제작만 조용히 실패</b>했다(crafted=0). 재료가 모자라
		///   거절된 것인데, 결과에는 0 만 남아 「제작이 고장났나」로 읽혔다.
		///   딱 2개만 모으던 그 전에는 상자를 짓는 순간 빈손이 되어 그 다음 걸음이 전부 막혔다.
		/// </summary>
		private const int WOOD_NEEDED = 11;
		private int gatheredWood;
		private int gatheredAmount;
		private bool brewed;
		private int potsSeen;
		private int myPotSteps;
		private string craftWhy = string.Empty;
		private bool crafted;
		private int completedItemId;
		private bool completed;
		private float stepCooldown;

		// 상자 왕복 — 지은 상자에 넣고 그대로 다시 꺼내 본다(같이 노는 알맹이).
		private const int CHEST_BUILDING_ID = 4005;
		private bool chestPlaced;
		private bool chestOpened;

		/// <summary>여러 칸 건물 한 채 — 세계가 몇 칸으로 아는지 재려고 짓는다 (2×2 임시 블럭).</summary>
		private const int BIG_BUILDING_ID = 3;
		private bool bigPlaced;
		private int bigWidth;
		private int bigLength;
		private bool bigRemoved;
		private bool bigGone;
		private bool chestFilled;
		private bool potPlaced;
		private int chestSeenAmount;

		// ★ 두 판이 같은 자리에 지으면 한쪽은 영영 상자가 없다 — 각자 <b>자기가 선 자리</b>에 짓는다.
		private int chestX;
		private int chestZ;

		/// <summary>이 판은 놀지 않고 「지난 판이 남긴 것」만 확인한다.</summary>
		private bool checkKeptOnly;

		/// <summary>만든 것을 상자에 두고 끝냈나 — 다음 판이 그걸 확인한다.</summary>
		private bool leftBehind;

		/// <summary>
		/// 파수꾼은 <b>스스로 선다</b> — 스모크 때만(환경변수가 있을 때만).
		/// 씬에 얹어야 켜지는 구조면 「스모크용 씬」이 따로 생기고, 그건 진짜 게임이 아니게 된다.
		/// </summary>
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void StandUp()
		{
			if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WM_WORLD_SMOKE_RESULT")))
				return;

			GameObject holder = new GameObject(nameof(WorldSmokeSentinel));
			DontDestroyOnLoad(holder);
			holder.AddComponent<WorldSmokeSentinel>();
		}

		private void Start()
		{
			resultPath = Environment.GetEnvironmentVariable("WM_WORLD_SMOKE_RESULT");

			// 「껐다 켜도 남나」를 재는 판 — 새로 놀지 않고, 지난 판이 상자에 넣어 둔 것을 확인만 한다.
			checkKeptOnly = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WM_WORLD_SMOKE_KEEP")) == false;

			// 스모크는 사람이 버튼을 안 누른다 — 파수꾼이 직접 세계로 들어간다.
			WorldDoor.Enter();
		}

		private void Update()
		{
			if (finished)
				return;

			waited += Time.unscaledDeltaTime;

			IWorldLink link = WorldDoor.Current;

			// ★ 「만났나」와 「놀 수 있나」는 다른 물음이다 (실측 2026-08-10).
			//   남을 기다렸다 노는 구조였더니 <b>혼자 켠 판은 영영 아무것도 못 했다</b> —
			//   그런데 사람 대부분은 혼자 켠다. 만남은 <b>세어서 적기만</b> 하고, 놀기는 붙자마자 시작한다.
			if (link != null && link.Dolls != null && link.Dolls.Length > mostPeersSeen)
				mostPeersSeen = link.Dolls.Length;

			if (link != null && link.IsLinked && checkKeptOnly)
			{
				CheckKept(link);

				if (chestSeenAmount != 0)
				{
					Write("pass", link.Dolls.Length, link, "kept what was left");
					return;
				}
			}
			else if (link != null && link.IsLinked)
			{
				PlayOneRound(link);

				// 만든 물약을 상자에 <b>두고</b> 끝낸다 — 다음 판이 「껐다 켜도 남나」를 볼 수 있게.
				if (completed && leftBehind == false)
				{
					link.RequestChestPut(chestX, 0, chestZ, completedItemId, 1);
					leftBehind = true;
					return;
				}

				// ★ 「남이 가져갔다」며 12초에 끝내던 양보를 <b>없앴다</b> (2026-08-20, TASK-WM-413).
				//   전제 = 「솥은 하나고 완성은 선착순」. 그건 <b>전역 솥</b> 시절 이야기다 —
				//   지금은 각자 자기 솥을 짓는다. 남이 내 솥의 완성을 가져갈 길이 없다.
				//   그 양보가 나무 줍는 12초 뒤 곧바로 터져서, 파수꾼은 한 바퀴를 <b>끝내 본 적이 없다</b>.
				//   못 끝내면 마감(180초)까지 논다 — 그리고 이유를 재서 적는다(짐작 X).
				//   관문은 300초를 기다리므로 자리는 넉넉하다.

				// ★ 다 놀았어도 <b>잠깐 머문다</b> (실측 2026-08-10): 3초 만에 끝내고 나갔더니
				//   나중에 들어온 판과 한 번도 겹치지 않아 「둘이 만났나」를 잴 수가 없었다.
				//   사람도 볼일 끝내자마자 창을 닫지는 않는다.
				if (leftBehind && waited >= LINGER_SECONDS)
				{
					Write("pass", link.Dolls.Length, link, "played one round");
					return;
				}
			}

			if (waited < DEADLINE_SECONDS)
				return;

			int seen = link?.Dolls?.Length ?? 0;
			// ★ 사다리에 「솥」 칸이 없어서 엉뚱한 이유가 적혔다 (2026-08-20, TASK-WM-413).
			//   pots=0 인데 「남이 가져갔다」로 적히면, 읽는 사람은 <b>세계 탓</b>을 하며
			//   엉뚱한 데를 판다. 이유는 재는 것이지 짐작하는 게 아니다.
			string why = link == null ? "no link"
				: gathered == false ? "could not gather"
				: chestSeenAmount == 0 ? "chest did not take it"
				: potPlaced == false ? "never asked for a cauldron"
				: CountBuildings(link, WorldSim.CAULDRON_BUILDING_ID) == 0 ? "asked for a cauldron but the world never built one"
				: potsSeen == 0 ? "a cauldron stands but the world shows no pot"
				: brewed == false ? "could not brew"
				: "no potion";

			Write("fail", seen, link, why);
		}

		private static int ReadNumber(string name)
		{
			string raw = Environment.GetEnvironmentVariable(name);
			return int.TryParse(raw, out int value) ? value : 0;
		}

		private void Write(string result, int dolls, IWorldLink link, string reason)
		{
			finished = true;

			// 내 안의 세계로 떨어졌으면 그건 「둘이 만났다」가 아니다 — 러너가 구별할 수 있게 적는다.
			bool local = WorldLinkProvider.Instance != null && WorldLinkProvider.Instance.IsLocalWorld;

			string body = string.Concat(
				"result=", result, "\n",
				"dolls=", dolls.ToString(CultureInfo.InvariantCulture), "\n",
				"local=", local ? "true" : "false", "\n",
				"myid=", (link?.MyDollId ?? 0).ToString(CultureInfo.InvariantCulture), "\n",
				// 인형 번호는 접속마다 새로 준다 — 「다시 들어와도 나」는 신원 번호로 확인한다.
				"identity=", (link?.MyIdentityId ?? 0).ToString(CultureInfo.InvariantCulture), "\n",
				// 「놀 수 있나」의 알맹이 — 주웠나·물약을 받았나. 붙기만 하고 아무것도 못 하면 이게 0 이다.
				// 한 번이라도 같이 있던 사람 수(나 포함) — 「둘이 만났나」는 러너가 이 수로 본다.
				"peers=", mostPeersSeen.ToString(CultureInfo.InvariantCulture), "\n",
				// ⚠ 번호로 적으면 안 된다 — 나무가 0번이라 「못 주웠다」로 읽힌다(그 함정을 또 밟았다).
				"gathered=", gathered ? "1" : "0", "\n",
				"gathereditem=", gatheredItemId.ToString(CultureInfo.InvariantCulture), "\n",
				"gatheredAmount=", gatheredAmount.ToString(CultureInfo.InvariantCulture), "\n",
				"potion=", completedItemId.ToString(CultureInfo.InvariantCulture), "\n",
				// 세계가 준 열쇠 — 다음 판이 「같은 사람」으로 들어오려면 이걸 물려받아야 한다.
				"secret=", WorldKeyStore.LastGranted, "\n",
				// 상자에 넣고 다시 꺼내 봤나 — 0 이면 나눔이 안 도는 세계다.
				"chest=", chestSeenAmount.ToString(CultureInfo.InvariantCulture), "\n",
				// 상자를 어디에 지었나 — 다음 판이 그 자리를 찾아가 「남아 있나」를 본다.
				"chestx=", chestX.ToString(CultureInfo.InvariantCulture), "\n",
				"chestz=", chestZ.ToString(CultureInfo.InvariantCulture), "\n",
				// 이름을 정했고 그게 세계의 그림에 실렸나 — 안 실리면 남의 화면에서 나는 영영 「손님」이다.
				"named=", myName, "\n",
				"worldname=", NameInWorld(link), "\n",
				// 제작을 청해 무엇이 나왔나 · 솥은 실제로 섰고 저은 자국이 남았나.
				// potion=0 일 때 <b>어디서 끊겼는지</b>를 남긴다 — 없으면 매번 처음부터 추측한다.
				"crafted=", crafted ? "1" : "0", "\n",
				"crafteditem=", craftedItemId.ToString(CultureInfo.InvariantCulture), "\n",
				"pots=", potsSeen.ToString(CultureInfo.InvariantCulture), "\n",
				// ★ TASK-WM-413: pots=0 이 왜 0 인지를 못 갈랐다 — 「청하지도 않았나」
				//   / 「청했는데 안 섰나」 / 「섰는데 솥이 안 놓였나」 는 서로 다른 결함이다.
				"potrequested=", potPlaced ? "1" : "0", "\n",
				"potbuilt=", CountBuildings(link, WorldSim.CAULDRON_BUILDING_ID).ToString(CultureInfo.InvariantCulture), "\n",
				"buildings=", (link?.Buildings == null ? 0 : link.Buildings.Length).ToString(CultureInfo.InvariantCulture), "\n",
				"potsteps=", myPotSteps.ToString(CultureInfo.InvariantCulture), "\n",
				// 세계의 시각이 <b>흐르고 있나</b> — 혼자 켠 판은 아무도 시계를 안 굴려 멈춰 있었다.
				// 멈추면 들판이 영영 안 자라고 밤낮도 안 바뀐다(실측 2026-08-10).
				"worldminute=", (link?.Time == null ? 0 : (link.Time.hour * 60) + link.Time.minute)
					.ToString(CultureInfo.InvariantCulture), "\n",
				"craftwhy=", craftWhy, "\n",
				// 세계가 마지막으로 「안 된다」고 한 말 — 관문에는 화면이 없어 이게 없으면 이유를 못 본다.
				"denied=", WorldNoticeBridge.LastNotice, "\n",
				// 세계가 그 여러 칸 건물을 몇 칸으로 아나 — 1×1 로 접히면 여기서 드러난다.
				"bigw=", bigWidth.ToString(CultureInfo.InvariantCulture), "\n",
				"bigl=", bigLength.ToString(CultureInfo.InvariantCulture), "\n",
				// 부순 자리가 실제로 비었나 — 안 비면 그 칸에 영영 못 짓는다.
				"biggone=", bigGone ? "1" : "0", "\n",
				"reason=", reason, "\n");

			try
			{
				File.WriteAllText(resultPath, body);
				Debug.Log($"[WORLD-SMOKE] {result} dolls={dolls} local={local} -> {resultPath}");
			}
			catch (IOException error)
			{
				Debug.LogWarning($"[WORLD-SMOKE] 결과를 못 적었다: {error.Message}");
			}

			// ★ 스스로 끝낸다 (실측 2026-08-10): 러너가 밖에서 죽이면 「나갈 때 적어 두는」 훅이 안 돌아
			//   혼자 놀던 세계가 <b>저장 없이</b> 사라진다 — 사람이 창을 닫는 것과 같게 만든다.
			Application.Quit();
		}
	}
}
