using System;
using System.Collections;
using System.Globalization;
using System.IO;
using FishNet.Managing;
using UnityEngine;
using WitchMendokusai.DomainSDK.Network;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-189 — 멀티(동기6) "진짜 2-peer" first-use 회귀 게이트의 *런타임측*.
    ///
    /// host-loopback(server + local client 한 프로세스)은 SyncVar 가 *wire 를 안 탄다*
    /// (로컬 연결 = 직접 참조). 사용자: "진짜 2-peer까지 추구". 그 근본 검증 =
    /// *별 프로세스 2개*가 실 transport(Tugboat 127.0.0.1:7770)로 연결되어, 서버
    /// WorldClock SyncVar 가 직렬화→소켓→역직렬화 거쳐 원격 클라에 도달함을 결정 확인.
    ///
    /// BootSmokeSentinel 과 동형: env WM_NET_ROLE=server|client 일 때만 self-install
    /// (미설정 = 완전 inert, 일반 실행 0 영향). 결정부팅(WM_BOOT_DETERMINISTIC=1)으로
    /// WorldClock.Instance 가 등장하면 역할을 시작하고, 끝에 결과파일 기록 후 Quit.
    /// 판정(서버 시각 == 클라 수신 시각)은 runner(wm-net-2peer-smoke.ps1)가 두 결과
    /// 파일 비교로 — 센티넬은 구동·계측·종료만(관심사 분리).
    ///
    /// 결과파일 = env WM_NET_SMOKE_RESULT. key=value: result/reason/role/year/season/
    /// day/hour/clients/nre/t. ready 핸드셰이크 = env WM_NET_SMOKE_READY 파일(서버
    /// listen 시 touch → runner 가 그때 클라 기동).
    /// </summary>
    public sealed class NetTwoPeerSmokeSentinel : MonoBehaviour
    {
        // 서버 시각을 default(Y1 S0 D1)에서 멀리 보냄 — 클라 수신값이 우연히 default 와
        // 일치할 여지 0 (== 진짜 wire 너머 도달 증명). days 단위라 Config(시/일/계절) 무관.
        private const int SENTINEL_SKIP_DAYS = 400;
        private const int SETTLE_FRAMES = 120;
        // step-2: 클라가 fake probe 로 자기 프록시를 구동할 distinctive 위치(spawn 원점 0,0,0·default
        // 와 우연일치 배제). 클라(owner)가 여기로 따라가 client-auth NetworkTransform 로 서버에 푸시.
        private const float PROBE_X = 50f;
        private const float PROBE_Y = 6f;
        private const float PROBE_Z = 70f;
        private const float MOVED_SQR_MIN = 25f; // spawn 원점에서 벗어남 판정 (>5 units = probe-follow 전파됨).

        private string _role;
        private int _nreCount;
        private bool _done;
        // 플레이어 프록시 위치 — server=고정 sentinel, client=관측 수신값. WriteResult 가 기록.
        private bool _proxyObserved;
        private float _proxyX;
        private float _proxyY;
        private float _proxyZ;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Install()
        {
            string role = Environment.GetEnvironmentVariable("WM_NET_ROLE");
            if (string.IsNullOrEmpty(role))
            {
                return; // 일반 실행 = 완전 inert.
            }

            GameObject host = new GameObject(nameof(NetTwoPeerSmokeSentinel));
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            NetTwoPeerSmokeSentinel sentinel = host.AddComponent<NetTwoPeerSmokeSentinel>();
            sentinel._role = role.Trim().ToLowerInvariant();
        }

        private void Awake()
        {
            UnityEngine.Application.logMessageReceived += OnLog;
            Debug.Log($"[NET-SMOKE] sentinel armed — role={_role} timeout={TimeoutSec}s result='{ResultPath}'");
            StartCoroutine(Run());
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception && condition != null
                && condition.IndexOf("NullReferenceException", StringComparison.Ordinal) >= 0)
            {
                _nreCount++;
            }
        }

        private float TimeoutSec => EnvFloat("WM_NET_SMOKE_TIMEOUT_SEC", 120f);

        private static string ResultPath
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("WM_NET_SMOKE_RESULT");
                if (string.IsNullOrEmpty(env) == false)
                {
                    return env;
                }
                return Path.Combine(UnityEngine.Application.persistentDataPath, "wm_net_smoke_result.txt");
            }
        }

        private IEnumerator Run()
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSec;

            // 1) 결정부팅으로 WorldClock.Instance 등장 대기 (브리지가 OnStartServer 에서 읽음).
            while (WorldClock.Instance == null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            if (WorldClock.Instance == null)
            {
                FailFinish("WorldClock.Instance 미등장 (결정부팅 실패?)");
                yield break;
            }

            // 2) NetworkManager 부트 (prefab 인스턴스화 — 런타임 AddComponent 불가).
            NetworkManager networkManager = TryEnsureNetworkManager(out string ensureError);
            if (networkManager == null)
            {
                FailFinish("NetworkManager 부트 실패: " + ensureError);
                yield break;
            }

            if (_role == "server")
            {
                yield return RunServer(networkManager, deadline);
            }
            else if (_role == "client")
            {
                yield return RunClient(networkManager, deadline);
            }
            else
            {
                FailFinish("알 수 없는 WM_NET_ROLE: " + _role);
            }
        }

        private IEnumerator RunServer(NetworkManager networkManager, float deadline)
        {
            if (networkManager.ServerManager.StartConnection() == false)
            {
                FailFinish("server: ServerManager.StartConnection() == false");
                yield break;
            }
            while (networkManager.ServerManager.Started == false && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            if (networkManager.ServerManager.Started == false)
            {
                FailFinish("server: ServerManager.Started 안 됨 (listen 실패)");
                yield break;
            }

            // listen OK — runner 에게 "이제 클라 기동해라" 핸드셰이크.
            TouchReadyFile();
            Debug.Log("[NET-SMOKE] server listening — ready file touched");

            // WorldClock 채널 NetworkObject 서버 스폰 → OnStartServer 가 초기 PushAll(Y1).
            WorldClockNetworkBridge bridge = TrySpawnBridge(networkManager, out string spawnError);
            if (bridge == null)
            {
                FailFinish("server: 브리지 스폰 실패: " + spawnError);
                yield break;
            }

            // 시각을 알려진 sentinel 로 고정 + 시계 정지(결정성) → OnDayChanged → bridge PushAll.
            WorldClock clock = WorldClock.Instance;
            clock.PauseClock(gameObject);
            clock.SkipDays(SENTINEL_SKIP_DAYS);
            Debug.Log($"[NET-SMOKE] server clock 고정 = {clock.ToDebugString()} (skip {SENTINEL_SKIP_DAYS}d)");

            // step-2: per-connection 프록시 스포너 활성 → 클라 연결 시 그 클라가 *소유*하는 프록시
            // 자동 스폰. 클라(owner)가 자기 probe 로 구동 → client-auth NetworkTransform 가 server 로
            // 푸시 → 여기서 그 위치를 관측 = client→server presence (step-1 의 server→client 역방향).
            PlayerNetProxySpawner.Enable(networkManager);
            Debug.Log("[NET-SMOKE] server per-connection 프록시 스포너 활성");

            // 원격 클라 1개 연결 대기.
            while (RemoteClientCount(networkManager) < 1 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            int clients = RemoteClientCount(networkManager);

            // 클라-소유 프록시가 스폰·전파되고, 클라가 probe(PROBE_*)로 구동한 위치가 client-auth 로
            // 서버에 도달할 때까지 대기 (spawn 원점에서 벗어남 = follow 전파 완료 신호).
            PlayerNetProxy clientProxy = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                PlayerNetProxySpawner.TryGetAnyClientProxy(out clientProxy);
                if (clientProxy != null && clientProxy.transform.position.sqrMagnitude > MOVED_SQR_MIN)
                {
                    break;
                }
                yield return null;
            }
            // 수렴 settle.
            for (int frame = 0; frame < SETTLE_FRAMES && Time.realtimeSinceStartup < deadline; frame++)
            {
                yield return null;
            }
            if (clientProxy != null)
            {
                Vector3 observed = clientProxy.transform.position;
                _proxyObserved = true;
                _proxyX = observed.x;
                _proxyY = observed.y;
                _proxyZ = observed.z;
                Debug.Log($"[NET-SMOKE] server 관측 클라-프록시 = ({observed.x:F2},{observed.y:F2},{observed.z:F2})");
            }

            bool pass = clients >= 1 && _proxyObserved && _nreCount == 0;
            WriteResult(
                pass ? "PASS" : "FAIL",
                pass ? "server up + bridge spawned + client-owned proxy observed (client→server presence)"
                     : $"server: 원격 클라 {clients}개 / proxy={_proxyObserved} / nre={_nreCount}",
                clock.Year, clock.Season, clock.Day, clock.Hour, clients);
            Finish(pass ? 0 : 1);
        }

        private IEnumerator RunClient(NetworkManager networkManager, float deadline)
        {
            // step-2: 헤드리스 클라엔 실 플레이어 없음 → distinctive 고정 위치 fake probe register.
            // 서버가 이 클라 소유 프록시를 스폰하면, 프록시(IsOwner)가 spawn 원점에서 이 위치로
            // *따라가* client-auth NetworkTransform 로 서버에 푸시 → 서버가 관측(client→server).
            LocalPlayerProbeBridge.Register(new FixedLocalPlayerProbe());
            Debug.Log($"[NET-SMOKE] client fake probe = ({PROBE_X},{PROBE_Y},{PROBE_Z}) register");

            if (networkManager.ClientManager.StartConnection() == false)
            {
                FailFinish("client: ClientManager.StartConnection() == false");
                yield break;
            }
            while (networkManager.ClientManager.Started == false && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            if (networkManager.ClientManager.Started == false)
            {
                FailFinish("client: ClientManager.Started 안 됨 (연결 실패)");
                yield break;
            }

            // 서버가 스폰한 브리지가 *원격 관측*으로 클라에 복제될 때까지 대기.
            WorldClockNetworkBridge bridge = null;
            while (bridge == null && Time.realtimeSinceStartup < deadline)
            {
                bridge = FindAnyObjectByType<WorldClockNetworkBridge>();
                yield return null;
            }
            if (bridge == null)
            {
                FailFinish("client: 브리지 미관측 (NetworkObject 스폰 전파 실패)");
                yield break;
            }

            // SyncVar 수신 대기 — Day 는 1-based(미수신=0). >=1 = 서버값 수신.
            while (bridge.SyncedDay < 1 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            // 4개 SyncVar 수렴 settle.
            for (int frame = 0; frame < SETTLE_FRAMES && Time.realtimeSinceStartup < deadline; frame++)
            {
                yield return null;
            }

            // step-2: 내가(클라=owner) 소유한 프록시가 스폰·probe-follow 로 distinctive 위치에 도달할
            // 때까지 대기 (server 가 이 클라용으로 스폰 → IsOwner==true → probe 따라감).
            PlayerNetProxy myProxy = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                myProxy = FindOwnedProxy();
                if (myProxy != null && myProxy.transform.position.sqrMagnitude > MOVED_SQR_MIN)
                {
                    break;
                }
                yield return null;
            }
            for (int frame = 0; frame < SETTLE_FRAMES && Time.realtimeSinceStartup < deadline; frame++)
            {
                yield return null;
            }
            if (myProxy != null)
            {
                Vector3 pp = myProxy.transform.position;
                _proxyObserved = true;
                _proxyX = pp.x;
                _proxyY = pp.y;
                _proxyZ = pp.z;
                Debug.Log($"[NET-SMOKE] client 소유 프록시(probe-follow) = ({pp.x:F2},{pp.y:F2},{pp.z:F2}) owner={myProxy.IsOwner}");
            }

            bool received = bridge.SyncedDay >= 1;
            bool pass = received && myProxy != null && _proxyObserved && _nreCount == 0;
            WriteResult(
                pass ? "PASS" : "FAIL",
                pass ? "client connected + bridge observed + SyncVar received + owned proxy probe-followed"
                     : $"client: day={bridge.SyncedDay} proxy={(myProxy != null)} obs={_proxyObserved} nre={_nreCount}",
                bridge.SyncedYear, bridge.SyncedSeason, bridge.SyncedDay, bridge.SyncedHour, -1);
            Finish(pass ? 0 : 1);
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static NetworkManager TryEnsureNetworkManager(out string error)
        {
            error = null;
            try
            {
                return NetworkBootstrap.EnsureNetworkManager();
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return null;
            }
        }

        private static WorldClockNetworkBridge TrySpawnBridge(NetworkManager networkManager, out string error)
        {
            error = null;
            try
            {
                return NetworkBootstrap.SpawnWorldClockBridge();
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return null;
            }
        }

        // 클라 측: 내가 소유(IsOwner)한 프록시 1개 찾기 (이 2-프로세스 셋업엔 클라-소유 프록시뿐).
        private static PlayerNetProxy FindOwnedProxy()
        {
            PlayerNetProxy[] all = FindObjectsByType<PlayerNetProxy>(FindObjectsSortMode.None);
            for (int index = 0; index < all.Length; index++)
            {
                if (all[index].IsOwner)
                {
                    return all[index];
                }
            }
            return all.Length > 0 ? all[0] : null;
        }

        // 헤드리스 클라 fake 로컬 플레이어 — distinctive 고정 위치 반환(실 플레이어 대역).
        private sealed class FixedLocalPlayerProbe : ILocalPlayerProbe
        {
            public bool TryGetPosition(out float x, out float y, out float z)
            {
                x = PROBE_X;
                y = PROBE_Y;
                z = PROBE_Z;
                return true;
            }
        }

        private static int RemoteClientCount(NetworkManager networkManager)
        {
            if (networkManager.ServerManager == null || networkManager.ServerManager.Clients == null)
            {
                return 0;
            }
            return networkManager.ServerManager.Clients.Count;
        }

        private static float EnvFloat(string key, float fallback)
        {
            string raw = Environment.GetEnvironmentVariable(key);
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && value > 0f)
            {
                return value;
            }
            return fallback;
        }

        private static void TouchReadyFile()
        {
            string path = Environment.GetEnvironmentVariable("WM_NET_SMOKE_READY");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dir) == false && Directory.Exists(dir) == false)
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(path, "ready\n");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NET-SMOKE] ready 파일 기록 실패: {ex.Message}");
            }
        }

        private void FailFinish(string reason)
        {
            WriteResult("FAIL", reason, 0, 0, 0, 0, -1);
            Finish(1);
        }

        private void WriteResult(string result, string reason, int year, int season, int day, int hour, int clients)
        {
            string body =
                $"result={result}\n" +
                $"reason={reason}\n" +
                $"role={_role}\n" +
                $"year={year}\n" +
                $"season={season}\n" +
                $"day={day}\n" +
                $"hour={hour}\n" +
                $"clients={clients}\n" +
                $"nre={_nreCount}\n" +
                $"proxyObserved={(_proxyObserved ? "true" : "false")}\n" +
                $"proxyX={_proxyX.ToString("F2", CultureInfo.InvariantCulture)}\n" +
                $"proxyY={_proxyY.ToString("F2", CultureInfo.InvariantCulture)}\n" +
                $"proxyZ={_proxyZ.ToString("F2", CultureInfo.InvariantCulture)}\n" +
                $"t={Time.realtimeSinceStartup.ToString("F1", CultureInfo.InvariantCulture)}\n";
            try
            {
                string path = ResultPath;
                string dir = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dir) == false && Directory.Exists(dir) == false)
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(path, body);
                Debug.Log($"[NET-SMOKE] {_role} {result} — {reason} → {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NET-SMOKE] 결과파일 기록 실패: {ex.Message}\n{body}");
            }
        }

        private void Finish(int exitCode)
        {
            if (_done)
            {
                return;
            }
            _done = true;
            UnityEngine.Application.logMessageReceived -= OnLog;
#if UNITY_EDITOR
            Debug.Log($"[NET-SMOKE] (editor) exitCode={exitCode} — Play 정지");
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit(exitCode);
#endif
        }
    }
}
