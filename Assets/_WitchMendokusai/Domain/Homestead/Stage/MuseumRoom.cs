using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 박물관 안 (WS_7_Museum). 마을과 다른 스테이지. 안내 홀에서 네 전시실로, 빈 받침 앞에서 바로 기증하면 그 자리에 놓임
	// 방은 스테이지에 들어설 때 HomesteadManager 가 붙임. 방 다섯은 같은 스테이지 안 떨어진 자리, 문은 화면 전환 뒤 순간 이동
	// 벽 넷과 천장을 닫은 방. 벽이 시야를 막으면 카메라가 방 안으로 당겨짐 (자유 카메라)
	public sealed class MuseumRoom : MonoBehaviour
	{
		public const int STAGE_ID = 7;
		private const string ROOM_KEY = "Museum_";
		private const string STAND_KEY = "Stand_";
		private const string CROP_KEY = "Crop_";
		private const string HALL_ROOM = "hall";

		[Serializable]
		public struct Wing
		{
			public string Id;
			[Tooltip("홀에서 이 전시실로 가는 문 앞 (홀 기준)")] public Vector3 Door;
			[Tooltip("받침 사이 (m)")] public float Spacing;
			[Tooltip("받침 위 놓이는 높이")] public float ItemHeight;
			public float ItemSize;
			public Color Light;
		}

		[Header("Rooms")]
		[SerializeField] private float roomGap = 90f;
		[Tooltip("방 바닥 (m). 모델 museum.py 의 W, D 에 배율 S 1.7 을 곱한 값. 자유 카메라가 벽에 막히지 않게 실제보다 크게")]
		[SerializeField] private Vector2 roomSize = new(22.1f, 17f);
		[SerializeField] private float wallHeight = 7.2f;
		[SerializeField] private float wallThickness = 1f;
		[SerializeField] private float ceilingHeight = 7.14f;
		[SerializeField] private float domeRadius = 34f;
		[SerializeField] private float domeRoof = 9f;
		[SerializeField] private Color shadeColor = new(0.1f, 0.09f, 0.13f);
		[SerializeField] private Vector3 door = new(0f, 0f, -7.8f);
		[Tooltip("문으로 들어와 서는 자리 (문 기준)")]
		[SerializeField] private Vector3 arrival = new(0f, 0.1f, 5.5f);
		[Tooltip("안내대 앞. 안내대는 키우지 않은 사람 크기라 기준점만 옮겨짐")]
		[SerializeField] private Vector3 desk = new(0f, 0f, 3.45f);
		[SerializeField] private Vector2 roomLight = new(22f, 7.1f);
		[SerializeField] private float roomLightHeight = 5.4f;
		[Tooltip("방 가운데에서 양옆 등까지 (방 폭에 대한 비율)")]
		[SerializeField] private float roomLightSpread = 0.27f;
		[SerializeField] private Color hallLight = new(1f, 0.88f, 0.7f);

		[Header("Stands")]
		[SerializeField] private float standInset = 0.95f;
		[Tooltip("벽을 따라 놓을 때 모서리에서 비우는 길이 (좌우 벽, 북쪽 벽)")]
		[SerializeField] private Vector2 standCorner = new(3.2f, 2.2f);
		[SerializeField] private float islandDepth = 0.5f;
		[SerializeField] private float standFront = 0.5f;
		[SerializeField] private float cropScale = 0.8f;
		[SerializeField] private float itemBob = 0.03f;
		[SerializeField] private float popSeconds = 0.22f;
		[SerializeField] private float popGrow = 0.35f;

		[Header("Return")]
		[Tooltip("마을에 돌아와 서는 자리. 박물관 문 앞에서 더 나온 길이")]
		[SerializeField] private float villageStep = 0.8f;

		[SerializeField] private Wing[] wings =
		{
			new() { Id = "plants", Door = new Vector3(-10.05f, 0f, 0.68f), Spacing = 2.2f, ItemHeight = 0.36f, ItemSize = 0.55f, Light = new Color(1f, 0.95f, 0.76f) },
			new() { Id = "waters", Door = new Vector3(10.05f, 0f, 0.68f), Spacing = 2.6f, ItemHeight = 0.92f, ItemSize = 0.5f, Light = new Color(0.62f, 0.86f, 1f) },
			new() { Id = "stones", Door = new Vector3(-5.78f, 0f, 7.5f), Spacing = 2.6f, ItemHeight = 0.62f, ItemSize = 0.6f, Light = new Color(1f, 0.84f, 0.62f) },
			new() { Id = "relics", Door = new Vector3(5.78f, 0f, 7.5f), Spacing = 2.4f, ItemHeight = 0.88f, ItemSize = 0.6f, Light = new Color(1f, 0.8f, 0.72f) },
		};

		private sealed class Exhibit
		{
			public GoodsDef Goods;
			public GameObject Item;
		}

		private readonly List<Exhibit> exhibits = new();
		private readonly Dictionary<Transform, Vector3> popBases = new();
		private UIManager uiManager;
		private UIRoot uiRoot;
		private PlayerProvider playerProvider;
		private HomesteadManager manager;
		private HomesteadSim builtFor;
		private bool refreshQueued;
		private bool isMoving;

		private HomesteadSim Sim => manager.Sim;
		private HomesteadCatalog Catalog => manager.Sim.Catalog;
		private static VillageModels Models => VillageModels.Instance;
		private static HomesteadArt Art => HomesteadArt.Instance;

		public static void Attach(HomesteadManager owner, StageObject stageObject)
		{
			MuseumRoom found = stageObject.GetComponent<MuseumRoom>();
			if (found == null)
				found = stageObject.gameObject.AddComponent<MuseumRoom>();
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
			// 세이브를 다시 읽으면 규칙 덩어리가 바뀜. 그때만 다시 지음
			if (builtFor != Sim)
				Build();
			// 마을과 같은 화면 색과 번짐 (bloom). 없으면 실내만 밋밋하게 보임
			VillageLook.Ensure(transform);
			Sim.Changed += OnChanged;
			manager.Rebuilt += OnRebuilt;
			Refresh();
			playerProvider.CurrentObject.UnitMovement.Teleport(transform.TransformPoint(door + arrival));
		}

		private void OnDisable() => Unbind();

		private void Unbind()
		{
			if (builtFor != null)
				builtFor.Changed -= OnChanged;
			if (manager != null)
				manager.Rebuilt -= OnRebuilt;
		}

		private void OnRebuilt() => Open(manager);

		private void OnChanged() => refreshQueued = true;

		private void LateUpdate()
		{
			if (refreshQueued == false)
				return;
			refreshQueued = false;
			Refresh();
		}

		private Vector3 RoomOffset(int index) => Vector3.right * (roomGap * index);

		// 방 0 은 홀, 그 뒤는 wings 순서
		private void Build()
		{
			for (int i = transform.childCount - 1; i >= 0; i--)
				Destroy(transform.GetChild(i).gameObject);
			exhibits.Clear();
			builtFor = Sim;
			Transform hall = BuildRoom(HALL_ROOM, 0, hallLight);
			HomesteadSpot.Create(hall, door, "MuseumExit", () => "박물관", () => "나가기", Leave, () => "나가기");
			HomesteadSpot.Create(hall, desk, "MuseumDesk", () => "안내대", DeskRole, () => manager.Panels.Show(HomesteadPanelType.Museum), () => "안내대");
			for (int i = 0; i < wings.Length; i++)
			{
				int room = i + 1;
				Wing setting = wings[i];
				WingDef wing = Catalog.Get<WingDef>(setting.Id);
				Transform root = BuildRoom(setting.Id, room, setting.Light);
				HomesteadSpot.Create(hall, setting.Door, "WingDoor", () => $"{wing.Name} 전시실", () => $"{Sim.DonatedCount(wing)} / {Sim.ExhibitsOf(wing).Count}", () => GoToRoom(room), () => "들어가기");
				HomesteadSpot.Create(root, door, "WingExit", () => "안내 홀", () => "돌아가기", () => GoToRoom(0), () => "돌아가기");
				BuildExhibits(root, setting, Sim.ExhibitsOf(wing));
			}
		}

		private string DeskRole()
		{
			foreach (WingDef wing in Catalog.Wings)
			{
				if (Sim.HasClaimableMilestone(wing))
					return "받을 보상이 있음";
			}
			return Sim.State.DirtyRelics > 0 ? $"손질할 유물 {Sim.State.DirtyRelics}" : "기증과 손질";
		}

		private Transform BuildRoom(string key, int index, Color light)
		{
			GameObject root = new("Museum " + key);
			root.transform.SetParent(transform, false);
			root.transform.localPosition = RoomOffset(index);
			VillageLights.BuildDome(root.transform, domeRadius, domeRoof, shadeColor);
			VillageInterior.Solidify(Models.Build(ROOM_KEY + key, root.transform, Vector3.zero, 0f));
			for (int side = -1; side <= 1; side += 2)
			{
				VillageLights.AddGlow(root.transform, new Vector3(side * roomSize.x * roomLightSpread, roomLightHeight, 0f), roomLight.x, roomLight.y, light);
				AddWall(root, new Vector3(side * (roomSize.x + wallThickness) * 0.5f, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, roomSize.y + wallThickness * 2f));
				AddWall(root, new Vector3(0f, wallHeight * 0.5f, side * (roomSize.y + wallThickness) * 0.5f), new Vector3(roomSize.x + wallThickness * 2f, wallHeight, wallThickness));
			}
			AddWall(root, new Vector3(0f, -wallThickness * 0.5f, 0f), new Vector3(roomSize.x + wallThickness * 2f, wallThickness, roomSize.y + wallThickness * 2f));
			AddWall(root, new Vector3(0f, ceilingHeight + wallThickness * 0.5f, 0f), new Vector3(roomSize.x + wallThickness * 2f, wallThickness, roomSize.y + wallThickness * 2f));
			// 벽과 천장이 시야를 막으면 카메라를 방 안으로 당김 (CinemachineComponentDeoccluder)
			root.AddComponent<GroundSurface>();
			return root.transform;
		}

		private static void AddWall(GameObject root, Vector3 center, Vector3 size)
		{
			BoxCollider box = root.AddComponent<BoxCollider>();
			box.center = center;
			box.size = size;
		}

		// ---- 오가기

		private void GoToRoom(int room) => Move(() => playerProvider.CurrentObject.UnitMovement.Teleport(transform.TransformPoint(RoomOffset(room) + door + arrival)));

		// 마을로. 박물관 문 앞에 섬
		private void Leave()
		{
			manager.Save();
			Move(() =>
			{
				StageManager.Instance.LoadStage(SOHelper.Get<WorldStage>(VillageStage.STAGE_ID));
				FindVillage().PlaceAtDoor(HomesteadSim.MUSEUM, villageStep);
			});
		}

		// 마을 스테이지는 방금 켜진 하나뿐
		private static VillageStage FindVillage() => StageManager.Instance.CurStageObject.GetComponent<VillageStage>();

		private void Move(Action during)
		{
			if (isMoving)
				return;
			isMoving = true;
			uiManager.Transition.Transition(aDuringTransition: () =>
			{
				during();
				isMoving = false;
			}).Forget();
		}

		// ---- 전시

		private void BuildExhibits(Transform root, Wing setting, List<GoodsDef> goods)
		{
			List<(Vector3 Position, float Yaw)> places = Places(setting.Spacing, goods.Count);
			for (int i = 0; i < goods.Count; i++)
			{
				(Vector3 position, float yaw) = places[i];
				GameObject stand = Models.Build(STAND_KEY + setting.Id, root, position, yaw);
				Exhibit exhibit = new() { Goods = goods[i], Item = MakeItem(stand.transform, setting, goods[i]) };
				exhibits.Add(exhibit);
				Vector3 front = position + Quaternion.Euler(0f, yaw, 0f) * Vector3.back * standFront;
				HomesteadSpot.Create(root, front, "Exhibit", () => NameOf(exhibit.Goods), () => RoleOf(exhibit.Goods), () => Use(exhibit), () => CanDonate(exhibit.Goods) ? "기증" : "보기");
			}
		}

		// 왼 벽, 북쪽 벽, 오른 벽을 따라 놓고 남으면 가운데에 등을 맞댄 두 줄
		private List<(Vector3 Position, float Yaw)> Places(float spacing, int count)
		{
			List<(Vector3, float)> places = new();
			float halfWidth = roomSize.x * 0.5f - standInset;
			float halfDepth = roomSize.y * 0.5f - standInset;
			int sides = Mathf.FloorToInt((roomSize.y - standCorner.x * 2f) / spacing) + 1;
			int back = Mathf.FloorToInt((roomSize.x - standCorner.y * 2f) / spacing) + 1;
			float Along(int index, int total) => (index - (total - 1) * 0.5f) * spacing;
			for (int i = 0; i < sides; i++)
				places.Add((new Vector3(-halfWidth, 0f, Along(i, sides)), 270f));
			for (int i = 0; i < back; i++)
				places.Add((new Vector3(Along(i, back), 0f, halfDepth), 0f));
			for (int i = sides - 1; i >= 0; i--)
				places.Add((new Vector3(halfWidth, 0f, Along(i, sides)), 90f));
			int rest = count - places.Count;
			int row = Mathf.CeilToInt(rest * 0.5f);
			for (int i = 0; i < rest; i++)
			{
				bool isFront = i < row;
				places.Add((new Vector3(Along(i % row, row), 0f, isFront ? -islandDepth : islandDepth), isFront ? 0f : 180f));
			}
			return places;
		}

		// 작물은 밭의 모델, 고기는 수조에서 헤엄, 나머지는 받침 위 그림
		private GameObject MakeItem(Transform stand, Wing setting, GoodsDef goods)
		{
			Vector3 place = Vector3.up * setting.ItemHeight;
			CropDef crop = Catalog.Crops.Find(entry => entry.Harvest == goods.Id);
			if (crop != null)
				return Models.Build(CROP_KEY + crop.Id + "_c", stand, place, 0f, cropScale);
			if (goods.Kind == GoodsKind.Fish)
				return VillageSwimmer.Create(stand, place, Art.IconOf(goods), setting.ItemSize).gameObject;
			return HomesteadSprite.Create(stand, place, Art.IconOf(goods), setting.ItemSize, itemBob).gameObject;
		}

		private bool CanDonate(GoodsDef goods) => Sim.IsDonated(goods.Id) == false && Sim.Count(goods.Id) > 0;

		private string NameOf(GoodsDef goods) => Sim.IsDonated(goods.Id) || Sim.IsKnown(goods.Id) || Sim.Count(goods.Id) > 0 ? goods.Name : "빈 자리";

		private string RoleOf(GoodsDef goods)
		{
			if (Sim.IsDonated(goods.Id))
				return Record(goods) ?? "살펴보기";
			return CanDonate(goods) ? "기증하기" : "아직 비어 있음";
		}

		private string Record(GoodsDef goods) =>
			goods.Kind == GoodsKind.Fish && Sim.State.FishRecords.TryGetValue(goods.Id, out FishRecord record) ? $"가장 큰 것 {record.BestSize}cm" : null;

		private WingDef WingOf(GoodsDef goods) => Catalog.Wings.Find(wing => wing.Kinds.Contains(goods.Kind));

		private void Use(Exhibit exhibit)
		{
			GoodsDef goods = exhibit.Goods;
			WingDef wing = WingOf(goods);
			if (Sim.IsDonated(goods.Id))
			{
				manager.Hud.Notice(goods.Name, Record(goods) ?? wing.Note, Art.IconOf(goods));
				return;
			}
			if (CanDonate(goods) == false)
			{
				Say("아직 놓을 것이 없습니다.");
				return;
			}
			HomesteadResult result = Sim.Donate(goods.Id);
			if (result.Ok == false)
			{
				UiSfx.Play(UiSfxEvent.Blocked);
				Say(HomesteadText.Reason(result, Catalog));
				return;
			}
			UiSfx.Play(UiSfxEvent.Chest);
			Refresh();
			StartCoroutine(Pop(exhibit.Item.transform));
			manager.Hud.Notice(goods.Name, $"{wing.Name} {Sim.DonatedCount(wing)} / {Sim.ExhibitsOf(wing).Count}", Art.IconOf(goods));
			if (Sim.HasClaimableMilestone(wing))
				Say("안내대에서 받을 것이 생겼습니다.");
			manager.Save();
		}

		private void Say(string text) => uiManager.SpeechBubble.Show(playerProvider.Current.transform, text);

		private void Refresh()
		{
			foreach (Exhibit exhibit in exhibits)
				exhibit.Item.SetActive(Sim.IsDonated(exhibit.Goods.Id));
		}

		// 놓인 순간 한 번 부풂. 겹쳐도 처음 크기로 돌아옴
		private IEnumerator Pop(Transform target)
		{
			if (popBases.TryGetValue(target, out Vector3 scale) == false)
			{
				scale = target.localScale;
				popBases[target] = scale;
			}
			for (float time = 0f; time < popSeconds && target != null; time += Time.deltaTime)
			{
				target.localScale = scale * (1f + Mathf.Sin(time / popSeconds * Mathf.PI) * popGrow);
				yield return null;
			}
			if (target != null)
				target.localScale = scale;
			popBases.Remove(target);
		}
	}
}
