using System.Collections;
using UnityEngine;
using WitchMendokusai.DomainSDK.Alchemy;
using WitchMendokusai.DomainSDK.Building;
using WitchMendokusai.Net;

namespace WitchMendokusai
{
	/// <summary>
	/// 세계로 들어가는 문 하나 (TASK-WM-217).
	///
	/// 「혼자 하기 / 같이 하기」를 묻지 않는다. 들어가면 세계가 있고, 그 세계가
	/// 멀리 있으면 거기 붙고 <b>없으면 내 안에서 띄운다.</b> 사람은 그 차이를 몰라도 된다.
	///
	/// ★ 왜 「못 붙으면 실패」가 아닌가: 인터넷이 없다고 게임이 안 열리면 그건 게임이 아니다.
	///   접속은 <b>더 좋은 경우</b>이지 <b>필요 조건</b>이 아니다.
	/// </summary>
	public sealed class WorldLinkProvider : MonoBehaviour, IWorldDoor
	{
		public static WorldLinkProvider Instance { get; private set; }

		/// <summary>
		/// 문은 <b>스스로 선다</b> (TASK-WM-217). 로비가 만들어 주는 구조면
		/// 「로비를 안 거치고 들어온 경우」에 문이 없어서 조용히 아무 일도 안 일어난다.
		/// </summary>
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void StandUp()
		{
			if (Instance != null)
				return;

			GameObject holder = new GameObject(nameof(WorldLinkProvider));
			DontDestroyOnLoad(holder);
			holder.AddComponent<WorldLinkProvider>();
		}

		[Header("멀리 있는 세계")]
		[SerializeField] private string remoteUrl = "ws://127.0.0.1:5199/ws";

		[Tooltip("이만큼 기다려도 안 붙으면 내 안의 세계로 들어간다 (초).")]
		[SerializeField] private float connectTimeoutSeconds = 2f;

		private WebWorldClient remote;
		private WorldLinkBuildChannel buildChannel;
		private WorldLinkBrewChannel brewChannel;
		private WorldLinkCraftChannel craftChannel;
		private WorldLinkChestChannel chestChannel;
		private WorldLinkNameChannel nameChannel;
		private WorldBagRelay bagRelay;

		/// <summary>지금 이어진 줄. 아직 안 들어갔으면 null.</summary>
		public IWorldLink Current { get; private set; }

		/// <summary>내 안의 세계에 들어와 있나 — 화면에 조용히 알려줄 때만 쓴다.</summary>
		public bool IsLocalWorld { get; private set; }

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}

			Instance = this;
			WorldDoor.Register(this);
		}

		/// <summary>세계로 들어간다. 이미 들어와 있으면 그대로 둔다.</summary>
		public void Enter()
		{
			if (Current != null)
				return;

			StartCoroutine(EnterRoutine());
		}

		private IEnumerator EnterRoutine()
		{
			remote = gameObject.AddComponent<WebWorldClient>();
			// 붙을 곳은 환경변수가 이긴다 — 스모크·개발 서버가 인스펙터 값을 고치지 않고도 붙는다.
			string overrideUrl = System.Environment.GetEnvironmentVariable("WM_WORLD_SERVER");
			remote.SetServerUrl(string.IsNullOrWhiteSpace(overrideUrl) ? remoteUrl : overrideUrl);
			remote.Connect();

			float waited = 0f;
			while (waited < connectTimeoutSeconds && remote.IsLinked == false)
			{
				waited += Time.unscaledDeltaTime;
				yield return null;
			}

			if (remote.IsLinked)
			{
				Current = remote;
				IsLocalWorld = false;
				EnsureBinder();
				RegisterChannels();
				yield break;
			}

			// 못 붙었다 — 조용히 내 안의 세계로. 사람에게는 그냥 「세계에 들어왔다」.
			remote.Disconnect();
			Destroy(remote);
			remote = null;

			// 지난번에 지은 것을 되살려 들어간다 — 혼자 놀아도 세계는 이어진다 (단계 5).
			WorldSim world = new WorldSim();

			// ★ 목록을 <b>먼저</b> 꽂고 되살린다 (실측 2026-08-10).
			//   순서를 바꿨더니 「그 자리가 몇 칸짜리 상자인가」를 모른 채 되살려 <b>상자를 통째로 버렸고</b>,
			//   그 상태로 다시 저장되면서 넣어 둔 것이 조용히 사라졌다(파일엔 건물만 남아 무증상).
			world.Buildables = BuildingCatalog.Loaded;
			world.Gatherables = new WorldGatherables(WorldSeeds.Gatherables());
			world.Ingredients = new WorldIngredients(WorldSeeds.Ingredients());
			world.Load(LocalWorldStore.TryLoad(), ItemCatalog.Loaded);

			Current = new LocalWorldLink(world);
			IsLocalWorld = true;
			EnsureBinder();
			RegisterChannels();
		}

		/// <summary>
		/// 게임의 건설·가마솥이 이 줄을 타게 꽂는다 (TASK-WM-217 단계 4).
		/// 게임은 「공유 채널」이라는 구멍으로만 말하므로, 그 구멍을 줄이 채우면 통로만 갈린다.
		/// </summary>
		private void RegisterChannels()
		{
			buildChannel = new WorldLinkBuildChannel(Current);
			SharedBuildChannelBridge.Register(buildChannel);

			brewChannel = new WorldLinkBrewChannel(Current);
			SharedBrewChannelBridge.Register(brewChannel);

			// 제작도 세계가 판정한다 (TASK-WM-217) — 안 꽂으면 게임 창만 자기 주사위로 만든다.
			craftChannel = new WorldLinkCraftChannel(Current);
			WorldCraftBridge.Register(craftChannel);

			// 상자·이름도 게임 화면이 쓸 수 있어야 한다 (TASK-WM-217/218) — 웹 창에만 있으면
			// 「같이 노는 세계」의 알맹이(나눔·누가 누군지)가 한쪽 창에만 있는 셈이다.
			chestChannel = new WorldLinkChestChannel(Current);
			WorldChestBridge.Register(chestChannel);

			nameChannel = new WorldLinkNameChannel(Current);
			WorldNameBridge.Register(nameChannel);

			bagRelay = new WorldBagRelay(Current);
			WorldBagBridge.Register(bagRelay);

			// 화면 쪽(PlayerBagSync)은 Domain 이 스스로 꽂는다 — 통신 층에서는 그 타입이 안 보인다.
			// 들어오자마자 「내 가방 뭐 있냐」고 묻는다 — 안 물으면 화면이 빈 채로 남는다.
			if (Current is WebWorldClient web)
				web.AskBag();
		}

		/// <summary>내 안의 세계였다면 지금 모습을 적어 둔다 — 다음에 켜면 그대로 있다.</summary>
		private void SaveLocalWorld()
		{
			if (Current is LocalWorldLink local)
				LocalWorldStore.TrySave(local.World.Save());
		}

		private void OnApplicationQuit() => SaveLocalWorld();

		private void OnApplicationPause(bool paused)
		{
			// 폰은 「끄기」 없이 그냥 사라진다 — 멈출 때 적어 두지 않으면 그날 지은 게 통째로 없어진다.
			if (paused)
				SaveLocalWorld();
		}

		/// <summary>
		/// 세계에 들어왔으면 <b>사람들이 보여야 한다</b> — 그리는 쪽을 문이 직접 붙인다 (TASK-WM-217 단계 3).
		/// 씬에 손으로 얹어야만 보이는 구조면 「씬에 안 얹혀서 조용히 아무도 안 보이는」 사고가 난다.
		/// </summary>
		private void EnsureBinder()
		{
			if (GetComponent<WorldDollBinder>() == null)
				gameObject.AddComponent<WorldDollBinder>();

			// 주울 것도 같이 — 안 보이면 게임 창에서는 아무것도 못 줍는다(웹 창만 노는 세계가 된다).
			if (GetComponent<WorldGatherableBinder>() == null)
				gameObject.AddComponent<WorldGatherableBinder>();
		}

		/// <summary>세계에서 나온다.</summary>
		public void Leave()
		{
			SaveLocalWorld();

			if (buildChannel != null)
			{
				SharedBuildChannelBridge.Clear(buildChannel);
				buildChannel = null;
			}

			if (chestChannel != null)
			{
				WorldChestBridge.Clear(chestChannel);
				chestChannel = null;
			}

			if (nameChannel != null)
			{
				WorldNameBridge.Clear(nameChannel);
				nameChannel = null;
			}

			if (craftChannel != null)
			{
				WorldCraftBridge.Clear(craftChannel);
				craftChannel = null;
			}

			if (brewChannel != null)
			{
				SharedBrewChannelBridge.Clear(brewChannel);
				brewChannel = null;
			}

			if (bagRelay != null)
			{
				WorldBagBridge.Clear(bagRelay);
				bagRelay = null;
			}

			if (remote != null)
			{
				remote.Disconnect();
				Destroy(remote);
				remote = null;
			}

			Current = null;
			IsLocalWorld = false;
		}
	}
}
