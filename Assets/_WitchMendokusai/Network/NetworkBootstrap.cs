using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using UnityEngine;

namespace WitchMendokusai
{
    /// <summary>
    /// 멀티(동기6) 라이브 sync first-use — NetworkManager 를 *런타임 코드*로 부트(씬 수동배치 X = 회귀망 밖).
    /// 이전: NetworkManager 부트 코드 0 (grep No matches) → OnStartServer/SyncVar 영영 미발화 = 컴파일 표면만.
    /// 이제 StartHost(server+local client) 로 FishNet 파이프라인 가동 = 컴파일 표면 → 관측 거동.
    /// Tugboat 기본값(localhost:7770) = 로컬 host 무설정. 검증 = TASK-WM-187 / WMNetSyncPlayVerify.
    /// </summary>
    public static class NetworkBootstrap
    {
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

            // SetActive(false) 동안 컴포넌트 추가 → Awake 보류 → 둘 다 존재한 뒤 활성화.
            // (NetworkManager.Awake 의 TransportManager 가 같은 GO 의 Tugboat 를 GetComponent 로 발견.)
            GameObject go = new GameObject("[NetworkManager]");
            go.SetActive(false);
            go.AddComponent<Tugboat>();
            _networkManager = go.AddComponent<NetworkManager>();
            Object.DontDestroyOnLoad(go);
            go.SetActive(true);
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
        /// </summary>
        public static WorldClockNetworkBridge SpawnWorldClockBridge()
        {
            NetworkManager networkManager = EnsureNetworkManager();
            GameObject go = new GameObject("WorldClockNetworkBridge");
            go.AddComponent<NetworkObject>();
            WorldClockNetworkBridge bridge = go.AddComponent<WorldClockNetworkBridge>();
            networkManager.ServerManager.Spawn(go);
            return bridge;
        }
    }
}
