using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 양봉. 벌통은 마을 꾸밈으로 놓고, 차면 손으로 거둠
	// - 벌은 죽거나 떠나지 않고 쏘지 않음. 다 차면 멈추고 기다림 (버려지는 것 없음)
	// - 꿀 종류는 거둘 때 벌통 둘레 (HiveReach 칸) 의 꽃을 셈. 꽃밭 꾸밈과 밭 칸의 작물
	public sealed partial class HomesteadSim
	{
		public const string HIVE = "beehive";
		public const string BEESWAX = "beeswax";

		// 밭 칸 index 마다 마을 칸. 밭은 마을 배치 밖이라 화면이 알려 줌
		private readonly List<(int X, int Z)> plotCells = new List<(int X, int Z)>();

		public void SetPlotCells(IEnumerable<(int X, int Z)> cells)
		{
			plotCells.Clear();
			plotCells.AddRange(cells);
		}

		public List<PlacedPiece> Hives() => State.Village.FindAll(piece => piece.Id == HIVE);

		private HomesteadResult CanMakeHive(string id) =>
			id == HIVE && Hives().Count >= Tuning.HiveLimit ? HomesteadResult.Fail(HomesteadFailure.NoRoom, HIVE) : HomesteadResult.Success;

		// 놓은 때부터 참. 옮겨도 그대로, 거두어 들였다 다시 놓으면 처음부터
		private void OnPiecePut(PlacedPiece piece)
		{
			if (piece.Id == HIVE)
			{
				State.Hives[piece.Uid] = State.Minutes;
			}
		}

		public int HiveMinutesLeft(PlacedPiece hive) => (int)Math.Max(0L, State.Hives[hive.Uid] + Tuning.HiveFillMinutes - State.Minutes);

		public bool IsHiveFull(PlacedPiece hive) => HiveMinutesLeft(hive) == 0;

		// 둘레의 꽃 수가 제일 많은 꿀. 같으면 자료에서 뒤의 것 (드문 꽃). 꽃이 없으면 기본 꿀
		public HoneyDef HoneyOf(PlacedPiece hive)
		{
			HoneyDef best = null;
			int bestCount = 0;
			foreach (HoneyDef honey in Catalog.Honeys)
			{
				if (honey.Flowers.Count == 0)
				{
					if (best == null)
					{
						best = honey;
					}
					continue;
				}
				int count = FlowersNear(hive, honey);
				if (count > 0 && count >= bestCount)
				{
					best = honey;
					bestCount = count;
				}
			}
			return best;
		}

		public int FlowersNear(PlacedPiece hive, HoneyDef honey)
		{
			int count = 0;
			foreach (PlacedPiece piece in State.Village)
			{
				if (honey.Flowers.Contains(piece.Id) && IsNear(hive, piece))
				{
					count++;
				}
			}
			for (int i = 0; i < Math.Min(plotCells.Count, State.Plots.Count); i++)
			{
				string crop = State.Plots[i].Crop;
				if (crop.Length > 0 && honey.Flowers.Contains(crop) && IsNear(hive, plotCells[i].X, plotCells[i].Z, 1, 1))
				{
					count++;
				}
			}
			return count;
		}

		private bool IsNear(PlacedPiece hive, PlacedPiece piece)
		{
			PlaceableDef placeable = PlaceableOf(piece);
			return IsNear(hive, piece.X, piece.Z, VillageRules.WidthOf(placeable, piece.Turn), VillageRules.DepthOf(placeable, piece.Turn));
		}

		// 벌통 칸에서 상자 (x, z, 너비, 깊이) 의 가장 가까운 칸까지 체비쇼프 거리
		private bool IsNear(PlacedPiece hive, int x, int z, int width, int depth)
		{
			int dx = Math.Max(0, Math.Max(x - hive.X, hive.X - (x + width - 1)));
			int dz = Math.Max(0, Math.Max(z - hive.Z, hive.Z - (z + depth - 1)));
			return Math.Max(dx, dz) <= Tuning.HiveReach;
		}

		public HomesteadResult TakeHoney(int uid, out HoneyDef honey)
		{
			honey = null;
			PlacedPiece hive = State.Village.Find(piece => piece.Uid == uid);
			if (hive == null || hive.Id != HIVE)
			{
				return HomesteadResult.Fail(HomesteadFailure.Unknown);
			}
			if (IsHiveFull(hive) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotReady);
			}
			honey = HoneyOf(hive);
			Give(honey.Goods, WithRest(Tuning.HiveHoney));
			Give(BEESWAX, Tuning.HiveWax);
			State.Hives[uid] = State.Minutes;
			NotifyChanged();
			return HomesteadResult.Success;
		}
	}
}
