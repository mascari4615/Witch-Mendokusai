using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-191 step-2 — 서버측 per-connection 플레이어 프록시 스포너. 클라가 연결될 때마다 그
    /// 연결이 *소유*하는 PlayerNetProxy 를 서버 스폰 → 그 클라의 PlayerNetProxy(IsOwner)가 로컬
    /// 플레이어(ILocalPlayerProbe)를 따라가 client-auth NetworkTransform 로 브로드캐스트 → 서버·다른
    /// 클라가 원격 아바타로 관측. host 자신의 clientHost 연결도 포함(자기 인형).
    ///
    /// "각 피어가 자기 인형을 공유 World 에서 조종 + 서로 보임"(A: MC/Stardew식)의 스폰 메커니즘.
    /// 로컬 플레이어(씬배치 싱글톤)는 불변 — 프록시는 별 표현(client-local-authoritative). 단일플레이어 회귀 0.
    /// FishNet 은 owner 연결 해제 시 그 소유 NetworkObject 를 자동 despawn(preventDespawnOnDisconnect=false)
    /// → 본 스포너는 스폰·dict 정리만(despawn 직접 호출 X).
    /// </summary>
    public static class PlayerNetProxySpawner
    {
        private static NetworkManager _networkManager;
        // clientId → 그 연결 소유 프록시. 중복 스폰 방지 + 검증용 조회.
        private static readonly Dictionary<int, PlayerNetProxy> _proxyByClient = new Dictionary<int, PlayerNetProxy>();

        /// <summary>서버 시작 후 1회 호출(멱등). 이후 연결마다 자동 스폰 + Enable 시점 기존 연결 보강.</summary>
        public static void Enable(NetworkManager networkManager)
        {
            if (_networkManager == networkManager)
            {
                return; // 멱등 — 같은 NM 재호출 무시.
            }
            Disable(); // 다른 NM 로 교체 시 이전 구독 해제.
            _networkManager = networkManager;
            _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;

            // Enable 전에 이미 붙은 연결(clientHost 등) 보강 스폰 — 이벤트 놓침 방지.
            foreach (NetworkConnection connection in _networkManager.ServerManager.Clients.Values)
            {
                SpawnFor(connection);
            }
        }

        public static void Disable()
        {
            if (_networkManager == null)
            {
                return;
            }
            _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            _proxyByClient.Clear();
            _networkManager = null;
        }

        /// <summary>스모크/검증용 — 임의의 클라-소유 프록시 1개(있으면). 없으면 false.</summary>
        public static bool TryGetAnyClientProxy(out PlayerNetProxy proxy)
        {
            foreach (PlayerNetProxy candidate in _proxyByClient.Values)
            {
                if (candidate != null)
                {
                    proxy = candidate;
                    return true;
                }
            }
            proxy = null;
            return false;
        }

        private static void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started)
            {
                SpawnFor(connection);
            }
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
            {
                // FishNet 이 owner 객체를 자동 despawn → dict 정리만.
                _proxyByClient.Remove(connection.ClientId);
            }
        }

        private static void SpawnFor(NetworkConnection connection)
        {
            if (_proxyByClient.ContainsKey(connection.ClientId))
            {
                return; // 이미 스폰됨(중복 이벤트 / 보강 충돌 방지).
            }
            PlayerNetProxy proxy = NetworkBootstrap.SpawnPlayerProxy(connection);
            _proxyByClient[connection.ClientId] = proxy;
            Debug.Log($"[NET-PROXY] spawned proxy for client {connection.ClientId} (owner)");
        }
    }
}
