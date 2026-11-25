using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 박물관 뜰. 기증한 것이 그대로 보임. 욘의 집은 마을에 없음 (HomeRoom)
	public sealed partial class VillageStage
	{
		private const int EXHIBIT_ROWS = 5;

		[Header("Glow")]
		[SerializeField] private Color glowColor = new(1f, 0.82f, 0.6f);
		[SerializeField] private Color shadeColor = new(0.35f, 0.3f, 0.42f);

		[Header("Museum garden")]
		[SerializeField] private Vector3 exhibitOrigin = new(-8.5f, 0f, 25.5f);
		[SerializeField] private int exhibitColumns = 15;
		[SerializeField] private Vector2 exhibitGap = new(1f, 1f);

		private readonly List<(string GoodsId, HomesteadSprite Sprite)> exhibits = new();
		private GameObject garden;

		private Light AddGlow(Transform body, Vector3 localPosition, float range, float intensity) =>
			VillageLights.AddGlow(body, localPosition, range, intensity, glowColor);

		private void BuildShade(Transform root, float radius, float height, float depth) =>
			VillageLights.BuildShade(root, radius, height, depth, shadeColor);

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
			garden.transform.localPosition = exhibitOrigin;
			for (int i = 0; i < order.Count; i++)
			{
				Vector3 position = new(i % exhibitColumns * exhibitGap.x, 0f, -(i / exhibitColumns) * exhibitGap.y);
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
