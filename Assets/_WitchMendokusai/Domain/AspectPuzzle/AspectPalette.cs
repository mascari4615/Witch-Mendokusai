using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>고를 수 있는 성질 칸들. 이름과 그림은 성질 번호로 AspectData 에서</summary>
	public sealed class AspectPalette : VisualElement
	{
		private const string USS_CLASS = "wm-aspect-palette";
		private const string CHIP = "wm-aspect-chip";
		private const string CHIP_SELECTED = CHIP + "--selected";
		private const string CHIP_FITS = CHIP + "--fits";
		private const string CHIP_FITS_ALL = CHIP + "--fits-all";
		private const string CHIP_IDLE = CHIP + "--idle";

		private readonly MenuFocus focus;
		private readonly Dictionary<int, Button> chips = new();

		public event Action<int> Picked = delegate { };

		public AspectPalette(MenuFocus focus)
		{
			this.focus = focus;
			AddToClassList(USS_CLASS);
		}

		public void Build(IEnumerable<int> aspects)
		{
			Clear();
			chips.Clear();
			foreach (int aspect in aspects)
			{
				AspectData data = SOHelper.GetAspectData(aspect);
				Button chip = new(() => Picked(aspect));
				chip.AddToClassList(CHIP);
				VisualElement icon = new() { pickingMode = PickingMode.Ignore };
				icon.AddToClassList(CHIP + "__icon");
				icon.style.backgroundImage = new StyleBackground(data.Sprite);
				chip.Add(icon);
				Label label = new(data.Name) { pickingMode = PickingMode.Ignore };
				label.AddToClassList(CHIP + "__name");
				chip.Add(label);
				focus.Register(chip);
				chips.Add(aspect, chip);
				Add(chip);
			}
		}

		/// <param name="selected">고른 성질. 없으면 null</param>
		/// <param name="fits">밝게 보일 성질. 고른 칸이 없어 가릴 수 없으면 null (전부 보통)</param>
		/// <param name="fitsAll">옆의 모두와 붙는 성질. 안 가리면 null</param>
		public void Show(int? selected, Func<int, bool> fits, Func<int, bool> fitsAll = null)
		{
			foreach (KeyValuePair<int, Button> pair in chips)
			{
				bool isFit = fits != null && fits(pair.Key);
				pair.Value.EnableInClassList(CHIP_SELECTED, selected == pair.Key);
				pair.Value.EnableInClassList(CHIP_FITS, isFit);
				pair.Value.EnableInClassList(CHIP_FITS_ALL, fitsAll != null && fitsAll(pair.Key));
				pair.Value.EnableInClassList(CHIP_IDLE, fits != null && isFit == false);
			}
		}
	}
}
