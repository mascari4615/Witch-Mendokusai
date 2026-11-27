using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 마을 배치. 건물은 옮기고 돌리기만, 꾸밈과 길은 놓고 거둠
	public sealed partial class HomesteadSim
	{
		public const string HALL = "townhall";
		// 마을에 처음 놓은 꾸밈과 길 종류. 가구의 첫 놓기와 같은 길드 경험치
		private const string FIRST_VILLAGE_PLACE = "village:";

		private readonly HashSet<(int X, int Z)> villageBlocked = new HashSet<(int X, int Z)>();

		// 배치가 바뀜. 몸과 바닥을 다시 맞출 때
		public event Action LayoutChanged = delegate { };

		// 물, 밭, 채집 자리처럼 규칙 밖에서 막힌 칸
		public void SetVillageBlocked(IEnumerable<(int X, int Z)> cells)
		{
			villageBlocked.Clear();
			villageBlocked.UnionWith(cells);
		}

		public PlacedPiece PieceOf(string id) => State.Village.Find(piece => piece.Id == id);

		public PlaceableDef PlaceableOf(PlacedPiece piece) => Catalog.Get<PlaceableDef>(piece.Id);

		// 위층부터. 길 위의 꾸밈을 먼저
		public PlacedPiece PieceAt(int x, int z)
		{
			PlacedPiece found = null;
			foreach (PlacedPiece piece in State.Village)
			{
				PlaceableDef placeable = PlaceableOf(piece);
				if (VillageRules.Covers(placeable, piece, x, z) && (found == null || placeable.Kind != PlaceableKind.Path))
				{
					found = piece;
				}
			}
			return found;
		}

		// 처음이면 정해진 배치 그대로. 뒤에 건물이 늘면 빠진 것만 빈자리에
		private void EnsureLayout()
		{
			bool isNew = State.Village.Count == 0;
			foreach (PlacedPiece piece in VillageRules.Expand(Catalog))
			{
				bool isMissing = PlaceableOf(piece).Kind == PlaceableKind.Building && PieceOf(piece.Id) == null;
				if (isNew || (isMissing && TryFindSpot(piece)))
				{
					piece.Uid = State.NextVillageUid++;
					State.Village.Add(piece);
				}
			}
		}

		private bool TryFindSpot(PlacedPiece piece)
		{
			VillageAreaDef area = Catalog.VillageArea;
			for (int ring = 0; ring <= Math.Max(area.Width, area.Depth); ring++)
			{
				for (int i = -ring; i <= ring; i++)
				{
					for (int j = -ring; j <= ring; j++)
					{
						if (Math.Max(Math.Abs(i), Math.Abs(j)) == ring && CanPut(piece.Id, piece.X + i, piece.Z + j, piece.Turn).Ok)
						{
							piece.X += i;
							piece.Z += j;
							return true;
						}
					}
				}
			}
			return false;
		}

		public HomesteadResult CanPut(string id, int x, int z, int turn, int ignoreUid = 0)
		{
			PlaceableDef placeable = Catalog.Get<PlaceableDef>(id);
			if (VillageRules.IsInside(Catalog.VillageArea, placeable, x, z, turn) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NoRoom);
			}
			PlacedPiece wanted = new PlacedPiece { Id = id, X = x, Z = z, Turn = turn };
			for (int i = 0; i < VillageRules.WidthOf(placeable, turn); i++)
			{
				for (int j = 0; j < VillageRules.DepthOf(placeable, turn); j++)
				{
					if (villageBlocked.Contains((x + i, z + j)))
					{
						return HomesteadResult.Fail(HomesteadFailure.NoRoom);
					}
				}
			}
			foreach (PlacedPiece piece in State.Village)
			{
				if (piece.Uid != ignoreUid && VillageRules.Collides(placeable, wanted, PlaceableOf(piece), piece))
				{
					return HomesteadResult.Fail(HomesteadFailure.Blocked, piece.Id);
				}
			}
			return HomesteadResult.Success;
		}

		public HomesteadResult CanMakeDecor(string id)
		{
			PlaceableDef placeable = Catalog.Get<PlaceableDef>(id);
			if (placeable.Kind == PlaceableKind.Building)
			{
				return HomesteadResult.Fail(HomesteadFailure.AlreadyDone);
			}
			return State.TownHall < placeable.TownHall ? TownHallTooLow(placeable.TownHall) : CanPay(placeable.Cost);
		}

		public HomesteadResult Put(string id, int x, int z, int turn, out PlacedPiece placed)
		{
			placed = null;
			HomesteadResult result = CanMakeDecor(id);
			if (result.Ok)
			{
				result = CanPut(id, x, z, turn);
			}
			if (result.Ok == false)
			{
				return result;
			}
			Pay(Catalog.Get<PlaceableDef>(id).Cost);
			placed = new PlacedPiece { Uid = State.NextVillageUid++, Id = id, X = x, Z = z, Turn = turn };
			State.Village.Add(placed);
			string firstKey = FIRST_VILLAGE_PLACE + id;
			if (State.Seen.Contains(firstKey) == false)
			{
				State.Seen.Add(firstKey);
				AddGuildExp(Tuning.FirstPlaceGuildExp);
			}
			NotifyLayout();
			return HomesteadResult.Success;
		}

		public HomesteadResult MovePiece(int uid, int x, int z, int turn)
		{
			PlacedPiece piece = State.Village.Find(candidate => candidate.Uid == uid);
			if (piece == null)
			{
				return HomesteadResult.Fail(HomesteadFailure.Unknown);
			}
			if (PlaceableOf(piece).Fixed)
			{
				return HomesteadResult.Fail(HomesteadFailure.Locked, piece.Id);
			}
			HomesteadResult result = CanPut(piece.Id, x, z, turn, uid);
			if (result.Ok == false)
			{
				return result;
			}
			piece.X = x;
			piece.Z = z;
			piece.Turn = turn;
			NotifyLayout();
			return HomesteadResult.Success;
		}

		// 거두면 든 재료를 그대로 돌려받음. 다시 꾸미는 데 벌이 없음
		public HomesteadResult RemovePiece(int uid)
		{
			PlacedPiece piece = State.Village.Find(candidate => candidate.Uid == uid);
			if (piece == null)
			{
				return HomesteadResult.Fail(HomesteadFailure.Unknown);
			}
			PlaceableDef placeable = PlaceableOf(piece);
			if (placeable.Kind == PlaceableKind.Building)
			{
				return HomesteadResult.Fail(HomesteadFailure.Locked, piece.Id);
			}
			State.Village.Remove(piece);
			Purse.Nyang += placeable.Cost.Nyang;
			foreach (KeyValuePair<string, int> pair in placeable.Cost.Goods)
			{
				Give(pair.Key, pair.Value);
			}
			NotifyLayout();
			return HomesteadResult.Success;
		}

		private void NotifyLayout()
		{
			LayoutChanged();
			NotifyChanged();
		}
	}
}
