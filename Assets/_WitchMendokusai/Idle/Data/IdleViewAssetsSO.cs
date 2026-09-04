using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.Idle
{
	[CreateAssetMenu(fileName = "IdleViewAssets", menuName = "WM/Idle/View Assets")]
	public sealed class IdleViewAssetsSO : ScriptableObject
	{
		[SerializeField] private VisualTreeAsset screen;
		[SerializeField] private VisualTreeAsset bagCell;
		[SerializeField] private VisualTreeAsset forgeKind;
		[SerializeField] private VisualTreeAsset card;
		[SerializeField] private VisualTreeAsset queueChip;
		[SerializeField] private VisualTreeAsset choiceCard;
		[SerializeField] private VisualTreeAsset waveDot;
		[SerializeField] private VisualTreeAsset producerRow;
		[SerializeField] private VisualTreeAsset rowButton;
		[SerializeField] private VisualTreeAsset rowLabel;

		public VisualTreeAsset Screen => screen;
		public VisualTreeAsset BagCell => bagCell;
		public VisualTreeAsset ForgeKind => forgeKind;
		public VisualTreeAsset Card => card;
		public VisualTreeAsset QueueChip => queueChip;
		public VisualTreeAsset ChoiceCard => choiceCard;
		public VisualTreeAsset WaveDot => waveDot;
		public VisualTreeAsset ProducerRow => producerRow;
		public VisualTreeAsset RowButton => rowButton;
		public VisualTreeAsset RowLabel => rowLabel;

		public bool TryValidate(out string error)
		{
			if (screen == null) { error = nameof(screen); return false; }
			if (bagCell == null) { error = nameof(bagCell); return false; }
			if (forgeKind == null) { error = nameof(forgeKind); return false; }
			if (card == null) { error = nameof(card); return false; }
			if (queueChip == null) { error = nameof(queueChip); return false; }
			if (choiceCard == null) { error = nameof(choiceCard); return false; }
			if (waveDot == null) { error = nameof(waveDot); return false; }
			if (producerRow == null) { error = nameof(producerRow); return false; }
			if (rowButton == null) { error = nameof(rowButton); return false; }
			if (rowLabel == null) { error = nameof(rowLabel); return false; }

			error = string.Empty;
			return true;
		}
	}
}
