using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 대결 축 v0 감독 (TASK-WM-411) — 판을 짓고, 라운드를 돌리고, 진 쪽에게 카드를 내민다.
	/// 씬을 미리 만들어 두지 않고 <b>코드가 판을 짓는다</b>: v0 가 답할 질문은 「친구랑 한 판 더?」 하나뿐이라
	/// 프리팹·머티리얼·씬 작업이 답을 앞당기지 못한다. 재미가 확인되면 그때 정식 씬·아트로 승격한다.
	/// 규칙 판정은 전부 <see cref="VersusMatchCore"/>(엔진 밖에서 도는 순수 코드) — 여기는 몸과 손만 담당.
	/// </summary>
	public sealed class VersusMatchDirector : MonoBehaviour
	{
		[SerializeField] private float arenaHalfWidth = 13f;
		[SerializeField] private float arenaHalfDepth = 9f;
		[SerializeField] private int randomSeed = 0;
		// 판 전체 수치 = 인스펙터에서 돌린다(하드코딩 0). 기본값은 VersusTuning.Default() 와 같게 맞춰 둔다.
		[SerializeField] private VersusTuning tuning = VersusTuning.Default();
		// 친구가 없을 때도 판을 돌려 보려고 2P 를 봇으로 둔다. 사람 둘이 모이면 끄면 된다.
		[SerializeField] private bool secondPlayerIsBot = true;
		[SerializeField] private VersusBotTuning botTuning = VersusBotTuning.Default();

		private VersusMatchCore match;
		private VersusArena arena;
		private VersusRules rules;
		private readonly VersusFighter[] fighters = new VersusFighter[VersusMatchCore.PLAYER_COUNT];
		private readonly IVersusInput[] inputs = new IVersusInput[VersusMatchCore.PLAYER_COUNT];
		private readonly List<VersusProjectile> projectiles = new List<VersusProjectile>();
		private float roundClock;
		private float intermissionClock;
		private int offerCursor;
		private bool roundRunning;
		private bool cursorLatched;

		private void Awake()
		{
			rules = VersusRules.Default();
			match = new VersusMatchCore(rules, randomSeed != 0 ? randomSeed : Random.Range(1, int.MaxValue));

			BuildArena();
			BuildCamera();

			// 몸을 먼저 짓는다 — 봇 손이 상대의 Transform 을 알아야 하기 때문.
			for (int playerIndex = 0; playerIndex < VersusMatchCore.PLAYER_COUNT; playerIndex++)
				fighters[playerIndex] = BuildFighter(playerIndex);

			inputs[0] = VersusInputScheme.CreatePlayerOne();
			inputs[1] = secondPlayerIsBot
				? (IVersusInput)new VersusBotInput(fighters[1].transform, fighters[0].transform, arena, botTuning)
				: VersusInputScheme.CreatePlayerTwo();

			for (int playerIndex = 0; playerIndex < VersusMatchCore.PLAYER_COUNT; playerIndex++)
				fighters[playerIndex].Initialize(playerIndex, arena, inputs[playerIndex], tuning);

			StartRound();
		}

		private void OnDestroy()
		{
			for (int playerIndex = 0; playerIndex < inputs.Length; playerIndex++)
			{
				if (inputs[playerIndex] != null)
					inputs[playerIndex].Dispose();
			}
		}

		private void Update()
		{
			float deltaTime = Time.deltaTime;

			if (roundRunning)
			{
				TickRound(deltaTime);
				return;
			}

			if (match.DraftingPlayerIndex != VersusMatchCore.NO_WINNER)
			{
				TickDraft();
				return;
			}

			if (match.IsConcluded)
				return;

			intermissionClock -= deltaTime;
			if (intermissionClock <= 0f)
				StartRound();
		}

		// ── 라운드 ──────────────────────────────────────────────────────────────

		private void StartRound()
		{
			ClearProjectiles();
			roundClock = 0f;
			roundRunning = true;

			for (int playerIndex = 0; playerIndex < VersusMatchCore.PLAYER_COUNT; playerIndex++)
				fighters[playerIndex].BeginRound(match.StatsOf(playerIndex));
		}

		private void TickRound(float deltaTime)
		{
			roundClock += deltaTime;

			for (int playerIndex = 0; playerIndex < VersusMatchCore.PLAYER_COUNT; playerIndex++)
				inputs[playerIndex].Tick(deltaTime);

			WarnBotOfIncomingShots();

			for (int playerIndex = 0; playerIndex < VersusMatchCore.PLAYER_COUNT; playerIndex++)
			{
				if (fighters[playerIndex].Tick(deltaTime, out Vector3 fireDirection))
					Fire(playerIndex, fireDirection);
			}

			ResolveHits();

			bool firstAlive = fighters[0].IsAlive;
			bool secondAlive = fighters[1].IsAlive;

			if (firstAlive && secondAlive)
			{
				if (rules.RoundTimeLimitSeconds > 0f && roundClock >= rules.RoundTimeLimitSeconds)
					EndRound(VersusMatchCore.NO_WINNER);

				return;
			}

			// 동시사 = 무승부. 즉사제에서 실제로 자주 나므로 「먼저 죽은 쪽」을 억지로 가르지 않는다.
			if (firstAlive == false && secondAlive == false)
			{
				EndRound(VersusMatchCore.NO_WINNER);
				return;
			}

			EndRound(firstAlive ? 0 : 1);
		}

		private void EndRound(int winnerIndex)
		{
			roundRunning = false;
			offerCursor = 0;
			intermissionClock = tuning.IntermissionSeconds;
			ClearProjectiles();
			match.ResolveRound(winnerIndex);
		}

		// ── 카드 고르기 ─────────────────────────────────────────────────────────
		// 진 쪽이 자기 조작으로 고른다(좌우 = 커서, 발사 = 확정). 마우스를 쓰면 패드 2P 가 못 고른다.

		private void TickDraft()
		{
			IVersusInput input = inputs[match.DraftingPlayerIndex];

			// 봇은 고민하지 않는다 — 아무거나 집고 다음 판으로. 사람을 기다리게 두면 판이 멈춘다.
			if (input is VersusBotInput)
			{
				match.TakeOffered(Random.Range(0, match.PendingOffer.Count));
				intermissionClock = tuning.IntermissionSeconds;
				return;
			}

			Vector2 move = input.ReadMove();

			if (Mathf.Abs(move.x) > 0.6f)
			{
				if (cursorLatched == false)
				{
					offerCursor = Mathf.Clamp(offerCursor + (move.x > 0f ? 1 : -1), 0, match.PendingOffer.Count - 1);
					cursorLatched = true;
				}
			}
			else
			{
				cursorLatched = false;
			}

			if (input.WasFirePressedThisFrame)
			{
				match.TakeOffered(offerCursor);
				intermissionClock = tuning.IntermissionSeconds;
			}
		}

		// 봇이 탄을 스스로 뒤지게 만들면 「봇이 왜 저러나」가 봇 코드 안으로 숨는다. 감독이 알려 준다.
		private void WarnBotOfIncomingShots()
		{
			for (int playerIndex = 0; playerIndex < VersusMatchCore.PLAYER_COUNT; playerIndex++)
			{
				VersusBotInput bot = inputs[playerIndex] as VersusBotInput;

				if (bot == null || fighters[playerIndex].IsAlive == false)
					continue;

				for (int index = 0; index < projectiles.Count; index++)
				{
					VersusProjectile projectile = projectiles[index];

					if (projectile == null || projectile.OwnerIndex == playerIndex)
						continue;

					if (projectile.Overlaps(fighters[playerIndex].transform.position, botTuning.DodgeRadius))
					{
						bot.NotifyIncoming();
						break;
					}
				}
			}
		}

		// ── 탄 ────────────────────────────────────────────────────────────────

		private void Fire(int playerIndex, Vector3 direction)
		{
			VersusFighter shooter = fighters[playerIndex];
			VersusFighterStats stats = shooter.Stats;
			int count = stats.ProjectileCount;
			float spreadDegrees = count > 1 ? tuning.ProjectileSpreadDegrees : 0f;
			float startDegrees = -spreadDegrees * (count - 1) * 0.5f;

			for (int index = 0; index < count; index++)
			{
				Vector3 rotated = Quaternion.Euler(0f, startDegrees + spreadDegrees * index, 0f) * direction;
				GameObject shot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				shot.name = "VersusProjectile";
				Destroy(shot.GetComponent<Collider>());
				shot.transform.SetParent(transform, false);
				shot.transform.position = shooter.transform.position + rotated.normalized * (shooter.Radius + 0.4f);
				shot.GetComponent<Renderer>().material.color = playerIndex == 0 ? new Color(0.45f, 0.8f, 1f) : new Color(1f, 0.6f, 0.4f);

				VersusProjectile projectile = shot.AddComponent<VersusProjectile>();
				projectile.Launch(arena, tuning, playerIndex, rotated, stats);
				projectiles.Add(projectile);
			}
		}

		private void ResolveHits()
		{
			for (int index = projectiles.Count - 1; index >= 0; index--)
			{
				VersusProjectile projectile = projectiles[index];

				if (projectile == null)
				{
					projectiles.RemoveAt(index);
					continue;
				}

				for (int playerIndex = 0; playerIndex < VersusMatchCore.PLAYER_COUNT; playerIndex++)
				{
					VersusFighter fighter = fighters[playerIndex];

					if (fighter.IsAlive == false || projectile.CanHit(playerIndex) == false)
						continue;

					if (projectile.Overlaps(fighter.transform.position, fighter.Radius) == false)
						continue;

					fighter.TakeHit();
					Destroy(projectile.gameObject);
					projectiles.RemoveAt(index);
					break;
				}
			}
		}

		private void ClearProjectiles()
		{
			for (int index = 0; index < projectiles.Count; index++)
			{
				if (projectiles[index] != null)
					Destroy(projectiles[index].gameObject);
			}

			projectiles.Clear();
		}

		// ── 판 짓기 ────────────────────────────────────────────────────────────

		private void BuildArena()
		{
			arena = gameObject.AddComponent<VersusArena>();
			arena.Configure(arenaHalfWidth, arenaHalfDepth);

			GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
			floor.name = "VersusFloor";
			floor.transform.SetParent(transform, false);
			floor.transform.localScale = new Vector3(arenaHalfWidth * 2f, 0.2f, arenaHalfDepth * 2f);
			floor.transform.position = new Vector3(0f, -0.1f, 0f);
			floor.GetComponent<Renderer>().material.color = new Color(0.16f, 0.17f, 0.21f);

			BuildWall(new Vector3(0f, 0.5f, arenaHalfDepth), new Vector3(arenaHalfWidth * 2f, 1f, 0.4f));
			BuildWall(new Vector3(0f, 0.5f, -arenaHalfDepth), new Vector3(arenaHalfWidth * 2f, 1f, 0.4f));
			BuildWall(new Vector3(arenaHalfWidth, 0.5f, 0f), new Vector3(0.4f, 1f, arenaHalfDepth * 2f));
			BuildWall(new Vector3(-arenaHalfWidth, 0.5f, 0f), new Vector3(0.4f, 1f, arenaHalfDepth * 2f));
		}

		private void BuildWall(Vector3 position, Vector3 scale)
		{
			GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
			wall.name = "VersusWall";
			wall.transform.SetParent(transform, false);
			wall.transform.position = position;
			wall.transform.localScale = scale;
			wall.GetComponent<Renderer>().material.color = new Color(0.30f, 0.31f, 0.38f);
		}

		// 쿼터뷰 고정 — 화면분할 없이 둘 다 한 화면에 담는 것이 이 축의 카메라 결정(2026-08-16).
		private void BuildCamera()
		{
			GameObject cameraObject = new GameObject("VersusCamera");
			cameraObject.transform.SetParent(transform, false);
			cameraObject.transform.position = new Vector3(0f, 24f, -16f);
			cameraObject.transform.rotation = Quaternion.Euler(56f, 0f, 0f);

			Camera camera = cameraObject.AddComponent<Camera>();
			camera.backgroundColor = new Color(0.07f, 0.07f, 0.09f);
			camera.clearFlags = CameraClearFlags.SolidColor;

			GameObject lightObject = new GameObject("VersusLight");
			lightObject.transform.SetParent(transform, false);
			lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
			Light light = lightObject.AddComponent<Light>();
			light.type = LightType.Directional;
			light.intensity = 1.1f;
		}

		private VersusFighter BuildFighter(int playerIndex)
		{
			GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
			body.name = "VersusFighter" + (playerIndex + 1);
			Destroy(body.GetComponent<Collider>());
			body.transform.SetParent(transform, false);
			body.GetComponent<Renderer>().material.color = playerIndex == 0 ? new Color(0.35f, 0.65f, 1f) : new Color(1f, 0.5f, 0.35f);

			return body.AddComponent<VersusFighter>();
		}

		// ── v0 전용 화면 표시 ───────────────────────────────────────────────────
		// IMGUI = **프로토타입 한정**. 재미가 확인돼 축이 살아남으면 UI Toolkit + USS 로 승격한다
		// (WitchMendokusai/CLAUDE.md § 코드로 짓는 UIToolkit). 지금 UI 에 시간을 쓰면 답만 늦어진다.
		private void OnGUI()
		{
			GUI.Label(new Rect(20f, 16f, 400f, 26f),
				"1P " + match.ScoreOf(0) + "  —  " + match.ScoreOf(1) + " 2P     (" + rules.RoundsToWin + "선승)");
			GUI.Label(new Rect(20f, 40f, 700f, 26f),
				"1P = WASD / F 발사 / G 대시          2P = 방향키 / RCtrl 발사 / RShift 대시");

			if (match.IsConcluded)
			{
				GUI.Label(new Rect(20f, 80f, 400f, 30f), "승자 = " + (match.WinnerIndex == 0 ? "1P" : "2P"));
				return;
			}

			if (match.DraftingPlayerIndex == VersusMatchCore.NO_WINNER)
				return;

			GUI.Label(new Rect(20f, 80f, 700f, 26f),
				(match.DraftingPlayerIndex == 0 ? "1P" : "2P") + " 가 카드를 고른다 — 좌우로 커서, 발사로 확정");

			for (int index = 0; index < match.PendingOffer.Count; index++)
			{
				VersusCardKind card = match.PendingOffer[index];
				string mark = index == offerCursor ? "▶ " : "    ";
				GUI.Label(new Rect(20f, 108f + index * 24f, 700f, 24f),
					mark + card + " — " + VersusCards.Describe(card));
			}
		}
	}
}
