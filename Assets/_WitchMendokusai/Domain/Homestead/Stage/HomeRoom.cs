using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 욘의 집 안 (WS_1_HomeInside). 큰 나무 아래라 마을, 길드와 별개
	// 방, 놓은 가구, 제작대, 옷장, 쉬는 자리. 스테이지에 들어설 때 HomesteadManager 가 붙임
	public sealed class HomeRoom : MonoBehaviour
	{
		public const int STAGE_ID = 1;
		private const string FURNITURE_KEY = "F_";
		private const int HALL_WIDTH = 6;

		[Header("Room")]
		[Tooltip("방 앞턱의 자리. 스테이지의 포탈 앞")]
		[SerializeField] private Vector3 doorstep = new(-0.5f, 0.14f, -11f);
		[SerializeField] private int hallLength = 5;
		[SerializeField] private float roomHeight = 2.8f;
		[Tooltip("천장 조각의 높이 (모델 RoomCeiling)")]
		[SerializeField] private float ceilingHeight = 2.7f;
		[SerializeField] private Color lampColor = new(1f, 0.9f, 0.74f);
		[SerializeField] private float lampIntensity = 3.2f;
		[SerializeField] private Color glowColor = new(1f, 0.82f, 0.6f);
		[SerializeField] private float glowRange = 3.5f;
		[SerializeField] private float glowIntensity = 1.6f;
		[SerializeField] private Color shadeColor = new(0.35f, 0.3f, 0.42f);
		[SerializeField] private float domeRadius = 26f;
		[Tooltip("임시 모형. 방이 대신함")]
		[SerializeField] private string placeholderName = "TreeHouse";

		private UIManager uiManager;
		private UIRoot uiRoot;
		private PlayerProvider playerProvider;
		private HomesteadManager manager;
		private HomesteadSim shownSim;
		private Transform room;
		private Transform furnitureRoot;
		private GameObject shell;
		private Transform deskSpot;
		private Transform restSpot;
		private Transform benchSpot;
		private Transform wardrobeSpot;
		private int shownTier;
		private string shownFurniture = string.Empty;
		private bool refreshQueued;

		private HomesteadSim Sim => manager.Sim;
		private VillageModels Models => VillageModels.Instance;

		public static void Attach(HomesteadManager owner, StageObject stageObject)
		{
			HomeRoom found = stageObject.GetComponent<HomeRoom>();
			if (found == null)
				found = stageObject.gameObject.AddComponent<HomeRoom>();
			owner.InjectInto(stageObject.gameObject);
			found.Open(owner);
		}

		[Inject]
		public void Construct(UIManager uiManager, UIRoot uiRoot, PlayerProvider playerProvider)
		{
			this.uiManager = uiManager;
			this.uiRoot = uiRoot;
			this.playerProvider = playerProvider;
		}

		private void Open(HomesteadManager owner)
		{
			Unbind();
			manager = owner;
			manager.EnsureScreens(uiManager, uiRoot);
			if (room == null)
				Build();
			shownSim = Sim;
			shownTier = 0;
			shownFurniture = string.Empty;
			Sim.Changed += OnChanged;
			manager.Rebuilt += OnRebuilt;
			Refresh();
		}

		private void OnDisable() => Unbind();

		private void Unbind()
		{
			if (shownSim != null)
				shownSim.Changed -= OnChanged;
			shownSim = null;
			if (manager != null)
				manager.Rebuilt -= OnRebuilt;
		}

		private void OnRebuilt() => Open(manager);

		private void OnChanged() => refreshQueued = true;

		private void LateUpdate()
		{
			if (refreshQueued == false || shownSim == null)
				return;
			refreshQueued = false;
			Refresh();
		}

		private void Build()
		{
			foreach (Transform part in GetComponentsInChildren<Transform>(true))
			{
				if (part.name == placeholderName)
					part.gameObject.SetActive(false);
			}
			room = new GameObject("Room").transform;
			room.SetParent(transform, false);
			VillageLights.BuildDome(room, domeRadius, 8f, shadeColor);
			furnitureRoot = new GameObject("Furniture").transform;
			furnitureRoot.SetParent(room, false);
			deskSpot = AddSpot("DeskSpot", "꾸밈 상자", () => "방 꾸미기", () => manager.Panels.Show(HomesteadPanelType.Room));
			restSpot = AddSpot("RestSpot", "쉬는 자리", () => manager.CanRest ? $"아침까지, 아늑함 {Sim.ComfortRank}단" : "오늘은 이미 쉼", Rest);
			benchSpot = AddSpot("BenchSpot", "제작대", () => "만들기", () => manager.Panels.Show(HomesteadPanelType.Bench));
			wardrobeSpot = AddSpot("WardrobeSpot", "옷장", () => "갈아입기", () => manager.Panels.Show(HomesteadPanelType.Wardrobe));
		}

		private Transform AddSpot(string objectName, string spotName, System.Func<string> role, System.Action use) =>
			HomesteadSpot.Create(room, Vector3.zero, objectName, () => spotName, role, use).transform;

		private void Refresh()
		{
			RefreshShell();
			RefreshFurniture();
		}

		private void RefreshShell()
		{
			if (shownTier == Sim.State.HouseTier)
				return;
			shownTier = Sim.State.HouseTier;
			if (shell != null)
				Destroy(shell);
			int width = Mathf.RoundToInt(Sim.HouseTier.Width);
			int depth = Mathf.RoundToInt(Sim.HouseTier.Depth);
			room.localPosition = doorstep + new Vector3(0f, 0f, depth * 0.5f);
			shell = new GameObject("RoomShell");
			shell.transform.SetParent(room, false);
			BuildShell(shell.transform, width, depth);
			VillageInterior.Solidify(shell);
			VillageLights.AddGlow(shell.transform, new Vector3(0f, roomHeight - 0.4f, 0f), Mathf.Max(width, depth) * 1.2f, lampIntensity, lampColor);
			deskSpot.localPosition = new Vector3(width / 2f - 0.6f, 0f, -depth / 2f + 0.9f);
			restSpot.localPosition = new Vector3(-width / 2f + 0.7f, 0f, depth / 2f - 0.9f);
			benchSpot.localPosition = new Vector3(-1.9f, 0f, -depth / 2f - 2f);
			wardrobeSpot.localPosition = new Vector3(1.9f, 0f, -depth / 2f - 2f);
		}

		// 바닥, 벽, 천장을 1m 조각으로. 앞벽도 닫고 현관 문만 엶. 벽이 시야를 막으면 카메라가 방 안으로 당겨짐 (자유 카메라)
		private void BuildShell(Transform shell, int width, int depth)
		{
			float halfWidth = width * 0.5f;
			float halfDepth = depth * 0.5f;
			for (int x = 0; x < width; x++)
			{
				float along = x - halfWidth + 0.5f;
				for (int z = 0; z < depth; z++)
				{
					Models.Build((x + z) % 2 == 0 ? "RoomFloor" : "RoomFloorB", shell, new Vector3(along, 0f, z - halfDepth + 0.5f), 0f);
					Models.Build(z % 2 == 0 ? "RoomCeilingBeam" : "RoomCeiling", shell, new Vector3(along, 0f, z - halfDepth + 0.5f), 0f);
				}
				Models.Build(WallPiece(x), shell, new Vector3(along, 0f, halfDepth), 0f);
				if (Mathf.Abs(along) > HALL_WIDTH * 0.5f)
					Models.Build(WallPiece(x), shell, new Vector3(along, 0f, -halfDepth), 180f);
			}
			for (int z = 0; z < depth; z++)
			{
				float along = z - halfDepth + 0.5f;
				Models.Build(WallPiece(z), shell, new Vector3(halfWidth, 0f, along), 90f);
				Models.Build(WallPiece(z), shell, new Vector3(-halfWidth, 0f, along), -90f);
			}
			for (int side = -1; side <= 1; side += 2)
			{
				Models.Build("RoomCorner", shell, new Vector3(side * halfWidth, 0f, halfDepth), 0f);
				AddSolid(shell, new Vector3(side * (halfWidth + 0.15f), 1.4f, 0f), new Vector3(0.3f, 3f, depth));
				// 앞벽은 현관 폭만큼 열림. 방이 현관보다 넓을 때만 양옆이 막힘
				float frontWall = halfWidth - HALL_WIDTH * 0.5f;
				if (frontWall > 0f)
					AddSolid(shell, new Vector3(side * (halfWidth + HALL_WIDTH * 0.5f) * 0.5f, 1.4f, -halfDepth - 0.15f), new Vector3(frontWall, 3f, 0.3f));
			}
			AddSolid(shell, new Vector3(0f, 1.4f, halfDepth + 0.15f), new Vector3(width, 3f, 0.3f));
			AddSolid(shell, new Vector3(0f, ceilingHeight + 0.15f, -hallLength * 0.5f), new Vector3(width + 1f, 0.3f, depth + hallLength + 1f));
			shell.gameObject.AddComponent<GroundSurface>();
			BuildHall(shell, -halfDepth);
			Models.Build("RoomChest", shell, new Vector3(halfWidth - 0.5f, 0f, -halfDepth + 0.4f), 0f);
		}

		private static string WallPiece(int index) => index % 2 == 0 ? "RoomWallPost" : "RoomWall";

		private static void AddSolid(Transform parent, Vector3 center, Vector3 size)
		{
			BoxCollider box = parent.gameObject.AddComponent<BoxCollider>();
			box.center = center;
			box.size = size;
		}

		// 문에서 포탈까지의 현관. 제작대와 옷장이 양옆
		private void BuildHall(Transform shell, float front)
		{
			for (int z = 0; z < hallLength; z++)
			{
				for (int x = 0; x < HALL_WIDTH; x++)
				{
					Models.Build((x + z) % 2 == 0 ? "RoomFloor" : "RoomFloorB", shell, new Vector3(x - HALL_WIDTH * 0.5f + 0.5f, 0f, front - 0.5f - z), 0f);
					Models.Build(z % 2 == 0 ? "RoomCeilingBeam" : "RoomCeiling", shell, new Vector3(x - HALL_WIDTH * 0.5f + 0.5f, 0f, front - 0.5f - z), 0f);
				}
				Models.Build(WallPiece(z), shell, new Vector3(HALL_WIDTH * 0.5f, 0f, front - 0.5f - z), 90f);
				Models.Build(WallPiece(z), shell, new Vector3(-HALL_WIDTH * 0.5f, 0f, front - 0.5f - z), -90f);
			}
			for (int side = -1; side <= 1; side += 2)
				AddSolid(shell, new Vector3(side * (HALL_WIDTH * 0.5f + 0.15f), 1.4f, front - hallLength * 0.5f), new Vector3(0.3f, 3f, hallLength));
			AddSolid(shell, new Vector3(0f, 1.4f, front - hallLength - 1.6f), new Vector3(HALL_WIDTH + 2f, 3f, 0.3f));
			// 현관 끝. 가운데는 바깥으로 나가는 문
			for (int x = 0; x < HALL_WIDTH; x++)
			{
				float along = x - HALL_WIDTH * 0.5f + 0.5f;
				if (Mathf.Abs(along) > 1f)
					Models.Build(WallPiece(x), shell, new Vector3(along, 0f, front - hallLength), 180f);
			}
			Models.Build("RoomDoor", shell, new Vector3(0f, 0f, front - hallLength), 0f);
			AddSolid(shell, new Vector3(0f, -0.6f, 0f), new Vector3(30f, 1.2f, 40f));
			Models.Build("Workbench", shell, new Vector3(-2.4f, 0f, front - 2f), -90f);
			Models.Build("Wardrobe", shell, new Vector3(2.5f, 0f, front - 2f), 90f);
		}

		private void RefreshFurniture()
		{
			StringBuilder key = new();
			foreach (PlacedFurniture piece in Sim.State.Placed)
				key.Append(piece.Uid).Append(':').Append(piece.X).Append(',').Append(piece.Z).Append(',').Append(piece.Yaw).Append(';');
			string wanted = key.ToString();
			if (wanted == shownFurniture)
				return;
			shownFurniture = wanted;
			for (int i = furnitureRoot.childCount - 1; i >= 0; i--)
				Destroy(furnitureRoot.GetChild(i).gameObject);
			foreach (PlacedFurniture piece in Sim.State.Placed)
			{
				FurnitureDef furniture = Sim.Catalog.Get<FurnitureDef>(piece.Id);
				GameObject body = Models.Build(FURNITURE_KEY + furniture.Id, furnitureRoot, new Vector3(piece.X, 0f, piece.Z), piece.Yaw);
				// 쉬는 자리는 침대가 있으면 침대 곁
				if (furniture.Shape == "bed")
					restSpot.localPosition = new Vector3(piece.X, 0f, piece.Z) + Quaternion.Euler(0f, piece.Yaw, 0f) * new Vector3(furniture.Width * 0.5f + 0.4f, 0f, 0f);
				if (furniture.Shape == "lamp" || furniture.Shape == "fireplace" || furniture.Shape == "candle")
					VillageLights.AddGlow(body.transform, new Vector3(0f, 1f, -0.3f), glowRange, glowIntensity, glowColor);
			}
		}

		private void Rest()
		{
			if (manager.CanRest == false)
			{
				UiSfx.Play(UiSfxEvent.Blocked);
				uiManager.SpeechBubble.Show(playerProvider.Current.transform, "오늘은 이미 쉬었습니다.");
				return;
			}
			uiManager.Transition.Transition(
				aDuringTransition: () =>
				{
					manager.RestUntilMorning();
					manager.Hud.Notice("잘 쉬었습니다", $"하루 동안 두 배로 얻을 확률 {Mathf.RoundToInt(Sim.State.RestedRank * Sim.Catalog.Tuning.RestDoubleChancePerRank * 100f)}%", HomesteadArt.Instance.Icon("house"));
					manager.Hud.ShowDigest(Sim.MakeDigest());
				}).Forget();
		}
	}
}
