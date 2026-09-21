using System.Collections.Generic;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Discovery;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	/// <summary>
	/// 인형 도감. 열렸나는 판정 층 등록소 (DiscoveryUnlocks, 출처 IdleDollDiscovery) 에 묻고, 채운 정도는 DiscoveryProgress
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
			DiscoveryProgress progress = new DiscoveryProgress(IdleDolls.Count, snapshot.Dolls.Length);
			summary.text = content.DiscoverySummaryText(
				snapshot.DiscoveryScore, snapshot.DiscoveryMultiplier, progress.Unlocked, progress.Total);
			EnsureRows();

			for (int dollId = 0; dollId < labels.Count; dollId++)
			{
				IdleDollKind kind = IdleDolls.KindOf(dollId);
				bool held = TryFindDoll(snapshot, dollId, out IdleDollView doll);
				bool owned = held && DiscoveryUnlocks.IsUnlocked(IdleDollDiscovery.CATALOG_ID, IdleDollDiscovery.EntryIdOf(dollId));
				labels[dollId].text = owned
					? content.DiscoveryDollText(kind.Name, content.StarsText(doll.Stars),
						content.GradeName(kind.Grade), content.AxisName(kind.Axis))
					: content.DiscoveryHiddenDollText(content.GradeName(kind.Grade));
				labels[dollId].EnableInClassList("idle-row-title--dim", owned == false);
			}
		}

		private void EnsureRows()
		{
			if (labels.Count == IdleDolls.Count)
			{
				return;
			}

			rows.Clear();
			labels.Clear();
			for (int dollId = 0; dollId < IdleDolls.Count; dollId++)
			{
				TemplateContainer tree = rowAsset.Instantiate();
				Label row = tree.RequireQ<Label>("row");
				row.RemoveFromHierarchy();
				rows.Add(row);
				labels.Add(row);
			}
		}

		private static bool TryFindDoll(IdleSnapshot snapshot, int dollId, out IdleDollView doll)
		{
			for (int index = 0; index < snapshot.Dolls.Length; index++)
			{
				if (snapshot.Dolls[index].Id == dollId)
				{
					doll = snapshot.Dolls[index];
					return true;
				}
			}

			doll = default;
			return false;
		}
	}
}
