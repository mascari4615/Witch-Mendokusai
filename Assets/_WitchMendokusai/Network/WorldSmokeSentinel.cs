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
	public sealed class WorldSmokeSentinel : MonoBehaviour
	{
		/// <summary>이만큼 기다려도 남이 안 보이면 실패로 적는다 (초).</summary>
		private const float DEADLINE_SECONDS = 60f;

		/// <summary>줍기까지 걸어갈 때 한 번에 보내는 걸음 사이 간격 (초).</summary>
		private const float STEP_SECONDS = 0.1f;

		/// <summary>다 놀았어도 이만큼은 세계에 머문다 — 늦게 온 사람과 겹칠 시간(초).</summary>
		private const float LINGER_SECONDS = 12f;

		private string resultPath;
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
		/// 모을 나무 — 상자 한 채(2개)를 짓고도 <b>상자에 넣어 볼 것</b>이 남아야 한다.
		/// 딱 2개만 모으면 짓는 순간 빈손이 되어 그 다음 걸음이 전부 막힌다(실측 2026-08-10).
		/// </summary>
		private const int WOOD_NEEDED = 4;
		private int gatheredWood;
		private int gatheredAmount;
		private bool brewed;
		private int completedItemId;
		private bool completed;
		private float stepCooldown;

		// 상자 왕복 — 지은 상자에 넣고 그대로 다시 꺼내 본다(같이 노는 알맹이).
		private const int CHEST_BUILDING_ID = 4005;
		private bool chestPlaced;
		private bool chestFilled;
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

				// ★ 솥은 <b>하나</b>고 완성은 선착순이다 (실측 2026-08-10) — 남이 먼저 가져가면
				//   내 재료로 만든 것도 내 것이 아니다. 그건 규칙대로이지 고장이 아니므로,
				//   「줍고·상자까지 됐다」면 물약 없이도 논 것으로 센다.
				if (completed == false && chestSeenAmount != 0 && waited >= LINGER_SECONDS)
				{
					Write("pass", link.Dolls.Length, link, "played but potion went to someone else");
					return;
				}

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
			string why = link == null ? "no link"
				: gathered == false ? "could not gather"
				: chestSeenAmount == 0 ? "chest did not take it"
				: brewed == false ? "could not brew"
				: "no potion";

			Write("fail", seen, link, why);
		}

		/// <summary>루프 한 바퀴 — 가장 가까운 것으로 걸어가 줍고, 그걸 솥에 넣고, 완성을 가져간다.</summary>
		private void PlayOneRound(IWorldLink link)
		{
			stepCooldown -= Time.unscaledDeltaTime;
			if (stepCooldown > 0f)
				return;

			stepCooldown = STEP_SECONDS;

			if (gathered == false)
			{
				WalkAndGather(link);
				return;
			}

			// 주운 것으로 먼저 상자 왕복을 해 본다 — 넣고, 그대로 다시 꺼낸다.
			// ★ 두 판이 같은 자리에 지으면 한쪽은 영영 상자가 없다 — 각자 자기가 선 자리에 짓는다.
			if (chestPlaced == false)
			{
				WhereIStand(link, out float standX, out float standZ);
				chestX = Mathf.RoundToInt(standX);
				chestZ = Mathf.RoundToInt(standZ);
				link.RequestPlace(chestX, 0, chestZ, CHEST_BUILDING_ID);
				chestPlaced = true;
				return;
			}

			if (chestFilled == false)
			{
				link.RequestChestPut(chestX, 0, chestZ, gatheredItemId, 1);
				chestFilled = true;
				return;
			}

			if (chestSeenAmount == 0)
			{
				// 상자가 정말 받았나 — 받았으면 도로 꺼내 가방으로 되돌린다.
				if (link.Chest != null && link.Chest.items != null && link.Chest.items.Length > 0)
				{
					chestSeenAmount = link.Chest.items[0].amount;
					link.RequestChestTake(chestX, 0, chestZ, gatheredItemId, chestSeenAmount);
					return;
				}

				link.RequestChest(chestX, 0, chestZ);
				return;
			}

			if (brewed == false)
			{
				link.RequestBrewStep(gatheredItemId);
				brewed = true;
				return;
			}

			// 완성은 세계가 내준다 — 못 받으면 다음 걸음에 다시 청한다(남이 먼저 가져갔을 수도 있다).
			link.RequestBrewComplete();

			WorldBrewView taken = link.TakeCompletedBrew();
			// 완성했나 — 여기도 번호가 아니라 「받았나」로 본다(0번 물건도 진짜 결과다).
			if (taken != null && taken.recipe != null && taken.grade > 0)
			{
				completedItemId = taken.itemId;
				completed = true;
			}
		}

		/// <summary>지난 판이 상자에 넣어 둔 것이 아직 있나 — 자리는 환경변수로 받는다.</summary>
		private void CheckKept(IWorldLink link)
		{
			stepCooldown -= Time.unscaledDeltaTime;
			if (stepCooldown > 0f)
				return;

			stepCooldown = STEP_SECONDS;

			int cellX = ReadNumber("WM_WORLD_SMOKE_CHEST_X");
			int cellZ = ReadNumber("WM_WORLD_SMOKE_CHEST_Z");

			if (link.Chest != null && link.Chest.items != null && link.Chest.items.Length > 0)
			{
				chestSeenAmount = link.Chest.items[0].amount;
				gatheredItemId = link.Chest.items[0].itemId;
				return;
			}

			link.RequestChest(cellX, 0, cellZ);
		}

		private static int ReadNumber(string name)
		{
			string raw = Environment.GetEnvironmentVariable(name);
			return int.TryParse(raw, out int value) ? value : 0;
		}

		/// <summary>내가 지금 선 자리 — 없으면 원점.</summary>
		private static void WhereIStand(IWorldLink link, out float x, out float z)
		{
			x = 0f;
			z = 0f;
			WorldDollView[] dolls = link.Dolls;
			for (int i = 0; i < dolls.Length; i++)
			{
				if (dolls[i].id != link.MyDollId)
					continue;

				x = dolls[i].x;
				z = dolls[i].z;
				return;
			}
		}

		private void WalkAndGather(IWorldLink link)
		{
			GatherableView[] alive = link.Gatherables;
			if (alive == null || alive.Length == 0)
				return;

			// 내가 선 자리 — 걸음은 「이쪽으로」라 지금 자리에서 뺀 방향을 보내야 한다.
			float meX = 0f;
			float meZ = 0f;
			WorldDollView[] dolls = link.Dolls;
			for (int i = 0; i < dolls.Length; i++)
			{
				if (dolls[i].id != link.MyDollId)
					continue;

				meX = dolls[i].x;
				meZ = dolls[i].z;
				break;
			}

			// ★ 아무거나 줍지 않는다 (TASK-WM-217): 짓기에 <b>나무</b>가 드는데 철광석만 주우면
			//   상자를 못 지어 관문이 그 자리에서 죽는다. 아직 재료가 모자라면 나무부터 찾는다.
			bool needWood = gatheredWood < WOOD_NEEDED;

			GatherableView nearest = null;
			float best = float.MaxValue;
			for (int i = 0; i < alive.Length; i++)
			{
				if (needWood && alive[i].itemId != WorldSeeds.WOOD)
					continue;

				float dx = alive[i].x - meX;
				float dz = alive[i].z - meZ;
				float distance = dx * dx + dz * dz;
				if (distance >= best)
					continue;

				best = distance;
				nearest = alive[i];
			}

			if (nearest == null)
				return;

			if (best <= 2.0f * 2.0f)
			{
				link.RequestGather(nearest.id);
				gatheredItemId = nearest.itemId;
				gatheredAmount = nearest.amount;

				if (nearest.itemId == WorldSeeds.WOOD)
					gatheredWood += nearest.amount;

				// 지을 재료(나무)가 찼을 때 비로소 「주웠다」 — 그전엔 계속 나무를 찾는다.
				gathered = gatheredWood >= WOOD_NEEDED;
				return;
			}

			link.RequestMove(nearest.x - meX, nearest.z - meZ);
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
