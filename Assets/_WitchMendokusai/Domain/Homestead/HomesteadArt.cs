using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	/// <summary>
	/// 생활 갈래의 그림, 재질, 화면 모양을 모은 자산. Resources 하나
	/// 아이콘 시트는 칸 이름 글 파일 순서로 자름. 던전 아이템과 같은 물건은 그 아이템 그림을 씀
	/// </summary>
	[CreateAssetMenu(fileName = nameof(HomesteadArt), menuName = "WM/Homestead/Art")]
	public sealed class HomesteadArt : ScriptableObject
	{
		[field: Header("UI")]
		[field: SerializeField] public StyleSheet StyleSheet { get; private set; }
		[field: SerializeField] public StyleSheet ShopStyleSheet { get; private set; }
		[field: SerializeField] public StyleSheet FullMenuStyleSheet { get; private set; }

		[Header("Icons")]
		[SerializeField] private Texture2D iconSheet;
		[SerializeField] private TextAsset iconNames;
		[SerializeField, Min(1)] private int iconSize = 32;

		[field: Header("Materials")]
		[field: SerializeField] public Material Plaster { get; private set; }
		[field: SerializeField] public Material Frame { get; private set; }
		[field: SerializeField] public Material Wood { get; private set; }
		[field: SerializeField] public Material Gold { get; private set; }
		[field: SerializeField] public Material Roof { get; private set; }
		[field: SerializeField] public Material Stone { get; private set; }
		[field: SerializeField] public Material Soil { get; private set; }
		[field: SerializeField] public Material Leaf { get; private set; }
		[field: SerializeField] public Material Metal { get; private set; }
		[field: SerializeField] public Material Dark { get; private set; }
		[field: SerializeField] public Material Glass { get; private set; }
		[field: SerializeField] public Material Glow { get; private set; }
		[field: SerializeField] public Material GlowPurple { get; private set; }
		[field: SerializeField] public Material Cloth { get; private set; }
		[field: SerializeField] public Material SpriteMaterial { get; private set; }

		private static HomesteadArt instance;

		public static HomesteadArt Instance
		{
			get
			{
				if (instance == null)
					instance = Resources.Load<HomesteadArt>(nameof(HomesteadArt));
				return instance;
			}
		}

		private Dictionary<string, Sprite> icons;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetState() => instance = null;

		public bool HasIcon(string key) => Icons.ContainsKey(key);

		// 없는 이름은 던짐. 자료에 물건을 더하면 시트에도 칸이 있어야 함 (HomesteadArtTest)
		public Sprite Icon(string key) => Icons[key];

		public Sprite IconOf(GoodsDef goods)
		{
			if (Icons.TryGetValue(goods.Id, out Sprite own))
				return own;
			return SOHelper.GetItemData(goods.ItemId).Sprite;
		}

		private Dictionary<string, Sprite> Icons
		{
			get
			{
				if (icons == null || (icons.Count > 0 && HasLostSprite()))
					icons = SliceIcons();
				return icons;
			}
		}

		private bool HasLostSprite()
		{
			foreach (Sprite sprite in icons.Values)
				return sprite == null;
			return false;
		}

		private Dictionary<string, Sprite> SliceIcons()
		{
			Dictionary<string, Sprite> sliced = new();
			string[] names = iconNames.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
			int columns = iconSheet.width / iconSize;
			for (int i = 0; i < names.Length; i++)
			{
				int column = i % columns;
				int row = i / columns;
				Rect rect = new(column * iconSize, iconSheet.height - (row + 1) * iconSize, iconSize, iconSize);
				Sprite sprite = Sprite.Create(iconSheet, rect, new Vector2(0.5f, 0f), iconSize, 0, SpriteMeshType.FullRect);
				sprite.name = names[i];
				sprite.hideFlags = HideFlags.DontSave;
				sliced.Add(names[i], sprite);
			}
			return sliced;
		}
	}
}
