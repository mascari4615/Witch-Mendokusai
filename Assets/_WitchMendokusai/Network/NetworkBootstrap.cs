using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace WitchMendokusai
{
    /// <summary>
    /// 멀티(동기6) 라이브 sync first-use — NetworkManager 를 *런타임 코드*로 부트(씬 수동배치 X = 회귀망 밖).
    /// 이전: NetworkManager 부트 코드 0 (grep No matches) → OnStartServer/SyncVar 영영 미발화 = 컴파일 표면만.
    /// 이제 StartHost(server+local client) 로 FishNet 파이프라인 가동 = 컴파일 표면 → 관측 거동.
    /// FishNet NetworkManager/NetworkObject 는 런타임 AddComponent 불가(직렬화 미baked → Awake/Spawn NRE) →
    /// SpawnablePrefabs(DefaultPrefabObjects) + Tugboat wired prefab(Resources) 인스턴스화. 검증 = TASK-WM-187.
    /// </summary>
    public static class NetworkBootstrap
    {
        public const string NETWORK_MANAGER_RESOURCE = "WMNetworkManager";
        public const string BRIDGE_PREFAB_RESOURCE = "WorldClockNetworkBridge";
        public const string PLAYER_PROXY_RESOURCE = "PlayerNetProxy";
        public const ushort DEFAULT_PORT = 7770; // Tugboat 기본

        private static NetworkManager _networkManager;
        private static WorldClockNetworkBridge _worldClockBridge;

        public static NetworkManager EnsureNetworkManager()
        {
            if (_networkManager != null)
                return _networkManager;

            NetworkManager existing = Object.FindAnyObjectByType<NetworkManager>();
            if (existing != null)
            {
                _networkManager = existing;
                return _networkManager;
            }

            // FishNet NetworkManager = 런타임 AddComponent 불가 (직렬화 SpawnablePrefabs null → Awake NRE).
            // SpawnablePrefabs(DefaultPrefabObjects) + Tugboat wired prefab 인스턴스화가 정석.
            GameObject prefab = Resources.Load<GameObject>(NETWORK_MANAGER_RESOURCE);
            GameObject go = Object.Instantiate(prefab);
            go.name = NETWORK_MANAGER_RESOURCE;
            Object.DontDestroyOnLoad(go);
            _networkManager = go.GetComponent<NetworkManager>();
            return _networkManager;
        }

        public static bool StartHost()
        {
            NetworkManager networkManager = EnsureNetworkManager();
            bool server = networkManager.ServerManager.StartConnection();
            bool client = networkManager.ClientManager.StartConnection();
            if (server)
            {
                // 호스트 단일 진입점("함께 만들기")이 두 라이브 채널 모두 기동:
                //  ① 시계 동기 bridge (server→client WorldClock SyncVar) — 참가자가 같은 절기/날짜.
                //  ② per-connection 프록시 스포너 — host 자신·참가 클라 각자 소유 프록시(서로 보임).
                // TASK-WM-191 step-2/3 (World 공동진입). bridge 는 멱등(중복 스폰 X).
                EnsureWorldClockBridge(networkManager);
                PlayerNetProxySpawner.Enable(networkManager);
            }
            return server && client;
        }

        /// <summary>
        /// 호스트 시작 시 시계 동기 bridge 1개 보장(멱등). server active *후* 스폰 — StartConnection 직후엔
        /// 서버 미active 라 ServerManager.Spawn 이 "server nor client active" 로 거부됨(검증 WM-191 step-3).
        /// 이미 active 면 즉시, 아니면 OnServerConnectionState(Started)에 1회. 센티넬은 SpawnWorldClockBridge 직접.
        /// </summary>
        private static void EnsureWorldClockBridge(NetworkManager networkManager)
        {
            if (_worldClockBridge != null)
            {
                return;
            }
            if (networkManager.ServerManager.Started)
            {
                _worldClockBridge = SpawnWorldClockBridge();
                return;
            }
            networkManager.ServerManager.OnServerConnectionState -= OnServerStartedSpawnBridge;
            networkManager.ServerManager.OnServerConnectionState += OnServerStartedSpawnBridge;
        }

        private static void OnServerStartedSpawnBridge(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started)
            {
                return;
            }
            if (_networkManager == null)
            {
                return;
            }
            _networkManager.ServerManager.OnServerConnectionState -= OnServerStartedSpawnBridge;
            if (_worldClockBridge == null)
            {
                _worldClockBridge = SpawnWorldClockBridge();
            }
        }

        /// <summary>참가(client only) — 호스트 주소로 연결. 멀티 진입 UX 「참가」 경로(TASK-WM-190).</summary>
        public static bool StartClient(string address, ushort port = DEFAULT_PORT)
        {
            NetworkManager networkManager = EnsureNetworkManager();
            // Tugboat 등 transport 공통 base API (Tugboat 타입 직접의존 0).
            networkManager.TransportManager.Transport.SetClientAddress(address);
            networkManager.TransportManager.Transport.SetPort(port);
            return networkManager.ClientManager.StartConnection();
        }

        /// <summary>세션 떠 있나 (server 또는 client 시작). NM 미부트면 false (생성 X).</summary>
        public static bool IsRunning
        {
            get
            {
                if (_networkManager == null)
                {
                    return false;
                }
                return _networkManager.ServerManager.Started || _networkManager.ClientManager.Started;
            }
        }

        public static void StopHost()
        {
            if (_networkManager == null)
                return;
            PlayerNetProxySpawner.Disable();
            _networkManager.ServerManager.OnServerConnectionState -= OnServerStartedSpawnBridge;
            _worldClockBridge = null;
            _networkManager.ClientManager.StopConnection();
            _networkManager.ServerManager.StopConnection(true);
        }

        /// <summary>
        /// WorldClock sync 채널(1번째 라이브 채널) NetworkObject 를 서버 스폰. host 시작 후 호출.
        /// baked prefab(Resources, DefaultPrefabObjects 등록) 인스턴스화 — 런타임 AddComponent 불가.
        /// </summary>
        public static WorldClockNetworkBridge SpawnWorldClockBridge()
        {
            NetworkManager networkManager = EnsureNetworkManager();
            GameObject prefab = Resources.Load<GameObject>(BRIDGE_PREFAB_RESOURCE);
            GameObject go = Object.Instantiate(prefab);
            WorldClockNetworkBridge bridge = go.GetComponent<WorldClockNetworkBridge>();
            networkManager.ServerManager.Spawn(go);
            return bridge;
        }

        /// <summary>
        /// 플레이어 프록시(per-peer presence) 서버 스폰. owner=연결이면 그 클라가 소유(자기 로컬
        /// 플레이어를 따라감), null=서버 소유(스모크 검증용). prefab(NetworkObject+NetworkTransform,
        /// _isGlobal) 인스턴스화. TASK-WM-191 step-1.
        /// </summary>
        public static PlayerNetProxy SpawnPlayerProxy(NetworkConnection owner = null)
        {
            NetworkManager networkManager = EnsureNetworkManager();
            GameObject prefab = Resources.Load<GameObject>(PLAYER_PROXY_RESOURCE);
            GameObject go = Object.Instantiate(prefab);
            PlayerNetProxy proxy = go.GetComponent<PlayerNetProxy>();
            if (owner != null)
            {
                networkManager.ServerManager.Spawn(go, owner);
            }
            else
            {
                networkManager.ServerManager.Spawn(go);
            }
            return proxy;
        }
    }
}
