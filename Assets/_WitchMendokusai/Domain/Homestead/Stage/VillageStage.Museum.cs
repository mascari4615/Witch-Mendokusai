using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 박물관 안. 안내 홀에서 네 전시실로. 빈 받침 앞에서 바로 기증하면 그 자리에 놓임
	// 갱도처럼 같은 스테이지의 먼 자리. 벽은 안쪽 면만 있어 어느 쪽에서 봐도 앞벽이 가리지 않음
	public sealed partial class VillageStage
	{
		private const string ROOM_KEY = "Museum_";
		private const string STAND_KEY = "Stand_";
		private const string HALL_ROOM = "hall";

		[Serializable]
		public struct MuseumWing
		{
			public string Id;
			[Tooltip("홀에서 이 전시실로 가는 문 앞 (홀 기준)")] public Vector3 Door;
			[Tooltip("받침 사이 (m)")] public float Spacing;
			[Tooltip("받침 위 놓이는 높이")] public float ItemHeight;
			public Color Light;
		}

		[Header("Glow")]
		[SerializeField] private Color glowColor = new(1f, 0.82f, 0.6f);
		[SerializeField] private Color shadeColor = new(0.35f, 0.3f, 0.42f);

		[Header("Museum")]
		[SerializeField] private Vector3 museumOffset = new(300f, 0f, 0f);
		[SerializeField] private float museumRoomGap = 90f;
		[SerializeField] private Vector2 museumSize = new(13f, 10f);
		[SerializeField] private float museumDome = 34f;
		[SerializeField, Range(0f, 1f)] private float museumShade = 0.3f;
		[SerializeField] private Vector3 museumDoor = new(0f, 0f, -4.3f);
		[SerializeField] private Vector3 museumDesk = new(0f, 0f, 1.7f);
		[SerializeField] private float standInset = 0.95f;
		[Tooltip("벽을 따라 놓을 때 모서리에서 비우는 길이 (좌우 벽, 북쪽 벽)")]
		[SerializeField] private Vector2 standCorner = new(1.9f, 1.3f);
		[SerializeField] private float islandDepth = 0.5f;
		[SerializeField] private float exhibitSize = 0.5f;
		[SerializeField] private float exhibitCropScale = 0.8f;
		[SerializeField] private float exhibitBob = 0.03f;
		[SerializeField] private Vector2 museumLight = new(13f, 4.2f);
		[SerializeField] private float museumLightHeight = 3.2f;
		[SerializeField] private Color hallLight = new(1f, 0.88f, 0.7f);
		[SerializeField] private MuseumWing[] museumWings =
		{
			new() { Id = "plants", Door = new Vector3(-5.5f, 0f, 0.4f), Spacing = 1.6f, ItemHeight = 0.36f, Light = new Color(1f, 0.95f, 0.76f) },
			new() { Id = "waters", Door = new Vector3(5.5f, 0f, 0.4f), Spacing = 2f, ItemHeight = 0.92f, Light = new Color(0.62f, 0.86f, 1f) },
			new() { Id = "stones", Door = new Vector3(-3.4f, 0f, 4f), Spacing = 2f, ItemHeight = 0.62f, Light = new Color(1f, 0.84f, 0.62f) },
			new() { Id = "relics", Door = new Vector3(3.4f, 0f, 4f), Spacing = 1.75f, ItemHeight = 0.88f, Light = new Color(1f, 0.8f, 0.72f) },
		};

		private sealed class ExhibitView
		{
			public GoodsDef Goods;
			public GameObject Item;
		}

		private readonly List<ExhibitView> exhibits = new();
		private readonly List<GameObject> museumRooms = new();

		private Light AddGlow(Transform body, Vector3 localPosition, float range, float intensity) =>
			VillageLights.AddGlow(body, localPosition, range, intensity, glowColor);

		private Vector3 RoomOffset(int index) => museumOffset + Vector3.right * (museumRoomGap * index);

		// 방 0 은 홀, 그 뒤는 museumWings 순서
		private void BuildMuseum()
		{
			Transform hall = BuildMuseumRoom(HALL_ROOM, 0, hallLight);
			HomesteadSpot.Create(hall, museumDoor, "MuseumExit", () => "박물관", () => "나가기", LeaveMuseum);
			HomesteadSpot.Create(hall, museumDesk, "MuseumDesk", () => "안내대", MuseumDeskRole, () => manager.Panels.Show(HomesteadPanelType.Museum));
			for (int i = 0; i < museumWings.Length; i++)
			{
				int room = i + 1;
				MuseumWing setting = museumWings[i];
				WingDef wing = Catalog.Get<WingDef>(setting.Id);
				Transform root = BuildMuseumRoom(setting.Id, room, setting.Light);
				HomesteadSpot.Create(hall, setting.Door, "WingDoor", () => $"{wing.Name} 전시실", () => $"{Sim.DonatedCount(wing)} / {Sim.ExhibitsOf(wing).Count}", () => EnterMuseumRoom(room));
				HomesteadSpot.Create(root, museumDoor, "WingExit", () => "안내 홀", () => "돌아가기", () => EnterMuseumRoom(0));
				BuildExhibits(root, setting, Sim.ExhibitsOf(wing));
			}
		}

		private string MuseumDeskRole()
		{
			foreach (WingDef wing in Catalog.Wings)
			{
				if (Sim.HasClaimableMilestone(wing))
					return "받을 보상이 있음";
			}
			return Sim.State.DirtyRelics > 0 ? $"손질할 유물 {Sim.State.DirtyRelics}" : "기증과 손질";
		}

		private Transform BuildMuseumRoom(string key, int index, Color light)
		{
			GameObject root = new("Museum " + key);
			root.transform.SetParent(transform, false);
			root.transform.localPosition = RoomOffset(index);
			museumRooms.Add(root);
			VillageLights.BuildDome(root.transform, museumDome, MINE_ROOF, shadeColor * museumShade);
			Models.Build(ROOM_KEY + key, root.transform, Vector3.zero, 0f);
			for (int side = -1; side <= 1; side += 2)
			{
				VillageLights.AddGlow(root.transform, new Vector3(side * museumSize.x * 0.27f, museumLightHeight, 0f), museumLight.x, museumLight.y, light);
				AddRoomWall(root, new Vector3(side * (museumSize.x * 0.5f + 0.5f), 2f, 0f), new Vector3(1f, 4f, museumSize.y + 2f));
				AddRoomWall(root, new Vector3(0f, 2f, side * (museumSize.y * 0.5f + 0.5f)), new Vector3(museumSize.x + 2f, 4f, 1f));
			}
			AddRoomWall(root, new Vector3(0f, -0.5f, 0f), new Vector3(museumSize.x + 2f, 1f, museumSize.y + 2f));
			return root.transform;
		}

		private static void AddRoomWall(GameObject root, Vector3 center, Vector3 size)
		{
			BoxCollider box = root.AddComponent<BoxCollider>();
			box.center = center;
			box.size = size;
		}

		private void EnterMuseum() => EnterMuseumRoom(0);

		private void EnterMuseumRoom(int room) => MovePlayer(RoomOffset(room) + museumDoor + new Vector3(0f, 0.1f, 0.9f));

		private void LeaveMuseum()
		{
			MovePlayer(DoorPoint(HomesteadSim.MUSEUM, 0.8f) + Vector3.up * 0.1f);
			manager.Save();
		}

		// ---- 전시

		private void BuildExhibits(Transform root, MuseumWing setting, List<GoodsDef> goods)
		{
			List<(Vector3 Position, float Yaw)> places = ExhibitPlaces(setting.Spacing, goods.Count);
			for (int i = 0; i < goods.Count; i++)
			{
				(Vector3 position, float yaw) = places[i];
				GameObject stand = Models.Build(STAND_KEY + setting.Id, root, position, yaw);
				ExhibitView view = new() { Goods = goods[i], Item = MakeExhibit(stand.transform, setting, goods[i]) };
				exhibits.Add(view);
				Vector3 front = position + Quaternion.Euler(0f, yaw, 0f) * Vector3.back * 0.5f;
				HomesteadSpot.Create(root, front, "Exhibit", () => ExhibitName(view.Goods), () => ExhibitRole(view.Goods), () => UseExhibit(view));
			}
		}

		// 왼 벽, 북쪽 벽, 오른 벽을 따라 놓고 남으면 가운데에 등을 맞댄 두 줄
		private List<(Vector3 Position, float Yaw)> ExhibitPlaces(float spacing, int count)
		{
			List<(Vector3, float)> places = new();
			float halfWidth = museumSize.x * 0.5f - standInset;
			float halfDepth = museumSize.y * 0.5f - standInset;
			float sideLength = museumSize.y - standCorner.x * 2f;
			float backLength = museumSize.x - standCorner.y * 2f;
			int sides = Mathf.FloorToInt(sideLength / spacing) + 1;
			int back = Mathf.FloorToInt(backLength / spacing) + 1;
			float Along(int index, int total, float step) => (index - (total - 1) * 0.5f) * step;
			for (int i = 0; i < sides; i++)
				places.Add((new Vector3(-halfWidth, 0f, Along(i, sides, spacing)), 270f));
			for (int i = 0; i < back; i++)
				places.Add((new Vector3(Along(i, back, spacing), 0f, halfDepth), 0f));
			for (int i = sides - 1; i >= 0; i--)
				places.Add((new Vector3(halfWidth, 0f, Along(i, sides, spacing)), 90f));
			int rest = count - places.Count;
			int row = Mathf.CeilToInt(rest * 0.5f);
			for (int i = 0; i < rest; i++)
			{
				bool isFront = i < row;
				places.Add((new Vector3(Along(i % row, row, spacing), 0f, isFront ? -islandDepth : islandDepth), isFront ? 0f : 180f));
			}
			return places;
		}

		// 작물은 밭의 모델, 고기는 수조에서 헤엄, 나머지는 받침 위 그림
		private GameObject MakeExhibit(Transform stand, MuseumWing setting, GoodsDef goods)
		{
			Vector3 place = Vector3.up * setting.ItemHeight;
			CropDef crop = Catalog.Crops.Find(entry => entry.Harvest == goods.Id);
			if (crop != null)
				return Models.Build(CROP_KEY + crop.Id + "_c", stand, place, 0f, exhibitCropScale);
			if (goods.Kind == GoodsKind.Fish)
				return VillageSwimmer.Create(stand, place, Art.IconOf(goods), exhibitSize).gameObject;
			return HomesteadSprite.Create(stand, place, Art.IconOf(goods), exhibitSize, exhibitBob).gameObject;
		}

		private bool CanDonateHere(GoodsDef goods) => Sim.IsDonated(goods.Id) == false && Sim.Count(goods.Id) > 0;

		private string ExhibitName(GoodsDef goods) => Sim.IsDonated(goods.Id) || Sim.IsKnown(goods.Id) || Sim.Count(goods.Id) > 0 ? goods.Name : "빈 자리";

		private string ExhibitRole(GoodsDef goods)
		{
			if (Sim.IsDonated(goods.Id))
				return RecordOf(goods);
			return CanDonateHere(goods) ? "기증하기" : "아직 비어 있음";
		}

		private string RecordOf(GoodsDef goods) =>
			goods.Kind == GoodsKind.Fish && Sim.State.FishRecords.TryGetValue(goods.Id, out FishRecord record) ? $"가장 큰 것 {record.BestSize}cm" : "살펴보기";

		private void UseExhibit(ExhibitView view)
		{
			if (Sim.IsDonated(view.Goods.Id))
			{
				manager.Hud.Notice(view.Goods.Name, RecordOf(view.Goods) == "살펴보기" ? WingOf(view.Goods).Note : RecordOf(view.Goods), Art.IconOf(view.Goods));
				return;
			}
			if (CanDonateHere(view.Goods) == false)
			{
				Say("아직 놓을 것이 없습니다.");
				return;
			}
			if (Report(Sim.Donate(view.Goods.Id)) == false)
				return;
			WingDef wing = WingOf(view.Goods);
			UiSfx.Play(UiSfxEvent.Chest);
			RefreshMuseum();
			StartCoroutine(Pop(view.Item.transform));
			manager.Hud.Notice(view.Goods.Name, $"{wing.Name} {Sim.DonatedCount(wing)} / {Sim.ExhibitsOf(wing).Count}", Art.IconOf(view.Goods));
			if (Sim.HasClaimableMilestone(wing))
				Say("안내대에서 받을 것이 생겼습니다.");
			manager.Save();
		}

		private WingDef WingOf(GoodsDef goods) => Catalog.Wings.Find(wing => wing.Kinds.Contains(goods.Kind));

		private void RefreshMuseum()
		{
			foreach (ExhibitView view in exhibits)
				view.Item.SetActive(Sim.IsDonated(view.Goods.Id));
			RefreshWaterSpots();
		}
	}

	// 수조 안을 오가는 고기 그림. 가는 쪽을 봄
	public sealed class VillageSwimmer : MonoBehaviour
	{
		[SerializeField] private float reach = 0.42f;
		[SerializeField] private Vector2 seconds = new(5f, 9f);
		[SerializeField] private float rise = 0.1f;

		private HomesteadSprite sprite;
		private float period;
		private float phase;

		public static VillageSwimmer Create(Transform parent, Vector3 localPosition, Sprite icon, float height)
		{
			GameObject root = new("Swimmer");
			root.transform.SetParent(parent, false);
			root.transform.localPosition = localPosition;
			VillageSwimmer swimmer = root.AddComponent<VillageSwimmer>();
			swimmer.sprite = HomesteadSprite.Create(root.transform, Vector3.zero, icon, height);
			swimmer.phase = Mathf.Repeat(parent.position.x * 0.37f + parent.position.z * 0.61f, 1f);
			swimmer.period = Mathf.Lerp(swimmer.seconds.x, swimmer.seconds.y, swimmer.phase);
			return swimmer;
		}

		private void Update()
		{
			float turn = (Time.time / period + phase) * Mathf.PI * 2f;
			sprite.transform.localPosition = new Vector3(Mathf.Sin(turn) * reach, Mathf.Sin(turn * 1.7f) * rise, 0f);
			Camera view = Camera.main;
			if (view == null)
				return;
			// 아이콘의 고기는 왼쪽을 봄
			Vector3 heading = transform.right * Mathf.Cos(turn);
			sprite.Renderer.flipX = Vector3.Dot(heading, view.transform.right) > 0f;
		}
	}
}
