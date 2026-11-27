using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Aspect;

namespace WitchMendokusai
{
	/// <summary>
	/// 마도서 창의 "마법": 배운 하찮은 마법 목록. 원할 때 누르면 씀, 어디서나 (기획 정본 11-25 23:06, 23:10).
	/// 배우는 길은 봉인된 두루마리 (개발 빌드 전용) 뿐이라 버튼도 개발 빌드에서만. 마법이 무엇을 하는지는 사용자 몫이라 지금은 쓰면 한 줄과 소리만
	/// </summary>
	public partial class MagicBookView
	{
		private const string SPELL_BUTTON = "마법";
		private const string SPELL_EMPTY = "아직 배운 마법이 없습니다. 봉인된 두루마리를 풀면 배웁니다.";
		private const string SPELL_FORMS_FORMAT = "모습 {0} / {1}";
		private const string SPELL_CAST = "쓰기";
		private const string USS_SPELL_SHOWN = "wm-mb-spells--shown";

		private ResourceManager resourceManager;
		private DataManager dataManager;
		private Button spellButton;
		private VisualElement spellPanel;
		private ScrollView spellList;
		private Label castNotice;

		private bool IsSpellPanelShown => spellPanel != null && spellPanel.ClassListContains(USS_SPELL_SHOWN);

		private void BuildSpellButton(VisualElement top)
		{
			if (DevContent.IsShown == false)
			{
				return;
			}
			spellButton = new(ToggleSpellPanel) { text = SPELL_BUTTON };
			spellButton.AddToClassList("wm-mb-arena");
			spellButton.AddToClassList("wm-mb-spell-button");
			focus.Register(spellButton);
			top.Add(spellButton);
		}

		// 하늘 위에 뜨는 판. 위 줄 아래에 놓여 위 줄 버튼은 그대로 눌림
		private void BuildSpellPanel()
		{
			if (DevContent.IsShown == false)
			{
				return;
			}
			spellPanel = Part(stage, "wm-mb-spells");
			// 마법이 많거나 설명이 길면 판 안에서 굴림
			spellList = new ScrollView(ScrollViewMode.Vertical);
			spellList.AddToClassList("wm-mb-spells__list");
			spellPanel.Add(spellList);
			castNotice = new Label { pickingMode = PickingMode.Ignore };
			castNotice.AddToClassList("wm-mb-spells__notice");
			spellPanel.Add(castNotice);
		}

		private void ToggleSpellPanel()
		{
			UiSfx.Play(UiSfxEvent.Confirm);
			if (IsSpellPanelShown)
			{
				HideSpellPanel();
				return;
			}
			HidePopup();
			FillSpells();
			castNotice.text = string.Empty;
			spellPanel.AddToClassList(USS_SPELL_SHOWN);
			if (inputManager.IsTouchMode == false && spellList.Query<Button>().First() is Button firstCast)
			{
				firstCast.Focus();
			}
		}

		private void HideSpellPanel()
		{
			if (IsSpellPanelShown == false)
			{
				return;
			}
			// 숨은 쓰기 버튼에 포커스가 남으면 다음 방향키가 엉뚱한 데서 시작. 마법 버튼으로 돌림
			bool hadFocus = container.focusController?.focusedElement is VisualElement focused && spellPanel.Contains(focused);
			spellPanel.RemoveFromClassList(USS_SPELL_SHOWN);
			if (hadFocus)
			{
				spellButton.Focus();
			}
		}

		private void FillSpells()
		{
			spellList.Clear();
			ScrollTableSO table = resourceManager.Scrolls;
			ScrollKnowledge knowledge = dataManager.FeatureSave<ScrollSaveSlice>().Knowledge;
			bool any = false;
			foreach (ScrollTableRow row in table.Rows)
			{
				int opened = knowledge.Opened(row.Id);
				if (row.Kind != ScrollResultKind.Spell || opened <= 0)
				{
					continue;
				}
				any = true;
				spellList.Add(SpellRow(table.Spells.Get(row.SpellId), opened, row.Forms));
			}
			if (any == false)
			{
				Label empty = new(SPELL_EMPTY) { pickingMode = PickingMode.Ignore };
				empty.AddToClassList("wm-mb-spells__empty");
				spellList.Add(empty);
			}
		}

		private VisualElement SpellRow(SpellEntry spell, int opened, int forms)
		{
			VisualElement row = new();
			row.AddToClassList("wm-mb-spell");
			VisualElement text = Part(row, "wm-mb-spell__text");
			Label name = new(spell.Name) { pickingMode = PickingMode.Ignore };
			name.AddToClassList("wm-mb-spell__name");
			text.Add(name);
			Label description = new(spell.Description) { pickingMode = PickingMode.Ignore };
			description.AddToClassList("wm-mb-spell__description");
			text.Add(description);
			Label formLabel = new(string.Format(SPELL_FORMS_FORMAT, opened, forms)) { pickingMode = PickingMode.Ignore };
			formLabel.AddToClassList("wm-mb-spell__forms");
			row.Add(formLabel);
			Button cast = new(() => Cast(spell, opened)) { text = SPELL_CAST };
			cast.AddToClassList("wm-mb-arena");
			cast.AddToClassList("wm-mb-spell__cast");
			focus.Register(cast);
			row.Add(cast);
			return row;
		}

		private void Cast(SpellEntry spell, int opened)
		{
			UiSfx.Play(UiSfxEvent.Confirm);
			castNotice.text = string.Format(spell.CastText, opened);
		}
	}
}
