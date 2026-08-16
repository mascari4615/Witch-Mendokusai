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
		}

		[SerializeField] private VersusMode mode = VersusMode.Practice;
		[SerializeField] private string serverUrl = "ws://127.0.0.1:5199/vs";
		[SerializeField] private string roomName = string.Empty;
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
		private VersusLocalInput localInput;
		private Camera viewCamera;
		private int mySeat;
		private int offerCursor;
		private bool cursorLatched;
		private int sentTick;

		private void Awake()
		{
			codec = new UnityVersusCodec();

			BuildArena();
			viewCamera = BuildCamera();

			for (int index = 0; index < bodies.Length; index++)
				bodies[index] = BuildBody(index);

			localInput = new VersusLocalInput(viewCamera);

			if (mode == VersusMode.Practice)
			{
				authority = new VersusAuthority(VersusRules.Default(), tuning, botTuning, codec,
					randomSeed != 0 ? randomSeed : Random.Range(1, int.MaxValue),
					arenaHalfWidth, arenaHalfDepth);
				authority.FillWithBot(1, Random.Range(1, int.MaxValue));
				mySeat = 0;
				return;
			}

			link = new VersusClientLink();
			guest = new VersusGuest(link, codec, 0);
			link.Connect(serverUrl, roomName, fillWithBotIfAlone, codec);
		}

		private void OnDestroy()
		{
			link?.Dispose();
		}

		private void Update()
		{
			if (mode == VersusMode.Practice)
				TickPractice();
			else
				TickOnline();
		}

		// ── 연습 (심판이 여기 있다) ─────────────────────────────────────────────

		private void TickPractice()
		{
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
			mySeat = guest.Seat;

			if (mySeat < guest.Fighters.Length)
				localInput.SelfPosition = new Numerics.Vector2(guest.Fighters[mySeat].x, guest.Fighters[mySeat].y);

			if (guest.Offer != null)
			{
				VersusInputFrame draftFrame = localInput.Read(null, mySeat, Time.deltaTime);
				MoveCursor(draftFrame, guest.Offer.cards.Length);

				if (draftFrame.Fire)
					guest.SendPick(offerCursor);
			}
			else
			{
				sentTick++;
				guest.SendInput(localInput.Read(null, mySeat, Time.deltaTime), sentTick);
			}

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

			if (mode == VersusMode.Practice)
			{
				GUI.Label(new Rect(20f, 40f, 500f, 26f),
					"연습 — 나 " + authority.Match.ScoreOf(0) + " vs " + authority.Match.ScoreOf(1) + " 봇");
				DrawOffer(authority.Match.DraftingPlayerIndex == mySeat ? OfferTexts(authority) : null);
				return;
			}

			if (link.IsOpen == false)
			{
				GUI.Label(new Rect(20f, 40f, 700f, 26f),
					link.IsConnecting ? "붙는 중… " + serverUrl : "못 붙었다 — " + link.LastError);
				return;
			}

			GUI.Label(new Rect(20f, 40f, 500f, 26f),
				"온라인 — 나 " + guest.ScoreMine + " vs " + guest.ScoreTheirs + " 상대");

			if (guest.OpponentLeft)
				GUI.Label(new Rect(20f, 64f, 400f, 26f), "상대가 나갔다");

			if (guest.MatchWinner != VersusMatchCore.NO_WINNER)
				GUI.Label(new Rect(20f, 88f, 400f, 26f), guest.MatchWinner == mySeat ? "내가 이겼다" : "상대가 이겼다");

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
