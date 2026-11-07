using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 오른쪽 위 상세
	public partial class ShopView
	{
		private sealed class CompareElements
		{
			public VisualElement Root;
			public VisualElement Chip;
			public Label Kind;
			public Label Selected;
			public Label Best;
			public Label Owner;
			public Label Difference;
		}

		private VisualElement infoContent;
		private Label infoEmpty;
		private VisualElement infoImage;
		private VisualElement infoDiamond;
		private ShopEmptyIcon infoDiamondEmpty;
		private Label infoEmptyLabel;
		private ShopOutlinedLabel infoName;
		private Label infoWorn;
		private Label infoDescription;
		private VisualElement compareTable;
		private VisualElement plainTable;
		private Label plainCategory;
		private Label plainOwned;
		private Label plainMax;
		private Label plainSale;
		private readonly List<CompareElements> compareRows = new();

		private void BuildInfo()
		{
			VisualElement panel = MakePanel("info", stage, out VisualElement body);
			infoEmpty = MakeLabel("empty-text", body, "팔 물건이 없습니다.");
			infoContent = Make("info-content", body);

			VisualElement head = Make("info-head", infoContent);
			VisualElement diamondBox = Make("diamond-box", head);
			infoDiamond = Make("diamond", diamondBox);
			infoDiamondEmpty = MakeEmptyIcon(infoDiamond);
			infoDiamondEmpty.AddToClassList(Class("diamond-empty"));
			infoImage = Make("diamond-image", diamondBox);
			infoEmptyLabel = MakeLabel("empty-label", diamondBox, "빈칸");
			infoEmptyLabel.AddToClassList(Class("diamond-empty-label"));

			VisualElement texts = Make("info-texts", head);
			VisualElement nameRow = Make("info-name-row", texts);
			infoName = MakeOutlined("info-name", nameRow);
			infoWorn = MakeLabel("info-worn", nameRow, "장착 중");
			infoDescription = MakeLabel("info-description", texts);

			compareTable = Make("compare", infoContent);
			VisualElement header = Make("compare-header", compareTable);
			MakeLabel("compare-head-kind", header, "강화 상한");
			MakeLabel("compare-head-selected", header, "선택");
			MakeLabel("compare-head-best", header, "장착 칸 최고");
			MakeLabel("compare-head-difference", header, "차이");

			plainTable = Make("plain", infoContent);
			plainCategory = AddPlainRow("분류");
			plainOwned = AddPlainRow("가방");
			plainMax = AddPlainRow("최대 소지");
			plainSale = AddPlainRow("판매가");
			plainSale.AddToClassList(Class("plain-value--sell"));

			FinishPanel(panel);
		}

		private Label AddPlainRow(string name)
		{
			VisualElement row = Make("plain-row", plainTable);
			MakeLabel("plain-name", row, name);
			return MakeLabel("plain-value", row);
		}

		private CompareElements MakeCompareRow()
		{
			CompareElements row = new();
			row.Root = Make("compare-row", compareTable);
			VisualElement kindCell = Make("compare-kind", row.Root);
			row.Chip = Make("compare-chip", kindCell);
			row.Kind = MakeLabel("compare-kind-name", kindCell);
			row.Selected = MakeLabel("compare-selected", row.Root);
			VisualElement bestCell = Make("compare-best", row.Root);
			row.Best = MakeLabel("compare-best-value", bestCell);
			row.Owner = MakeLabel("compare-best-owner", bestCell);
			row.Difference = MakeLabel("compare-difference", row.Root);
			return row;
		}

		public void SetInfo(ShopInfoModel model)
		{
			SetVisible(infoEmpty, model == null);
			SetVisible(infoContent, model != null);
			if (model == null)
				return;

			bool hasIcon = model.Icon != null;
			SetIcon(infoImage, model.Icon);
			SetVisible(infoImage, hasIcon);
			SetVisible(infoDiamondEmpty, hasIcon == false);
			SetVisible(infoEmptyLabel, hasIcon == false);
			infoDiamond.EnableInClassList(Class("diamond--empty"), hasIcon == false);

			infoName.text = model.Name;
			SetVisible(infoWorn, model.IsWorn);
			infoDescription.text = model.HasDescription ? model.Description : "설명 없음";
			infoDescription.EnableInClassList(Class("info-description--none"), model.HasDescription == false);

			bool compares = model.CompareRows != null;
			SetVisible(compareTable, compares);
			SetVisible(plainTable, compares == false);
			if (compares)
				ApplyCompare(model.CompareRows);
			else
				ApplyPlain(model);
		}

		private void ApplyPlain(ShopInfoModel model)
		{
			plainCategory.text = model.CategoryName;
			plainOwned.text = $"{Number(model.Owned)}개";
			plainMax.text = $"{model.MaxAmount.ToString(CultureInfo.InvariantCulture)}개";
			plainSale.text = $"{Number(model.SalePrice)}냥";
		}

		private void ApplyCompare(List<ShopCompareRow> rows)
		{
			while (compareRows.Count < rows.Count)
				compareRows.Add(MakeCompareRow());

			for (int i = 0; i < compareRows.Count; i++)
			{
				bool used = i < rows.Count;
				SetVisible(compareRows[i].Root, used);
				if (used == false)
					continue;

				ShopCompareRow model = rows[i];
				CompareElements row = compareRows[i];
				row.Chip.ClearClassList();
				row.Chip.AddToClassList(Class("compare-chip"));
				row.Chip.AddToClassList(Class($"compare-chip--{model.Kind.ToString().ToLowerInvariant()}"));
				row.Kind.text = model.KindName;
				row.Selected.text = model.Selected.ToString(CultureInfo.InvariantCulture);
				row.Best.text = model.Best.ToString(CultureInfo.InvariantCulture);
				row.Owner.text = model.BestOwner;
				SetVisible(row.Owner, string.IsNullOrEmpty(model.BestOwner) == false);
				row.Difference.text = model.Difference;
				row.Difference.EnableInClassList(Class("compare-difference--up"), model.Tone == ShopCompareTone.Better);
				row.Difference.EnableInClassList(Class("compare-difference--down"), model.Tone == ShopCompareTone.Worse);
			}
		}
	}
}
