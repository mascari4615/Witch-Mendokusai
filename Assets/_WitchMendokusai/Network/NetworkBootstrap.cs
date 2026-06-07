using FishNet.Managing;
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

        private static NetworkManager _networkManager;

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
            return server && client;
        }

        public static void StopHost()
        {
            if (_networkManager == null)
                return;
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
    }
}
