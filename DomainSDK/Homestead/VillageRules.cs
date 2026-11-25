using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 마을 칸 규칙. 1m 칸, 네 방향. 좌표는 차지한 네모의 작은 쪽 칸
	// BuildingFootprint 의 기준 칸 (-X, +Z 로 퍼짐) 은 여기 네모의 (X + 폭 - 1, Z)
	public static class VillageRules
	{
		public const int TURNS = 4;

		public static int WidthOf(PlaceableDef placeable, int turn) => turn % 2 == 0 ? placeable.Width : placeable.Depth;

		public static int DepthOf(PlaceableDef placeable, int turn) => turn % 2 == 0 ? placeable.Depth : placeable.Width;

		public static bool IsInside(VillageAreaDef area, PlaceableDef placeable, int x, int z, int turn) =>
			x >= area.X && z >= area.Z && x + WidthOf(placeable, turn) <= area.X + area.Width && z + DepthOf(placeable, turn) <= area.Z + area.Depth;

		// 길 위의 꾸밈만 겹칠 수 있음
		public static bool CanShare(PlaceableKind first, PlaceableKind second) =>
			(first == PlaceableKind.Path && second == PlaceableKind.Decor) || (first == PlaceableKind.Decor && second == PlaceableKind.Path);

		public static bool Collides(PlaceableDef firstDef, PlacedPiece first, PlaceableDef secondDef, PlacedPiece second)
		{
			if (CanShare(firstDef.Kind, secondDef.Kind))
			{
				return false;
			}
			return first.X < second.X + WidthOf(secondDef, second.Turn) && second.X < first.X + WidthOf(firstDef, first.Turn)
				&& first.Z < second.Z + DepthOf(secondDef, second.Turn) && second.Z < first.Z + DepthOf(firstDef, first.Turn);
		}

		public static bool Covers(PlaceableDef placeable, PlacedPiece piece, int x, int z) =>
			x >= piece.X && z >= piece.Z && x < piece.X + WidthOf(placeable, piece.Turn) && z < piece.Z + DepthOf(placeable, piece.Turn);

		// 처음 배치의 이은 줄을 낱개로. Uid 는 비움
		public static List<PlacedPiece> Expand(HomesteadCatalog catalog)
		{
			List<PlacedPiece> pieces = new List<PlacedPiece>();
			foreach (LayoutDef line in catalog.Layout)
			{
				for (int i = 0; i < line.CountX; i++)
				{
					for (int j = 0; j < line.CountZ; j++)
					{
						pieces.Add(new PlacedPiece { Id = line.Id, X = line.X + i, Z = line.Z + j, Turn = line.Turn });
					}
				}
			}
			return pieces;
		}
	}
}
