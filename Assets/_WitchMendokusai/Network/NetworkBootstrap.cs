using System;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-187 — FishNet 라이브 sync 1채널 first-use 박힘.
	///
	/// 씬 수동배치 X = 회귀망 안. 프로그래매틱하게 NetworkManager + Tugboat 호스트(server+client) 자체연결.
	/// 게임 heavy-boot 와 격리 — 빈 씬에서도 단독 기동 가능(WM-085 wedge 회피, PlayMode 자율검증 substrate).
	///
	/// 정본 진입점 = <see cref="EnsureHostStarted"/>(loopback localhost). 기동 후 <see cref="IsHostFullyStarted"/>
	/// 가 true 가 되면 server+client 양측 active = SyncVar 모서리 거동 관측 가능.
	/// </summary>
	public static class NetworkBootstrap
	{
		public const string LOOPBACK_ADDRESS = "127.0.0.1";
		public const ushort DEFAULT_PORT = 7770;
		private const string ROOT_NAME = "[WM.NetworkBootstrap]";

		private static GameObject root;
		private static NetworkManager networkManager;
		private static Tugboat tugboat;
		private static SinglePrefabObjects runtimePrefabObjects;

		/// <summary> 서버·클라 양측 활성 (host 모드 완전 기동). </summary>
		public static bool IsHostFullyStarted
		{
			get
			{
				if (networkManager == null)
					return false;
				return networkManager.IsServerStarted && networkManager.IsClientStarted;
			}
		}

		/// <summary> 활성 NetworkManager. 미기동 시 null. </summary>
		public static NetworkManager Manager => networkManager;

		/// <summary>
		/// 호스트 idempotent 기동. 이미 띄워져 있으면 no-op. 다른 NetworkManager(씬 배치)가 있으면 그것을 채택해 idempotent 재사용.
		/// 호출 후 즉시 IsHostFullyStarted=true 보장 X — 연결 ticks 가 흘러야 함 (transport handshake).
		/// </summary>
		public static void EnsureHostStarted(ushort port = DEFAULT_PORT)
		{
			if (IsHostFullyStarted == true)
				return;

			if (networkManager == null)
				networkManager = UnityEngine.Object.FindAnyObjectByType<NetworkManager>(); // init-order-ok: 호스트 진입 시점 = lazy resolve

			if (networkManager == null)
				CreateRoot();

			EnsureTugboat(port);

			if (networkManager.IsServerStarted == false)
				networkManager.ServerManager.StartConnection();

			if (networkManager.IsClientStarted == false)
				networkManager.ClientManager.StartConnection(LOOPBACK_ADDRESS);
		}

		/// <summary>
		/// 호스트 해제. PlayMode 종료 등 정리 시점에 호출.
		/// </summary>
		public static void StopHost()
		{
			if (networkManager == null)
				return;

			if (networkManager.IsClientStarted == true)
				networkManager.ClientManager.StopConnection();
			if (networkManager.IsServerStarted == true)
				networkManager.ServerManager.StopConnection(true);

			if (root != null)
			{
				UnityEngine.Object.Destroy(root);
				root = null;
				networkManager = null;
				tugboat = null;
				runtimePrefabObjects = null;
			}
		}

		/// <summary>
		/// 런타임 NetworkObject 를 서버 권위로 스폰. 호스트 모드 전용 — 클라(host client) 가 같은 프로세스라 즉시 미러.
		/// SpawnablePrefabs 등록 없이도 직접 NetworkObject 인스턴스를 받음.
		/// </summary>
		public static void ServerSpawn(NetworkObject networkObject)
		{
			if (networkManager == null)
				throw new InvalidOperationException(ROOT_NAME + " ServerSpawn 전에 EnsureHostStarted 호출 필수");

			networkManager.ServerManager.Spawn(networkObject);
		}

		private static void CreateRoot()
		{
			root = new GameObject(ROOT_NAME);
			UnityEngine.Object.DontDestroyOnLoad(root);
			networkManager = root.AddComponent<NetworkManager>();
			// 빈 런타임 prefab collection — Tests/PlayVerify 는 ServerSpawn(직접) 경로 사용. 게임 부팅 경로는 별도 prefab 등록 정본.
			runtimePrefabObjects = ScriptableObject.CreateInstance<SinglePrefabObjects>();
			runtimePrefabObjects.name = "WM_RuntimeSpawnables";
			networkManager.SpawnablePrefabs = runtimePrefabObjects;
		}

		private static void EnsureTugboat(ushort port)
		{
			if (tugboat == null)
				tugboat = networkManager.GetComponent<Tugboat>();
			if (tugboat == null)
				tugboat = networkManager.gameObject.AddComponent<Tugboat>();

			tugboat.SetPort(port);
			tugboat.SetClientAddress(LOOPBACK_ADDRESS);
			// transport 가 NetworkManager.TransportManager 에 채택되도록 명시.
			networkManager.TransportManager.Transport = tugboat;
		}
	}
}
