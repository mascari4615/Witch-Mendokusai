using System.Collections.Generic;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	public sealed class CodexPageController
	{
		private readonly UIContentSO content;
		private readonly VisualTreeAsset rowAsset;
		private readonly Label summary;
		private readonly VisualElement rows;
		private readonly List<Label> labels = new List<Label>();

		public CodexPageController(VisualElement page, VisualTreeAsset rowAsset, UIContentSO content)
		{
			this.rowAsset = rowAsset;
			this.content = content;
			summary = page.Q<Label>("codex-label");
			rows = page.Q<VisualElement>("codex-rows");
		}

		public void Render(IdleSnapshot snapshot)
		{
			summary.text = content.CodexSummaryText(
				snapshot.CodexScore, snapshot.CodexMultiplier, snapshot.Heroes.Length, IdleHeroes.Count);
			EnsureRows();

			for (int heroId = 0; heroId < labels.Count; heroId++)
			{
				IdleHeroKind kind = IdleHeroes.KindOf(heroId);
				bool owned = TryFindHero(snapshot, heroId, out IdleHeroView hero);
				labels[heroId].text = owned
					? content.CodexHeroText(kind.Name, content.StarsText(hero.Stars),
						content.GradeName(kind.Grade), content.AxisName(kind.Axis))
					: content.CodexHiddenHeroText(content.GradeName(kind.Grade));
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
				Label row = tree.Q<Label>("row");
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
