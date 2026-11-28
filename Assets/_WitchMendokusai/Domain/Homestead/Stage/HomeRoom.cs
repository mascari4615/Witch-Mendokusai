using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 욘의 집 안 (WS_1_HomeInside). 큰 나무 아래라 마을, 길드와 별개
	// 건물 한 채 (프리팹 HomeHouse), 놓은 가구, 제작대, 옷장, 쉬는 자리, 부엌 (HomeRoom.Cook). 스테이지에 들어설 때 HomesteadManager 가 붙임
	// 방 크기 단계는 가구 칸과 개수만 늘리고 건물은 그대로
	public sealed partial class HomeRoom : MonoBehaviour
	{
		public const int STAGE_ID = 1;
		private const string FURNITURE_KEY = "F_";

		[Header("Room")]
		[Tooltip("집 문의 자리 (프리팹 원점). 스테이지의 들어오는 자리 (z -12.1) 가 문 안쪽에 오게")]
		[SerializeField] private Vector3 doorstep = new(-0.5f, 0.14f, -13.6f);
		[SerializeField] private Color glowColor = new(1f, 0.82f, 0.6f);
		[SerializeField] private float glowRange = 3.5f;
		[SerializeField] private float glowIntensity = 1.6f;
		[Tooltip("임시 모형. 방이 대신함")]
		[SerializeField] private string placeholderName = "TreeHouse";

		private UIManager uiManager;
		private UIRoot uiRoot;
		private PlayerProvider playerProvider;
		private HomesteadManager manager;
		private HomesteadSim shownSim;
		private Transform room;
		private Transform furnitureRoot;
		private HomeHouse house;
		private OrbitCameraProfile outdoorView;
		private Transform deskSpot;
		private Transform restSpot;
		private Transform benchSpot;
		private Transform wardrobeSpot;
		private Transform cookSpot;
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
			IndoorScope.Enter(this);
			EnterView();
			shownSim = Sim;
			shownFurniture = string.Empty;
			Sim.Changed += OnChanged;
			manager.Rebuilt += OnRebuilt;
			manager.CookRequested += BeginCook;
			Refresh();
		}

		private void OnDisable()
		{
			IndoorScope.Leave(this);
			LeaveView();
			Unbind();
		}

		// 실내 카메라는 집이 들고 있음. 나갈 때 들어오기 전 것으로 되돌림
		private void EnterView()
		{
			CameraManager cameras = CameraManager.Instance;
			if (cameras.OrbitProfile != house.IndoorView)
				outdoorView = cameras.OrbitProfile;
			cameras.SetOrbitProfile(house.IndoorView);
		}

		private void LeaveView()
		{
			CameraManager cameras = CameraManager.Instance;
			if (house == null || cameras == null || cameras.OrbitProfile != house.IndoorView)
				return;
			cameras.SetOrbitProfile(outdoorView);
		}

		private void Unbind()
		{
			if (shownSim != null)
				shownSim.Changed -= OnChanged;
			shownSim = null;
			if (manager != null)
			{
				manager.Rebuilt -= OnRebuilt;
				manager.CookRequested -= BeginCook;
			}
			StopCook();
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
			// 집을 세운 뒤에야 room 을 채움. 프리팹이 비어 실패하면 다음 Open 이 다시 짓고 같은 오류를 다시 냄
			Transform built = new GameObject("Room").transform;
			built.SetParent(transform, false);
			built.localPosition = doorstep;
			house = Instantiate(Models.HomeHouse, built, false);
			room = built;
			VillageInterior.Solidify(house.Shell);
			furnitureRoot = new GameObject("Furniture").transform;
			furnitureRoot.SetParent(house.FurnitureArea, false);
			deskSpot = AddSpot("DeskSpot", "꾸밈 상자", () => "방 꾸미기", () => manager.Panels.Show(HomesteadPanelType.Room), () => "꾸미기");
			restSpot = AddSpot("RestSpot", "쉬는 자리", () => manager.CanRest ? $"아침까지, 아늑함 {Sim.ComfortRank}단" : "오늘은 이미 쉼", Rest, () => manager.CanRest ? "쉬기" : string.Empty);
			benchSpot = AddSpot("BenchSpot", "제작대", () => "만들기", () => manager.Panels.Show(HomesteadPanelType.Bench), () => "만들기");
			wardrobeSpot = AddSpot("WardrobeSpot", "옷장", () => "갈아입기", () => manager.Panels.Show(HomesteadPanelType.Wardrobe), () => "옷장");
			deskSpot.position = house.DeskSpot.position;
			benchSpot.position = house.BenchSpot.position;
			wardrobeSpot.position = house.WardrobeSpot.position;
			cookSpot = AddSpot("CookSpot", "화덕", CookRole, () => manager.Panels.Show(HomesteadPanelType.Cook), () => "요리");
			cookSpot.position = house.CookSpot.position;
			restSpot.position = house.RestSpot.position;
			BuildFamiliars();
		}

		private Transform AddSpot(string objectName, string spotName, System.Func<string> role, System.Action use, System.Func<string> verb) =>
			HomesteadSpot.Create(room, Vector3.zero, objectName, () => spotName, role, use, verb).transform;

		private void Refresh()
		{
			RefreshFurniture();
			RefreshFamiliars();
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
			// 침대를 치우면 집의 쉬는 자리로 돌아감
			restSpot.position = house.RestSpot.position;
			foreach (PlacedFurniture piece in Sim.State.Placed)
			{
				FurnitureDef furniture = Sim.Catalog.Get<FurnitureDef>(piece.Id);
				GameObject body = Models.Build(FURNITURE_KEY + furniture.Id, furnitureRoot, new Vector3(piece.X, 0f, piece.Z), piece.Yaw);
				// 쉬는 자리는 침대가 있으면 침대 곁
				if (furniture.Shape == "bed")
					restSpot.position = furnitureRoot.TransformPoint(new Vector3(piece.X, 0f, piece.Z) + Quaternion.Euler(0f, piece.Yaw, 0f) * new Vector3(furniture.Width * 0.5f + 0.4f, 0f, 0f));
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
					manager.Hud.ShowDigest(Sim.TakeNewDigest());
				}).Forget();
		}
	}
}
