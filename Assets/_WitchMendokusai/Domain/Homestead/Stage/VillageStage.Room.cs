using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 집 안과 박물관 뜰. 놓은 가구와 기증한 것이 그대로 보임
	public sealed partial class VillageStage
	{
		private const float WALL_THICKNESS = 0.3f;

		[Header("Room")]
		[SerializeField] private float roomHeight = 2.8f;
		[SerializeField] private Color glowColor = new(1f, 0.82f, 0.6f);
		[SerializeField] private Color shadeColor = new(0.35f, 0.3f, 0.42f);
		[SerializeField] private float roomLightIntensity = 3.2f;
		[SerializeField] private float furnitureGlowRange = 3.5f;
		[SerializeField] private float furnitureGlowIntensity = 1.6f;

		[Header("Museum garden")]
		[SerializeField] private Vector3 gardenOrigin = new(5.6f, 0f, 18.2f);
		[SerializeField] private int gardenColumns = 15;
		[SerializeField] private Vector2 gardenGap = new(1.05f, 1.25f);

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
			restSpot = HomesteadSpot.Create(room, Vector3.zero, "RestSpot", () => "쉬는 자리", () => $"아침까지, 아늑함 {Sim.ComfortRank}단", Rest).transform;
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
			VillageKit kit = new();
			int boards = Mathf.CeilToInt(width / 0.5f);
			for (int i = 0; i < boards; i++)
				kit.Box(Art.Wood, i % 2 == 0 ? Color.white : new Color(0.92f, 0.9f, 0.88f), new Vector3(-width / 2f + (i + 0.5f) * width / boards, -0.2f, 0f), new Vector3(width / boards, 0.2f, depth));
			// 뒤와 옆 벽만 높고 앞은 낮은 턱. 내려다보는 카메라가 방을 봄
			kit.Box(Art.Plaster, Color.white, new Vector3(0f, -0.2f, depth / 2f + WALL_THICKNESS / 2f), new Vector3(width + WALL_THICKNESS * 2f, roomHeight, WALL_THICKNESS), default, true);
			for (int side = -1; side <= 1; side += 2)
			{
				kit.Box(Art.Plaster, new Color(0.97f, 0.95f, 0.97f), new Vector3(side * (width / 2f + WALL_THICKNESS / 2f), -0.2f, 0f), new Vector3(WALL_THICKNESS, roomHeight, depth), default, true);
				kit.Box(Art.Frame, Color.white, new Vector3(side * (width / 2f), -0.2f, depth / 2f), new Vector3(0.24f, roomHeight, 0.24f));
				kit.Box(Art.Frame, Color.white, new Vector3(side * (width / 2f - 0.02f), 0f, 0f), new Vector3(0.06f, 0.22f, depth));
			}
			kit.Box(Art.Frame, Color.white, new Vector3(0f, 0f, depth / 2f - 0.02f), new Vector3(width, 0.22f, 0.06f));
			kit.Box(Art.Frame, Color.white, new Vector3(0f, roomHeight - 0.4f, depth / 2f - 0.02f), new Vector3(width, 0.18f, 0.1f));
			float half = width / 2f - 0.7f;
			for (int side = -1; side <= 1; side += 2)
				kit.Box(Art.Wood, Color.white, new Vector3(side * (0.7f + half / 2f), -0.2f, -depth / 2f - WALL_THICKNESS / 2f), new Vector3(half, 0.4f, WALL_THICKNESS));
			kit.Solid(new Vector3(0f, 0f, -depth / 2f - 1.2f), new Vector3(width + 1f, 3f, 0.3f));
			for (int side = -1; side <= 1; side += 2)
				kit.Solid(new Vector3(side * (0.7f + half / 2f), 0f, -depth / 2f - WALL_THICKNESS / 2f), new Vector3(half, 3f, WALL_THICKNESS));
			kit.Box(Art.Stone, Color.white, new Vector3(0f, -0.2f, -depth / 2f - 0.6f), new Vector3(1.4f, 0.2f, 1.2f));
			kit.Solid(new Vector3(0f, -1.2f, 0f), new Vector3(width + 4f, 1.2f, depth + 4f));
			// 꾸밈 상자는 문 옆 구석
			kit.Box(Art.Wood, new Color(0.8f, 0.68f, 0.9f), new Vector3(width / 2f - 0.5f, 0f, -depth / 2f + 0.4f), new Vector3(0.6f, 0.42f, 0.4f));
			kit.Box(Art.Gold, Color.white, new Vector3(width / 2f - 0.5f, 0.3f, -depth / 2f + 0.18f), new Vector3(0.12f, 0.12f, 0.06f));
			roomShell = kit.Build("RoomShell", room, Vector3.zero, 0f);
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

		private void RefreshFurniture()
		{
			System.Text.StringBuilder key = new();
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
				FurnitureDef furniture = Catalog.Get<FurnitureDef>(piece.Id);
				VillageKit kit = FurnitureBodies.Make(furniture);
				if (furniture.Layer == FurnitureLayer.Floor)
					kit.Solid(Vector3.zero, new Vector3(furniture.Width * 0.9f, 1f, furniture.Depth * 0.9f));
				GameObject body = kit.Build(furniture.Name, furnitureRoot, new Vector3(piece.X, 0f, piece.Z), piece.Yaw);
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
			VillageKit stands = new();
			List<string> order = new();
			foreach (WingDef wing in Catalog.Wings)
			{
				foreach (GoodsDef goods in Sim.ExhibitsOf(wing))
					order.Add(goods.Id);
			}
			garden = new GameObject("MuseumGarden");
			garden.transform.SetParent(outdoor, false);
			garden.transform.localPosition = gardenOrigin;
			VillageKit paving = new();
			int rows = Mathf.CeilToInt(order.Count / (float)gardenColumns);
			paving.Box(Art.Stone, Color.white, new Vector3((gardenColumns - 1) * gardenGap.x * 0.5f, -0.18f, -(rows - 1) * gardenGap.y * 0.5f), new Vector3(gardenColumns * gardenGap.x + 0.8f, 0.22f, rows * gardenGap.y + 0.6f));
			paving.Build("Paving", garden.transform, Vector3.zero, 0f);
			for (int i = 0; i < order.Count; i++)
			{
				Vector3 position = new(i % gardenColumns * gardenGap.x, 0.04f, -(i / gardenColumns) * gardenGap.y);
				stands.Prism(Art.Stone, Color.white, position, new Vector3(0.6f, 0.1f, 0.6f), 8);
				stands.Prism(Art.Plaster, Color.white, position + Vector3.up * 0.08f, new Vector3(0.38f, 0.36f, 0.38f), 8, 0.9f);
				stands.Prism(Art.Gold, Color.white, position + Vector3.up * 0.42f, new Vector3(0.48f, 0.06f, 0.48f), 8);
				HomesteadSprite sprite = HomesteadSprite.Create(garden.transform, position + Vector3.up * 0.52f, Art.IconOf(Catalog.Get<GoodsDef>(order[i])), 0.5f, 0.04f);
				exhibits.Add((order[i], sprite));
			}
			stands.Build("Stands", garden.transform, Vector3.zero, 0f);
			Vector3 center = gardenOrigin + new Vector3((gardenColumns - 1) * gardenGap.x * 0.5f, 0f, -(rows - 1) * gardenGap.y * 0.5f);
			GameObject blocker = new("GardenBlock");
			blocker.transform.SetParent(outdoor, false);
			blocker.transform.localPosition = center + Vector3.up * 0.5f;
			blocker.AddComponent<BoxCollider>().size = new Vector3(gardenColumns * gardenGap.x, 1f, rows * gardenGap.y);
			blocker.transform.SetParent(garden.transform, true);
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
