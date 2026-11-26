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
		public const int STAGE_ID = 6;

		[Header("Layout")]
		[SerializeField] private Vector3 pondCenter = new(21f, 0f, -11f);
		[SerializeField] private float pondRadius = 6.5f;
		[SerializeField] private Vector3 mineOffset = new(-300f, 0f, 0f);
		[Tooltip("마을 밖과 이어진 길. 시작과 끝 (x, z, x, z)")]
		[SerializeField] private Vector4 entryRoad = new(0f, -24f, 0f, -13f);

		private UIManager uiManager;
		private UIRoot uiRoot;
		private PlayerProvider playerProvider;
		private InputManager inputManager;
		private CameraManager cameraManager;
		private VillageGround ground;
		private Transform outdoor;
		private HomesteadSim builtFor;
		private bool isGroundReady;
		private HomesteadManager manager;

		private HomesteadSim Sim => manager.Sim;
		private HomesteadCatalog Catalog => manager.Sim.Catalog;
		private static HomesteadArt Art => HomesteadArt.Instance;

		[Inject]
		public void Construct(UIManager uiManager, UIRoot uiRoot, PlayerProvider playerProvider, InputManager inputManager, CameraManager cameraManager)
		{
			this.uiManager = uiManager;
			this.uiRoot = uiRoot;
			this.playerProvider = playerProvider;
			this.inputManager = inputManager;
			this.cameraManager = cameraManager;
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
			StopBuilding();
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
			Sim.LayoutChanged += OnLayoutChanged;
			Sim.Unveiled += OnUnveiled;
			RefreshAll();
			manager.Hud.ShowGoal(true);
			manager.Hud.ShowDigest(Sim.TakeNewDigest());
		}

		private void Unbind()
		{
			if (builtFor == null)
				return;
			builtFor.Changed -= OnSimChanged;
			builtFor.LayoutChanged -= OnLayoutChanged;
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
			VillageLook.Ensure(transform);
			Sim.SetVillageBlocked(BlockedCells());
			BuildSites();
			BuildFarm();
			BuildWild();
			BuildWater();
			BuildMine();
		}

		// 이 물체는 풀 소속이라 월드를 나갔다 와도 남음. 세이브를 다시 읽어 규칙이 바뀌었으면 새로 지음
		private void ClearBodies()
		{
			if (builtFor == null)
				return;
			Destroy(outdoor.gameObject);
			Destroy(mineRoot.gameObject);
			siteBodies.Clear();
			decorBodies.Clear();
			spotObjects.Clear();
			plots.Clear();
			gathers.Clear();
			restPoses.Clear();
			mineNodes.Clear();
			people.Clear();
			workers.Clear();
			animals.Clear();
			heldCrop = string.Empty;
			risingId = null;
		}

		// 물, 밭, 채집 자리를 바닥에 먼저 알림. 놓인 것 아래는 RefreshGroundCells
		private void MarkGround()
		{
			ground.AddPath(new Vector2(entryRoad.x, entryRoad.y), new Vector2(entryRoad.z, entryRoad.w));
			MarkWater();
			MarkFarm();
			MarkWild();
		}

		// 규칙 밖에서 막힌 칸. 물과 물가, 밭, 채집 자리
		private IEnumerable<(int X, int Z)> BlockedCells()
		{
			VillageAreaDef area = Catalog.VillageArea;
			for (int x = area.X; x < area.X + area.Width; x++)
			{
				for (int z = area.Z; z < area.Z + area.Depth; z++)
				{
					if (ground.IsWet(x + 0.5f, z + 0.5f) || IsWorkedCell(x, z))
						yield return (x, z);
				}
			}
		}

		private bool IsWorkedCell(int x, int z)
		{
			for (int i = 0; i < PLOT_MAX; i++)
			{
				Vector3 plot = PlotPosition(i);
				if (Mathf.FloorToInt(plot.x) == x && Mathf.FloorToInt(plot.z) == z)
					return true;
			}
			if (Mathf.FloorToInt(scarecrowPosition.x) == x && Mathf.FloorToInt(scarecrowPosition.z) == z)
				return true;
			foreach (GatherSite site in gatherSites)
			{
				if (Mathf.Abs(site.Position.x - x - 0.5f) < 1f && Mathf.Abs(site.Position.z - z - 0.5f) < 1f)
					return true;
			}
			return false;
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
			StepBuilding();
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
			RefreshWaterSpots();
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
