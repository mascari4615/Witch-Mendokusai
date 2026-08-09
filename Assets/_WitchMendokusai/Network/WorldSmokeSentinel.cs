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

		private string resultPath;
		private float waited;
		private bool finished;

		// ── 「놀 수 있나」 재기 (TASK-WM-217) ──────────────────────────────
		// 남을 본 뒤에도 끝내지 않고 <b>루프 한 바퀴</b>를 돌아 본다: 걸어가 줍고 → 넣고 → 완성.
		// 붙는 것만 재면 「접속은 되는데 아무것도 못 하는」 세계를 초록으로 통과시킨다.
		private bool sawOther;
		private int gatheredItemId;
		private int gatheredAmount;
		private bool brewed;
		private int completedItemId;
		private float stepCooldown;

		// 상자 왕복 — 지은 상자에 넣고 그대로 다시 꺼내 본다(같이 노는 알맹이).
		private const int CHEST_BUILDING_ID = 4005;
		private bool chestPlaced;
		private bool chestFilled;
		private int chestSeenAmount;

		// ★ 두 판이 같은 자리에 지으면 한쪽은 영영 상자가 없다 — 각자 <b>자기가 선 자리</b>에 짓는다.
		private int chestX;
		private int chestZ;

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

			// 스모크는 사람이 버튼을 안 누른다 — 파수꾼이 직접 세계로 들어간다.
			WorldDoor.Enter();
		}

		private void Update()
		{
			if (finished)
				return;

			waited += Time.unscaledDeltaTime;

			IWorldLink link = WorldDoor.Current;
			if (link != null && link.IsLinked && link.Dolls != null && link.Dolls.Length >= 2)
				sawOther = true;

			// 남을 봤으면 이제 <b>놀아 본다</b> — 줍고, 넣고, 가져간다.
			if (sawOther && link != null && link.IsLinked)
			{
				PlayOneRound(link);

				if (completedItemId != 0)
				{
					Write("pass", link.Dolls.Length, link, "played one round");
					return;
				}
			}

			if (waited < DEADLINE_SECONDS)
				return;

			int seen = link?.Dolls?.Length ?? 0;
			string why = link == null ? "no link"
				: sawOther == false ? "nobody else"
				: gatheredItemId == 0 ? "could not gather"
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

			if (gatheredItemId == 0)
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
			if (taken != null && taken.itemId != 0)
				completedItemId = taken.itemId;
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

			GatherableView nearest = alive[0];
			float best = float.MaxValue;
			for (int i = 0; i < alive.Length; i++)
			{
				float dx = alive[i].x - meX;
				float dz = alive[i].z - meZ;
				float distance = dx * dx + dz * dz;
				if (distance >= best)
					continue;

				best = distance;
				nearest = alive[i];
			}

			if (best <= 2.0f * 2.0f)
			{
				link.RequestGather(nearest.id);
				gatheredItemId = nearest.itemId;
				gatheredAmount = nearest.amount;
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
				"gathered=", gatheredItemId.ToString(CultureInfo.InvariantCulture), "\n",
				"gatheredAmount=", gatheredAmount.ToString(CultureInfo.InvariantCulture), "\n",
				"potion=", completedItemId.ToString(CultureInfo.InvariantCulture), "\n",
				// 세계가 준 열쇠 — 다음 판이 「같은 사람」으로 들어오려면 이걸 물려받아야 한다.
				"secret=", WorldKeyStore.LastGranted, "\n",
				// 상자에 넣고 다시 꺼내 봤나 — 0 이면 나눔이 안 도는 세계다.
				"chest=", chestSeenAmount.ToString(CultureInfo.InvariantCulture), "\n",
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
		}
	}
}
