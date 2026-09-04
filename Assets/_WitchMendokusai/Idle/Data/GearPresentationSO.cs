using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle
{
	[CreateAssetMenu(fileName = "IdleGearPresentation", menuName = "WM/Idle/Gear Presentation")]
	public sealed class GearPresentationSO : ScriptableObject
	{
		[SerializeField] private Sprite[] tierSprites = new Sprite[0];
		[SerializeField] private Color[] tierColors = new Color[0];
		[SerializeField, Min(0f)] private float tierBorderWidth = 4f;

		public float TierBorderWidth => tierBorderWidth;

		public Sprite SpriteOf(int slot, int tier)
		{
			int index = (tier - 1) * IdleGear.SLOT_COUNT + slot;
			return index >= 0 && index < tierSprites.Length ? tierSprites[index] : null;
		}

		public Color ColorOf(int tier)
		{
			int index = tier - 1;
			return index >= 0 && index < tierColors.Length ? tierColors[index] : Color.clear;
		}

		public bool TryValidate(out string error)
		{
			if (tierColors.Length == 0 || tierSprites.Length != tierColors.Length * IdleGear.SLOT_COUNT)
			{
				error = "tierSprites must contain one sprite per tier and gear slot";
				return false;
			}

			for (int index = 0; index < tierSprites.Length; index++)
			{
				if (tierSprites[index] == null)
				{
					error = "tierSprites contains an empty entry at " + index;
					return false;
				}
			}

			error = string.Empty;
			return true;
		}
	}
}
