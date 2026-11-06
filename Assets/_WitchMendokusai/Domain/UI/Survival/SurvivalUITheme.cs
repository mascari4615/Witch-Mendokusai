using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum SurvivalUIKind
	{
		Count = 0,
		Damage = 1,
		Speed = 2,
		Size = 3,
		Advanced = 4
	}

	public enum UpgradeCategory
	{
		Attack = 0,
		Survival = 1,
		MoveAndPickup = 2,
		Reward = 3
	}

	[CreateAssetMenu(fileName = nameof(SurvivalUITheme), menuName = "WM/UI/SurvivalUITheme")]
	public class SurvivalUITheme : ScriptableObject
	{
		[Serializable]
		public class KindStyle
		{
			public SurvivalUIKind Kind;
			public Color Main;
			public Color Dark;
			public Sprite Tile;
		}

		[Serializable]
		public class CategoryStyle
		{
			public UpgradeCategory Category;
			public string DisplayName;
			public Color Main;
		}

		[field: SerializeField] public List<KindStyle> Kinds { get; private set; } = new();
		[field: SerializeField] public List<CategoryStyle> Categories { get; private set; } = new();
		[field: SerializeField] public Color[] DifficultyColors { get; private set; } = new Color[3];
		[field: SerializeField] public Color SelectedLine { get; private set; } = Color.white;
		[field: SerializeField] public Color IdleLine { get; private set; } = Color.gray;
		[field: SerializeField] public Color ValueColor { get; private set; } = Color.yellow;
		[field: SerializeField] public Color MaxColor { get; private set; } = Color.yellow;
		[field: SerializeField] public Color SelectedBandText { get; private set; } = Color.black;
		[field: SerializeField] public Color BandText { get; private set; } = Color.white;
		[field: SerializeField] public float HoverLift { get; private set; } = 10f;
		[field: SerializeField] public float HoverDuration { get; private set; } = 0.12f;
		[field: SerializeField] public float CardAppearDuration { get; private set; } = 0.32f;
		[field: SerializeField] public float CardAppearInterval { get; private set; } = 0.06f;
		[field: SerializeField] public float ResultTimeDuration { get; private set; } = 0.65f;
		[field: SerializeField] public float ResultRowStart { get; private set; } = 0.5f;
		[field: SerializeField] public float ResultRowInterval { get; private set; } = 0.26f;
		[field: SerializeField] public float ResultRowCountDuration { get; private set; } = 0.38f;
		[field: SerializeField] public float ResultEntryInterval { get; private set; } = 0.055f;
		[field: SerializeField] public float ResultTotalDuration { get; private set; } = 0.52f;
		[field: SerializeField] public float DefeatSpeedScale { get; private set; } = 0.6f;
		[field: SerializeField] public float PunchAmount { get; private set; } = 0.14f;
		[field: SerializeField] public float PunchDuration { get; private set; } = 0.3f;
		[field: SerializeField] public float MinCardNameFontSize { get; private set; } = 22f;

		public KindStyle Style(SurvivalUIKind kind) => Kinds[(int)kind];
		public CategoryStyle Style(UpgradeCategory category) => Categories[(int)category];
		public Color Difficulty(DungeonDifficulty difficulty) => DifficultyColors[(int)difficulty];
	}
}
