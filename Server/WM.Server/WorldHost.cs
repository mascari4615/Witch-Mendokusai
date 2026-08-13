using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Server
{
	/// <summary>
	/// 세계 하나를 호스팅하는 서버 (TASK-WM-217).
	///
	/// ★ 왜 클래스인가 (전에는 static Program 이었다): <b>시험이 서버를 띄울 수 있어야</b>
	///   「둘이 붙어서 서로 보이나」를 기계가 확인한다. FishNet 을 지우려면 그 확인이 먼저다 —
	///   지금 유일하게 검증된 멀티가 FishNet 이기 때문이다.
	///   전역 상태였을 때는 시험마다 같은 세계·같은 저장 파일을 물어 서로를 오염시켰다.
	/// </summary>
	public sealed class WorldHost
	{
		/// <summary>1초에 몇 번 모두에게 알릴 것인가.</summary>
		// ⚠ 실험용 손잡이 — 「20Hz 가 정말 필요한가」를 재려고 잠깐 연다 (TASK-WM-243).
		//   값을 정하는 것은 실측이지 취향이 아니다.
		private static readonly int SNAPSHOT_HZ =
			int.TryParse(System.Environment.GetEnvironmentVariable("WM_SNAPSHOT_HZ"), out int said) && said > 0 ? said : 20;

		/// <summary>세계를 디스크로 내리는 간격 — 바뀐 게 있을 때만 쓴다.</summary>
		private const int SAVE_INTERVAL_MILLISECONDS = 5000;

		/// <summary>
		/// <b>사람이 한 일</b>이 생긴 뒤 이만큼 안에 적는다 (ms, TASK-WM-310).
		///
		/// ★ 왜 따로 두나 (실측 2026-08-13): 세계는 「그거 했다」고 답한 뒤 최대 <b>5초</b>를
		///   디스크에 안 적고 있었다. 그 사이에 세계가 갑자기 죽으면 <b>답해 놓고 없던 일</b>이 된다 —
		///   세 판 중 한 판에서 주운 물건이 사라졌다. 사람에겐 「분명히 주웠는데」다.
		///
		/// ★ 왜 「했다」를 저장 뒤로 미루지 않나: 그러면 모든 줍기·짓기가 저장을 기다린다 —
		///   손맛이 통째로 느려진다(WM-283 이 271ms 를 70ms 로 줄인 그 자리를 도로 무른다).
		///   대신 <b>적는 쪽을 당긴다</b> — 잃을 수 있는 창이 5초에서 이 값으로 줄어든다.
		///
		/// ★ 왜 0 이 아닌가: 한 판에 여러 사람이 동시에 주우면 그때마다 세계를 통째로 적게 된다.
		///   짧게 모아서 한 번에 적는다(디바운스).
		/// </summary>
		private const int SAVE_AFTER_DEED_MILLISECONDS = 300;

		/// <summary>저장 루프가 깨어나는 간격 — 위 두 값 중 짧은 쪽을 지킬 수 있어야 한다.</summary>
		private const int SAVE_TICK_MILLISECONDS = 100;

		/// <summary>
		/// 한 곳에서 한꺼번에 붙을 수 있는 창 수 — 넘으면 더 안 받는다 (TASK-WM-220).
		///
		/// ★ 왜: 인사 안 한 손님도 접속마다 인형과 신원을 받는다. 한 사람이 소켓을 계속 열면
		///   세계의 기억과 품이 그만큼 늘어난다(창 하나로 세계를 재우는 길). 사람이 한 기기에서
		///   창 몇 개를 여는 건 정상이라, 넉넉히 두되 <b>끝이 있게</b> 한다.
		/// </summary>
		private const int MAX_WINDOWS_PER_PLACE = 8;

		/// <summary>실제 1초에 세계의 몇 분이 흐르나 — 게임의 WorldClockSO 와 맞춰야 할 값.</summary>
		private const float MINUTES_PER_REAL_SECOND = 1f;

		/// <summary>
		/// 하늘이 시작한 순간 (Unix ms) — <b>모든 세계가 같은 값을 쓴다</b> (TASK-WM-266).
		/// 이 값 하나로 어느 구역이든, 몇 번을 껐다 켜든 같은 시각을 셈해 낸다(세계끼리 조율 0).
		/// ⚠ 구역들이 <b>같은 값</b>을 써야 한다 — 다르면 그게 곧 다른 하늘이다(`WM_SKY_BEGAN`).
		/// </summary>
		private static readonly long SkyBeganMs = ReadSkyBegan();

		/// <summary>
		/// 기본값 = 2026-08-12T00:00:00Z (세계가 처음 선 날).
		/// ⚠ <b>반드시 지난 시각</b>이어야 한다 — 앞선 시각을 박으면 하늘이 통째로 멎는다
		///   (첫 시도가 그랬다: 오늘 날짜를 UTC 로 박았더니 한국 새벽에는 아직 안 온 시각이었다).
		/// </summary>
		private const long SKY_BEGAN_DEFAULT_MS = 1786492800000L;

		private static long ReadSkyBegan()
		{
			string said = System.Environment.GetEnvironmentVariable("WM_SKY_BEGAN");
			return long.TryParse(said, out long given) && given > 0 ? given : SKY_BEGAN_DEFAULT_MS;
		}

		/// <summary>
		/// 하늘을 <b>앞으로 당겨 둔 분</b> (TASK-WM-305).
		///
		/// ★ 왜 필요한가: 하늘은 벽시계에서 유도된다(WM-266). 그래서 시험이 <c>AdvanceMinutes</c> 로
		///   날을 밀어도 <b>다음 판에 되돌아간다</b> — 「하루가 지나면 창이 안다」 시험이 판마다
		///   되기도 안 되기도 했다(실측 2026-08-13, 207개 중 이 하나). 밀 수 있는 자리를 안 두면
		///   시험은 <b>운</b>에 기댈 수밖에 없다.
		///
		/// ★ 무엇이 안 흔들리나: 구역끼리 같은 하늘을 쓰는 성질은 그대로다 — 이 값은 이 세계 안에서만
		///   더해지고, 기본값은 0이다(아무도 안 건드리면 예전과 똑같이 돈다).
		/// </summary>
		public static long SkyHurryMinutes { get; set; }

		/// <summary>세계가 시작한 뒤 몇 분이 흘렀나 — 벽시계에서 바로 유도한다.</summary>
		private static long SkyMinutesNow()
		{
			long sinceMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - SkyBeganMs;
			if (sinceMs < 0)
				return 0;

			return (long)(sinceMs / 1000.0 * MINUTES_PER_REAL_SECOND) + SkyHurryMinutes;
		}

		/// <summary>
		/// 아무도 없어도 <b>세계의 시간이 이만큼 흐르면</b> 한 번 적는다 (TASK-WM-218).
		///
		/// ★ 왜: 시각은 사람이 없어도 흐르는데 저장은 사람이 있을 때만 했다 — 그래서 서버를 껐다 켜면
		///   <b>시계가 뒤로 감겼다</b>(실측: 7:34 → 6:45). 지은 건 남는데 시간만 되돌아가는 세계는 이상하다.
		///   그렇다고 매번 쓰면 빈 밤에도 디스크가 돈다 — 그 사이를 이 값이 정한다.
		/// </summary>
		private const int IDLE_SAVE_WORLD_MINUTES = 60;
		private const int MAX_MESSAGE_BYTES = 1024 * 1024;
		private const float PLAYER_INTEREST_RADIUS = 32f;
		private const float INTEREST_CELL_SIZE = 16f;

		private int savedAtWorldMinute;
		private long broadcastSnapshotMessages;

		/// <summary>
		/// 세계 소식을 <b>몇 벌 지었나</b> — 보낸 건수와 견주면 「한 칸이 같이 쓰기」가 도는지 보인다.
		/// (같이 쓰기가 안 돌면 지은 벌 수 = 보낸 건수다. 눈으로는 절대 못 보는 자리라 숫자로 남긴다.)
		/// </summary>
		private long builtSnapshots;

		/// <summary>걸음 지갑이 비어 되돌린 걸음 수 — 속이는 창이 있으면 여기가 오른다 (TASK-WM-222).</summary>
		private long refusedSteps;

		/// <summary>판과 판 사이가 가장 많이 벌어진 순간 (ms) — 세계가 멎은 자리 (TASK-WM-242).</summary>
		private long longestTickGapMs;

		/// <summary>창이 이미 들고 있어 안 보낸 낱말표 묶음 수 (TASK-WM-238).</summary>
		private long catalogsSkipped;

		/// <summary>미리 눌러 둔 창 파일들 (TASK-WM-226).</summary>
		private StaticSqueeze squeeze;

		/// <summary>옆 세계들 (TASK-WM-254).</summary>
		private WitchMendokusai.Net.ZoneMap neighbours = WitchMendokusai.Net.ZoneMap.Alone;

		/// <summary>통행증 도장에 쓰는, 두 세계만 아는 말.</summary>
		private string zoneSecret = string.Empty;

		private string catalogStampCache;

		/// <summary>
		/// 낱말표·지을 것·솥 재료·마도서·제작표를 아우르는 <b>도장</b> (TASK-WM-238).
		///
		/// ★ 왜: 이것들은 서버가 도는 동안 <b>안 바뀐다</b>. 그런데 붙을 때마다 다시 나갔다 —
		///   실측 2026-08-12: 한 번 붙는 데 30.2KB, 그중 <b>7.2KB 가 이 다섯</b>이다.
		///   회선이 나쁘면 다시 붙는 일이 잦고, 그때마다 같은 7.2KB 를 또 받는다
		///   (초당 4KB 회선에서는 그것만 2초다 — 그동안 세계는 안 흐른다).
		///   창이 「나 이 도장 들고 있다」고 하면 안 보낸다.
		/// </summary>
		private string CatalogStamp
		{
			get
			{
				if (catalogStampCache != null)
					return catalogStampCache;

				string all = Protocol.Catalog(ItemsCatalog.Names())
					+ Protocol.BuildCatalog(World.Buildables.All)
					+ Protocol.BrewShelf(World.Ingredients.All)
					+ Protocol.Spellbook(ServerRecipeBook.Book.Pages)
					+ Protocol.CraftBook(ServerCraftBook.Book.Recipes);

				using System.Security.Cryptography.SHA256 maker = System.Security.Cryptography.SHA256.Create();
				byte[] print = maker.ComputeHash(Encoding.UTF8.GetBytes(all));
				catalogStampCache = System.Convert.ToHexString(print).Substring(0, 16).ToLowerInvariant();
				return catalogStampCache;
			}
		}

		/// <summary>누가 <b>언제</b> 움직였나 — 몰린 자리에서 자리를 떼어 줄 사람을 고르는 데 쓴다 (TASK-WM-227).</summary>
		private readonly ConcurrentDictionary<int, long> movedAt = new ConcurrentDictionary<int, long>();

		/// <summary>이 안에 움직였으면 「움직이는 중」으로 본다 — 걸음 한 판(50ms)의 몇 배.</summary>
		private const long MOVING_WINDOW_MS = 400;

		/// <summary>
		/// 칸마다 <b>지난 판에 그 칸으로 내보낸 사람과 자리</b> (TASK-WM-220).
		///
		/// ★ 왜 칸마다인가: 「이 사람 자리는 이미 알렸다」를 세계 통틀어 하나로 두면,
		///   A 칸에 보낸 것을 B 칸도 보낸 셈이 된다 — B 의 창들은 그 사람이 <b>영영 안 움직이는</b>
		///   것으로 본다. 알린 것은 칸마다 따로 세야 한다.
		/// </summary>
		private readonly System.Collections.Generic.Dictionary<string,
			System.Collections.Generic.Dictionary<int, (float X, float Z)>> lastCellCast =
			new System.Collections.Generic.Dictionary<string,
				System.Collections.Generic.Dictionary<int, (float X, float Z)>>();

		/// <summary>칸마다 <b>지난 판에 그 칸으로 내보낸 들판</b> (번호 → 개수) — 바뀐 자리만 보내려고.</summary>
		/// <summary>곳마다 지금 몇 창이 붙어 있나 (TASK-WM-220).</summary>
		private readonly System.Collections.Generic.Dictionary<string, int> windowsPerPlace =
			new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal);

		private readonly System.Collections.Generic.Dictionary<string,
			System.Collections.Generic.Dictionary<int, int>> lastCellField =
			new System.Collections.Generic.Dictionary<string,
				System.Collections.Generic.Dictionary<int, int>>();

		/// <summary>마지막으로 모두에게 알린 이름표 — 바뀐 사람만 다시 보내려고 들고 있는다 (TASK-WM-220).</summary>
		private readonly System.Collections.Generic.Dictionary<int, string> toldNames =
			new System.Collections.Generic.Dictionary<int, string>();
		private long broadcastSnapshotBytes;
		private long largestBroadcastSnapshotBytes;

		// 마지막으로 창들에 보낸 판 — 이 수가 그대로면 그 목록은 다시 안 보낸다.
		/// <summary>
		/// 지금 어느 상자를 열어 두고 있나 (TASK-WM-217).
		/// ★ 왜: 둘이 같은 상자를 열어 두면, 한쪽이 꺼내 갔는데 다른 쪽 화면엔 그대로 남아 있다 —
		///   그 상태로 누르면 「왜 안 되지」가 된다. 안이 바뀌면 보고 있는 창에 다시 보낸다.
		/// </summary>
		private readonly ConcurrentDictionary<int, Vector3Int> watchingChest = new ConcurrentDictionary<int, Vector3Int>();

		private int sentStorageVersion = -1;
		/// <summary>이만큼(세계의 날) 안 오고 아무것도 안 남긴 사람은 장부에서 지운다.</summary>
		private const int GUEST_FORGET_DAYS = 90;

		private readonly WorldStore store;
		/// <summary>
		/// 창 하나 — 소켓과 <b>차례 서는 자리</b> (TASK-WM-218).
		///
		/// ★ 왜 차례가 필요한가: 소켓 하나에 두 곳에서 동시에 쓰면 터진다(알림 루프는 20Hz 로 쓰고,
		///   답장은 사람이 말할 때 쓴다). 그 예외는 WebSocketException 이 아니라서 조용히 새 나가
		///   <b>인사에 대한 답이 통째로 사라졌다</b> — 창은 접속은 됐는데 자기가 누군지 모르게 됐다.
		///   시험이 그 자리를 재현해 잡았다.
		/// </summary>
		private sealed class Connection
		{
			public Connection(WebSocket socket)
			{
				Socket = socket;
			}

			public WebSocket Socket { get; }

			public SemaphoreSlim SendGate { get; } = new SemaphoreSlim(1, 1);

			/// <summary>
			/// 이 창이 보낸 걸음 중 <b>세계가 본 마지막 번호</b> (TASK-WM-271).
			/// 받아들였든 물렸든 「봤다」다 — 창은 이걸로 아직 답 안 온 걸음을 가려낸다.
			/// </summary>
			public int SawStep;

			/// <summary>그중 창에 <b>알려 준</b> 번호 — 안 바뀌었으면 안 보낸다(줄을 먹지 않게).</summary>
			public int ToldStep;

			/// <summary>
			/// 지금 이 창에 알림을 보내는 중인가 (TASK-WM-217).
			///
			/// ★ 왜 필요한가: 방송 루프가 창들을 <b>차례로 기다리며</b> 보냈다. 그래서 화면을 안 읽는
			///   창이 하나 있으면(브라우저 탭이 잠들었거나, 시험이 잠깐 안 읽거나) 그 창의 버퍼가 차는
			///   순간 <b>모두의 세계가 멈췄다</b> — 다른 사람은 이유도 모른 채 얼어붙는다(실측 2026-08-10).
			///   밀린 창에는 이번 그림을 <b>버린다</b>. 세계 그림은 낡으면 값이 없다.
			/// </summary>
			public int Sending;

			public int SentBuildVersion = -1;
			public int SentFieldVersion = -1;
			public int SentPotVersion = -1;
			/// <summary>
			/// 지난 판을 <b>건너뛰었다</b> — 다음엔 「전부」를 줘야 한다 (TASK-WM-220).
			///
			/// ⚠ 「바뀐 것만」 보내기가 생기면서 <b>건너뛰기가 위험해졌다</b>: 밀린 창은 그 판을
			///   영영 못 받는다. 그 판에 움직이고 그 뒤로 가만히 선 사람은 그 창에서 <b>엉뚱한
			///   자리에 영원히</b> 서 있게 된다. 전에는(늘 전부 보낼 때) 다음 판이 알아서 고쳐 줬다.
			/// </summary>
			public bool MissedAPlate;

			/// <summary>낱말표를 이미 보냈나 — 인사와 유예가 겹쳐도 한 번만 나가게 (TASK-WM-238).</summary>
			public int CatalogsSent;

			/// <summary>연달아 몇 판을 못 받았나 — 이만큼 사람 수를 줄여 준다 (TASK-WM-228).</summary>
			public int MissedInARow;

			/// <summary>이 창에 마지막으로 말해 준 「네 자리」 (TASK-WM-236) — 안 바뀌면 다시 안 말한다.</summary>
			public float ToldMyX = float.NaN;

			public float ToldMyZ = float.NaN;

			/// <summary>이 창이 인사 때 내민 기기 열쇠 — 세계는 지문만 갖기에 여기 들고 있는다.</summary>
			public string DeviceSecret = string.Empty;

			public int InterestCellX = int.MinValue;
			public int InterestCellZ = int.MinValue;
		}

		private readonly ConcurrentDictionary<int, Connection> sockets = new ConcurrentDictionary<int, Connection>();

		/// <summary>걸음 심판 — 시계를 보고 「걸어서 갈 수 있는 만큼」만 통과시킨다 (TASK-WM-222).</summary>
		private readonly WitchMendokusai.Net.MoveAllowance moveAllowance = new WitchMendokusai.Net.MoveAllowance();

		/// <summary>사람마다 최근 1초의 발자국 (TASK-WM-303) — 되감아 판정할 때 읽는다.</summary>
		private readonly WitchMendokusai.Net.PastPlaces pastPlaces = new WitchMendokusai.Net.PastPlaces();

		/// <summary>사람마다 회선이 얼마나 먼가 (TASK-WM-303) — <b>세계가</b> 잰다, 창이 말한 값이 아니다.</summary>
		private readonly WitchMendokusai.Net.LineTime lineTime = new WitchMendokusai.Net.LineTime();

		/// <summary>이 사람의 이 번호를 이미 했나 (TASK-WM-305) — 다시 보낸 것을 두 번 하지 않는다.</summary>
		private readonly WitchMendokusai.Net.ActionOnce actionOnce = new WitchMendokusai.Net.ActionOnce();

		/// <summary>국경 너머에서 비쳐 오는 사람들 (TASK-WM-263) — 보이기만 하고 못 건드린다.</summary>
		private readonly NeighbourShadows shadows = new NeighbourShadows();

		/// <summary>지금 비쳐 보이는 국경 너머 사람 수 — 시험이 「유령이 남았나」를 묻는 자리다.</summary>
		public int ShadowCount => shadows.Alive(System.Environment.TickCount64).Length;

		/// <summary>이미 쓴 통행증 (TASK-WM-259) — 한 장으로 두 번 들어오면 가방이 두 벌 온다.</summary>
		private readonly WitchMendokusai.Net.PassOnce passesUsed = new WitchMendokusai.Net.PassOnce();

		// 제작 주사위 — <b>세계가 굴린다</b>. 창이 굴리면 창을 고친 사람은 언제나 성공한다.
		// 시험이 성공·실패를 모두 잴 수 있게 판정 자체는 WorldCraftBook 이 하고, 여기선 숫자만 넣는다.
		private readonly System.Random craftDice = new System.Random();
		private int worldDirty;
		private long snapshotSequence;

		public WorldHost(WorldStore worldStore)
		{
			store = worldStore;
		}

		/// <summary>이 서버가 굴리는 세계 — 시험이 들여다본다.</summary>
		public WorldSim World { get; } = new WorldSim
		{
			Gatherables = ServerGatherables.Field,
			Buildables = ServerBuildingCatalog.Catalog,
			Ingredients = ServerIngredients.Shelf,
		};

		/// <summary>KarmoLab 계정에 「이 사람 누구냐」고 묻는 자리 — 못 물어보면 손님으로 받는다.</summary>
		public KarmoLabAccounts Accounts { get; set; } = new KarmoLabAccounts();

		/// <summary>세계가 아는 사람들 (TASK-WM-218) — 열쇠로 알아본다.</summary>
		public WitchMendokusai.Identity.WorldIdentityRegistry Identities { get; } = new WitchMendokusai.Identity.WorldIdentityRegistry();

		/// <summary>가방을 되살릴 때 쓰는 아이템 목록 — 게임에서 뽑아 온 그것.</summary>
		private WorldItemCatalog ItemsCatalog => ServerItemCatalog.Catalog;

		/// <summary>세계를 띄운다. <paramref name="url"/> 를 주면 그 자리에(시험은 빈 포트를 쓴다).</summary>
		public WebApplication Build(string[] args, string url = null)
		{
			// ★ 이 세계가 맡은 땅 (TASK-WM-252) — 「이름:fromX,fromZ,toX,toZ」.
			//   안 주면 온 세상이 내 것이다(안 나눈 세계는 지금 그대로 돈다).
			World.Patch = WitchMendokusai.Net.ZonePatch.Read(
				System.Environment.GetEnvironmentVariable("WM_ZONE"));

			// 옆 세계가 어디에 있나 (TASK-WM-254) — 「이름:from,from,to,to=주소」 를 ; 로 이어서.
			neighbours = WitchMendokusai.Net.ZoneMap.Read(
				System.Environment.GetEnvironmentVariable("WM_ZONE_NEIGHBOURS"));

			// ★ 이름이 같은 번호로 뭉개지는 이웃이 있나 (TASK-WM-265) — 있으면 국경에서 한 사람이
			//   다른 한 사람을 조용히 지운다. 띄울 때 크게 알린다(고치는 것은 이름을 바꾸는 일이다).
			{
				System.Collections.Generic.List<string> names = new System.Collections.Generic.List<string>();
				if (World.Patch.Bounded)
					names.Add(World.Patch.Name);

				foreach ((WitchMendokusai.Net.ZonePatch Patch, string Address) land in neighbours.Lands)
					names.Add(land.Patch.Name);

				string clash = WitchMendokusai.Net.BorderBand.FirstClash(names);
				if (clash != null)
					Console.WriteLine($"[zone] ⚠ 세계 이름이 같은 번호로 뭉개진다: {clash} — 국경에서 사람이 서로를 지운다");
			}

			// 두 세계만 아는 말 — 통행증 도장을 찍고 확인하는 데 쓴다.
			zoneSecret = System.Environment.GetEnvironmentVariable("WM_ZONE_SECRET") ?? string.Empty;

			WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

			// ★ 창을 통째로 눌러서 보낸다 (TASK-WM-225).
			//   실측: 창이 쓰는 three.module.js 는 <b>1.3MB 무압축</b>이다. 좁은 회선(256kbps)에서는
			//   그것만 40초 — 그동안 사람은 <b>백지</b>를 본다(30초 시험이 아예 안 붙었다).
			//   글(js·html)은 눌리면 4분의 1 이하가 된다. 회선이 좋을 때는 안 보이지만,
			//   모바일에서는 「세계가 있다」와 「안 뜬다」를 가르는 자리다.
			builder.Services.AddResponseCompression(options =>
			{
				// 로컬 시험도 https 로 돌 수 있다 — 켜 두지 않으면 그 길에서만 조용히 안 눌린다.
				options.EnableForHttps = true;
				options.Providers.Add<BrotliCompressionProvider>();
				options.Providers.Add<GzipCompressionProvider>();
				options.MimeTypes = new[]
				{
					"text/html", "text/css", "text/plain", "text/javascript",
					"application/javascript", "application/json", "application/wasm",
					"image/svg+xml",
				};
			});

			WebApplication app = builder.Build();
			if (string.IsNullOrEmpty(url) == false)
				app.Urls.Add(url);

			// 누르기는 <b>정적 파일보다 먼저</b> 서야 한다 — 뒤에 서면 이미 나간 뒤라 아무 일도 안 한다.
			//
			// ★ 두 겹이다 (TASK-WM-226): 창을 이루는 파일들은 <b>미리 최고로 눌러 둔 것</b>을 그대로 주고
			//   (요청당 CPU 0 · 최고 압축률), 그 밖의 답(세계 상태·json)은 아래 UseResponseCompression 이 맡는다.
			squeeze = new StaticSqueeze(app.Environment.WebRootPath);
			squeeze.SqueezeAllInBackground();
			app.Use(async (context, next) =>
			{
				if (HttpMethods.IsGet(context.Request.Method)
					&& StaticSqueeze.WantsBrotli(context.Request)
					&& squeeze.TryTake(context.Request.Path.Value, out StaticSqueeze.Pressed ready))
				{
					context.Response.Headers.ContentEncoding = "br";
					context.Response.Headers.Vary = "Accept-Encoding";
					context.Response.Headers.ETag = ready.Tag;
					context.Response.Headers.LastModified = ready.When.ToString("R");
					context.Response.Headers.CacheControl = "public, max-age=0, must-revalidate";
					context.Response.ContentType = StaticSqueeze.KindOf(context.Request.Path.Value);

					// 「이거 그대로면 안 보내도 돼」 — 다시 오는 사람에게 138KB 를 또 받게 하지 않는다.
					if (context.Request.Headers.IfNoneMatch.ToString() == ready.Tag)
					{
						context.Response.StatusCode = StatusCodes.Status304NotModified;
						return;
					}

					context.Response.ContentLength = ready.Bytes.Length;
					await context.Response.Body.WriteAsync(ready.Bytes);
					return;
				}

				await next();
			});

			app.UseResponseCompression();

			// 골격 창(wwwroot/index.html) — 서버가 자기 확인용 화면을 같이 준다.
			app.UseDefaultFiles();
			app.UseStaticFiles();
			app.UseWebSockets();

			// 사람이 눈으로 살아있음을 확인하는 자리 — 게이트도 여기를 찌른다.
			// ★ 「살아 있다」만으로는 부족하다: 세계가 <b>돌고 있는지</b>(시각이 흐르는지, 사람이 있는지,
			//   장부가 남아 있는지)를 같이 말한다. 안 그러면 「떠 있는데 시간이 멈춘 세계」를 못 알아본다.
			app.MapGet("/health", () => Results.Json(new
			{
				ok = true,
				people = World.Snapshot().Length,
				identities = Identities.Count,
				buildings = World.Buildings().Length,

				// 지금 살아 있는 들판 자리 (TASK-WM-306) — 창이 아는 수와 <b>대조</b>하는 자리다.
				//   창은 델타만 받으므로, 오래 돌면 세계의 진실과 갈라졌는지 밖에서 볼 길이 있어야 한다.
				gatherables = World.Gatherables.Alive(World.Calendar.TotalMinutes()).Count,
				day = World.Calendar.TotalDays(),
				hour = World.Calendar.Hour,
				minute = World.Calendar.Minute,
				broadcastSnapshotMessages = Interlocked.Read(ref broadcastSnapshotMessages),
				builtSnapshots = Interlocked.Read(ref builtSnapshots),
				refusedSteps = Interlocked.Read(ref refusedSteps),
				longestTickGapMs = Interlocked.Read(ref longestTickGapMs),
				zone = World.Patch.Bounded
					? World.Patch.Name + ":" + World.Patch.FromX + "," + World.Patch.FromZ
						+ "," + World.Patch.ToX + "," + World.Patch.ToZ
					: "온 세상",
				catalogsSkipped = Interlocked.Read(ref catalogsSkipped),
				narrowedWindows = CountNarrowed(),
				squeezedFiles = squeeze == null ? 0 : squeeze.Count,

				// 쓰레기 치우기 — 세계가 이따금 멎는 이유를 볼 때 쓴다 (TASK-WM-220).
				gcServerMode = System.Runtime.GCSettings.IsServerGC,
				gcGen0 = GC.CollectionCount(0),
				gcGen1 = GC.CollectionCount(1),
				gcGen2 = GC.CollectionCount(2),
				gcPausePercent = System.Math.Round(GC.GetGCMemoryInfo().PauseTimePercentage, 2),
				// ⚠ 이건 <b>여태 얼마나 새로 담았나</b>(누적)다 — 늘 자란다. 「새는가」는 이걸로 못 본다.
				allocatedMegabytes = GC.GetTotalAllocatedBytes(false) / 1048576,

				// ★ 지금 <b>들고 있는</b> 양 (TASK-WM-296) — 오래 돌 때 자라는지 보는 자리는 이쪽이다.
				heldMegabytes = GC.GetTotalMemory(false) / 1048576,
				broadcastSnapshotBytes = Interlocked.Read(ref broadcastSnapshotBytes),
				largestBroadcastSnapshotBytes = Interlocked.Read(ref largestBroadcastSnapshotBytes),
				worldFile = store.Path,
			}));

			app.Map("/ws", async (HttpContext context) =>
			{
				if (context.WebSockets.IsWebSocketRequest == false)
				{
					context.Response.StatusCode = 400;
					return;
				}

				// ★ 줄 위에서 <b>압축해서</b> 나른다 (TASK-WM-217). 세계 소식은 같은 낱말이 반복되는
				//   JSON 이라 잘 줄어든다. 브라우저는 이 압축(permessage-deflate)을 스스로 청한다 —
				//   못 하는 창은 그냥 예전처럼 받는다(협상이라 깨지지 않는다).
				// ★ 창 크기를 11비트로 줄인 이유: 기본값은 창 하나마다 300KB 를 물고 있어
				//   사람 400명이면 100MB 를 압축 버퍼로만 쓴다. 11비트면 그 1/16 이고,
				//   판 하나가 3KB 짜리라 압축률은 거의 안 떨어진다.
				// 한 곳에서 너무 많이 붙으면 더 안 받는다 — 세계가 한 창에 잠기지 않게.
				string origin = ClientOrigin.Of(context);
				if (TryEnterPlace(origin) == false)
				{
					context.Response.StatusCode = 429;
					return;
				}

				WebSocket socket = await context.WebSockets.AcceptWebSocketAsync(new WebSocketAcceptContext
				{
					DangerousEnableCompression = true,
					ServerMaxWindowBits = 11,
				});
				try
				{
					await ServeAsync(socket, app.Lifetime.ApplicationStopping);
				}
				finally
				{
					// 창이 닫히면 그 곳의 자리도 돌려준다 — 안 돌려주면 그 곳은 <b>영영</b> 못 들어온다.
					LeavePlace(origin);
				}
			});

			// ★ 이웃 세계 전용 문 (TASK-WM-263) — 사람이 쓰는 문(/ws)과 따로 둔다.
			//   같은 문으로 받으면 이웃 세계가 <b>사람 하나</b>로 세어져 국경에 인형이 선다.
			app.Map("/peer", async (HttpContext context) =>
			{
				if (context.WebSockets.IsWebSocketRequest == false)
				{
					context.Response.StatusCode = 400;
					return;
				}

				WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
				await ServePeerAsync(socket, app.Lifetime.ApplicationStopping);
			});

			// 세계는 서버보다 오래 산다 (단계 5) — 뜨자마자 지난 기억을 되살린다.
			WorldSaveData loaded = store.TryLoad();
			Identities.Load(loaded?.identities);
			int restored = World.Load(loaded, ItemsCatalog);
			savedAtWorldMinute = World.Calendar.TotalMinutes();
			Console.WriteLine($"[world] 되살린 건물 {restored}개 ({store.Path})");

			// 알림 루프는 서버가 실제로 뜬 뒤에 시작한다 — 뜨기 전에 시작하면 조용히 죽어도 아무도 모른다.
			app.Lifetime.ApplicationStarted.Register(() =>
			{
				_ = RunBroadcastLoopAsync(app.Lifetime.ApplicationStopping);
				_ = RunSaveLoopAsync(app.Lifetime.ApplicationStopping);
				_ = RunBorderLoopsAsync(app.Lifetime.ApplicationStopping);
				_ = RunHealthJournalLoopAsync(app.Lifetime.ApplicationStopping);
			});

			// 꺼질 때 한 번 더 — 마지막 몇 초 사이에 지은 것도 남는다.
			app.Lifetime.ApplicationStopping.Register(() => store.TrySave(SaveWorld()));

			return app;
		}

		/// <summary>
		/// 국경을 넘는 창을 <b>데리고 있어 주는</b> 시간 (ms, TASK-WM-279).
		/// 창은 저 세계에 먼저 붙어 보고 첫 그림이 온 뒤에 이 줄을 놓는다 — 그 사이를 기다린다.
		/// </summary>
		private const int HANDOVER_GRACE_MS = 5000;

		/// <summary>인사를 안 하는 창에도 낱말표를 주기까지 기다리는 시간.</summary>
		private const int WAIT_FOR_HELLO_MS = 1000;

		/// <summary>
		/// 낱말표 다섯을 보낸다 — 창이 같은 도장을 들고 있으면 <b>안 보낸다</b> (TASK-WM-238).
		/// 두 번 보내지 않는다(인사가 와도, 유예가 끝나도 한 번뿐이다).
		/// </summary>
		private async Task SendCatalogsUnlessKnownAsync(Connection connection, string knownStamp, int waitMilliseconds)
		{
			if (waitMilliseconds > 0)
				await Task.Delay(waitMilliseconds);

			if (Interlocked.CompareExchange(ref connection.CatalogsSent, 1, 0) != 0)
				return;

			if (knownStamp == CatalogStamp)
			{
				Interlocked.Increment(ref catalogsSkipped);
				return;
			}

			await SendAsync(connection, Protocol.Catalog(ItemsCatalog.Names()));
			await SendAsync(connection, Protocol.BuildCatalog(World.Buildables.All));
			await SendAsync(connection, Protocol.BrewShelf(World.Ingredients.All));
			await SendAsync(connection, Protocol.Spellbook(ServerRecipeBook.Book.Pages));
			await SendAsync(connection, Protocol.CraftBook(ServerCraftBook.Book.Recipes));
		}

		private async Task ServeAsync(WebSocket socket, CancellationToken stopping)
		{
			// ★ 먼저 받아 주고, 열쇠는 오면 그때 붙인다 (TASK-WM-218).
			//   「인사를 받고 나서 인형을 준다」로 했더니 인사 안 하는 옛 창이 영영 환영을 못 받고
			//   멈춰 섰다(스모크 4개가 그 자리에서 죽었다). 접속은 인사를 기다리지 않는다.
			WorldDoll doll = World.Join();
			Connection connection = new Connection(socket);

			// ⚠ 여기서 <b>아직 방송 목록에 안 넣는다</b> (TASK-WM-301). 넣어 두면 방송 루프가
			//   <b>첫 전체 그림보다 먼저</b> 「바뀐 것만」 판을 보낼 수 있다 — 그 판은 번호가 더 커서,
			//   뒤늦게 도착한 첫 전체 그림이 창에게 「지난 판」으로 버려진다.
			//   그러면 창은 붙었는데도 <b>텅 빈 세계</b>를 본다(실측: 지연 없는 회선에서 seq 10 → 9).
			//   전체 그림을 보낸 <b>뒤에</b> 목록에 넣는다.
			await SendAsync(connection, Protocol.Welcome(doll.Id, catalogStamp: CatalogStamp));

			// 이름표도 들어올 때 한 번 — 그 뒤로는 바뀔 때만 온다 (TASK-WM-220).
			{
				WorldDoll[] everyoneNow = EveryoneNow();
				System.Collections.Generic.List<(int DollId, string Name)> allNames =
					new System.Collections.Generic.List<(int, string)>();
				for (int i = 0; i < everyoneNow.Length; i++)
					allNames.Add((everyoneNow[i].Id, NameOfDoll(everyoneNow[i])));

				if (allNames.Count > 0)
					await SendAsync(connection, Protocol.Names(allNames));
			}

			// ★ 낱말표·지을 것·솥 재료·마도서·제작표는 <b>인사를 보고</b> 보낸다 (TASK-WM-238).
			//   창이 「나 이 도장 들고 있다」고 하면 안 보낸다 — 7.2KB 를 아낀다.
			//   ⚠ 인사를 안 하는 옛 창도 있다(그래서 접속은 인사를 안 기다린다). 그런 창에는
			//     잠깐 기다렸다가 그냥 보낸다 — 낱말표가 없으면 화면이 「17450 3개」가 된다.
			_ = SendCatalogsUnlessKnownAsync(connection, null, WAIT_FOR_HELLO_MS);

			// ⚠ 판 번호를 <b>그림 뜨기 전에</b> 붙잡는다 — 뜬 뒤에 누가 집을 지으면 그 집이
			//   「이미 보낸 것」으로 둔갑해 이 창에서 영영 안 보인다.
			int joinBuildVersion = World.BuildVersion;
			int joinFieldVersion = World.Gatherables.Version;
			int joinPotVersion = World.Cauldrons.Version;

			// ★ 방금 온 창에는 <b>전체 그림</b>을 한 번 준다 (TASK-WM-217).
			//   방송은 「바뀐 것만」 싣기 때문에, 늦게 들어온 사람은 이 한 장이 없으면
			//   집도 들판도 없는 빈 세계를 본다(다음에 누가 뭘 지을 때까지).
			await SendAsync(connection, Protocol.WorldSnapshot(
				DollsVisibleTo(doll.Id),
				BuildingsVisibleTo(doll.Id),
				World.Calendar,
				null,
				GatherablesVisibleTo(doll.Id),
				World.Cauldrons,
				NextSnapshotSequence(),
				CauldronCellsVisibleTo(doll.Id)));
			MarkSnapshotState(connection, doll.Id, joinBuildVersion, joinFieldVersion, joinPotVersion);

			// ★ 이제야 방송 목록에 넣는다 — 첫 전체 그림이 <b>확실히 먼저</b> 나간 뒤다 (TASK-WM-301).
			sockets[doll.Id] = connection;

			// 이 연결의 말 예산 — 창 하나가 모두의 세계를 느리게 만들지 못하게 (TASK-WM-218).
			WitchMendokusai.Net.MessageBudget budget = new WitchMendokusai.Net.MessageBudget();
			DateTime lastSpoke = DateTime.UtcNow;

			byte[] buffer = new byte[4096];
			try
			{
				while (socket.State == WebSocketState.Open && stopping.IsCancellationRequested == false)
				{
					string text = await ReceiveTextAsync(socket, buffer, stopping);
					if (text == null)
						break;

					DateTime now = DateTime.UtcNow;
					budget.Refill((float)(now - lastSpoke).TotalSeconds);
					lastSpoke = now;

					// 예산을 넘긴 말은 버린다(끊지는 않는다 — 잠깐 몰릴 수도 있다).
					if (budget.TrySpend() == false)
						continue;

					await HandleMessageAsync(doll.Id, connection, text);
				}
			}
			catch (WebSocketException)
			{
				// 창이 그냥 닫히는 건 사고가 아니다 — 조용히 정리한다.
			}
			finally
			{
				sockets.TryRemove(doll.Id, out Connection _);
				watchingChest.TryRemove(doll.Id, out Vector3Int _);
				moveAllowance.Forget(doll.Id);
				pastPlaces.Forget(doll.Id);
				lineTime.Forget(doll.Id);
				movedAt.TryRemove(doll.Id, out long _);
				World.Leave(doll.Id);
				Interlocked.Exchange(ref worldDirty, 1); // 나간 사람의 자리·가방을 디스크로 내린다.
			}
		}

		/// <summary>인사를 받으면 그 연결의 인형에 주인을 붙이고, 새 사람이면 열쇠를 준다.</summary>
		private async Task HandleMessageAsync(int dollId, Connection socket, string text)
		{
			string kind = ReadMessageType(text);
			if (kind == Protocol.INVITE_ASK)
			{
				// 지금 이 연결의 주인에게만 초대 열쇠를 낸다 — 손님(주인 없음)은 낼 수 없다.
				int owner = World.OwnerOf(dollId);
				string code = owner == 0 ? null : Identities.IssueInvite(owner, World.Calendar.TotalDays());
				await SendAsync(socket, Protocol.Invite(code));
				Interlocked.Exchange(ref worldDirty, 1);
				return;
			}

			if (kind == Protocol.LINK)
			{
				string code = ReadStringField(text, "code");
				string deviceSecret = CurrentSecretOf(dollId);
				WitchMendokusai.Identity.WorldIdentityRecord linked = Identities.RedeemInvite(
					code, deviceSecret, World.Calendar.TotalDays(), out int previousIdentity);

				// 이 기기가 전에 쓰던 사람이 갖고 있던 것을 옮겨 준다 — 안 옮기면 사람 눈엔 사라진 것이다.
				if (linked != null && previousIdentity != 0 && previousIdentity != linked.id)
					World.MergePerson(previousIdentity, linked.id, ItemsCatalog);

				// 이었어도 지금 인형은 안 바꾼다(접속 도중 주인 갈아타기는 막혀 있다) —
				// 다시 들어오면 그때부터 그 사람이다.
				await SendAsync(socket, Protocol.Linked(linked != null, linked?.id ?? 0));
				Interlocked.Exchange(ref worldDirty, 1);
				return;
			}

			if (kind == Protocol.HELLO)
			{
				// 창이 이미 들고 있다는 도장 — 같으면 낱말표 다섯을 안 보낸다 (TASK-WM-238).
				_ = SendCatalogsUnlessKnownAsync(socket, ReadStringField(text, "knownCatalogs"), 0);

				string secret = ReadHelloSecret(text);

				// 계정을 댔으면 그걸 먼저 본다 — 기기 열쇠는 기기만 알아보기 때문이다.
				string klSession = ReadStringField(text, "klSession");
				string externalId = await Accounts.TryResolveAsync(klSession);

				// 쿠키를 못 읽는 창(게임)은 코드로 온다 — 둘 중 되는 쪽을 쓴다.
				if (string.IsNullOrEmpty(externalId))
					externalId = await Accounts.TryResolveCodeAsync(ReadStringField(text, "klCode"));

				// ★ 옆 세계에서 <b>걸어 들어온</b> 사람인가 (TASK-WM-254·259). 신원을 정하기 <b>전</b>에 본다 —
				//   이 통행증이 이 사람의 것이면, 이 세계는 그 이름표로 그를 <b>이어서</b> 알아봐야 한다.
				string travelPass = ReadStringField(text, "pass");
				long nowMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
				WitchMendokusai.Net.TravelPass.Bundle came = default;
				// 이 통행증으로 <b>처음</b> 들어오나 — 짐(가방·자리·몸)은 처음에만 준다 (TASK-WM-309).
				bool firstCrossing = false;
				bool travelling = string.IsNullOrEmpty(travelPass) == false
					&& WitchMendokusai.Net.TravelPass.TryRead(travelPass, zoneSecret, nowMs, out came, out _);

				if (travelling)
				{
					// ⚠ 이 창이 <b>이미 이 세계의 딴 사람</b>이면 통행증은 안 통한다 — 주워 온 남의 통행증이다.
					WitchMendokusai.Identity.WorldIdentityRecord already =
						string.IsNullOrEmpty(externalId) ? Identities.TryFind(secret) : Identities.TryFindExternal(externalId);
					if (already != null
						&& string.Equals(WitchMendokusai.Identity.WorldIdentityRegistry.MarkOf(already), came.Mark, StringComparison.Ordinal) == false)
					{
						travelling = false;
					}
					// 그리고 <b>짐은</b> 한 번만 — 복사한 통행증으로 두 번 들어오면 가방이 두 벌 온다.
					//   다만 <b>들어오는 것</b> 자체는 다시 허락한다 (TASK-WM-309): 통행증을 내밀다 줄이
					//   끊긴 사람을 손님으로 맞으면 가방도 자리도 잃는다(실측: 그때 장부에 신원이 하나 더 쌓였다).
					else if (passesUsed.TryClaim(travelPass, nowMs, out firstCrossing) == false)
					{
						// 같은 순간에 같은 통행증을 둘이 내밀었다 — 뒤엣것이 진짜 복사 시도다.
						travelling = false;
					}
				}

				WitchMendokusai.Identity.WorldIdentityRecord person;
				bool created = false;
				string grantedSecret = string.Empty;
				if (travelling)
					person = Identities.RecognizeMark(came.Mark, secret, World.Calendar.TotalDays());
				else if (string.IsNullOrEmpty(externalId) == false)
					person = Identities.RecognizeExternal(externalId, secret, World.Calendar.TotalDays(), out created, out grantedSecret);
				else
					person = Identities.Recognize(secret, out created, out grantedSecret, World.Calendar.TotalDays());

				// 이 창이 내민 기기 열쇠를 적어 둔다 — 세계는 지문만 가지므로 나중에 못 되돌린다.
				if (sockets.TryGetValue(dollId, out Connection speaking))
					speaking.DeviceSecret = string.IsNullOrEmpty(grantedSecret) ? secret : grantedSecret;
				// 계정으로 들어왔으면 그 이름으로 불린다 — 「karmolab:mascari」 뒤쪽만 쓴다.
				if (string.IsNullOrEmpty(externalId) == false)
				{
					int mark = externalId.IndexOf(':');
					Identities.NameIfEmpty(person.id, mark >= 0 ? externalId.Substring(mark + 1) : externalId);
				}

				World.Adopt(dollId, person.id, ItemsCatalog, out int evictedDollId);

				// 도장이 맞는 통행증이면 그 자리·그 가방·그 몸으로 세운다 — <b>이 세계의</b> 번호로.
				//   도장이 안 맞으면 그냥 손님이다(지어낸 통행증으로 남의 가방을 들고 오지 못한다).
				if (travelling)
				{
					// 이름도 같이 건너온다 — 안 그러면 국경을 넘는 순간 친구가 「손님 7」이 된다.
					Identities.NameIfEmpty(person.id, came.Name);

					// ⚠ 짐은 <b>처음 넘어올 때만</b> 준다. 다시 들어오는 사람은 이미 이 세계에 제 가방이 있다 —
					//   또 주면 그게 복사다(TASK-WM-309).
					if (firstCrossing)
					{
						World.WelcomeTraveller(dollId, person.id, new Vector3(came.X, 0f, came.Z), came.Bag, ItemsCatalog, came.Health);
						Interlocked.Exchange(ref worldDirty, 1);

						// ★ 「썼다」는 <b>짐을 건넨 뒤에</b> 적는다 (TASK-WM-309) — 내밀자마자 적으면
						//   그 사이에 줄이 끊긴 사람은 짐도 못 받고 통행증만 태운다(실측: 가방 1 → 0).
						passesUsed.MarkDelivered(travelPass, nowMs);
					}
				}

				// 중복 로그인 — 일반 MMORPG 처럼 나중에 온 쪽이 이긴다. 밀려난 창에는 이유를 말하고 닫는다
				// (조용히 끊으면 사람은 「버그」로 읽는다).
				if (evictedDollId != 0 && sockets.TryRemove(evictedDollId, out Connection evicted))
				{
					// ★ 밀려난 창 정리를 <b>기다리지 않는다</b> (TASK-WM-218).
					//   기다렸더니 새 창의 인사 답장이 통째로 막혔다 — 닫기(CloseAsync)는 상대의 답을
					//   기다리는데, 그 상대는 이미 우리 말을 안 듣는 중일 수 있다(시험이 잡았다).
					//   그래서 「보내고 닫기」는 옆으로 보내고, 새로 온 사람의 길을 먼저 연다.
					_ = EvictAsync(evicted);
				}

				await SendAsync(socket, Protocol.Welcome(dollId, grantedSecret, person.id, CatalogStamp));

				// 인사 뒤에도 전체 그림을 한 번 — 이때 자리·가방이 그 사람 것으로 바뀌고,
				// 방송은 「바뀐 것만」 실으므로 이 한 장이 없으면 집·들판을 영영 못 볼 수 있다.
				// ⚠ 판 번호는 그림 뜨기 <b>전</b>에 붙잡는다(위 MarkSnapshotState 주석 참고).
				int helloBuildVersion = World.BuildVersion;
				int helloFieldVersion = World.Gatherables.Version;
				int helloPotVersion = World.Cauldrons.Version;
				await SendAsync(socket, Protocol.WorldSnapshot(
					DollsVisibleTo(dollId),
					BuildingsVisibleTo(dollId),
					World.Calendar,
					null,
					GatherablesVisibleTo(dollId),
					World.Cauldrons,
					NextSnapshotSequence(),
					CauldronCellsVisibleTo(dollId)));
				MarkSnapshotState(socket, dollId, helloBuildVersion, helloFieldVersion, helloPotVersion);

				Interlocked.Exchange(ref worldDirty, 1);
				return;
			}

			HandleMessage(dollId, text);
		}

		/// <summary>밀려난 창에 이유를 말하고 닫는다 — 오래 걸려도 다른 사람의 길을 막지 않는다.</summary>
		private async Task EvictAsync(Connection evicted)
		{
			try
			{
				await SendAsync(evicted, Protocol.Kicked());

				// 답을 안 하는 상대도 있다 — 출력만 닫고 손을 뗀다(CloseAsync 는 상대의 답을 기다린다).
				await evicted.Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "same person elsewhere", CancellationToken.None);
			}
			catch (Exception error)
			{
				Console.WriteLine("[identity] 밀려난 창을 닫다 문제 — 무시하고 계속: " + error.Message);
			}
		}

		/// <summary>세계 + 신원 장부를 함께 뜬다 — 둘이 따로 저장되면 「누구 가방인지」가 갈라진다.</summary>
		private WorldSaveData SaveWorld()
		{
			WorldSaveData data = World.Save();
			data.identities = Identities.Save();
			return data;
		}

		/// <summary>한 마디 받는다. 닫히면 null.</summary>
		private static async Task<string> ReceiveTextAsync(WebSocket socket, byte[] buffer, CancellationToken stopping)
		{
			try
			{
				StringBuilder message = new StringBuilder();
				int receivedBytes = 0;

				while (true)
				{
					WebSocketReceiveResult received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), stopping);
					if (received.MessageType == WebSocketMessageType.Close)
						return null;

					if (received.MessageType != WebSocketMessageType.Text)
						return null;

					receivedBytes += received.Count;
					if (receivedBytes > MAX_MESSAGE_BYTES)
						return null;

					message.Append(Encoding.UTF8.GetString(buffer, 0, received.Count));
					if (received.EndOfMessage)
						return message.ToString();
				}
			}
			catch (WebSocketException)
			{
				return null;
			}
			catch (OperationCanceledException)
			{
				// ★ 세계가 닫히는 중이다 — 이 사람의 다음 말을 더 기다리지 않는다 (TASK-WM-217).
				//   전에는 <b>사람이 한 명만 붙어 있어도 서버가 멎는 데 30초</b>가 걸렸다(실측):
				//   수신이 「영원히」로 걸려 있어 종료가 그 대기를 붙잡았다. 배포마다 그만큼 세계가 닫힌다.
				return null;
			}
		}

		/// <summary>
		/// 그 연결이 <b>내민</b> 기기 열쇠 — 이을 때 이 열쇠를 그 사람에 붙인다.
		///
		/// ⚠ 전에는 장부에서 그 사람의 열쇠를 꺼내 썼다. 이제 세계는 열쇠를 <b>안 갖는다</b>(지문만) —
		///   되돌릴 수 없으니, 창이 인사할 때 내민 것을 그 연결에 적어 뒀다가 쓴다 (TASK-WM-220).
		/// </summary>
		private string CurrentSecretOf(int dollId)
		{
			return sockets.TryGetValue(dollId, out Connection connection) ? connection.DeviceSecret : null;
		}

		private static string ReadStringField(string text, string name)
		{
			try
			{
				using JsonDocument document = JsonDocument.Parse(text);
				return document.RootElement.TryGetProperty(name, out JsonElement value) ? value.GetString() : null;
			}
			catch (JsonException)
			{
				return null;
			}
		}

		private static string ReadHelloSecret(string text)
		{
			try
			{
				using JsonDocument document = JsonDocument.Parse(text);
				return document.RootElement.TryGetProperty("secret", out JsonElement secret) ? secret.GetString() : null;
			}
			catch (JsonException)
			{
				return null;
			}
		}

		/// <summary>창이 보낸 말을 계약(<see cref="Protocol"/>)대로 읽는다.</summary>
		/// <summary>시험용 — 판과 판 사이가 가장 많이 벌어진 순간 (ms) (TASK-WM-242).</summary>
		public long LongestTickGapMs => Interlocked.Read(ref longestTickGapMs);

		/// <summary>시험용 — 회선이 좁아 사람 수를 줄여 준 창 수 (TASK-WM-228).</summary>
		public int NarrowedWindowCount => CountNarrowed();

		/// <summary>회선이 좁아 사람 수를 줄여 준 창이 몇이나 되나 — /health 창구 (TASK-WM-228).</summary>
		private int CountNarrowed()
		{
			int narrowed = 0;
			foreach (System.Collections.Generic.KeyValuePair<int, Connection> entry in sockets)
			{
				if (entry.Value.MissedInARow > 0)
					narrowed += 1;
			}

			return narrowed;
		}

		/// <summary>지금 움직이는 중인 사람들 — 몰린 자리에서 떼어 둔 자리의 주인이 된다 (TASK-WM-227).</summary>
		private System.Collections.Generic.HashSet<int> MovingNow()
		{
			long now = System.Environment.TickCount64;
			System.Collections.Generic.HashSet<int> moving = new System.Collections.Generic.HashSet<int>();
			foreach (System.Collections.Generic.KeyValuePair<int, long> entry in movedAt)
			{
				if (now - entry.Value <= MOVING_WINDOW_MS)
					moving.Add(entry.Key);
			}

			return moving;
		}

		/// <summary>
		/// 이 행동을 <b>지금 해도 되나</b> (TASK-WM-305) — 번호가 붙어 있고 이미 한 것이면 안 한다.
		///
		/// ★ 왜 필요한가: 끊기는 순간 보낸 줍기는 <b>조용히 사라진다</b>(실측). 그걸 고치려면 창이
		///   답 못 받은 것을 다시 보내야 하는데, 그러면 세계가 두 번 할 위험이 생긴다.
		///   여기서 한 번만 하게 막고, 이미 한 것에도 <b>했다</b>고 답해 준다 — 창은 그래야 손을 놓는다.
		/// </summary>
		private bool ShouldDo(int dollId, JsonElement root, out long actionId)
		{
			actionId = root.TryGetProperty("did", out JsonElement idElement) ? (long)idElement.GetDouble() : 0;
			if (actionId <= 0)
				return true;

			int identityId = World.OwnerOf(dollId);
			if (actionOnce.FirstTime(identityId, actionId))
				return true;

			// 이미 한 일이다 — 다시 하진 않지만, 창이 계속 다시 보내지 않도록 답은 준다.
			TellRaw(dollId, Protocol.Did(actionId));
			return false;
		}

		/// <summary>창이 얹어 보낸 <b>세계의 도장</b>으로 그 사람의 회선을 잰다 (TASK-WM-303).</summary>
		private void HearLine(int dollId, JsonElement root)
		{
			if (root.TryGetProperty("ack", out JsonElement stamp) == false)
				return;

			lineTime.HeardStamp(dollId, (long)stamp.GetDouble(), System.Environment.TickCount64);
		}

		private void HandleMessage(int dollId, string text)
		{
			try
			{
				using JsonDocument document = JsonDocument.Parse(text);
				JsonElement root = document.RootElement;

				if (root.TryGetProperty("type", out JsonElement type) == false)
					return;

				string kind = type.GetString();

				if (kind == Protocol.MOVE)
				{
					float x = root.TryGetProperty("x", out JsonElement xElement) ? (float)xElement.GetDouble() : 0f;
					float z = root.TryGetProperty("z", out JsonElement zElement) ? (float)zElement.GetDouble() : 0f;

					// ★ 이 걸음의 번호를 적어 둔다 (TASK-WM-271) — <b>물린 걸음도</b> 적는다.
					//   창은 「세계가 여기까지 봤다」를 알아야 아직 답 안 온 걸음만 다시 굴린다.
					//   안 그러면 창은 늦은 회선만큼 앞서 나가고, 그 앞섬은 회선에 비례해 자란다(WM-270).
					if (sockets.TryGetValue(dollId, out Connection walking))
					{
						int step = root.TryGetProperty("seq", out JsonElement seqElement) ? (int)seqElement.GetDouble() : 0;
						if (step > walking.SawStep)
							walking.SawStep = step;

						// ★ 창이 세계의 도장을 되돌려 줬으면 그것으로 회선을 잰다 (TASK-WM-303).
						HearLine(dollId, root);
					}

					// 한 걸음 크기만 자르는 것으로는 못 막는다 — 빨리 보내면 빨리 갔다. 시계가 심판한다.
					Vector3 allowed = moveAllowance.Allow(dollId, System.Environment.TickCount64, new Vector3(x, 0f, z));
					if (allowed.x == 0f && allowed.z == 0f)
					{
						Interlocked.Increment(ref refusedSteps);
						return;
					}

					// ★ 내 땅 밖으로 가려 하는데 <b>이웃이 그 자리를 맡았으면</b> 넘겨준다 (TASK-WM-254).
					//   안 넘겨주면 경계는 벽이 된다 — 나눈 세계가 하나로 안 이어진다.
					Vector3 wanted = World.PositionOf(dollId) + allowed;
					if (World.Patch.Contains(wanted) == false
						&& neighbours.TryOwner(wanted, out string zoneName, out string zoneAddress))
					{
						if (sockets.TryGetValue(dollId, out Connection leaving))
							_ = HandOverAsync(dollId, leaving, zoneName, zoneAddress, wanted);

						return;
					}

					World.TryMove(dollId, allowed);
					movedAt[dollId] = System.Environment.TickCount64;
					return;
				}

				if (kind == Protocol.STRIKE)
				{
					// 같은 것을 두 번 하지 않는다 (TASK-WM-308) — 다시 보낸 것이면 답만 준다.
					if (ShouldDo(dollId, root, out long actionId) == false)
						return;

					if (actionId > 0) TellRaw(dollId, Protocol.Did(actionId));

					// 때리는 사람은 가만히 서 있을 수 있다 — 이 말에 얹힌 도장으로도 회선을 잰다.
					HearLine(dollId, root);

					// ★ 싸움도 <b>세계가</b> 판정한다 (TASK-WM-251) — 거리·간격·대상 셋 다.
					//   창이 우기면 그건 창의 화면에서만 일어난 일이다.
					int targetId = ReadInt(root, "targetId");
					// ★ 판정은 <b>때린 사람이 보고 있던 순간</b>으로 되감아 한다 (TASK-WM-303).
					//   회선이 먼 사람은 옛 화면을 보고 휘두른다 — 지금 자리로만 재면 그 사람만 계속 헛친다
					//   (실측: 같은 싸움에 곧은 회선 46번 · 지연 250ms 70번).
					long rewindMs = lineTime.RewindMsFor(dollId);
					WitchMendokusai.Net.StrikeRule.Denial why = World.TryStrike(dollId, targetId,
						System.Environment.TickCount64, pastPlaces, rewindMs, out int healthLeft, out bool wentDown);
					if (why != WitchMendokusai.Net.StrikeRule.Denial.None)
						return;

					_ = TellNearbyHurtAsync(targetId, dollId, healthLeft, wentDown);
					Interlocked.Exchange(ref worldDirty, 1);
					return;
				}

				if (kind == Protocol.SAY)
				{
					// 말도 끊기는 순간 사라지면 안 된다 (TASK-WM-307) — 같은 번호는 두 번 안 옮긴다.
					if (ShouldDo(dollId, root, out long saidId) == false)
						return;

					if (saidId > 0) TellRaw(dollId, Protocol.Did(saidId));

					// ★ 말은 사람이 직접 짓는 유일한 것이라 세계가 본다 (TASK-WM-250).
					//   빈 줄은 말이 아니고, 줄바꿈은 한 칸이 되고, 너무 길면 잘린다.
					string line = WitchMendokusai.Net.SaidLine.Clean(ReadStringField(text, "text"));
					if (line == null)
						return;

					// ★ <b>보이는 사람에게만</b> 간다 — 세계 반대편 사람에게까지 가면 그건 확성기다.
					//   누가 보이나는 이미 세계가 아는 것(관심 반경)이라 여기서 새로 정하지 않는다.
					_ = TellNearbyAsync(dollId, line);
					return;
				}

				if (kind == Protocol.CONSUME)
				{
					// 같은 것을 두 번 하지 않는다 (TASK-WM-305) — 다시 보낸 먹기 라면 답만 준다.
					if (ShouldDo(dollId, root, out long actionId) == false)
						return;

					// ★ 「받았다」를 먼저 말한다 — 되든 안 되든 <b>이 번호는 처리했다</b>는 뜻이다.
					//   거절이면 거절대로 따로 말이 간다(창은 그 둘을 다르게 읽는다).
					if (actionId > 0) TellRaw(dollId, Protocol.Did(actionId));

					// 없는 걸 썼다고 우겨도 소용없다 — 있는 만큼만 빠진다.
					World.TryConsume(dollId, ReadInt(root, "itemId"), System.Math.Max(1, ReadInt(root, "amount")));
					_ = SendBagAsync(dollId);
					Interlocked.Exchange(ref worldDirty, 1);
					return;
				}

				if (kind == Protocol.BAG_ASK)
				{
					// 다시 들어온 창이 자기 가방을 그리려면 물어볼 수 있어야 한다.
					_ = SendBagAsync(dollId);
					return;
				}

				if (kind == Protocol.GATHER)
				{
					// 같은 것을 두 번 하지 않는다 (TASK-WM-305) — 다시 보낸 줍기 라면 답만 준다.
					if (ShouldDo(dollId, root, out long actionId) == false)
						return;

					// ★ 「받았다」를 먼저 말한다 — 되든 안 되든 <b>이 번호는 처리했다</b>는 뜻이다.
					//   거절이면 거절대로 따로 말이 간다(창은 그 둘을 다르게 읽는다).
					if (actionId > 0) TellRaw(dollId, Protocol.Did(actionId));

					// ★ 창은 「저기 있는 저것을 줍겠다」만 말한다 (TASK-WM-217).
					//   전에는 「아이템 X 를 N개 주웠다」고 말하면 세계가 그냥 넣어 줬다 — 그건
					//   판정이 아니라 신고였고, 창을 고친 사람은 무엇이든 무한히 가질 수 있었다.
					int nodeId = ReadInt(root, "nodeId");
					Vector3 standing = World.PositionOf(dollId);
					if (World.Gatherables.TryTake(nodeId, standing.x, standing.z, World.Calendar.TotalMinutes(),
						out int itemId, out int amount, out GatherDenial why) == false)
					{
						// 왜 안 되는지 갈라서 말한다 (TASK-WM-220) — 사람에게도 다른 말이고,
						// 고칠 때도 다른 자리다. 뭉쳐 두면 관문이 빨개져도 어디를 볼지 모른다.
						Tell(dollId, Protocol.DENIED_GATHER, why switch
						{
							GatherDenial.NO_SUCH_PLACE => "거기엔 주울 게 없다",
							GatherDenial.OUT_OF_REACH => "손이 안 닿는다 — 더 가까이 가야 한다",
							GatherDenial.STILL_REGROWING => "아직 다시 자라는 중이다",
							GatherDenial.JUST_TAKEN => "남이 방금 가져갔다",
							_ => "지금은 주울 수 없다",
						});
						return;
					}

					// 가방이 꽉 차서 못 받으면 <b>도로 세운다</b> — 자리도 비고 손도 비는 일은 없다.
					//
					// ★ 실측 2026-08-10: 「하나도 못 받았을 때」만 되돌렸다. 그래서 3개짜리 자리를
					//   한 칸 남은 가방으로 주우면 1개만 들어가고 <b>2개가 증발했다</b>(자리도 비었다).
					//   못 든 만큼은 그 자리에 그대로 남아야 한다.
					int leftover = World.TryGather(dollId, ServerItemCatalog.Find(itemId), amount);
					if (leftover >= amount)
					{
						World.Gatherables.Restore(nodeId);

						// 왜 안 되는지 말해 준다 — 아무 말도 없으면 사람은 버튼이 고장 난 줄 안다.
						Tell(dollId, Protocol.DENIED_GATHER, "가방이 꽉 찼다 — 비우고 다시 오라");
						return;
					}

					if (leftover > 0)
					{
						World.Gatherables.RestorePartial(nodeId, leftover);
						Tell(dollId, Protocol.DENIED_GATHER, $"가방이 모자라 {leftover}개는 그 자리에 두고 왔다");
					}

					Interlocked.Exchange(ref worldDirty, 1);
					_ = SendBagAsync(dollId);
					return;
				}

				if (kind == Protocol.CHEST_ASK || kind == Protocol.CHEST_PUT || kind == Protocol.CHEST_TAKE)
				{
					Vector3Int cell = new Vector3Int(ReadInt(root, "x"), ReadInt(root, "y"), ReadInt(root, "z"));
					Vector3 standing = World.PositionOf(dollId);

					if (kind == Protocol.CHEST_PUT || kind == Protocol.CHEST_TAKE)
					{
						// 상자에 넣고 빼는 것은 <b>세계를 바꾼다</b> (TASK-WM-308) — 두 번 하면 물건이 는다/준다.
						if (ShouldDo(dollId, root, out long chestId) == false)
							return;

						if (chestId > 0) TellRaw(dollId, Protocol.Did(chestId));
					}

					if (kind == Protocol.CHEST_PUT)
					{
						// 가방에서 먼저 뺀다 — 넣다 남으면 도로 돌려준다(중간에 사라지면 안 된다).
						int itemId = ReadInt(root, "itemId");
						int wanted = System.Math.Max(1, ReadInt(root, "amount"));
						int missing = World.TryConsume(dollId, itemId, wanted);
						int moving = wanted - missing;
						if (moving > 0)
						{
							int leftover = World.Storages.Put(cell, ServerItemCatalog.Find(itemId), moving, standing.x, standing.z);
							if (leftover > 0)
								World.TryGather(dollId, ServerItemCatalog.Find(itemId), leftover);

							Interlocked.Exchange(ref worldDirty, 1);
							_ = SendBagAsync(dollId);
						}
					}
					else if (kind == Protocol.CHEST_TAKE)
					{
						int itemId = ReadInt(root, "itemId");
						int wanted = System.Math.Max(1, ReadInt(root, "amount"));
						int taken = World.Storages.Take(cell, itemId, wanted, standing.x, standing.z);
						if (taken > 0)
						{
							// 가방이 좁아 못 받으면 그만큼 상자로 되돌린다 — 사라지는 물건은 없다.
							int leftover = World.TryGather(dollId, ServerItemCatalog.Find(itemId), taken);
							if (leftover > 0)
								World.Storages.Put(cell, ServerItemCatalog.Find(itemId), leftover, standing.x, standing.z);

							Interlocked.Exchange(ref worldDirty, 1);
							_ = SendBagAsync(dollId);
						}
					}

					// 이 창은 지금 그 상자를 보고 있다 — 안이 바뀌면 다시 보내 준다.
					watchingChest[dollId] = cell;

					// 이 자리는 async 가 아니다 — 답장은 옆으로 보낸다(창 하나 때문에 세계가 기다리지 않게).
					if (sockets.TryGetValue(dollId, out Connection asking))
						_ = SendAsync(asking, Protocol.Chest(cell.x, cell.y, cell.z, World.Storages.Contents(cell)));

					return;
				}

				if (kind == Protocol.BREW)
				{
					// 같은 것을 두 번 하지 않는다 (TASK-WM-308) — 다시 보낸 것이면 답만 준다.
					if (ShouldDo(dollId, root, out long actionId) == false)
						return;

					if (actionId > 0) TellRaw(dollId, Protocol.Did(actionId));

					// ★ 창은 「무엇을 넣는지」만 말한다 (TASK-WM-217).
					//   전에는 방향과 세기를 창이 보냈다 — 아무것도 안 들고 저을 수 있었고,
					//   창을 고친 사람은 한 번에 목표 한가운데로 갈 수 있었다.
					//   이제 재료를 <b>가방에서 실제로 꺼내</b> 넣는다 — 그래서 줍기가 조리의 재료가 된다.
					int ingredientId = ReadInt(root, "itemId");
					if (World.Ingredients.TryStep(ingredientId, out WitchMendokusai.DomainSDK.Alchemy.BrewStep step) == false)
					{
						Tell(dollId, Protocol.BREW, "그건 솥에 넣는 것이 아니다");
						return;
					}

					if (World.TryConsume(dollId, ingredientId, 1) != 0)
					{
						Tell(dollId, Protocol.BREW, "가방에 없다 — 빈손으로는 못 젓는다");
						return;
					}

					// ★ 자리를 주면 <b>그 자리의 솥</b>에 넣는다 (TASK-WM-217) — 여럿이 각자 조리한다.
					//   자리를 안 주는 옛 창은 세계에 하나뿐인 솥을 쓴다(회귀 0).
					WitchMendokusai.DomainSDK.Alchemy.BrewStep placed = step;
					WorldCauldron pot = PotFor(dollId, root);
					if (pot == null)
					{
						// 재료는 이미 뺐다 — 못 넣으면 도로 돌려준다.
						World.TryGather(dollId, ServerItemCatalog.Find(ingredientId), 1);
						Tell(dollId, Protocol.BREW, "거기엔 솥이 없다 — 솥을 짓거나 가까이 가야 한다");
						return;
					}

					pot.AddStep(placed);
					World.Cauldrons.Touch();
					Interlocked.Exchange(ref worldDirty, 1);
					_ = SendBagAsync(dollId);

					return;
				}

				if (kind == Protocol.BREW_COMPLETE)
				{
					// 같은 것을 두 번 하지 않는다 (TASK-WM-308) — 다시 보낸 것이면 답만 준다.
					if (ShouldDo(dollId, root, out long actionId) == false)
						return;

					if (actionId > 0) TellRaw(dollId, Protocol.Did(actionId));

					// ★ 받을 자리부터 본다 (TASK-WM-217): 완성은 되돌릴 수 없다 —
					//   넣고 남은 걸 버리면 사람 눈엔 「만들었는데 사라졌다」다. 자리가 없으면 솥을 그대로 둔다.
					WorldCauldron completing = PotFor(dollId, root);
					if (completing == null)
					{
						Tell(dollId, Protocol.DENIED_COMPLETE, "거기엔 솥이 없다");
						return;
					}

					BrewCompletion peek = ServerRecipeBook.Book.Judge(completing.State);
					if (peek.Empty == false
						&& World.CanReceive(dollId, ServerItemCatalog.Find(peek.ResultItemId), peek.Amount) == false)
					{
						// 가방을 비우고 다시 오면 그 솥은 그대로 있다 — 조용히 무시하면 「고장」으로 읽힌다.
						Tell(dollId, Protocol.DENIED_COMPLETE, "가방이 꽉 찼다 — 비우고 다시 오면 그 솥은 그대로 있다");
						return;
					}

					// 완성은 세계가 한 사람에게만 내준다 — 둘이 같은 순간에 눌러도 뒤엣사람은 빈 솥.
					// 무엇이 나왔는지도 세계가 정한다(마도서) — 그리고 **그 자리에서 가방에 넣는다**.
					if (completing.TryComplete(ServerRecipeBook.Book, out BrewCompletion taken))
					{
						if (taken.Empty == false)
						{
							IItemData reward = ServerItemCatalog.Find(taken.ResultItemId);
							int leftover = World.TryGather(dollId, reward, taken.Amount);
							if (leftover > 0)
								Console.WriteLine($"[brew] 가방이 좁아 {leftover}개는 못 넣었다 (인형 {dollId})");

							Interlocked.Exchange(ref worldDirty, 1);
							_ = SendBagAsync(dollId);
						}

						World.Cauldrons.Touch();

						if (sockets.TryGetValue(dollId, out Connection claimer))
							_ = SendAsync(claimer, Protocol.BrewTaken(taken));
					}

					return;
				}

				if (kind == Protocol.RENAME)
				{
					// 같은 것을 두 번 하지 않는다 (TASK-WM-308) — 다시 보낸 것이면 답만 준다.
					if (ShouldDo(dollId, root, out long actionId) == false)
						return;

					if (actionId > 0) TellRaw(dollId, Protocol.Did(actionId));

					// ★ 이름은 남에게 보이는 것이라 <b>세계가 검사한다</b> (TASK-WM-218):
					//   빈 이름·공백만·끝없이 긴 이름·남과 똑같은 이름이 박히면 「누가 누군지」가 무너진다.
					int owner = World.OwnerOf(dollId);
					if (owner == 0)
					{
						Tell(dollId, Protocol.RENAME, "먼저 인사를 해야 이름을 정할 수 있다");
						return;
					}

					string wanted = root.TryGetProperty("name", out JsonElement said) ? said.GetString() : null;
					if (Identities.TryRename(owner, wanted, out string refused))
					{
						Interlocked.Exchange(ref worldDirty, 1);
						return;
					}

					Tell(dollId, Protocol.RENAME, refused);
					return;
				}

				if (kind == Protocol.CRAFT)
				{
					// 같은 것을 두 번 하지 않는다 (TASK-WM-308) — 다시 보낸 것이면 답만 준다.
					if (ShouldDo(dollId, root, out long actionId) == false)
						return;

					if (actionId > 0) TellRaw(dollId, Protocol.Did(actionId));

					// ★ 제작도 세계가 판정한다 (TASK-WM-217). 전에는 재료 확인도, <b>성공 주사위도</b>,
					//   지급도 창이 했다 — 창을 고친 사람은 언제나 성공하고 무엇이든 만들었다.
					int recipeId = ReadInt(root, "recipeId");
					CraftResult judged = ServerCraftBook.Book.Judge(
						recipeId,
						itemId => World.BagCount(dollId, itemId),
						(float)(craftDice.NextDouble() * 100.0));

					if (judged.Attempted == false)
					{
						Tell(dollId, Protocol.CRAFT, judged.Denied);
						if (sockets.TryGetValue(dollId, out Connection refused))
							_ = SendAsync(refused, Protocol.Crafted(judged));

						return;
					}

					// ★ 받을 자리부터 본다: 만들고 나서 못 받으면 재료만 사라진다.
					if (judged.Succeeded
						&& World.CanReceive(dollId, ServerItemCatalog.Find(judged.ResultItemId), judged.ResultAmount) == false)
					{
						CraftResult noRoom = new CraftResult
						{
							RecipeId = recipeId, Denied = "가방이 꽉 찼다 — 비우고 다시 오면 재료는 그대로다",
						};

						Tell(dollId, Protocol.CRAFT, noRoom.Denied);
						if (sockets.TryGetValue(dollId, out Connection full))
							_ = SendAsync(full, Protocol.Crafted(noRoom));

						return;
					}

					// 재료는 <b>성공하든 실패하든</b> 든다 — 그게 주사위를 굴리는 값이다.
					CraftRecipeEntry recipe = ServerCraftBook.Book.Find(recipeId);
					CraftIngredientEntry[] items = recipe.items ?? System.Array.Empty<CraftIngredientEntry>();
					for (int i = 0; i < items.Length; i++)
					{
						if (items[i] == null || items[i].amount <= 0)
							continue;

						World.TryConsume(dollId, items[i].itemId, items[i].amount);
					}

					if (judged.Succeeded)
						World.TryGather(dollId, ServerItemCatalog.Find(judged.ResultItemId), judged.ResultAmount);

					Interlocked.Exchange(ref worldDirty, 1);
					_ = SendBagAsync(dollId);

					if (sockets.TryGetValue(dollId, out Connection maker))
						_ = SendAsync(maker, Protocol.Crafted(judged));

					return;
				}

				if (kind == Protocol.BREW_RESET)
				{
					// 같은 것을 두 번 하지 않는다 (TASK-WM-308) — 다시 보낸 것이면 답만 준다.
					if (ShouldDo(dollId, root, out long actionId) == false)
						return;

					if (actionId > 0) TellRaw(dollId, Protocol.Did(actionId));

					WorldCauldron clearing = PotFor(dollId, root);
					if (clearing == null)
						return;

					clearing.ResetBrew();
					World.Cauldrons.Touch();
					return;
				}

				if (kind == Protocol.REMOVE)
				{
					// 같은 것을 두 번 하지 않는다 (TASK-WM-305) — 다시 보낸 부수기 라면 답만 준다.
					if (ShouldDo(dollId, root, out long actionId) == false)
						return;

					// ★ 「받았다」를 먼저 말한다 — 되든 안 되든 <b>이 번호는 처리했다</b>는 뜻이다.
					//   거절이면 거절대로 따로 말이 간다(창은 그 둘을 다르게 읽는다).
					if (actionId > 0) TellRaw(dollId, Protocol.Did(actionId));

					// 부수기도 서버가 판정한다 — 빈 칸을 찍으면 아무 일도 안 일어난다.
					if (World.TryRemoveBuilding(new Vector3Int(ReadInt(root, "x"), ReadInt(root, "y"), ReadInt(root, "z")),
						out int removedBuildingId))
					{
						// ★ 재료를 <b>절반</b> 돌려준다 (TASK-WM-217): 잘못 지었을 때 손해만 남으면
						//   사람은 아예 안 짓는다. 전액이면 남의 집을 부숴 재료를 버는 길이 열린다.
						World.Buildables.TryCost(removedBuildingId, out int backItemId, out int backAmount);
						int refund = backAmount / 2;
						if (refund > 0)
						{
							World.TryGather(dollId, ServerItemCatalog.Find(backItemId), refund);
							_ = SendBagAsync(dollId);
						}

						Interlocked.Exchange(ref worldDirty, 1);
					}

					return;
				}

				if (kind == Protocol.PLACE)
				{
					// 같은 것을 두 번 하지 않는다 (TASK-WM-305) — 다시 보낸 짓기 라면 답만 준다.
					if (ShouldDo(dollId, root, out long actionId) == false)
						return;

					// ★ 「받았다」를 먼저 말한다 — 되든 안 되든 <b>이 번호는 처리했다</b>는 뜻이다.
					//   거절이면 거절대로 따로 말이 간다(창은 그 둘을 다르게 읽는다).
					if (actionId > 0) TellRaw(dollId, Protocol.Did(actionId));

					int cellX = ReadInt(root, "x");
					int cellY = ReadInt(root, "y");
					int cellZ = ReadInt(root, "z");

					int buildingId = ReadInt(root, "buildingId");

					// 겹치면 서버가 거절한다 — 거절도 판정이다(창이 우기지 못한다).
					// 크기도 창에게 안 묻는다 (TASK-WM-217): 세계의 목록이 정본이라, 「이건 1×1 이다」로
					// 남의 집에 겹쳐 짓는 길이 아예 없다. 모르는 건물은 서지 않는다.
					// ★ 짓기는 <b>재료를 쓴다</b> (TASK-WM-217): 공짜로 무한히 지으면 줍기가 뜻을 잃는다.
					//   먼저 빼고, 못 지으면 도로 돌려준다(사라지는 물건은 없다).
					World.Buildables.TryCost(buildingId, out int costItemId, out int costAmount);
					int missing = costAmount > 0 ? World.TryConsume(dollId, costItemId, costAmount) : 0;
					if (missing > 0)
					{
						// 뺀 만큼은 도로 넣어 준다 — 절반만 빠지는 일은 없다.
						if (costAmount - missing > 0)
							World.TryGather(dollId, ServerItemCatalog.Find(costItemId), costAmount - missing);

						Tell(dollId, Protocol.DENIED_PLACE,
							"재료가 모자란다 — " + ServerItemCatalog.Catalog.NameOf(costItemId) + " " + costAmount + "개가 든다");
						return;
					}

					if (World.TryPlaceBuilding(new Vector3Int(cellX, cellY, cellZ), buildingId, World.Buildables))
					{
						Interlocked.Exchange(ref worldDirty, 1);
						if (costAmount > 0)
							_ = SendBagAsync(dollId);
					}
					else
					{
						if (costAmount > 0)
							World.TryGather(dollId, ServerItemCatalog.Find(costItemId), costAmount);

						Tell(dollId, Protocol.DENIED_PLACE, "거기엔 못 짓는다 — 겹치거나, 세계가 모르는 것이다");
					}
				}
			}
			catch (JsonException)
			{
				// 못 알아들을 말은 그냥 버린다 — 창이 이상한 걸 보냈다고 서버가 죽지 않는다.
			}
		}

		/// <summary>그 창에게만 자기 가방을 알린다.</summary>
		private async Task SendBagAsync(int dollId)
		{
			if (sockets.TryGetValue(dollId, out Connection socket) == false)
				return;

			// 가방에 든 것 **전부**. 전에는 서버가 아는 두 종류만 물어 봐서, 나머지는 갖고 있어도 창에 안 보였다.
			System.Collections.Generic.List<BagSaveEntry> bag = World.BagOf(dollId);
			System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, int>> counts =
				new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, int>>(bag.Count);

			for (int i = 0; i < bag.Count; i++)
				counts.Add(new System.Collections.Generic.KeyValuePair<int, int>(bag[i].itemId, bag[i].amount));

			await SendAsync(socket, Protocol.Bag(counts));
		}

		/// <summary>그 창에게만 「안 된다」고 말한다 — 답장은 옆으로 보낸다(세계가 기다리지 않게).</summary>
		/// <summary>이 사람에게 <b>그대로</b> 한 마디 (TASK-WM-305) — 거절이 아닌 말도 보내야 한다.</summary>
		private void TellRaw(int dollId, string line)
		{
			if (sockets.TryGetValue(dollId, out Connection listener))
				_ = SendAsync(listener, line);
		}

		private void Tell(int dollId, string what, string why)
		{
			if (sockets.TryGetValue(dollId, out Connection listener))
				_ = SendAsync(listener, Protocol.Denied(what, why));
		}

		/// <summary>
		/// 이 말이 가리키는 솥 (TASK-WM-217). 자리(x·z)를 주면 그 자리의 솥,
		/// 안 주면 세계에 하나뿐인 옛 솥. 손이 닿는지는 세계가 본다.
		/// </summary>
		private WorldCauldron PotFor(int dollId, JsonElement root)
		{
			// ★ 자리를 안 주면 <b>솥이 없다</b> (TASK-WM-217): 세계에 하나뿐이던 옛 솥은 폐기했다.
			//   규칙이 두 벌이면 「내 솥에 넣었는데 남의 화면에선 딴 솥이 움직이는」 일이 생긴다.
			if (root.TryGetProperty("x", out JsonElement _) == false)
				return null;

			Vector3 standing = World.PositionOf(dollId);
			Vector3Int cell = new Vector3Int(ReadInt(root, "x"), ReadInt(root, "y"), ReadInt(root, "z"));
			return World.Cauldrons.Reachable(cell, standing.x, standing.z);
		}

		private int ReadInt(JsonElement root, string name)
		{
			return root.TryGetProperty(name, out JsonElement element) ? (int)element.GetDouble() : 0;
		}

		private static string ReadMessageType(string text)
		{
			try
			{
				using JsonDocument document = JsonDocument.Parse(text);
				JsonElement root = document.RootElement;
				if (root.ValueKind != JsonValueKind.Object)
					return null;

				if (root.TryGetProperty("type", out JsonElement type) == false
					|| type.ValueKind != JsonValueKind.String)
					return null;

				return type.GetString();
			}
			catch (JsonException)
			{
				return null;
			}
			catch (InvalidOperationException)
			{
				return null;
			}
		}

		/// <summary>세계가 제 상태를 적는 간격 (ms) — 기본 10분, 시험은 짧게 준다.</summary>
		private static int HealthEveryMs =>
			int.TryParse(System.Environment.GetEnvironmentVariable("WM_HEALTH_EVERY_MS"), out int given) && given > 0
				? given
				: 600000;

		/// <summary>세계 파일 옆에 남는 상태 기록 (TASK-WM-297).</summary>
		private HealthJournal journal;

		/// <summary>
		/// 몇 분마다 <b>한 줄</b>씩 적는다 (TASK-WM-297).
		///
		/// ★ 왜: 소크 시험은 3분짜리다. 「며칠 돌면 어떻게 되나」는 prod 에서만 답이 나오는데,
		///   지금은 그 답을 볼 <b>기록이 없다</b> — 서버가 죽으면 그때까지의 상태도 같이 사라진다.
		/// </summary>
		private async Task RunHealthJournalLoopAsync(CancellationToken stopping)
		{
			journal = journal ?? new HealthJournal(store.Path);

			while (stopping.IsCancellationRequested == false)
			{
				try { await Task.Delay(HealthEveryMs, stopping); }
				catch (System.OperationCanceledException) { return; }

				journal.Write(JsonSerializer.Serialize(new
				{
					at = System.DateTimeOffset.UtcNow.ToString("o"),
					people = World.Snapshot().Length,
					heldMegabytes = GC.GetTotalMemory(false) / 1048576,
					allocatedMegabytes = GC.GetTotalAllocatedBytes(false) / 1048576,
					gcGen2 = GC.CollectionCount(2),
					longestTickGapMs = Interlocked.Read(ref longestTickGapMs),
					worldMinutes = World.Calendar.TotalMinutes(),
					buildings = World.Buildings().Length,
					identities = Identities.Count,
				}));
			}
		}

		/// <summary>
		/// 바뀐 게 있을 때만 디스크로 내려간다 (TASK-WM-217 단계 5).
		/// 매번 쓰면 아무도 안 짓는 밤에도 디스크가 초당 20번 돈다 — 그건 세계가 아니라 소음이다.
		/// </summary>
		private async Task RunSaveLoopAsync(CancellationToken stopping)
		{
			try
			{
				long lastSavedAtMs = System.Environment.TickCount64;
				long deedSeenAtMs = 0;

				while (stopping.IsCancellationRequested == false)
				{
					// ★ 자주 깨어나되 <b>적는 것은 드물게</b> (TASK-WM-310). 깨어나는 것은 공짜에 가깝고,
					//   적는 것만 비싸다 — 그래서 「언제 적을지」를 아래에서 따로 고른다.
					await Task.Delay(SAVE_TICK_MILLISECONDS, CancellationToken.None);

					long nowMs = System.Environment.TickCount64;
					bool deedWaiting = Interlocked.CompareExchange(ref worldDirty, 0, 0) != 0;
					if (deedWaiting && deedSeenAtMs == 0)
						deedSeenAtMs = nowMs;

					// 사람이 한 일이 있으면 <b>곧</b> 적는다(잃을 창을 5초 → 0.3초로).
					bool deedIsDue = deedSeenAtMs != 0 && nowMs - deedSeenAtMs >= SAVE_AFTER_DEED_MILLISECONDS;

					// 그 밖의 것(걸음·시계)은 예전처럼 느긋하게 — 매번 적으면 그건 세계가 아니라 소음이다.
					bool slowTurnIsDue = nowMs - lastSavedAtMs >= SAVE_INTERVAL_MILLISECONDS;

					if (deedIsDue == false && slowTurnIsDue == false)
						continue;

					lastSavedAtMs = nowMs;
					deedSeenAtMs = 0;

					// ⚠ 움직임은 dirty 를 안 찍는다(초당 20번 찍으면 뜻이 없다). 그래서 사람이 있으면
					//   그 자체로 「바뀌는 중」으로 본다 — 안 그러면 걷기만 하다 서버가 죽었을 때
					//   그동안 걸어온 자리가 통째로 사라진다(가방은 남고 자리만 옛것이 되는 이상한 상태).
					bool someoneIsHere = World.Snapshot().Length > 0;

					// 아무도 없어도 시간이 꽤 흘렀으면 적는다 — 안 그러면 시계가 뒤로 감긴다.
					int now = World.Calendar.TotalMinutes();
					bool clockDrifted = now - savedAtWorldMinute >= IDLE_SAVE_WORLD_MINUTES;

					if (Interlocked.Exchange(ref worldDirty, 0) == 0 && someoneIsHere == false && clockDrifted == false)
						continue;

					savedAtWorldMinute = now;

					// 빈손이고 오래 안 온 손님은 장부에서 지운다 — 안 그러면 장부가 영원히 커진다.
					// 뭔가 남긴 사람은 절대 안 지운다(세계를 지우는 짓이다).
					int forgotten = Identities.PruneGuests(World.Calendar.TotalDays(), GUEST_FORGET_DAYS, World.OwnsSomething);
					if (forgotten > 0)
						Console.WriteLine($"[identity] 빈손 손님 {forgotten}명을 장부에서 지웠다.");

					store.TrySave(SaveWorld());
				}
			}
			catch (Exception exception)
			{
				Console.WriteLine("[wm-server] 저장 루프가 죽었다: " + exception);
			}
		}

		/// <summary>한 창에 그림 하나 — 끝나면 다음 그림을 받을 수 있다고 표시한다.</summary>
		private async Task SendSnapshotAsync(Connection target, byte[] snapshot,
			int? sentBuildVersion = null, int? sentFieldVersion = null, int? sentPotVersion = null)
		{
			try
			{
				long bytes = snapshot.Length;
				Interlocked.Increment(ref broadcastSnapshotMessages);
				Interlocked.Add(ref broadcastSnapshotBytes, bytes);
				UpdateLargestSnapshot(bytes);
				await SendBytesAsync(target, snapshot);

				// 나갔다 — 이제서야 「이 판까지 보냈다」고 적는다(위 ⚠ 참고).
				if (sentBuildVersion.HasValue)
					target.SentBuildVersion = sentBuildVersion.Value;
				if (sentFieldVersion.HasValue)
					target.SentFieldVersion = sentFieldVersion.Value;
				if (sentPotVersion.HasValue)
					target.SentPotVersion = sentPotVersion.Value;
			}
			finally
			{
				Interlocked.Exchange(ref target.Sending, 0);
			}
		}

		private async Task RunBroadcastLoopAsync(CancellationToken stopping)
		{
			try
			{
				await BroadcastLoopAsync(stopping);
			}
			catch (Exception exception)
			{
				Console.WriteLine("[wm-server] 알림 루프가 죽었다: " + exception);
			}
		}

		/// <summary>윈도우의 잠자기 알갱이를 1ms 로 — 이게 없으면 50ms 를 재워도 62ms 를 잔다.</summary>
		[System.Runtime.InteropServices.DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
		private static extern uint BeginFineTimers(uint milliseconds);

		private async Task BroadcastLoopAsync(CancellationToken stopping)
		{
			// ★ 「초당 20번 말한다」고 적어 놓고 <b>16번</b>만 말하고 있었다 (실측 2026-08-12, TASK-WM-220).
			//   윈도우의 잠자기 알갱이가 15.6ms 라 50ms 를 재우면 실제로는 62ms 를 잔다 — 20% 손해다.
			//   ① 알갱이를 1ms 로 줄이고 ② 「다음 차례」를 붙잡아 <b>늦은 만큼 덜 잔다</b>.
			double periodMilliseconds = 1000.0 / SNAPSHOT_HZ;
			double nextDue = periodMilliseconds;

			if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
			{
				try { BeginFineTimers(1); }
				catch (System.Exception) { /* 못 줄여도 세계는 돈다 — 조금 뜸할 뿐이다 */ }
			}

			// ★ 시계는 <b>틱 수</b>가 아니라 <b>실제로 흐른 시간</b>으로 굴린다 (실측 2026-08-10).
			//   전에는 「한 바퀴 = 0.05분」으로 셌는데, 한 바퀴는 Task.Delay(50) 라 늘 50ms 보다 길다.
			//   그래서 <b>실제 5초에 세계는 4분</b>만 흘렀다 — 20% 느림, 하루면 다섯 시간이 밀린다.
			//   밤낮도 재생 시각도 다 같이 밀리고, 사람은 「이 세계는 시간이 이상하다」고 느낀다.
			System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
			double lastSeconds = 0.0;

			while (stopping.IsCancellationRequested == false)
			{
				double nowSeconds = clock.Elapsed.TotalSeconds;
				float sinceLast = (float)(nowSeconds - lastSeconds);
				lastSeconds = nowSeconds;

				// ★ 판과 판 사이가 <b>가장 많이 벌어진</b> 순간을 적어 둔다 (TASK-WM-242).
				//   평균은 예뻐도 한 번 크게 멎으면 사람은 그걸 「끊겼다」로 느낀다.
				//   저장(5초마다 세계를 통째로 적는다)이 세계를 멈추는지가 여기에 드러난다.
				long gapMs = (long)(sinceLast * 1000.0);
				long wasWorst = Interlocked.Read(ref longestTickGapMs);
				while (gapMs > wasWorst)
				{
					long swapped = Interlocked.CompareExchange(ref longestTickGapMs, gapMs, wasWorst);
					if (swapped == wasWorst)
						break;

					wasWorst = swapped;
				}

				// ★ 세계의 시간은 <b>사람이 있든 없든, 서버가 켜져 있든 아니든</b> 흐른다 (TASK-WM-266).
				//   가동 시간만큼만 흘리면, 나중에 뜬 세계·오래 꺼져 있던 세계는 영영 뒤처진다 —
				//   국경을 넘는 순간 밤이 낮이 된다. 그래서 <b>벽시계에서 유도</b>한다:
				//   맞춰 주는 게 아니라 각자 같은 셈을 하므로, 세계끼리 말을 섞을 필요가 없다.
				if (World.Calendar.SetTotalMinutes(SkyMinutesNow()))
					Interlocked.Exchange(ref worldDirty, 1);

				// ★ 안 바뀐 것은 안 보낸다 (TASK-WM-217). 건물 63채 + 들판 169자리를 20Hz 로 나르면
				//   사람이 몇 늘기도 전에 줄이 막힌다 — 창은 못 받은 프레임엔 지난 그림을 그대로 쓴다.
				int buildVersion = World.BuildVersion;
				int fieldVersion = World.Gatherables.Version;
				int potVersion = World.Cauldrons.Version;

				// 상자 안이 바뀌었으면, 그 상자를 보고 있는 창들에 다시 보낸다.
				int storageVersion = World.Storages.Version;
				if (storageVersion != sentStorageVersion)
				{
					sentStorageVersion = storageVersion;
					foreach (System.Collections.Generic.KeyValuePair<int, Vector3Int> watcher in watchingChest)
					{
						if (sockets.TryGetValue(watcher.Key, out Connection looking) == false)
							continue;

						_ = SendAsync(looking, Protocol.Chest(watcher.Value.x, watcher.Value.y, watcher.Value.z,
							World.Storages.Contents(watcher.Value)));
					}
				}

				// 이름표는 <b>바뀔 때만</b> — 자리는 초당 20번 바뀌지만 이름은 거의 안 바뀐다 (TASK-WM-220).
				await TellChangedNamesAsync();

				long sequence = NextSnapshotSequence();

				// ★ 같은 칸에 선 사람들은 <b>거의 같은 것</b>을 본다 — 그러면 글도 한 번만 지으면 된다
				//   (TASK-WM-217). 창마다 짓던 때, 400명이면 같은 글을 400번 지었다.
				//   칸에 상한보다 많이 모이면 공유가 깨지므로 그때만 창마다 짓는다(아래 fallback).
				System.Collections.Generic.Dictionary<string, (byte[] Bytes, System.Collections.Generic.HashSet<int> Inside)> madeForCell =
					new System.Collections.Generic.Dictionary<string, (byte[], System.Collections.Generic.HashSet<int>)>();
				WorldDoll[] everyone = EveryoneNow();

				// ★ 지나간 자리를 적어 둔다 (TASK-WM-303) — 되감아 판정하려면 <b>조금 전</b> 자리를 알아야 한다.
				//   그림을 만드는 이 자리가 곧 「세계가 정한 자리」라, 여기서 적으면 판정과 그림이 같은 것을 본다.
				long placeStampMs = System.Environment.TickCount64;
				for (int i = 0; i < everyone.Length; i += 1)
					pastPlaces.Remember(everyone[i].Id, placeStampMs, everyone[i].Position);

				foreach (System.Collections.Generic.KeyValuePair<int, Connection> entry in sockets)
				{
					if (entry.Value.Socket.State != WebSocketState.Open)
						continue;

					// 아직 지난 그림을 못 보낸 창은 건너뛴다 — 기다리면 모두가 그 창의 속도로 산다.
					// 대신 「건너뛰었다」고 적어 둔다: 다음 판은 <b>전부</b>를 줘야 그 창의 세계가 안 어긋난다.
					if (Interlocked.CompareExchange(ref entry.Value.Sending, 1, 0) != 0)
					{
						entry.Value.MissedAPlate = true;
						entry.Value.MissedInARow += 1;
						continue;
					}

					Connection target = entry.Value;

					// 줄이 뚫렸다 = 회선이 따라오고 있다. 줄여 뒀던 사람 수를 곧바로 되돌린다.
					if (target.MissedAPlate == false)
						target.MissedInARow = 0;

					// ★ 회선이 감당 못 하면 <b>감당할 만큼만</b> 보여 준다 (TASK-WM-228).
					//   이때는 칸 공유도 델타도 안 쓴다 — 작은 한 장을 통째로 준다.
					int allowedDolls = InterestCrowd.LimitWhenBehind(target.MissedInARow);
					if (allowedDolls < InterestCrowd.MAX_VISIBLE_DOLLS)
					{
						// ⚠ 작은 한 장에는 <b>「그 사람 나갔다」가 없다</b>(칸 장부를 안 쓰기 때문이다).
						//   그래서 이 창이 좁힘에서 돌아오면 <b>전체</b>를 한 장 줘야 한다 — 안 그러면
						//   좁힘 동안 떠난 사람이 그 창에 <b>유령으로 영영</b> 남는다(CI 가 그 자리를 잡았다).
						target.MissedAPlate = true;
						_ = SendSnapshotAsync(target, Encoding.UTF8.GetBytes(SmallPlateFor(entry.Key, allowedDolls, sequence)),
							null, null, null);
						continue;
					}
					bool interestChanged = UpdateInterestCell(target, entry.Key);

					// 건너뛴 창은 이번에 전부 받는다(그리고 표시를 지운다).
					if (target.MissedAPlate)
					{
						interestChanged = true;
						target.MissedAPlate = false;
					}
					bool sendBuildings = buildVersion != target.SentBuildVersion || interestChanged;
					bool sendField = fieldVersion != target.SentFieldVersion || interestChanged;
					bool sendPots = potVersion != target.SentPotVersion || interestChanged;

					// ⚠ 칸을 막 옮긴 창은 <b>전부</b> 받아야 한다 — 「바뀐 것만」을 주면 안 움직이는 사람들이
					//   그 창에는 영영 안 보인다(들어올 때 한 장을 못 받은 셈이다).
					string key = (interestChanged ? "F" : string.Empty)
						+ target.InterestCellX + ":" + target.InterestCellZ
						+ (sendBuildings ? "b" : string.Empty)
						+ (sendField ? "f" : string.Empty)
						+ (sendPots ? "p" : string.Empty);

					if (madeForCell.TryGetValue(key, out (byte[] Bytes, System.Collections.Generic.HashSet<int> Inside) ready) == false)
					{
						(string text, System.Collections.Generic.HashSet<int> inside) = SnapshotForCell(
							everyone, target.InterestCellX, target.InterestCellZ,
							sendBuildings, sendField, sendPots, sequence, interestChanged);

						// ★ 글자 → 바이트는 <b>한 번만</b>. 이 한 벌을 그 칸의 모든 창이 같이 쓴다.
						ready = (Encoding.UTF8.GetBytes(text), inside);
						madeForCell[key] = ready;
					}

					byte[] snapshot = ready.Bytes;

					// ★ 몰린 칸에서는 가까운 몇 명만 그 한 벌에 든다 — 자기가 빠진 창에게는
					//   <b>자기 자리만</b> 따로 알려 준다(60바이트). 자기가 안 보이면 화면이 통째로 멎는다.
					//
					// ⚠ 단 <b>안 바뀌었으면 안 보낸다</b> (TASK-WM-236): 한때 이 자리는 매 판 나갔다 —
					//   광장에 가만히 선 사람에게도 초당 20번, 사람 수만큼. 실측 2026-08-12:
					//   200명 광장에서 창 하나가 8초에 me 326개를 받았고 그중 움직인 판은 거의 없었다.
					//   「바뀐 것만 보낸다」는 이 세계의 규칙인데(WM-220) 이 한 자리만 예외였다.
					if (ready.Inside != null && ready.Inside.Contains(entry.Key) == false)
					{
						WorldDoll mine = FindDoll(everyone, entry.Key);
						if (mine != null && MyPlaceChanged(target, mine))
							_ = SendAsync(target, Protocol.Me(mine, Identities.NameOf));
					}
					// ★ 네 걸음을 여기까지 봤다 (TASK-WM-271) — <b>바뀌었을 때만</b> 한 마디.
					//   가만히 선 사람에게는 한 번도 안 간다(걷는 사람만 이 스무 바이트를 받는다).
					if (target.SawStep != target.ToldStep)
					{
						target.ToldStep = target.SawStep;

						_ = SendAsync(target, Protocol.StepSeen(target.SawStep));
					}

					// ⚠ 「보냈다」 표시는 <b>실제로 나간 뒤에</b> 한다. 먼저 표시했다가 그 보내기가
					//   실패하면 그 창은 그 집을 <b>영영</b> 못 받는다(다음에 또 바뀌기 전까지).
					//   화면엔 아무 일도 안 일어난 것처럼 보인다 — 「남이 지은 집이 안 보이던 것」의 부류다.
					_ = SendSnapshotAsync(target, snapshot, sendBuildings ? buildVersion : (int?)null,
						sendField ? fieldVersion : (int?)null, sendPots ? potVersion : (int?)null);
				}

				// 셈은 TickSchedule 에 있다(거기서 시험한다) — 여기서는 그만큼 잘 뿐이다.
				// ⚠ 밀렸어도 <b>1ms 는 잔다</b>: 아예 안 자면 닫기·받기가 끼어들 틈이 없다.
				(double waitMilliseconds, double due) = TickSchedule.Next(
					clock.Elapsed.TotalMilliseconds, nextDue, periodMilliseconds);
				nextDue = due;
				await Task.Delay(waitMilliseconds < 1.0 ? 1 : (int)waitMilliseconds, CancellationToken.None);
			}
		}

		/// <summary>
		/// 회선이 좁은 창에게 주는 <b>작은 한 장</b> (TASK-WM-228) — 가까운 몇 명만, 통째로.
		/// 건물·들판·솥은 안 싣는다: 지금 이 창에 모자란 건 대역폭이고, 그것들은 안 움직인다.
		/// </summary>
		private string SmallPlateFor(int viewerDollId, int limit, long sequence)
		{
			WorldDoll[] all = EveryoneNow();
			Vector3 viewer = World.PositionOf(viewerDollId);
			float radiusSquared = PLAYER_INTEREST_RADIUS * PLAYER_INTEREST_RADIUS;
			System.Collections.Generic.List<WorldDoll> near = new System.Collections.Generic.List<WorldDoll>();
			for (int i = 0; i < all.Length; i++)
			{
				float deltaX = all[i].Position.x - viewer.x;
				float deltaZ = all[i].Position.z - viewer.z;
				if (deltaX * deltaX + deltaZ * deltaZ <= radiusSquared)
					near.Add(all[i]);
			}

			WorldDoll[] few = InterestCrowd.Nearest(near, viewer, viewerDollId, limit, MovingNow());
			return Protocol.WorldSnapshot(few, null, World.Calendar, null, null, null,
				sequence, null, true, null, false, null);
		}

		/// <summary>
		/// 시험용 — 모든 창을 「연달아 이만큼 놓쳤다」로 표시한다 (TASK-WM-246).
		/// 회선이 진짜로 막히기를 기다리면 기계 속도에 기대게 된다(그 시험은 느린 러너에서 못 잰다).
		/// </summary>
		public void MarkBehindForTest(int misses)
		{
			foreach (System.Collections.Generic.KeyValuePair<int, Connection> entry in sockets)
			{
				entry.Value.MissedInARow = misses;

				// ⚠ 「놓쳤다」 표식도 같이 세운다 — 진짜로 밀릴 때는 둘이 함께 선다.
				//   숫자만 세우면 다음 판이 곧바로 0 으로 되돌린다(그래서 좁힘이 아예 안 걸렸다).
				entry.Value.MissedAPlate = misses > 0;
			}
		}

		/// <summary>시험용 — 모든 창을 「지난 판 건너뜀」으로 표시한다 (TASK-WM-220).</summary>
		public void MarkMissedForTest()
		{
			foreach (System.Collections.Generic.KeyValuePair<int, Connection> entry in sockets)
				entry.Value.MissedAPlate = true;
		}

		/// <summary>그 곳에서 하나 더 붙어도 되나 — 되면 세어 둔다.</summary>
		private bool TryEnterPlace(string origin)
		{
			// 같은 기계에서 온 창은 안 센다 — 그건 세계를 돌리는 사람 자신이다(위 ClientOrigin 참고).
			if (ClientOrigin.IsSameMachine(origin))
				return true;

			lock (windowsPerPlace)
			{
				windowsPerPlace.TryGetValue(origin, out int open);
				if (open >= MAX_WINDOWS_PER_PLACE)
					return false;

				windowsPerPlace[origin] = open + 1;
				return true;
			}
		}

		/// <summary>그 곳의 창 하나가 닫혔다 — 0 이 되면 장부에서 지운다(오래 돌아도 안 부풀게).</summary>
		private void LeavePlace(string origin)
		{
			if (ClientOrigin.IsSameMachine(origin))
				return;

			lock (windowsPerPlace)
			{
				if (windowsPerPlace.TryGetValue(origin, out int open) == false)
					return;

				if (open <= 1)
					windowsPerPlace.Remove(origin);
				else
					windowsPerPlace[origin] = open - 1;
			}
		}

		private void UpdateLargestSnapshot(long bytes)
		{
			long previous = Interlocked.Read(ref largestBroadcastSnapshotBytes);
			while (bytes > previous)
			{
				long exchanged = Interlocked.CompareExchange(ref largestBroadcastSnapshotBytes, bytes, previous);
				if (exchanged == previous)
					return;

				previous = exchanged;
			}
		}

		/// <summary>시험용 — 지금까지 나간 판의 마지막 번호 (TASK-WM-246).</summary>
		public long LastSnapshotSequence => Interlocked.Read(ref snapshotSequence);

		private long NextSnapshotSequence()
		{
			return Interlocked.Increment(ref snapshotSequence);
		}

		/// <summary>
		/// 「이 판까지 보냈다」를 적는다 — <b>그림을 뜬 그 순간의 판</b>으로 적어야 한다.
		///
		/// ⚠ 지금(마친 뒤)의 판으로 적으면, 그림을 뜬 뒤 들어온 집이 <b>이미 보낸 것</b>으로 둔갑한다.
		///   그 창은 그 집을 영영 못 받는다(다음에 또 누가 지을 때까지). 시험이 이 자리에서
		///   드문드문 빨개졌고, 원인은 「그림 뜨기 → 짓기 → 표시」 순서였다.
		/// </summary>
		private void MarkSnapshotState(Connection connection, int viewerDollId,
			int buildVersion, int fieldVersion, int potVersion)
		{
			connection.SentBuildVersion = buildVersion;
			connection.SentFieldVersion = fieldVersion;
			connection.SentPotVersion = potVersion;
			UpdateInterestCell(connection, viewerDollId);
		}

		private bool UpdateInterestCell(Connection connection, int viewerDollId)
		{
			Vector3 viewer = World.PositionOf(viewerDollId);
			int cellX = (int)MathF.Floor(viewer.x / INTEREST_CELL_SIZE);
			int cellZ = (int)MathF.Floor(viewer.z / INTEREST_CELL_SIZE);
			bool changed = connection.InterestCellX != cellX || connection.InterestCellZ != cellZ;
			connection.InterestCellX = cellX;
			connection.InterestCellZ = cellZ;
			return changed;
		}

		/// <summary>
		/// 그 사람을 옆 세계로 <b>넘겨준다</b> (TASK-WM-254).
		/// 통행증(신원·자리·가방 + 도장)을 쥐여 보내고, 이 세계에서는 내보낸다 —
		/// 둘 다 데리고 있으면 그 사람은 두 세계에 동시에 있게 된다(가방이 복사된다).
		/// </summary>
		private async Task HandOverAsync(int dollId, Connection socket, string zoneName, string zoneAddress, Vector3 landing)
		{
			int identityId = World.OwnerOf(dollId);
			System.Collections.Generic.List<(int ItemId, int Amount)> carried =
				new System.Collections.Generic.List<(int, int)>();

			foreach (BagSaveEntry held in World.BagOf(dollId))
				carried.Add((held.itemId, held.amount));

			// ⚠ 실어 보내는 것은 <b>세계 공통 이름표</b>다 (TASK-WM-259) — 이 세계의 번호를 보내면
			//   저쪽에서 그 번호로 사는 <b>남</b>이 된다(이름도 저장분도 그 사람 것이 된다).
			string pass = WitchMendokusai.Net.TravelPass.Write(
				new WitchMendokusai.Net.TravelPass.Bundle(Identities.MarkOf(identityId), Identities.NameOf(identityId),
					landing.x, landing.z, carried,
					System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), World.HealthOf(dollId)),
				zoneSecret);

			await SendAsync(socket, Protocol.MoveOn(zoneName, zoneAddress, landing.x, landing.z, pass));

			// ★ <b>창이 실제로 떠날 때까지</b> 데리고 있는다 (TASK-WM-279).
			//   전에는 200ms 뒤 무조건 내보냈다. 그런데 창은 이제 저 세계에 <b>먼저 붙어 보고</b>
			//   첫 그림이 온 뒤에 이 줄을 놓는다(멎는 시간 1097ms → 341ms). 그 사이에 내보내면
			//   저 세계가 꺼져 있을 때 그 사람은 <b>두 세계 어디에도 없는</b> 사람이 된다.
			//   그러니 줄이 닫히면 그때 내보낸다 — 안 닫히면 유예까지만 기다린다.
			long until = System.Environment.TickCount64 + HANDOVER_GRACE_MS;
			while (System.Environment.TickCount64 < until && socket.Socket.State == WebSocketState.Open)
				await Task.Delay(50);

			// 아직 붙어 있으면 안 넘어간 것이다 — 저 세계가 안 열렸다(그대로 여기 산다).
			if (socket.Socket.State == WebSocketState.Open)
				return;

			World.Leave(dollId);

			// 들고 간 것을 이 세계도 기억하고 있으면, 돌아왔을 때 <b>두 벌</b>이 된다 (TASK-WM-259).
			World.ForgetPerson(identityId);
			Interlocked.Exchange(ref worldDirty, 1);
		}

		/// <summary>
		/// 이 인형을 뭐라고 부르나 — 국경 너머 그림자는 <b>빌려 온 이름</b>을 쓴다 (TASK-WM-263).
		/// 그림자는 이 세계의 신원부에 없으므로, 여기서 안 갈라 주면 국경 너머 사람은 이름이 없다.
		/// </summary>
		private string NameOfDoll(WorldDoll one)
		{
			if (string.IsNullOrEmpty(one.BorrowedName) == false)
				return one.BorrowedName;

			return Identities.NameOf(one.IdentityId) ?? string.Empty;
		}

		/// <summary>이웃 세계임을 증명하는 도장 — 두 세계만 아는 말로 찍는다 (TASK-WM-263).</summary>
		private string BorderSeal(string zoneName)
		{
			using System.Security.Cryptography.HMACSHA256 stamp =
				new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(zoneSecret ?? string.Empty));

			byte[] print = stamp.ComputeHash(Encoding.UTF8.GetBytes("국경:" + (zoneName ?? string.Empty)));
			StringBuilder hex = new StringBuilder(print.Length * 2);
			foreach (byte one in print)
				hex.Append(one.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));

			return hex.ToString();
		}

		/// <summary>
		/// 이웃 세계가 들어오는 문 (TASK-WM-263) — 여기서는 인형을 만들지 않는다.
		/// 오는 말은 하나뿐이다: 「내 국경 띠에 이 사람들이 있다」.
		/// </summary>
		private async Task ServePeerAsync(WebSocket socket, CancellationToken stopping)
		{
			byte[] buffer = new byte[65536];
			try
			{
				while (socket.State == WebSocketState.Open && stopping.IsCancellationRequested == false)
				{
					string text = await ReceiveTextAsync(socket, buffer, stopping);
					if (text == null)
						break;

					string kind = ReadMessageType(text);
					if (kind != Protocol.NEARBY && kind != Protocol.HEARD)
						continue;

					string zone = ReadStringField(text, "zone");
					if (string.IsNullOrEmpty(zone))
						continue;

					// ⚠ 도장이 없으면 아무나 남의 세계에 사람을 그려 넣는다(있지도 않은 무리를 세운다).
					if (SameSeal(ReadStringField(text, "seal"), BorderSeal(zone)) == false)
						continue;

					if (kind == Protocol.HEARD)
					{
						// 국경 너머에서 건너온 말 (TASK-WM-264) — 그 자리 가까이 있는 내 사람들에게.
						(int who, float x, float z) = ReadHeardWhere(text);
						await HearFromNeighbourAsync(zone, who,
							ReadStringField(text, "name"), ReadStringField(text, "text"), x, z);
						continue;
					}

					shadows.TakeFrom(zone, ReadNearby(text), System.Environment.TickCount64);
				}
			}
			catch (WebSocketException)
			{
				// 이웃이 꺼지는 건 사고가 아니다 — 그림자는 시간이 지나면 스스로 사라진다.
			}
		}

		/// <summary>도장 비교는 <b>끝까지</b> 본다 — 빨리 틀리면 맞춰 갈 수 있다.</summary>
		private static bool SameSeal(string said, string mine)
		{
			if (said == null || mine == null || said.Length != mine.Length)
				return false;

			int different = 0;
			for (int i = 0; i < said.Length; i++)
				different |= said[i] ^ mine[i];

			return different == 0;
		}

		/// <summary>건너온 말이 <b>어디서</b> 났나 — 못 읽으면 0,0(그 자리엔 아무도 없다).</summary>
		private static (int Who, float X, float Z) ReadHeardWhere(string text)
		{
			try
			{
				using JsonDocument document = JsonDocument.Parse(text);
				JsonElement root = document.RootElement;
				return (
					root.TryGetProperty("dollId", out JsonElement who) ? who.GetInt32() : 0,
					root.TryGetProperty("x", out JsonElement x) ? (float)x.GetDouble() : 0f,
					root.TryGetProperty("z", out JsonElement z) ? (float)z.GetDouble() : 0f);
			}
			catch (JsonException)
			{
				return (0, 0f, 0f);
			}
		}

		private static System.Collections.Generic.List<(int DollId, float X, float Z, string Name)> ReadNearby(string text)
		{
			System.Collections.Generic.List<(int, float, float, string)> people =
				new System.Collections.Generic.List<(int, float, float, string)>();

			try
			{
				using JsonDocument document = JsonDocument.Parse(text);
				if (document.RootElement.TryGetProperty("dolls", out JsonElement dolls) == false)
					return people;

				foreach (JsonElement one in dolls.EnumerateArray())
				{
					if (one.TryGetProperty("id", out JsonElement id) == false)
						continue;

					people.Add((
						id.GetInt32(),
						one.TryGetProperty("x", out JsonElement x) ? (float)x.GetDouble() : 0f,
						one.TryGetProperty("z", out JsonElement z) ? (float)z.GetDouble() : 0f,
						one.TryGetProperty("name", out JsonElement name) ? (name.GetString() ?? string.Empty) : string.Empty));
				}
			}
			catch (JsonException)
			{
				// 못 읽는 판은 그냥 버린다 — 다음 판이 100ms 뒤에 온다.
			}

			return people;
		}

		/// <summary>이웃에게 국경 띠를 알려 주는 간격 (ms) — 사람 판(50ms)보다 뜸해도 눈에 안 띈다.</summary>
		private const int TELL_NEIGHBOURS_EVERY_MS = 100;

		/// <summary>
		/// 이웃마다 살아 있는 줄 하나 (TASK-WM-264) — 국경 띠 알림도, 넘어가는 말도 이 줄로 간다.
		/// ⚠ 한 줄에 두 곳에서 동시에 쓰면 소켓이 깨진다 — 자물쇠를 같이 들고 다닌다.
		/// </summary>
		private sealed class PeerLine
		{
			public PeerLine(System.Net.WebSockets.ClientWebSocket socket)
			{
				Socket = socket;
			}

			public System.Net.WebSockets.ClientWebSocket Socket { get; }

			public SemaphoreSlim Turn { get; } = new SemaphoreSlim(1, 1);
		}

		private readonly System.Collections.Concurrent.ConcurrentDictionary<string, PeerLine> peerLines =
			new System.Collections.Concurrent.ConcurrentDictionary<string, PeerLine>();

		/// <summary>그 이웃에게 한 마디 — 줄이 아직 안 이어졌으면 조용히 흘린다(다음 판이 곧 온다).</summary>
		private async Task SendToPeerAsync(string address, string word)
		{
			if (peerLines.TryGetValue(address, out PeerLine line) == false)
				return;

			if (line.Socket.State != WebSocketState.Open)
				return;

			await line.Turn.WaitAsync();
			try
			{
				byte[] payload = Encoding.UTF8.GetBytes(word);
				await line.Socket.SendAsync(new System.ArraySegment<byte>(payload),
					WebSocketMessageType.Text, true, CancellationToken.None);
			}
			catch (System.Exception)
			{
				// 줄이 끊겼다 — 잇는 것은 저쪽 루프가 한다.
			}
			finally
			{
				line.Turn.Release();
			}
		}

		/// <summary>이웃마다 줄 하나 — 끊기면 다시 잇는다(옆 세계가 나중에 떠도 저절로 이어진다).</summary>
		private async Task RunBorderLoopsAsync(CancellationToken stopping)
		{
			foreach ((WitchMendokusai.Net.ZonePatch Patch, string Address) land in neighbours.Lands)
				_ = TellNeighbourLoopAsync(land.Patch, land.Address, stopping);

			await Task.CompletedTask;
		}

		/// <summary>
		/// 저 이웃의 땅에서 <see cref="WitchMendokusai.Net.BorderBand.BAND"/> 안에 있는 내 사람들을
		/// 계속 알려 준다 — 그래야 국경 너머가 보인다.
		/// </summary>
		private async Task TellNeighbourLoopAsync(WitchMendokusai.Net.ZonePatch land, string address, CancellationToken stopping)
		{
			// 사람이 쓰는 문 주소를 받았다 — 이웃 전용 문으로 바꾼다.
			string door = address.EndsWith("/ws", System.StringComparison.Ordinal)
				? address.Substring(0, address.Length - 3) + "/peer"
				: address;

			while (stopping.IsCancellationRequested == false)
			{
				System.Net.WebSockets.ClientWebSocket socket = new System.Net.WebSockets.ClientWebSocket();
				PeerLine line = new PeerLine(socket);
				try
				{
					await socket.ConnectAsync(new System.Uri(door), stopping);
					peerLines[address] = line;

					while (socket.State == WebSocketState.Open && stopping.IsCancellationRequested == false)
					{
						await SendToPeerAsync(address, Protocol.Nearby(World.Patch.Name,
							BorderSeal(World.Patch.Name), AtTheBorderOf(land), Identities.NameOf));

						await Task.Delay(TELL_NEIGHBOURS_EVERY_MS, stopping);
					}
				}
				catch (System.Exception)
				{
					// 이웃이 아직 안 떴거나 꺼졌다 — 잠깐 뒤에 다시 잇는다(사람 손 없이).
				}
				finally
				{
					peerLines.TryRemove(address, out PeerLine _);
					socket.Dispose();
				}

				if (stopping.IsCancellationRequested)
					return;

				try { await Task.Delay(1000, stopping); }
				catch (System.OperationCanceledException) { return; }
			}
		}

		/// <summary>저 이웃 땅에서 띠 안에 있는 내 사람들 — 붐벼도 정해진 수까지만.</summary>
		private WorldDoll[] AtTheBorderOf(WitchMendokusai.Net.ZonePatch land)
		{
			WorldDoll[] mine = World.Snapshot();
			System.Collections.Generic.List<WorldDoll> close = new System.Collections.Generic.List<WorldDoll>();
			foreach (WorldDoll one in mine)
			{
				if (WitchMendokusai.Net.BorderBand.WorthTelling(land, one.Position) == false)
					continue;

				close.Add(one);
				if (close.Count >= WitchMendokusai.Net.BorderBand.MOST_SHADOWS)
					break;
			}

			return close.ToArray();
		}

		/// <summary>
		/// 이 세계 사람 + <b>국경 너머 그림자</b> (TASK-WM-263).
		/// 알림·관심 반경·이름표가 다 이 한 목록을 본다 — 한 곳이라도 빠지면 그 자리에서만 안 보인다.
		/// </summary>
		private WorldDoll[] EveryoneNow()
		{
			WorldDoll[] mine = World.Snapshot();
			WorldDoll[] beyond = shadows.Alive(System.Environment.TickCount64);
			if (beyond.Length == 0)
				return mine;

			WorldDoll[] all = new WorldDoll[mine.Length + beyond.Length];
			mine.CopyTo(all, 0);
			beyond.CopyTo(all, mine.Length);
			return all;
		}

		/// <summary>누가 맞았다를 <b>그 사람이 보이는 사람</b>에게 나른다 (TASK-WM-251).</summary>		/// <summary>누가 맞았다를 <b>그 사람이 보이는 사람</b>에게 나른다 (TASK-WM-251).</summary>
		private async Task TellNearbyHurtAsync(int dollId, int byDollId, int health, bool wentDown)
		{
			string hurt = Protocol.Hurt(dollId, byDollId, health, wentDown);

			Vector3 from = World.PositionOf(dollId);
			float radiusSquared = PLAYER_INTEREST_RADIUS * PLAYER_INTEREST_RADIUS;
			WorldDoll[] everyone = World.Snapshot();

			for (int i = 0; i < everyone.Length; i++)
			{
				WorldDoll one = everyone[i];
				float awayX = one.Position.x - from.x;
				float awayZ = one.Position.z - from.z;

				// 때린 사람에게는 늘 간다 — 맞았는지 안 맞았는지 모르면 싸움이 안 된다.
				if (one.Id != byDollId && (awayX * awayX) + (awayZ * awayZ) > radiusSquared)
					continue;

				if (sockets.TryGetValue(one.Id, out Connection watcher))
					await SendAsync(watcher, hurt);
			}
		}

		/// <summary>그 사람이 한 말을 <b>그 사람이 보이는 사람</b>에게 나른다 (TASK-WM-250).</summary>
		private async Task TellNearbyAsync(int dollId, string line)
		{
			string name = Identities.NameOf(World.OwnerOf(dollId)) ?? string.Empty;
			string said = Protocol.Said(dollId, name, line);

			Vector3 from = World.PositionOf(dollId);
			float radiusSquared = PLAYER_INTEREST_RADIUS * PLAYER_INTEREST_RADIUS;
			WorldDoll[] everyone = World.Snapshot();

			for (int i = 0; i < everyone.Length; i++)
			{
				WorldDoll one = everyone[i];
				float awayX = one.Position.x - from.x;
				float awayZ = one.Position.z - from.z;
				if ((awayX * awayX) + (awayZ * awayZ) > radiusSquared)
					continue;

				if (sockets.TryGetValue(one.Id, out Connection listener))
					await SendAsync(listener, said);
			}

			// ★ 말도 국경을 건넌다 (TASK-WM-264) — 안 건너가면 1m 옆 사람에게 말을 못 건다.
			//   보이는데 말이 안 통하면 그건 더 이상한 세계다(WM-263 이 눈만 이어 준 셈).
			await TellNeighboursHeardAsync(dollId, name, line, from);
		}

		/// <summary>국경 띠에 선 사람의 말을 이웃 세계로 넘긴다 (TASK-WM-264).</summary>
		private async Task TellNeighboursHeardAsync(int dollId, string name, string line, Vector3 from)
		{
			foreach ((WitchMendokusai.Net.ZonePatch Patch, string Address) land in neighbours.Lands)
			{
				if (WitchMendokusai.Net.BorderBand.WorthTelling(land.Patch, from) == false)
					continue;

				await SendToPeerAsync(land.Address, Protocol.Heard(World.Patch.Name,
					BorderSeal(World.Patch.Name), dollId, name, line, from.x, from.z));
			}
		}

		/// <summary>이웃이 넘겨 온 말을 <b>그 자리 가까이</b> 있는 내 사람들에게 나른다.</summary>
		private async Task HearFromNeighbourAsync(string zone, int dollId, string name, string line, float x, float z)
		{
			int shadowId = WitchMendokusai.Net.BorderBand.ShadowId(zone, dollId);
			if (shadowId == 0)
				return;

			string said = Protocol.Said(shadowId, name, line);
			float radiusSquared = PLAYER_INTEREST_RADIUS * PLAYER_INTEREST_RADIUS;

			foreach (WorldDoll one in World.Snapshot())
			{
				float awayX = one.Position.x - x;
				float awayZ = one.Position.z - z;
				if ((awayX * awayX) + (awayZ * awayZ) > radiusSquared)
					continue;

				if (sockets.TryGetValue(one.Id, out Connection listener))
					await SendAsync(listener, said);
			}
		}

		/// <summary>이름이 바뀐 사람만 모두에게 알린다 — 새로 온 사람·이름을 고친 사람 (TASK-WM-220).</summary>
		private async Task TellChangedNamesAsync()
		{
			WorldDoll[] everyone = EveryoneNow();
			System.Collections.Generic.List<(int DollId, string Name)> changed =
				new System.Collections.Generic.List<(int, string)>();

			System.Collections.Generic.HashSet<int> here = new System.Collections.Generic.HashSet<int>();
			for (int i = 0; i < everyone.Length; i++)
			{
				WorldDoll one = everyone[i];
				here.Add(one.Id);
				string now = NameOfDoll(one);
				if (toldNames.TryGetValue(one.Id, out string was) && was == now)
					continue;

				toldNames[one.Id] = now;
				changed.Add((one.Id, now));
			}

			// 나간 사람은 장부에서 지운다 — 안 그러면 오래 돌수록 부푼다.
			if (toldNames.Count > everyone.Length)
			{
				System.Collections.Generic.List<int> gone = new System.Collections.Generic.List<int>();
				foreach (int dollId in toldNames.Keys)
				{
					if (here.Contains(dollId) == false)
						gone.Add(dollId);
				}

				for (int i = 0; i < gone.Count; i++)
					toldNames.Remove(gone[i]);
			}

			if (changed.Count == 0)
				return;

			string message = Protocol.Names(changed);
			foreach (System.Collections.Generic.KeyValuePair<int, Connection> entry in sockets)
			{
				if (entry.Value.Socket.State == WebSocketState.Open)
					_ = SendAsync(entry.Value, message);
			}

			await Task.CompletedTask;
		}

		/// <summary>
		/// 한 칸이 같이 쓸 세계 소식 한 벌 — 못 만들면 <c>null</c>(그 칸은 창마다 따로 지어야 한다).
		/// 칸 한복판을 기준으로 고르되, <b>그 칸에 선 사람은 다 들어간다</b>(자기 인형을 찾아야 하니까).
		/// </summary>
		private (string Text, System.Collections.Generic.HashSet<int> Inside) SnapshotForCell(
			WorldDoll[] everyone, int cellX, int cellZ,
			bool sendBuildings, bool sendField, bool sendPots, long sequence, bool forceFull = false)
		{
			Vector3 center = new Vector3(
				(cellX + 0.5f) * INTEREST_CELL_SIZE, 0f, (cellZ + 0.5f) * INTEREST_CELL_SIZE);

			// 칸 한복판에서 반경만큼 + 칸 반쪽만큼 — 칸 구석에 선 사람이 봐야 할 것을 안 놓치게.
			float reach = PLAYER_INTEREST_RADIUS + INTEREST_CELL_SIZE;
			float reachSquared = reach * reach;

			System.Collections.Generic.List<WorldDoll> candidates = new System.Collections.Generic.List<WorldDoll>();
			System.Collections.Generic.List<WorldDoll> members = new System.Collections.Generic.List<WorldDoll>();

			for (int i = 0; i < everyone.Length; i++)
			{
				WorldDoll one = everyone[i];
				int oneCellX = (int)MathF.Floor(one.Position.x / INTEREST_CELL_SIZE);
				int oneCellZ = (int)MathF.Floor(one.Position.z / INTEREST_CELL_SIZE);
				if (oneCellX == cellX && oneCellZ == cellZ)
					members.Add(one);

				float deltaX = one.Position.x - center.x;
				float deltaZ = one.Position.z - center.z;
				if (deltaX * deltaX + deltaZ * deltaZ <= reachSquared)
					candidates.Add(one);
			}

			// 칸에 상한보다 많이 모였으면 <b>칸 한복판에 가까운 순</b>으로 자른다. 잘린 사람에게는
			// 위(방송 루프)에서 자기 자리를 따로 보낸다 — 그래서 여기서 공유를 포기하지 않아도 된다.
			//
			// ⚠ 여기서 고를 대상은 <b>candidates</b>(반경 안 전부)다 — 한때 members(그 칸 사람)만
			//   골랐는데, 그러면 광장이 몰리는 순간 약속한 반경 32m 가 <b>조용히 「내 칸(16m)」으로</b>
			//   줄었다. 칸 경계 너머 두 발짝 옆 사람이 안 보인다 — 사람 눈에는 「사람이 사라졌다」다
			//   (실측 2026-08-12: 200명 광장에서 25m 옆의 걷는 사람이 한 판도 안 실렸다).
			WorldDoll[] shared = InterestCrowd.SharedForCell(candidates, members, center, InterestCrowd.MAX_VISIBLE_DOLLS)
				?? InterestCrowd.Nearest(candidates, center, 0, InterestCrowd.MAX_VISIBLE_DOLLS, MovingNow());

			System.Collections.Generic.HashSet<int> inside = new System.Collections.Generic.HashSet<int>();
			for (int i = 0; i < shared.Length; i++)
				inside.Add(shared[i].Id);

			Interlocked.Increment(ref builtSnapshots);

			// ★ 안 움직인 사람은 안 싣는다 (TASK-WM-220) — 광장에 200명이 서 있어도
			//   그 판에 실리는 건 <b>움직인 사람</b>뿐이다. 창은 못 받은 사람을 그 자리에 그대로 둔다.
			string castKey = cellX + ":" + cellZ;
			lastCellCast.TryGetValue(castKey, out System.Collections.Generic.Dictionary<int, (float X, float Z)> lastCast);
			bool firstTimeForCell = lastCast == null || forceFull;

			System.Collections.Generic.Dictionary<int, (float X, float Z)> nowCast =
				new System.Collections.Generic.Dictionary<int, (float X, float Z)>(shared.Length);
			System.Collections.Generic.List<WorldDoll> changed = new System.Collections.Generic.List<WorldDoll>();

			for (int i = 0; i < shared.Length; i++)
			{
				WorldDoll one = shared[i];
				nowCast[one.Id] = (one.Position.x, one.Position.z);

				if (firstTimeForCell == false
					&& lastCast.TryGetValue(one.Id, out (float X, float Z) was)
					&& was.X == one.Position.x && was.Z == one.Position.z)
				{
					continue;
				}

				changed.Add(one);
			}

			// 이 칸에서 빠진 사람 = 창이 지워야 할 사람.
			System.Collections.Generic.List<int> gone = null;
			if (firstTimeForCell == false)
			{
				foreach (int dollId in lastCast.Keys)
				{
					if (nowCast.ContainsKey(dollId))
						continue;

					gone ??= new System.Collections.Generic.List<int>();
					gone.Add(dollId);
				}
			}

			// ⚠ 「전부」 판은 칸 장부를 건드리지 않는다 — 그 판은 한 창을 위한 것이고,
			//   장부를 흔들면 같은 칸의 다른 창들이 받을 「바뀐 것」이 어긋난다.
			if (forceFull == false)
				lastCellCast[castKey] = nowCast;

			// ⚠ 지은 것·들판·솥도 <b>칸 한복판 기준</b>으로 담는다. 칸에 선 아무개 한 사람 기준으로
			//   담으면, 같은 칸의 다른 사람이 봐야 할 집이 빠진다 — 「남이 지은 집이 안 보이던 것」의 재판이다.
			//   한복판 + 반경 + 칸 하나만큼이면 그 칸 누구의 시야도 다 덮는다(넉넉히 보내고 창이 고른다).
			// 들판도 「바뀐 자리만」 — 남이 저 멀리서 하나 주웠다고 내 들판 169자리가 다시 올 이유가 없다.
			System.Collections.Generic.List<GatherableNode> field = null;
			System.Collections.Generic.List<int> fieldGone = null;
			bool fieldIsDelta = false;

			if (sendField)
			{
				System.Collections.Generic.List<GatherableNode> nearby = GatherablesNear(center, reach);
				lastCellField.TryGetValue(castKey, out System.Collections.Generic.Dictionary<int, int> lastField);
				// ⚠ 들판은 <b>사람 목록의 「전부 다시」와 무관</b>하다 (TASK-WM-220).
				//   묶어 뒀더니, 밀린 창이 「전부」를 받을 때마다 들판 8KB 가 같이 날아갔고,
				//   그 큰 판 때문에 다시 밀려서 또 「전부」를 받는 <b>고리</b>가 생겼다(실측).
				//   들판이 그 칸에 이미 나갔는지는 들판 장부만 보면 된다.
				bool fieldFromScratch = lastField == null;

				System.Collections.Generic.Dictionary<int, int> nowField =
					new System.Collections.Generic.Dictionary<int, int>(nearby.Count);
				field = new System.Collections.Generic.List<GatherableNode>();

				for (int i = 0; i < nearby.Count; i++)
				{
					GatherableNode one = nearby[i];
					nowField[one.Id] = one.Amount;

					if (fieldFromScratch == false
						&& lastField.TryGetValue(one.Id, out int wasAmount) && wasAmount == one.Amount)
					{
						continue;
					}

					field.Add(one);
				}

				if (fieldFromScratch == false)
				{
					foreach (int nodeId in lastField.Keys)
					{
						if (nowField.ContainsKey(nodeId))
							continue;

						fieldGone ??= new System.Collections.Generic.List<int>();
						fieldGone.Add(nodeId);
					}
				}

				fieldIsDelta = fieldFromScratch == false;
				lastCellField[castKey] = nowField;

				// 바뀐 게 없으면 아예 안 싣는다(그 자리는 「안 바뀌었다」로 읽힌다).
				if (fieldIsDelta && field.Count == 0 && fieldGone == null)
					field = null;
			}

			return (Protocol.WorldSnapshot(
				firstTimeForCell ? shared : (System.Collections.Generic.IEnumerable<WorldDoll>)changed,
				sendBuildings ? BuildingsNear(center, reach) : null,
				World.Calendar,
				null,
				field,
				sendPots ? World.Cauldrons : null,
				sequence,
				sendPots ? CauldronCellsNear(center, reach) : null,
				firstTimeForCell,
				gone,
				fieldIsDelta,
				fieldGone), inside);
		}

		/// <summary>
		/// 이 창에게 「네 자리는 여기다」를 <b>다시 말할 필요가 있나</b> (TASK-WM-236).
		/// 자리가 그대로면 안 말한다 — 안 바뀐 것을 초당 20번 말하는 것은 소음이다.
		/// </summary>
		private static bool MyPlaceChanged(Connection target, WorldDoll mine)
		{
			if (target.ToldMyX == mine.Position.x && target.ToldMyZ == mine.Position.z)
				return false;

			target.ToldMyX = mine.Position.x;
			target.ToldMyZ = mine.Position.z;
			return true;
		}

		/// <summary>그 번호의 인형 — 이번 틱에 뜬 목록에서 찾는다(다시 뜨면 자리가 어긋난다).</summary>
		private static WorldDoll FindDoll(WorldDoll[] everyone, int dollId)
		{
			for (int i = 0; i < everyone.Length; i++)
			{
				if (everyone[i].Id == dollId)
					return everyone[i];
			}

			return null;
		}

		private WorldDoll[] DollsVisibleTo(int viewerDollId)
		{
			WorldDoll[] all = EveryoneNow();
			Vector3 viewer = World.PositionOf(viewerDollId);
			float radiusSquared = PLAYER_INTEREST_RADIUS * PLAYER_INTEREST_RADIUS;
			System.Collections.Generic.List<WorldDoll> visible = new System.Collections.Generic.List<WorldDoll>();

			for (int i = 0; i < all.Length; i++)
			{
				WorldDoll candidate = all[i];
				float deltaX = candidate.Position.x - viewer.x;
				float deltaZ = candidate.Position.z - viewer.z;
				if (deltaX * deltaX + deltaZ * deltaZ <= radiusSquared)
					visible.Add(candidate);
			}

			// 반경 안이어도 <b>가까운 몇 명까지</b>다 — 광장에 몰리면 반경만으로는 못 버틴다
			// (실측: 200명이 한자리에 모이자 초당 27MB).
			return InterestCrowd.Nearest(visible, viewer, viewerDollId, InterestCrowd.MAX_VISIBLE_DOLLS, MovingNow());
		}

		private PlacedBuilding[] BuildingsVisibleTo(int viewerDollId)
		{
			return BuildingsNear(World.PositionOf(viewerDollId), PLAYER_INTEREST_RADIUS);
		}

		private PlacedBuilding[] BuildingsNear(Vector3 viewer, float radius)
		{
			PlacedBuilding[] all = World.Buildings();
			float radiusSquared = radius * radius;
			System.Collections.Generic.List<PlacedBuilding> visible = new System.Collections.Generic.List<PlacedBuilding>();

			for (int i = 0; i < all.Length; i++)
			{
				PlacedBuilding candidate = all[i];
				float minX = candidate.Pivot.x;
				float maxX = candidate.Pivot.x + candidate.Size.x;
				float minZ = candidate.Pivot.z;
				float maxZ = candidate.Pivot.z + candidate.Size.y;
				float closestX = viewer.x < minX ? minX : viewer.x > maxX ? maxX : viewer.x;
				float closestZ = viewer.z < minZ ? minZ : viewer.z > maxZ ? maxZ : viewer.z;
				float deltaX = closestX - viewer.x;
				float deltaZ = closestZ - viewer.z;
				if (deltaX * deltaX + deltaZ * deltaZ <= radiusSquared)
					visible.Add(candidate);
			}

			return visible.ToArray();
		}

		private System.Collections.Generic.List<GatherableNode> GatherablesVisibleTo(int viewerDollId)
		{
			return GatherablesNear(World.PositionOf(viewerDollId), PLAYER_INTEREST_RADIUS);
		}

		private System.Collections.Generic.List<GatherableNode> GatherablesNear(Vector3 viewer, float radius)
		{
			System.Collections.Generic.List<GatherableNode> all = World.Gatherables.Alive(World.Calendar.TotalMinutes());
			float radiusSquared = radius * radius;
			System.Collections.Generic.List<GatherableNode> visible = new System.Collections.Generic.List<GatherableNode>();

			for (int i = 0; i < all.Count; i++)
			{
				GatherableNode candidate = all[i];
				float deltaX = candidate.X - viewer.x;
				float deltaZ = candidate.Z - viewer.z;
				if (deltaX * deltaX + deltaZ * deltaZ <= radiusSquared)
					visible.Add(candidate);
			}

			return visible;
		}

		private System.Collections.Generic.List<Vector3Int> CauldronCellsVisibleTo(int viewerDollId)
		{
			return CauldronCellsNear(World.PositionOf(viewerDollId), PLAYER_INTEREST_RADIUS);
		}

		private System.Collections.Generic.List<Vector3Int> CauldronCellsNear(Vector3 viewer, float radius)
		{
			System.Collections.Generic.List<Vector3Int> all = World.Cauldrons.Cells();
			float radiusSquared = radius * radius;
			System.Collections.Generic.List<Vector3Int> visible = new System.Collections.Generic.List<Vector3Int>();

			for (int i = 0; i < all.Count; i++)
			{
				Vector3Int candidate = all[i];
				float deltaX = candidate.x - viewer.x;
				float deltaZ = candidate.z - viewer.z;
				if (deltaX * deltaX + deltaZ * deltaZ <= radiusSquared)
					visible.Add(candidate);
			}

			return visible;
		}

		/// <summary>
		/// 한 창에 한 마디. <b>차례를 서서</b> 보낸다 — 두 곳에서 동시에 쓰면 소켓이 터진다.
		/// </summary>
		private async Task SendAsync(Connection connection, string text)
		{
			await SendBytesAsync(connection, Encoding.UTF8.GetBytes(text));
		}

		/// <summary>
		/// 이미 바이트로 만들어 둔 말을 보낸다 — <b>같은 말은 한 번만 만든다</b> (TASK-WM-220).
		///
		/// ★ 왜: 한 칸의 소식은 한 벌인데, 보낼 때마다 그 글자를 다시 바이트로 바꿨다.
		///   사람 800명이면 같은 3KB 를 800번 다시 만든 셈이고, 그 쓰레기가 쌓여
		///   이따금 세계가 <b>170ms 씩 멎었다</b>(GC). 만든 바이트는 안 바뀌니 같이 쓰면 된다.
		/// </summary>
		private async Task SendBytesAsync(Connection connection, byte[] payload)
		{
			await connection.SendGate.WaitAsync();
			try
			{
				await connection.Socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, CancellationToken.None);
			}
			catch (WebSocketException)
			{
				// 끊긴 창에 보내다 나는 오류 — 다음 정리 때 빠진다.
			}
			finally
			{
				connection.SendGate.Release();
			}
		}
	}
}
