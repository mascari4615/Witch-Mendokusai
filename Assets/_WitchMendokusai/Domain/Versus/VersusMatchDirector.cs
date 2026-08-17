using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 대결 판을 <b>보여 주고 조작을 넘기는</b> 쪽 (TASK-WM-411). 규칙은 하나도 없다.
	///
	/// 두 가지로 산다:
	///   · <b>연습</b> — 이 컴퓨터가 심판(<see cref="VersusAuthority"/>)을 돌리고 상대는 봇.
	///   · <b>온라인</b> — 심판은 저쪽(서버 또는 P2P 호스트). 나는 의도를 보내고 그림을 받는다(<see cref="VersusGuest"/>).
	/// 둘의 차이는 「심판이 어디 있나」뿐이고, 규칙 코드는 같은 한 벌이다.
	/// </summary>
	public sealed class VersusMatchDirector : MonoBehaviour
	{
		public enum VersusMode
		{
			/// <summary>혼자 연습 — 내 컴퓨터가 심판, 상대는 봇.</summary>
			Practice = 0,

			/// <summary>온라인 대결 — 심판은 서버(또는 P2P 호스트).</summary>
			Online = 1,

			/// <summary>내가 방을 연다(P2P 호스트) — 서버 없이 친구가 내 주소로 직접 붙는다. 심판은 나.</summary>
			Host = 2,
		}

		[SerializeField] private VersusMode mode = VersusMode.Practice;
		// 기본은 살아 있는 서버 — 친구가 브라우저로도 같은 방에 들어올 수 있다(https://wm.mascari4615.com/versus.html).
		[SerializeField] private string serverUrl = "wss://wm.mascari4615.com/vs";
		[SerializeField] private string roomName = string.Empty;
		[SerializeField] private int hostPort = 57411;
		// 집 밖에서도 받으려면 켠다. 윈도우에서는 관리자 권한이나 urlacl 등록이 필요하다(안 되면 화면에 이유가 뜬다).
		[SerializeField] private bool hostOpenToNetwork = false;
		[SerializeField] private bool fillWithBotIfAlone = true;
		[SerializeField] private float arenaHalfWidth = VersusDuelSim.ARENA_HALF_WIDTH;
		[SerializeField] private float arenaHalfDepth = VersusDuelSim.ARENA_HALF_DEPTH;
		[SerializeField] private int randomSeed = 0;
		[SerializeField] private VersusTuning tuning = VersusTuning.Default();
		[SerializeField] private VersusBotTuning botTuning = VersusBotTuning.Default();

		private readonly List<Transform> shotViews = new List<Transform>();
		private readonly List<VersusBodyView> shotBuffer = new List<VersusBodyView>();
		private readonly Transform[] bodies = new Transform[MatchConstants.VERSUS_PLAYER_COUNT];

		private UnityVersusCodec codec;
		private VersusAuthority authority;   // 연습 모드에서만.
		private VersusGuest guest;           // 온라인 모드에서만.
		private VersusClientLink link;
		private VersusHostListener hostListener;
		private float waitingForGuestSeconds;
		private VersusLocalInput localInput;
		private Camera viewCamera;
		private int mySeat;
		private int offerCursor;
		private bool cursorLatched;
		private int sentTick;
		private bool guestSeated;

		private void Awake()
		{
			codec = new UnityVersusCodec();

			BuildArena();
			viewCamera = BuildCamera();

			for (int index = 0; index < bodies.Length; index++)
				bodies[index] = BuildBody(index);

			localInput = new VersusLocalInput(viewCamera);

			if (mode == VersusMode.Practice || mode == VersusMode.Host)
			{
				authority = new VersusAuthority(VersusRules.Default(), tuning, botTuning, codec,
					randomSeed != 0 ? randomSeed : Random.Range(1, int.MaxValue),
					arenaHalfWidth, arenaHalfDepth);
				mySeat = 0;

				if (mode == VersusMode.Practice)
				{
					authority.FillWithBot(1, Random.Range(1, int.MaxValue));
					return;
				}

				// 내가 방장 — 문을 열어 두고 친구를 기다린다. 안 오면 봇으로 채운다.
				hostListener = new VersusHostListener(hostPort, "/vs/", hostOpenToNetwork);
				hostListener.Start();
				return;
			}

			link = new VersusClientLink();
			guest = new VersusGuest(link, codec, 0);
			link.Connect(serverUrl, roomName, fillWithBotIfAlone, codec);
		}

		private void OnDestroy()
		{
			link?.Dispose();
			hostListener?.Dispose();
		}

		private void Update()
		{
			if (mode == VersusMode.Online)
			{
				TickOnline();
				return;
			}

			if (mode == VersusMode.Host)
				TickHostDoor();

			TickPractice();
		}

		// 문 앞을 살핀다 — 친구가 왔으면 그 자리에 앉히고, 오래 안 오면 봇으로 채운다.
		private void TickHostDoor()
		{
			if (guestSeated)
				return;

			IVersusTransport arrived = hostListener != null ? hostListener.TryAccept() : null;

			if (arrived != null)
			{
				authority.Attach(1, arrived);
				guestSeated = true;
				return;
			}

			if (fillWithBotIfAlone == false)
				return;

			waitingForGuestSeconds += Time.deltaTime;

			if (waitingForGuestSeconds < 8f)
				return;

			authority.FillWithBot(1, Random.Range(1, int.MaxValue));
			guestSeated = true;
		}

		// ── 연습 (심판이 여기 있다) ─────────────────────────────────────────────

		private void TickPractice()
		{
			if (authority.Match.IsConcluded && localInput.WasRematchPressedThisFrame)
				authority.SubmitLocalRematch(mySeat);

			if (authority.Match.DraftingPlayerIndex == mySeat)
			{
				TickDraftLocal();
				authority.Tick(Time.deltaTime);
				return;
			}

			if (authority.Round != null)
				localInput.SelfPosition = authority.Round.PositionOf(mySeat);

			authority.SubmitLocalInput(mySeat, localInput.Read(authority.Round, mySeat, Time.deltaTime));
			authority.Tick(Time.deltaTime);
			DrawFromRound(authority.Round);
		}

		private void TickDraftLocal()
		{
			VersusInputFrame frame = localInput.Read(authority.Round, mySeat, Time.deltaTime);
			int count = authority.Match.PendingOffer.Count;

			if (MoveCursor(frame, count) && frame.Fire)
				return;

			if (frame.Fire)
				authority.SubmitLocalPick(mySeat, offerCursor);
		}

		// ── 온라인 (심판은 저쪽) ───────────────────────────────────────────────

		private void TickOnline()
		{
			guest.Pump();

			// 손이 마우스를 떠나지 않게 키로도 받는다 — 「한 판 더」는 빠를수록 좋다.
			if (guest.MatchWinner != VersusMatchCore.NO_WINNER && localInput.WasRematchPressedThisFrame)
				guest.SendRematch();
			mySeat = guest.Seat;

			if (guest.Predicted != null)
				localInput.SelfPosition = guest.Predicted.PositionOf(mySeat);
			else if (mySeat < guest.Fighters.Length)
				localInput.SelfPosition = new Numerics.Vector2(guest.Fighters[mySeat].x, guest.Fighters[mySeat].y);

			if (guest.Offer != null)
			{
				VersusInputFrame draftFrame = localInput.Read(guest.Predicted, mySeat, Time.deltaTime);
				MoveCursor(draftFrame, guest.Offer.cards.Length);

				if (draftFrame.Fire)
					guest.SendPick(offerCursor);
			}
			else
			{
				// ★ 미리 굴린다 — 내 조작이 서버 왕복을 기다리지 않는다. 정정은 스냅샷이 올 때 되감기로.
				guest.StepAndSend(localInput.Read(guest.Predicted, mySeat, Time.deltaTime));
			}

			// 그리는 것은 <b>내가 미리 굴린 판</b>이다(60Hz). 서버 그림은 정정으로만 들어온다.
			if (guest.Predicted != null)
				DrawFromRound(guest.Predicted);
			else
				DrawFromGuest();
		}

		private bool MoveCursor(VersusInputFrame frame, int count)
		{
			if (count <= 0)
				return false;

			if (Mathf.Abs(frame.Move.x) > 0.6f)
			{
				if (cursorLatched == false)
				{
					offerCursor = Mathf.Clamp(offerCursor + (frame.Move.x > 0f ? 1 : -1), 0, count - 1);
					cursorLatched = true;
					return true;
				}

				return false;
			}

			cursorLatched = false;
			return false;
		}

		// ── 그리기 ─────────────────────────────────────────────────────────────

		private void DrawFromRound(VersusRoundState round)
		{
			if (round == null)
				return;

			for (int index = 0; index < bodies.Length; index++)
			{
				Numerics.Vector2 position = round.PositionOf(index);
				PlaceBody(bodies[index], position.x, position.y, round.RadiusOf(index), round.IsAlive(index));
			}

			round.CollectShots(shotBuffer);
			DrawShots();
		}

		private void DrawFromGuest()
		{
			for (int index = 0; index < bodies.Length && index < guest.Fighters.Length; index++)
			{
				Net.VersusBodyMessage fighter = guest.Fighters[index];
				PlaceBody(bodies[index], fighter.x, fighter.y, fighter.r, fighter.alive);
			}

			shotBuffer.Clear();
			for (int index = 0; index < guest.Shots.Length; index++)
			{
				Net.VersusBodyMessage shot = guest.Shots[index];
				shotBuffer.Add(new VersusBodyView
				{
					Position = new Numerics.Vector2(shot.x, shot.y),
					Radius = shot.r,
					Owner = shot.owner,
					Alive = true,
				});
			}

			DrawShots();
		}

		private void PlaceBody(Transform body, float x, float y, float radius, bool alive)
		{
			body.position = new Vector3(x, 0.5f, y);
			body.localScale = Vector3.one * (radius * 2f);
			body.gameObject.SetActive(alive);
		}

		private void DrawShots()
		{
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
					shotBuffer[index].Owner == mySeat ? new Color(0.45f, 0.8f, 1f) : new Color(1f, 0.6f, 0.4f);
			}
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
			GUI.Label(new Rect(20f, 16f, 700f, 26f), "WASD 이동 · 마우스 조준 · 좌클릭 발사 · Space 대시");

			if (mode != VersusMode.Online)
			{
				string who = mode == VersusMode.Host ? (guestSeated ? "친구" : "기다리는 중") : "봇";
				GUI.Label(new Rect(20f, 40f, 700f, 26f),
					(mode == VersusMode.Host ? "방장 — " : "연습 — ") +
					"나 " + authority.Match.ScoreOf(0) + " vs " + authority.Match.ScoreOf(1) + " " + who);

				if (authority.Match.IsConcluded && GUI.Button(new Rect(20f, 116f, 160f, 30f), "한 판 더 (R)"))
					authority.SubmitLocalRematch(mySeat);

				if (mode == VersusMode.Host && hostListener != null)
				{
					GUI.Label(new Rect(20f, 64f, 900f, 26f), hostListener.IsListening
						? "친구가 붙을 주소: " + hostListener.Url.Replace("http://", "ws://")
						: "문을 못 열었다 — " + hostListener.LastError);
				}
				DrawOffer(authority.Match.DraftingPlayerIndex == mySeat ? OfferTexts(authority) : null);
				return;
			}

			if (link.IsOpen == false)
			{
				GUI.Label(new Rect(20f, 40f, 700f, 26f),
					link.IsConnecting ? "붙는 중… " + serverUrl : "못 붙었다 — " + link.LastError);
				return;
			}

			GUI.Label(new Rect(20f, 40f, 700f, 26f),
				"온라인 — 나 " + guest.ScoreMine + " vs " + guest.ScoreTheirs + " 상대" +
				(guest.Predicted != null ? "   (미리 굴림 · 정정 " + guest.RollbackCount + "회)" : "   (정정 대기)"));

			if (guest.OpponentLeft)
				GUI.Label(new Rect(20f, 64f, 400f, 26f), "상대가 나갔다");

			if (guest.MatchWinner != VersusMatchCore.NO_WINNER)
			{
				GUI.Label(new Rect(20f, 88f, 500f, 26f),
					(guest.MatchWinner == mySeat ? "내가 이겼다" : "상대가 이겼다") +
					(guest.RematchNeeded > 0 ? "   (한 판 더 " + guest.RematchReady + "/" + guest.RematchNeeded + ")" : string.Empty));

				if (GUI.Button(new Rect(20f, 116f, 140f, 30f), "한 판 더 (R)"))
					guest.SendRematch();
			}

			DrawOffer(guest.Offer != null ? guest.Offer.texts : null);
		}

		private static string[] OfferTexts(VersusAuthority authority)
		{
			string[] texts = new string[authority.Match.PendingOffer.Count];

			for (int index = 0; index < texts.Length; index++)
				texts[index] = VersusCards.Describe(authority.Match.PendingOffer[index]);

			return texts;
		}

		private void DrawOffer(string[] texts)
		{
			if (texts == null || texts.Length == 0)
				return;

			GUI.Label(new Rect(20f, 112f, 700f, 26f), "졌다 - 카드를 고른다 (A/D 커서, 좌클릭 확정)");

			for (int index = 0; index < texts.Length; index++)
			{
				GUI.Label(new Rect(20f, 140f + index * 24f, 700f, 24f),
					(index == offerCursor ? "> " : "   ") + texts[index]);
			}
		}
	}
}
