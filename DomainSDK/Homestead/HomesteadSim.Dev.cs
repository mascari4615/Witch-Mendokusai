namespace WitchMendokusai.DomainSDK.Homestead
{
	// 개발 확인용 지름길. 규칙을 건너뛰므로 놀이 코드에서 부르지 않음
	public sealed partial class HomesteadSim
	{
		public void DevFill(int amount, int nyang)
		{
			foreach (GoodsDef goods in Catalog.Goods)
			{
				Give(goods.Id, amount);
			}
			State.DirtyRelics += amount;
			Purse.Nyang += nyang;
			NotifyChanged();
		}

		public void DevBuildAll()
		{
			State.Works.Clear();
			State.TownHall = Catalog.TownHalls.Count;
			foreach (BuildingDef building in Catalog.Buildings)
			{
				State.Buildings[building.Id] = building.Levels.Count;
			}
			EnsurePlots();
			CheckRecruits();
			NotifyChanged();
		}

		public void DevAddGuildExp(int exp)
		{
			AddGuildExp(exp);
			NotifyChanged();
		}

		public void DevGiveFurniture()
		{
			foreach (FurnitureDef furniture in Catalog.Furniture)
			{
				AddFurniture(furniture.Id, 1);
			}
			foreach (CostumeDef costume in Catalog.Costumes)
			{
				AddCostume(costume.Id);
			}
			NotifyChanged();
		}
	}
}
