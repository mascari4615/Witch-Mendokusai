using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 대결 판을 <b>보여 주는</b> 쪽 (TASK-WM-411). 규칙은 하나도 없다 —
	/// 판정은 전부 <see cref="VersusRoundState"/>·<see cref="VersusMatchCore"/>(엔진 0, 서버·웹과 같은 한 벌)에 있고
	/// 여기는 ① 입력을 모아 넘기고 ② 결과를 캡슐·구슬로 그린다.
	///
	/// ★ 2컴 온라인이 목표라 이 분리가 필수다: 심판이 서버로 올라가도 이 스크립트는
	///   「상태를 받아 그린다」로 그대로 남는다(지금은 심판이 여기 얹혀 있을 뿐이다).
	/// </summary>
	public sealed class VersusMatchDirector : MonoBehaviour
	{
		[SerializeField] private float arenaHalfWidth = VersusDuelSim.ARENA_HALF_WIDTH;
		[SerializeField] private float arenaHalfDepth = VersusDuelSim.ARENA_HALF_DEPTH;
		[SerializeField] private int randomSeed = 0;
		[SerializeField] private VersusTuning tuning = VersusTuning.Default();
		[SerializeField] private VersusBotTuning botTuning = VersusBotTuning.Default();
		// 친구가 없을 때 혼자 굴려 보려고 상대를 봇으로 둔다. 온라인이 붙으면 이 자리에 「네트워크 손」이 온다.
		[SerializeField] private bool opponentIsBot = true;

		private VersusMatchCore match;
		private VersusRoundState round;
		private VersusRules rules;
		private Camera viewCamera;
		private readonly IVersusInput[] hands = new IVersusInput[VersusRoundState.PLAYER_COUNT];
		private readonly VersusInputFrame[] frames = new VersusInputFrame[VersusRoundState.PLAYER_COUNT];
		private readonly Transform[] bodies = new Transform[VersusRoundState.PLAYER_COUNT];
		private readonly List<Transform> shotViews = new List<Transform>();
		private readonly List<VersusBodyView> shotBuffer = new List<VersusBodyView>();
		private float intermissionClock;
		private int offerCursor;
		private bool cursorLatched;
		private float tickAccumulator;

		private void Awake()
		{
			rules = VersusRules.Default();
			match = new VersusMatchCore(rules, randomSeed != 0 ? randomSeed : Random.Range(1, int.MaxValue));

			BuildArena();
			viewCamera = BuildCamera();

			for (int index = 0; index < VersusRoundState.PLAYER_COUNT; index++)
				bodies[index] = BuildBody(index);

			hands[0] = new VersusLocalInput(viewCamera);
			hands[1] = opponentIsBot
				? (IVersusInput)new VersusBotHand(botTuning, arenaHalfWidth, arenaHalfDepth, Random.Range(1, int.MaxValue))
				: new VersusLocalInput(viewCamera);

			StartRound();
		}

		private void Update()
		{
			if (round != null && round.IsOver == false)
			{
				// 판정은 60Hz 고정 틱이다 — 화면이 몇 프레임이든 같은 답이 나와야 서버와 갈리지 않는다.
				tickAccumulator += Time.deltaTime;

				while (tickAccumulator >= VersusRoundState.TICK && round.IsOver == false)
				{
					tickAccumulator -= VersusRoundState.TICK;

					for (int index = 0; index < VersusRoundState.PLAYER_COUNT; index++)
						frames[index] = hands[index].Read(round, index, VersusRoundState.TICK);

					round.Step(frames, rules.RoundTimeLimitSeconds);
				}

				DrawRound();

				if (round.IsOver)
					EndRound();

				return;
			}

			if (match.DraftingPlayerIndex != VersusMatchCore.NO_WINNER)
			{
				TickDraft();
				return;
			}

			if (match.IsConcluded)
				return;

			intermissionClock -= Time.deltaTime;
			if (intermissionClock <= 0f)
				StartRound();
		}

		// ── 라운드 ──────────────────────────────────────────────────────────────

		private void StartRound()
		{
			round = new VersusRoundState(match.StatsOf(0), match.StatsOf(1), tuning, arenaHalfWidth, arenaHalfDepth,
				new Numerics.Vector2(-arenaHalfWidth * 0.7f, 0f),
				new Numerics.Vector2(arenaHalfWidth * 0.7f, 0f));

			tickAccumulator = 0f;
			offerCursor = 0;

			for (int index = 0; index < VersusRoundState.PLAYER_COUNT; index++)
				bodies[index].gameObject.SetActive(true);

			DrawRound();
		}

		private void EndRound()
		{
			intermissionClock = tuning.IntermissionSeconds;
			match.ResolveRound(round.Winner);
			ClearShotViews();
		}

		private void TickDraft()
		{
			IVersusInput hand = hands[match.DraftingPlayerIndex];

			// 봇은 고민하지 않는다 — 아무거나 집고 다음 판으로. 사람을 기다리게 두면 판이 멈춘다.
			if (hand is VersusBotHand)
			{
				match.TakeOffered(Random.Range(0, match.PendingOffer.Count));
				intermissionClock = tuning.IntermissionSeconds;
				return;
			}

			VersusInputFrame frame = hand.Read(round, match.DraftingPlayerIndex, Time.deltaTime);

			if (Mathf.Abs(frame.Move.x) > 0.6f)
			{
				if (cursorLatched == false)
				{
					offerCursor = Mathf.Clamp(offerCursor + (frame.Move.x > 0f ? 1 : -1), 0, match.PendingOffer.Count - 1);
					cursorLatched = true;
				}
			}
			else
			{
				cursorLatched = false;
			}

			if (frame.Fire)
			{
				match.TakeOffered(offerCursor);
				intermissionClock = tuning.IntermissionSeconds;
			}
		}

		// ── 그리기 ─────────────────────────────────────────────────────────────

		private void DrawRound()
		{
			for (int index = 0; index < VersusRoundState.PLAYER_COUNT; index++)
			{
				Numerics.Vector2 position = round.PositionOf(index);
				bodies[index].position = new Vector3(position.x, 0.5f, position.y);
				bodies[index].localScale = Vector3.one * (round.RadiusOf(index) * 2f);
				bodies[index].gameObject.SetActive(round.IsAlive(index));
			}

			round.CollectShots(shotBuffer);

			while (shotViews.Count < shotBuffer.Count)
				shotViews.Add(BuildShotView());

			for (int index = 0; index < shotViews.Count; index++)
			{
				bool used = index < shotBuffer.Count;
				shotViews[index].gameObject.SetActive(used);

				if (used == false)
					continue;

				shotViews[index].position = new Vector3(shotBuffer[index].Position.x, 0.6f, shotBuffer[index].Position.y);
				shotViews[index].localScale = Vector3.one * (shotBuffer[index].Radius * 2f);
				shotViews[index].GetComponent<Renderer>().material.color =
					shotBuffer[index].Owner == 0 ? new Color(0.45f, 0.8f, 1f) : new Color(1f, 0.6f, 0.4f);
			}
		}

		private void ClearShotViews()
		{
			for (int index = 0; index < shotViews.Count; index++)
				shotViews[index].gameObject.SetActive(false);
		}

		private Transform BuildShotView()
		{
			GameObject shot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			shot.name = "VersusShot";
			Destroy(shot.GetComponent<Collider>());
			shot.transform.SetParent(transform, false);
			return shot.transform;
		}

		private Transform BuildBody(int playerIndex)
		{
			GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
			body.name = "VersusFighter" + (playerIndex + 1);
			Destroy(body.GetComponent<Collider>());
			body.transform.SetParent(transform, false);
			body.GetComponent<Renderer>().material.color =
				playerIndex == 0 ? new Color(0.35f, 0.65f, 1f) : new Color(1f, 0.5f, 0.35f);
			return body.transform;
		}

		private void BuildArena()
		{
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

		// 쿼터뷰 고정 — 둘 다 한 화면에 담는다(2026-08-16 결정).
		private Camera BuildCamera()
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

			return camera;
		}

		// v0 전용 표시. 재미가 확인되면 UI Toolkit + USS 로 승격한다(WitchMendokusai/CLAUDE.md).
		private void OnGUI()
		{
			GUI.Label(new Rect(20f, 16f, 500f, 26f),
				"나 " + match.ScoreOf(0) + "  vs  " + match.ScoreOf(1) + " 상대     (" + rules.RoundsToWin + "선승)");
			GUI.Label(new Rect(20f, 40f, 700f, 26f), "WASD 이동 · 마우스 조준 · 좌클릭 발사 · Space 대시");

			if (match.IsConcluded)
			{
				GUI.Label(new Rect(20f, 80f, 400f, 30f), match.WinnerIndex == 0 ? "내가 이겼다" : "상대가 이겼다");
				return;
			}

			if (match.DraftingPlayerIndex == VersusMatchCore.NO_WINNER)
				return;

			GUI.Label(new Rect(20f, 80f, 700f, 26f), "졌다 - 카드를 고른다 (A/D 커서, 좌클릭 확정)");

			for (int index = 0; index < match.PendingOffer.Count; index++)
			{
				VersusCardKind card = match.PendingOffer[index];
				GUI.Label(new Rect(20f, 108f + index * 24f, 700f, 24f),
					(index == offerCursor ? "> " : "   ") + card + " - " + VersusCards.Describe(card));
			}
		}
	}
}
