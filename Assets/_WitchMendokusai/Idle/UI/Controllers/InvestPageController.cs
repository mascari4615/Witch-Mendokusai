using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;
using BigNumberText = WitchMendokusai.Numerics.BigNumberText;

namespace WitchMendokusai.Idle.UI
{
	public sealed class InvestPageController
	{
		private readonly IdleSession session;
		private readonly UIContentSO content;
		private readonly VisualTreeAsset rowAsset;
		private readonly Action requestRender;
		private readonly Label summary;
		private readonly VisualElement host;
		private readonly List<Button> buttons = new List<Button>();

		public InvestPageController(VisualElement page, VisualTreeAsset rowAsset,
			IdleSession session, UIContentSO content, Action requestRender)
		{
			this.rowAsset = rowAsset;
			this.session = session;
			this.content = content;
			this.requestRender = requestRender;
			summary = page.Q<Label>("base-summary");
			host = page.Q<VisualElement>("producers");
		}

		public void Render(IdleSnapshot snapshot)
		{
			EnsureRows(snapshot.Producers.Length);
			summary.text = content.ProducerSummaryText(BigNumberText.Format(snapshot.IncomePerSecond));
			for (int kind = 0; kind < buttons.Count; kind++)
			{
				IdleProducerView view = snapshot.Producers[kind];
				buttons[kind].style.display = view.Hidden ? DisplayStyle.None : DisplayStyle.Flex;
				buttons[kind].text = content.ProducerRowText(
					kind + 1, view.Owned, BigNumberText.Format(view.OutputTotal), BigNumberText.Format(view.NextCost));
				buttons[kind].SetEnabled(view.CanAfford);
			}
		}

		private void EnsureRows(int count)
		{
			if (buttons.Count == count)
			{
				return;
			}

			host.Clear();
			buttons.Clear();
			for (int kind = 0; kind < count; kind++)
			{
				int captured = kind;
				TemplateContainer tree = rowAsset.Instantiate();
				Button row = tree.Q<Button>("row");
				row.RemoveFromHierarchy();
				row.clicked += () => Buy(captured);
				host.Add(row);
				buttons.Add(row);
			}
		}

		private void Buy(int kind)
		{
			session.Send(new IdleBuyProducerIntent(kind));
			requestRender();
		}
	}
}
