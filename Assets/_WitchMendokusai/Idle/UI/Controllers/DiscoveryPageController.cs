using System.Collections.Generic;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Discovery;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	/// <summary>
	/// 인형 도감. 열렸나는 판정 층 등록소 (DiscoveryUnlocks, 출처 IdleHeroDiscovery) 에 묻고, 채운 정도는 DiscoveryProgress
	/// 본편 도감과 같은 조각. 화면만 다름 (자리와 조작이 달라서)
	/// </summary>
	public sealed class DiscoveryPageController
	{
		private readonly UIContentSO content;
		private readonly VisualTreeAsset rowAsset;
		private readonly Label summary;
		private readonly VisualElement rows;
		private readonly List<Label> labels = new List<Label>();

		public DiscoveryPageController(VisualElement page, VisualTreeAsset rowAsset, UIContentSO content)
		{
			this.rowAsset = rowAsset;
			this.content = content;
			summary = page.RequireQ<Label>("discovery-label");
			rows = page.RequireQ<VisualElement>("discovery-rows");
		}

		public void Render(IdleSnapshot snapshot)
		{
			DiscoveryProgress progress = new DiscoveryProgress(IdleHeroes.Count, snapshot.Heroes.Length);
			summary.text = content.DiscoverySummaryText(
				snapshot.DiscoveryScore, snapshot.DiscoveryMultiplier, progress.Unlocked, progress.Total);
			EnsureRows();

			for (int heroId = 0; heroId < labels.Count; heroId++)
			{
				IdleHeroKind kind = IdleHeroes.KindOf(heroId);
				bool held = TryFindHero(snapshot, heroId, out IdleHeroView hero);
				bool owned = held && DiscoveryUnlocks.IsUnlocked(IdleHeroDiscovery.CATALOG_ID, IdleHeroDiscovery.EntryIdOf(heroId));
				labels[heroId].text = owned
					? content.DiscoveryHeroText(kind.Name, content.StarsText(hero.Stars),
						content.GradeName(kind.Grade), content.AxisName(kind.Axis))
					: content.DiscoveryHiddenHeroText(content.GradeName(kind.Grade));
				labels[heroId].EnableInClassList("idle-row-title--dim", owned == false);
			}
		}

		private void EnsureRows()
		{
			if (labels.Count == IdleHeroes.Count)
			{
				return;
			}

			rows.Clear();
			labels.Clear();
			for (int heroId = 0; heroId < IdleHeroes.Count; heroId++)
			{
				TemplateContainer tree = rowAsset.Instantiate();
				Label row = tree.RequireQ<Label>("row");
				row.RemoveFromHierarchy();
				rows.Add(row);
				labels.Add(row);
			}
		}

		private static bool TryFindHero(IdleSnapshot snapshot, int heroId, out IdleHeroView hero)
		{
			for (int index = 0; index < snapshot.Heroes.Length; index++)
			{
				if (snapshot.Heroes[index].Id == heroId)
				{
					hero = snapshot.Heroes[index];
					return true;
				}
			}

			hero = default;
			return false;
		}
	}
}
