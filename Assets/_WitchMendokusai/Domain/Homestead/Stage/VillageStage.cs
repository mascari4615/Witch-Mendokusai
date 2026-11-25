using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 마을 스테이지의 뿌리. 바닥, 건물 자리, 밭, 들, 물가, 갱도, 집 안을 켜질 때 짓고 규칙 상태를 따라 모습을 바꿈
	// 집 안과 갱도는 같은 스테이지 안 먼 자리. 문을 지나면 가림막 뒤에서 옮김
	[RequireComponent(typeof(VillageGround))]
	public sealed partial class VillageStage : MonoBehaviour
	{
		[Serializable]
		public struct BuildingSite
		{
			public string Id;
			public Vector3 Position;
			public float Yaw;
			[Tooltip("폐허와 가림막의 크기 (m)")] public Vector2 Size;
			[Tooltip("건물 기준 문 자리")] public Vector3 Door;
		}

		[Header("Layout")]
		[SerializeField] private Vector3 townHallPosition = new(0f, 0f, 14f);
		[SerializeField] private Vector3 townHallDoor = new(0f, 0f, -3.6f);
		[SerializeField] private Vector3 boardPosition = new(4f, 0f, 11f);
		[SerializeField] private Vector3 housePosition = new(-15f, 0f, 10f);
		[SerializeField] private Vector3 houseDoor = new(0f, 0f, -3.2f);
		[SerializeField] private float houseYaw = 0f;
		[SerializeField] private Vector3 wellPosition = new(4f, 0f, 2f);
		[SerializeField] private Vector3 plazaCenter = new(0f, 0f, 5f);
		[SerializeField] private Vector3 pondCenter = new(21f, 0f, -11f);
		[SerializeField] private float pondRadius = 6.5f;
		[SerializeField] private Vector3 roomOffset = new(300f, 0f, 0f);
		[SerializeField] private Vector3 mineOffset = new(-300f, 0f, 0f);
		[Tooltip("곧은 길. 시작과 끝 (x, z, x, z)")]
		[SerializeField] private Vector4[] streets =
		{
			new(0f, -24f, 0f, 11f), new(-24f, 6f, 24f, 6f), new(-22f, -6f, 13f, -6f), new(-18f, 18f, 24f, 18f), new(-3f, 6f, -3f, 18f), new(24f, 6f, 24f, 18f),
		};
		[SerializeField] private BuildingSite[] sites =
		{
			new() { Id = "farm", Position = new Vector3(-20f, 0f, -2f), Yaw = 0f, Size = new Vector2(3f, 2.6f), Door = new Vector3(0f, 0f, -2f) },
			new() { Id = "workshop", Position = new Vector3(8f, 0f, 10f), Yaw = 0f, Size = new Vector2(4.2f, 3.2f), Door = new Vector3(0f, 0f, -2.4f) },
			new() { Id = "dock", Position = new Vector3(11.6f, 0f, -11f), Yaw = -90f, Size = new Vector2(2.2f, 2f), Door = new Vector3(0f, 0f, -0.4f) },
			new() { Id = "quarry", Position = new Vector3(24f, 0f, 14f), Yaw = 0f, Size = new Vector2(4f, 3f), Door = new Vector3(0f, 0f, -2.6f) },
			new() { Id = "museum", Position = new Vector3(12f, 0f, 22f), Yaw = 0f, Size = new Vector2(6f, 4f), Door = new Vector3(0f, 0f, -4f) },
			new() { Id = "ranch", Position = new Vector3(-22f, 0f, 10f), Yaw = 0f, Size = new Vector2(3.6f, 3f), Door = new Vector3(0f, 0f, -2.6f) },
			new() { Id = "tailor", Position = new Vector3(-6f, 0f, 22f), Yaw = 0f, Size = new Vector2(3.8f, 3f), Door = new Vector3(0f, 0f, -2.6f) },
			new() { Id = "training", Position = new Vector3(-16f, 0f, 22f), Yaw = 0f, Size = new Vector2(4.6f, 3.6f), Door = new Vector3(0f, 0f, -1.6f) },
			new() { Id = "shop", Position = new Vector3(8f, 0f, -2f), Yaw = 0f, Size = new Vector2(3.4f, 2.8f), Door = new Vector3(0f, 0f, -2.4f) },
			new() { Id = "smithy", Position = new Vector3(-8f, 0f, 10f), Yaw = 0f, Size = new Vector2(3.8f, 3f), Door = new Vector3(0f, 0f, -2.6f) },
			new() { Id = "atelier", Position = new Vector3(-8f, 0f, -2f), Yaw = 0f, Size = new Vector2(4f, 3.2f), Door = new Vector3(0f, 0f, -2.6f) },
			new() { Id = "trial", Position = new Vector3(24f, 0f, 4f), Yaw = 0f, Size = new Vector2(4.6f, 4.6f), Door = new Vector3(0f, 0f, -2.6f) },
		};

		private UIManager uiManager;
		private UIRoot uiRoot;
		private PlayerProvider playerProvider;
		private InputManager inputManager;
		private VillageGround ground;
		private Transform outdoor;
		private HomesteadSim builtFor;
		private bool isGroundReady;
		private HomesteadManager manager;

		private HomesteadSim Sim => manager.Sim;
		private HomesteadCatalog Catalog => manager.Sim.Catalog;
		private static HomesteadArt Art => HomesteadArt.Instance;

		[Inject]
		public void Construct(UIManager uiManager, UIRoot uiRoot, PlayerProvider playerProvider, InputManager inputManager)
		{
			this.uiManager = uiManager;
			this.uiRoot = uiRoot;
			this.playerProvider = playerProvider;
			this.inputManager = inputManager;
		}

		private void OnEnable()
		{
			if (HomesteadManager.TryGetExistingInstance(out HomesteadManager found) == false)
				return;
			manager = found;
			// 풀에 남아 있던 몸은 옛 월드의 것을 쥠
			if (uiManager == null)
				manager.InjectInto(gameObject);
			manager.Rebuilt += Rebind;
			Bind();
		}

		private void OnDisable()
		{
			StopFishing();
			LeaveMine();
			if (manager is null)
				return;
			manager.Rebuilt -= Rebind;
			Unbind();
			if (manager.Panels != null)
				manager.Panels.ClosePanel();
			if (manager.Hud != null)
				manager.Hud.ShowGoal(false);
		}

		private void Rebind()
		{
			StopFishing();
			LeaveMine();
			Unbind();
			Bind();
		}

		private void Bind()
		{
			manager.EnsureScreens(uiManager, uiRoot);
			if (builtFor != manager.Sim)
				BuildAll();
			Sim.Changed += OnSimChanged;
			Sim.Unveiled += OnUnveiled;
			RefreshAll();
			manager.Hud.ShowGoal(true);
			manager.Hud.ShowDigest(Sim.MakeDigest());
		}

		private void Unbind()
		{
			if (builtFor == null)
				return;
			builtFor.Changed -= OnSimChanged;
			builtFor.Unveiled -= OnUnveiled;
		}

		private void BuildAll()
		{
			ClearBodies();
			builtFor = manager.Sim;
			ground = GetComponent<VillageGround>();
			outdoor = new GameObject("Outdoor").transform;
			outdoor.SetParent(transform, false);
			if (isGroundReady == false)
			{
				isGroundReady = true;
				MarkGround();
				ground.Build();
			}
			BuildSites();
			BuildFarm();
			BuildWild();
			BuildWater();
			BuildMuseumGarden();
			BuildRoom();
			BuildMine();
		}

		// 이 물체는 풀 소속이라 월드를 나갔다 와도 남음. 세이브를 다시 읽어 규칙이 바뀌었으면 새로 지음
		private void ClearBodies()
		{
			if (builtFor == null)
				return;
			Destroy(outdoor.gameObject);
			Destroy(room.gameObject);
			Destroy(mineRoot.gameObject);
			siteBodies.Clear();
			lampLights.Clear();
			plots.Clear();
			gathers.Clear();
			restPoses.Clear();
			exhibits.Clear();
			mineNodes.Clear();
			people.Clear();
			workers.Clear();
			animals.Clear();
			heldCrop = string.Empty;
			risingId = null;
			shownRoomTier = 0;
			shownFurniture = string.Empty;
			roomShell = null;
		}

		// 길과 비울 자리를 바닥에 먼저 알림
		private void MarkGround()
		{
			Vector2 plaza = new(plazaCenter.x, plazaCenter.z);
			ground.AddBlock(plaza, 3.2f);
			foreach (Vector4 street in streets)
				ground.AddPath(new Vector2(street.x, street.y), new Vector2(street.z, street.w));
			Link(townHallPosition + townHallDoor, 3.2f, townHallPosition);
			Link(housePosition + houseDoor, 2.8f, housePosition);
			ground.AddBlock(new Vector2(boardPosition.x, boardPosition.z), 1.1f);
			foreach (BuildingSite site in sites)
			{
				Vector3 door = site.Position + Quaternion.Euler(0f, site.Yaw, 0f) * site.Door;
				Link(door, Mathf.Max(site.Size.x, site.Size.y) * 0.5f, site.Position);
			}
			MarkWater();
			MarkFarm();
			MarkWild();
		}

		// 문 앞에서 가장 가까운 길까지 직각으로
		private void Link(Vector3 door, float radius, Vector3 center)
		{
			Vector2 from = new(door.x, door.z);
			Vector2 nearest = from;
			float best = float.MaxValue;
			foreach (Vector4 street in streets)
			{
				Vector2 start = new(Mathf.Min(street.x, street.z), Mathf.Min(street.y, street.w));
				Vector2 end = new(Mathf.Max(street.x, street.z), Mathf.Max(street.y, street.w));
				Vector2 point = new(Mathf.Clamp(from.x, start.x, end.x), Mathf.Clamp(from.y, start.y, end.y));
				if (Vector2.Distance(from, point) >= best)
					continue;
				best = Vector2.Distance(from, point);
				nearest = point;
			}
			Vector2 corner = new(from.x, nearest.y);
			ground.AddPath(from, corner);
			ground.AddPath(corner, nearest);
			ground.AddBlock(new Vector2(center.x, center.z), radius);
		}

		private bool refreshQueued;

		private void OnSimChanged() => refreshQueued = true;

		// 분이 흐를 때마다 알림이 옴. 모습은 프레임에 한 번만 맞춤
		private void LateUpdate()
		{
			if (manager == null || builtFor == null)
				return;
			StepFishing();
			StepMine();
			if (refreshQueued == false)
				return;
			refreshQueued = false;
			RefreshAll();
		}

		private void RefreshAll()
		{
			RefreshSites();
			RefreshFarm();
			RefreshWild();
			RefreshMuseumGarden();
			RefreshRoom();
			RefreshLife();
		}

		private void Say(string text) => uiManager.SpeechBubble.Show(playerProvider.Current.transform, text);

		// 거절은 말풍선으로. 조용한 실패는 고장으로 읽힘
		private bool Report(HomesteadResult result)
		{
			if (result.Ok)
				return true;
			UiSfx.Play(UiSfxEvent.Blocked);
			Say(HomesteadText.Reason(result, Catalog));
			return false;
		}

		private bool isMoving;

		private void MovePlayer(Vector3 localPosition, Action onMoved = null)
		{
			isMoving = true;
			uiManager.Transition.Transition(
				aDuringTransition: () =>
				{
					playerProvider.CurrentObject.UnitMovement.Teleport(transform.TransformPoint(localPosition));
					onMoved?.Invoke();
					isMoving = false;
				}).Forget();
		}
	}
}
