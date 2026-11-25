using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 방 안 배치의 기하와 아늑함 점수. 상태 없음
	// 방은 가운데가 원점, 문은 z 가 작은 쪽. 벽 가구는 네 벽에 등을 붙임
	public static class RoomRules
	{
		// 맞닿은 두 가구가 소수점 오차로 겹침이 되지 않게
		private const float TOUCH_SLACK = 0.01f;

		public static bool IsInside(FurnitureDef furniture, HouseTierDef room, float x, float z, float yaw)
		{
			Extent(furniture, yaw, out float halfX, out float halfZ);
			return Math.Abs(x) + halfX <= room.Width / 2f + TOUCH_SLACK && Math.Abs(z) + halfZ <= room.Depth / 2f + TOUCH_SLACK;
		}

		// 돌린 직사각형의 축 정렬 반폭
		public static void Extent(FurnitureDef furniture, float yaw, out float halfX, out float halfZ)
		{
			double radians = yaw * Math.PI / 180.0;
			float cosine = (float)Math.Abs(Math.Cos(radians));
			float sine = (float)Math.Abs(Math.Sin(radians));
			halfX = (furniture.Width * cosine + furniture.Depth * sine) / 2f;
			halfZ = (furniture.Width * sine + furniture.Depth * cosine) / 2f;
		}

		// 벽 가구를 가장 가까운 벽에 붙임. yaw 는 방 안쪽을 보는 각
		public static void SnapToWall(FurnitureDef furniture, HouseTierDef room, ref float x, ref float z, out float yaw)
		{
			float toEast = room.Width / 2f - x;
			float toWest = x + room.Width / 2f;
			float toNorth = room.Depth / 2f - z;
			float toSouth = z + room.Depth / 2f;
			float nearest = Math.Min(Math.Min(toEast, toWest), Math.Min(toNorth, toSouth));
			float halfDepth = furniture.Depth / 2f;
			float limitX = room.Width / 2f - furniture.Width / 2f;
			float limitZ = room.Depth / 2f - furniture.Width / 2f;
			if (nearest == toNorth)
			{
				yaw = 180f;
				z = room.Depth / 2f - halfDepth;
				x = Clamp(x, -limitX, limitX);
			}
			else if (nearest == toSouth)
			{
				yaw = 0f;
				z = -room.Depth / 2f + halfDepth;
				x = Clamp(x, -limitX, limitX);
			}
			else if (nearest == toEast)
			{
				yaw = 270f;
				x = room.Width / 2f - halfDepth;
				z = Clamp(z, -limitZ, limitZ);
			}
			else
			{
				yaw = 90f;
				x = -room.Width / 2f + halfDepth;
				z = Clamp(z, -limitZ, limitZ);
			}
		}

		private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));

		// 분리축. 축 넷 중 하나라도 떨어져 있으면 안 겹침
		public static bool Overlaps(FurnitureDef first, float firstX, float firstZ, float firstYaw, FurnitureDef second, float secondX, float secondZ, float secondYaw)
		{
			return IsSeparated(first, firstX, firstZ, firstYaw, second, secondX, secondZ, secondYaw) == false
				&& IsSeparated(second, secondX, secondZ, secondYaw, first, firstX, firstZ, firstYaw) == false;
		}

		private static bool IsSeparated(FurnitureDef owner, float ownerX, float ownerZ, float ownerYaw, FurnitureDef other, float otherX, float otherZ, float otherYaw)
		{
			double radians = ownerYaw * Math.PI / 180.0;
			float axisX = (float)Math.Cos(radians);
			float axisZ = (float)-Math.Sin(radians);
			float deltaX = otherX - ownerX;
			float deltaZ = otherZ - ownerZ;

			double relative = (otherYaw - ownerYaw) * Math.PI / 180.0;
			float cosine = (float)Math.Abs(Math.Cos(relative));
			float sine = (float)Math.Abs(Math.Sin(relative));

			float alongWidth = Math.Abs(deltaX * axisX + deltaZ * axisZ);
			float otherWidth = (other.Width * cosine + other.Depth * sine) / 2f;
			if (alongWidth >= owner.Width / 2f + otherWidth - TOUCH_SLACK)
			{
				return true;
			}
			float alongDepth = Math.Abs(-deltaX * axisZ + deltaZ * axisX);
			float otherDepth = (other.Width * sine + other.Depth * cosine) / 2f;
			return alongDepth >= owner.Depth / 2f + otherDepth - TOUCH_SLACK;
		}

		public static int Comfort(HomesteadCatalog catalog, List<PlacedFurniture> placed)
		{
			Dictionary<string, int> counts = new Dictionary<string, int>();
			Dictionary<string, HashSet<string>> themes = new Dictionary<string, HashSet<string>>();
			float total = 0f;
			foreach (PlacedFurniture piece in placed)
			{
				FurnitureDef furniture = catalog.Get<FurnitureDef>(piece.Id);
				int seen = counts.TryGetValue(piece.Id, out int count) ? count : 0;
				total += furniture.Comfort * (float)Math.Pow(catalog.Tuning.DuplicateFalloff, seen);
				counts[piece.Id] = seen + 1;
				if (themes.TryGetValue(furniture.Theme, out HashSet<string> kinds) == false)
				{
					kinds = new HashSet<string>();
					themes.Add(furniture.Theme, kinds);
				}
				kinds.Add(piece.Id);
			}
			foreach (KeyValuePair<string, HashSet<string>> pair in themes)
			{
				total += pair.Value.Count / catalog.Tuning.ThemePieces * catalog.Tuning.ThemeBonus;
			}
			return (int)Math.Round(total);
		}
	}
}
