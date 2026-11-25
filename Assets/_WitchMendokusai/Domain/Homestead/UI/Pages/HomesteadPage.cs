using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 화면 내용들이 같이 쓰는 조각. 값 줄, 보상 글, 조작 묶음
	public abstract class HomesteadPage : IHomesteadPage
	{
		protected HomesteadManager Manager { get; }
		protected HomesteadSim Sim => Manager.Sim;
		protected HomesteadCatalog Catalog => Manager.Sim.Catalog;
		protected static HomesteadArt Art => HomesteadArt.Instance;

		protected HomesteadPage(HomesteadManager manager) => Manager = manager;

		public abstract string Title { get; }
		protected abstract string IconKey { get; }
		public Sprite Icon => Art.Icon(IconKey);
		public abstract IReadOnlyList<string> Tabs { get; }
		public virtual bool IsGrid(int tab) => false;
		public abstract List<HomesteadEntry> Entries(int tab);
		public virtual string Status => string.Empty;
		public virtual string EmptyText(int tab) => string.Empty;

		protected void AddCost(HomesteadDetail detail, CostDef cost)
		{
			foreach (KeyValuePair<string, int> pair in cost.Goods)
				AddGoods(detail, pair.Key, pair.Value);
			if (cost.Nyang > 0)
				detail.Costs.Add(new HomesteadCostLine { Icon = Art.Icon("nyang"), Name = "냥", Have = Sim.Purse.Nyang, Need = cost.Nyang });
		}

		protected void AddGoods(HomesteadDetail detail, string goodsId, int need)
		{
			GoodsDef goods = Catalog.Get<GoodsDef>(goodsId);
			detail.Costs.Add(new HomesteadCostLine { Icon = Art.IconOf(goods), Name = goods.Name, Have = Sim.Count(goodsId), Need = need });
		}

		protected static void AddAction(HomesteadDetail detail, string label, HomesteadResult can, Func<HomesteadResult> run, UiSfxEvent sound = UiSfxEvent.Confirm) =>
			detail.Actions.Add(new HomesteadAction { Label = label, Can = can, Run = run, Sound = sound });

		protected static HomesteadResult Done(Action action)
		{
			action();
			return HomesteadResult.Success;
		}
	}
}
