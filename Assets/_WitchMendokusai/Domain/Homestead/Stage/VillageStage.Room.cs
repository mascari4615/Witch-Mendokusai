using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 집 안과 박물관 뜰. 놓은 가구와 기증한 것이 그대로 보임
	public sealed partial class VillageStage
	{
		private const string FURNITURE_KEY = "F_";

		[Header("Room")]
		[SerializeField] private float roomHeight = 2.8f;
		[SerializeField] private Color glowColor = new(1f, 0.82f, 0.6f);
		[SerializeField] private Color shadeColor = new(0.35f, 0.3f, 0.42f);
		[SerializeField] private float roomLightIntensity = 3.2f;
		[SerializeField] private float furnitureGlowRange = 3.5f;
		[SerializeField] private float furnitureGlowIntensity = 1.6f;

		[Header("Museum garden")]
		[SerializeField] private Vector3 gardenOrigin = new(6.5f, 0f, 16.5f);
		[SerializeField] private int gardenColumns = 15;
		[SerializeField] private Vector2 gardenGap = new(1f, 1f);

		private Transform room;
		private GameObject roomShell;
		private int shownRoomTier;
		private Transform furnitureRoot;
		private string shownFurniture = string.Empty;
		private readonly List<(string GoodsId, HomesteadSprite Sprite)> exhibits = new();
		private GameObject garden;
		private Transform doorSpot;
		private Transform deskSpot;
		private Transform restSpot;
		private int shownComfortRank;

		private void BuildRoom()
		{
			room = new GameObject("Room").transform;
			room.SetParent(transform, false);
			room.localPosition = roomOffset;
			BuildShade(room, 13f, 8f, 1.4f);
			furnitureRoot = new GameObject("Furniture").transform;
			furnitureRoot.SetParent(room, false);
			doorSpot = HomesteadSpot.Create(room, Vector3.zero, "DoorSpot", () => "문", () => "나가기", LeaveRoom).transform;
			deskSpot = HomesteadSpot.Create(room, Vector3.zero, "DeskSpot", () => "꾸밈 상자", () => "방 꾸미기", () => manager.Panels.Show(HomesteadPanelType.Room)).transform;
			restSpot = HomesteadSpot.Create(room, Vector3.zero, "RestSpot", () => "쉬는 자리", () => manager.CanRest ? $"아침까지, 아늑함 {shownComfortRank}단" : "오늘은 이미 쉼", Rest).transform;
		}

		private void RefreshRoom()
		{
			RefreshRoomShell();
			RefreshFurniture();
		}

		private void RefreshRoomShell()
		{
			if (shownRoomTier == Sim.State.HouseTier)
				return;
			shownRoomTier = Sim.State.HouseTier;
			if (roomShell != null)
				Destroy(roomShell);
			float width = Sim.HouseTier.Width;
			float depth = Sim.HouseTier.Depth;
			roomShell = new GameObject("RoomShell");
			roomShell.transform.SetParent(room, false);
			BuildShell(roomShell.transform, Mathf.RoundToInt(width), Mathf.RoundToInt(depth));
			GameObject lightObject = new("RoomLight");
			lightObject.transform.SetParent(roomShell.transform, false);
			lightObject.transform.localPosition = new Vector3(0f, roomHeight - 0.4f, 0f);
			Light lamp = lightObject.AddComponent<Light>();
			lamp.type = LightType.Point;
			lamp.range = Mathf.Max(width, depth) * 1.2f;
			lamp.intensity = roomLightIntensity;
			lamp.color = new Color(1f, 0.9f, 0.74f);
			doorSpot.localPosition = new Vector3(0f, 0f, -depth / 2f - 0.4f);
			deskSpot.localPosition = new Vector3(width / 2f - 0.6f, 0f, -depth / 2f + 0.7f);
			restSpot.localPosition = new Vector3(-width / 2f + 0.7f, 0f, -depth / 2f + 0.7f);
		}

		// 바닥과 벽을 1m 조각으로. 뒤와 옆 벽만 높고 앞은 낮은 턱이라 내려다보는 카메라가 방을 봄
		private void BuildShell(Transform shell, int width, int depth)
		{
			float halfWidth = width * 0.5f;
			float halfDepth = depth * 0.5f;
			for (int x = 0; x < width; x++)
			{
				float along = x - halfWidth + 0.5f;
				for (int z = 0; z < depth; z++)
					Models.Build((x + z) % 2 == 0 ? "RoomFloor" : "RoomFloorB", shell, new Vector3(along, 0f, z - halfDepth + 0.5f), 0f);
				Models.Build(WallPiece(x), shell, new Vector3(along, 0f, halfDepth), 0f);
				if (Mathf.Abs(along) > 1f)
					Models.Build("RoomSill", shell, new Vector3(along, 0f, -halfDepth), 0f);
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
				AddSolid(shell, new Vector3(side * (halfWidth + 1f) * 0.5f, 1.4f, -halfDepth - 0.15f), new Vector3(halfWidth - 1f, 3f, 0.3f));
			}
			AddSolid(shell, new Vector3(0f, 1.4f, halfDepth + 0.15f), new Vector3(width, 3f, 0.3f));
			AddSolid(shell, new Vector3(0f, 1.4f, -halfDepth - 1.2f), new Vector3(width + 1f, 3f, 0.3f));
			AddSolid(shell, new Vector3(0f, -0.6f, 0f), new Vector3(width + 4f, 1.2f, depth + 4f));
			Models.Build("RoomFloor", shell, new Vector3(-0.5f, 0f, -halfDepth - 0.5f), 0f);
			Models.Build("RoomFloorB", shell, new Vector3(0.5f, 0f, -halfDepth - 0.5f), 0f);
			Models.Build("RoomChest", shell, new Vector3(halfWidth - 0.5f, 0f, -halfDepth + 0.4f), 0f);
		}

		private static string WallPiece(int index) => index % 2 == 0 ? "RoomWallPost" : "RoomWall";

		private static void AddSolid(Transform parent, Vector3 center, Vector3 size)
		{
			BoxCollider box = parent.gameObject.AddComponent<BoxCollider>();
			box.center = center;
			box.size = size;
		}

		private void RefreshFurniture()
		{
			System.Text.StringBuilder key = new();
			foreach (PlacedFurniture piece in Sim.State.Placed)
				key.Append(piece.Uid).Append(':').Append(piece.X).Append(',').Append(piece.Z).Append(',').Append(piece.Yaw).Append(';');
			string wanted = key.ToString();
			if (wanted == shownFurniture)
				return;
			shownFurniture = wanted;
			shownComfortRank = Sim.ComfortRank;
			for (int i = furnitureRoot.childCount - 1; i >= 0; i--)
				Destroy(furnitureRoot.GetChild(i).gameObject);
			foreach (PlacedFurniture piece in Sim.State.Placed)
			{
				FurnitureDef furniture = Catalog.Get<FurnitureDef>(piece.Id);
				GameObject body = Models.Build(FURNITURE_KEY + furniture.Id, furnitureRoot, new Vector3(piece.X, 0f, piece.Z), piece.Yaw);
				// 쉬는 자리는 침대가 있으면 침대 곁
				if (furniture.Shape == "bed")
					restSpot.localPosition = new Vector3(piece.X, 0f, piece.Z) + Quaternion.Euler(0f, piece.Yaw, 0f) * new Vector3(furniture.Width * 0.5f + 0.4f, 0f, 0f);
				if (furniture.Shape == "lamp" || furniture.Shape == "fireplace" || furniture.Shape == "candle")
					AddGlow(body.transform, new Vector3(0f, 1f, -0.3f), furnitureGlowRange, furnitureGlowIntensity);
			}
		}

		private Light AddGlow(Transform body, Vector3 localPosition, float range, float intensity)
		{
			GameObject lightObject = new("Glow");
			lightObject.transform.SetParent(body, false);
			lightObject.transform.localPosition = localPosition;
			Light lamp = lightObject.AddComponent<Light>();
			lamp.type = LightType.Point;
			lamp.range = range;
			lamp.intensity = intensity;
			lamp.color = glowColor;
			return lamp;
		}

		// 바깥 하늘과 해를 가림. 어두운 바닥과 둘레 벽, 해만 막는 지붕
		private void BuildShade(Transform root, float radius, float height, float depth)
		{
			VillageKit dark = new();
			dark.Prism(Art.Dark, shadeColor, new Vector3(0f, -depth, 0f), new Vector3(radius * 6f, 0.3f, radius * 6f), 24);
			VillageKit roof = new();
			roof.Prism(Art.Dark, Color.white, new Vector3(0f, height, 0f), new Vector3(radius * 2.4f, 0.4f, radius * 2.4f), 16);
			const int COUNT = 20;
			for (int i = 0; i < COUNT; i++)
			{
				float angle = i / (float)COUNT * Mathf.PI * 2f;
				Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
				Vector3 euler = new(0f, 90f - angle * Mathf.Rad2Deg, 0f);
				float far = radius * 1.6f;
				// 카메라가 서는 남쪽은 비움
				if (direction.z > -0.5f)
					dark.Box(Art.Dark, shadeColor, direction * far - Vector3.up * depth, new Vector3(far * 0.36f, height * 2.6f, 0.4f), euler);
				roof.Box(Art.Dark, Color.white, direction * radius * 1.15f - Vector3.up * depth, new Vector3(radius * 0.4f, height + depth, 0.4f), euler);
			}
			dark.Build("Dark", root, Vector3.zero, 0f, castShadow: false);
			foreach (MeshRenderer part in roof.Build("Shade", root, Vector3.zero, 0f).GetComponentsInChildren<MeshRenderer>())
				part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
		}

		private void EnterRoom() => MovePlayer(roomOffset + new Vector3(0f, 0.1f, -Sim.HouseTier.Depth / 2f + 0.6f));

		private void LeaveRoom() => MovePlayer(housePosition + Quaternion.Euler(0f, houseYaw, 0f) * (houseDoor + new Vector3(0f, 0f, -0.8f)) + Vector3.up * 0.1f);

		private void Rest()
		{
			if (manager.CanRest == false)
			{
				UiSfx.Play(UiSfxEvent.Blocked);
				Say("오늘은 이미 쉬었습니다.");
				return;
			}
			MovePlayer(roomOffset + restSpot.localPosition + Vector3.up * 0.1f, () =>
			{
				manager.RestUntilMorning();
				manager.Hud.Notice("잘 쉬었습니다", $"하루 동안 두 배로 얻을 확률 {Mathf.RoundToInt(Sim.State.RestedRank * Catalog.Tuning.RestDoubleChancePerRank * 100f)}%", Art.Icon("house"));
				manager.Hud.ShowDigest(Sim.MakeDigest());
			});
		}

		// ---- 박물관 뜰. 빈 받침대가 남은 것을 보임

		private void BuildMuseumGarden()
		{
			List<string> order = new();
			foreach (WingDef wing in Catalog.Wings)
			{
				foreach (GoodsDef goods in Sim.ExhibitsOf(wing))
					order.Add(goods.Id);
			}
			garden = new GameObject("MuseumGarden");
			garden.transform.SetParent(outdoor, false);
			garden.transform.localPosition = gardenOrigin;
			for (int i = 0; i < order.Count; i++)
			{
				Vector3 position = new(i % gardenColumns * gardenGap.x, 0f, -(i / gardenColumns) * gardenGap.y);
				Models.Build(i % 2 == 0 ? "StonePath" : "StonePathB", garden.transform, position, 0f);
				Models.Build("Pedestal", garden.transform, position, 0f);
				HomesteadSprite sprite = HomesteadSprite.Create(garden.transform, position + Vector3.up * 0.52f, Art.IconOf(Catalog.Get<GoodsDef>(order[i])), 0.5f, 0.04f);
				exhibits.Add((order[i], sprite));
			}
		}

		private void RefreshMuseumGarden()
		{
			garden.SetActive(Sim.IsBuilt(HomesteadSim.MUSEUM));
			foreach ((string goodsId, HomesteadSprite sprite) in exhibits)
				sprite.gameObject.SetActive(Sim.IsDonated(goodsId));
			RefreshWaterSpots();
		}
	}
}
