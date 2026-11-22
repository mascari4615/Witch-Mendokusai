using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 마도서 별 팝업. 상세는 늘 떠 있지 않고 고른 별 옆에만. 별 왼쪽과 오른쪽 중 무대 안에 들어가는 쪽,
	/// 카메라가 움직이면 따라 붙음. 팝업이 떠 있는 동안 다른 별로 옮기면 그 별의 내용으로 바뀜
	/// </summary>
	public partial class MagicBookView
	{
		private const string USS_POPUP = "wm-mb-popup";
		private const string USS_POPUP_SHOWN = "wm-mb-popup--shown";
		private const string USS_POPUP_LEFT = "wm-mb-popup--left";

		private VisualElement popup;
		private QuestDetail questDetail;
		private MagicBookStar popupStar;

		private bool IsPopupShown => popupStar != null;

		private void BuildPopup()
		{
			popup = new VisualElement();
			popup.AddToClassList(USS_POPUP);
			questDetail = new QuestDetail(questManager);
			popup.Add(questDetail);
			// 작업 시작, 보상 받기도 방향키로 가는 칸
			questDetail.Query<Button>().ForEach(focus.Register);
			// 그 버튼을 누르면 퀘스트가 바뀜. 별과 선과 진행 숫자를 다시
			popup.RegisterCallback<ClickEvent>(_ => popup.schedule.Execute(RefreshAfterAction));
			popup.RegisterCallback<GeometryChangedEvent>(_ => PlacePopup());
			stage.Add(popup);

			stage.RegisterCallback<FocusInEvent>(evt =>
			{
				if (IsPopupShown && evt.target is MagicBookStar star && star != popupStar)
				{
					ShowPopup(star);
				}
			});
		}

		private void OnStarSubmitted(MagicBookStar star)
		{
			if (popupStar == star)
			{
				UiSfx.Play(UiSfxEvent.WindowClose);
				HidePopup();
				return;
			}
			UiSfx.Play(UiSfxEvent.WindowOpen);
			ShowPopup(star);
		}

		private void ShowPopup(MagicBookStar star)
		{
			popupStar = star;
			questDetail.Bind(star.Quest, questManager.GetQuest(star.Quest));
			popup.AddToClassList(USS_POPUP_SHOWN);
			PlacePopup();
		}

		private void HidePopup()
		{
			popupStar = null;
			popup?.RemoveFromClassList(USS_POPUP_SHOWN);
		}

		private void RefreshAfterAction()
		{
			if (IsOpen == false)
			{
				return;
			}
			RefreshState();
			if (popupStar != null)
			{
				questDetail.Bind(popupStar.Quest, questManager.GetQuest(popupStar.Quest));
			}
		}

		// 오른쪽에 들어가면 오른쪽, 아니면 왼쪽. 세로는 별 높이에 맞추고 무대 안으로 눌러 넣음
		private void PlacePopup()
		{
			if (popupStar == null)
			{
				return;
			}
			float width = popup.resolvedStyle.width;
			float height = popup.resolvedStyle.height;
			Vector2 stageSize = stage.contentRect.size;
			if (float.IsNaN(width) || float.IsNaN(height) || stageSize.x <= 0f)
			{
				return;
			}
			// 하늘은 무대를 가득 채우고 원점이 같아 하늘 안 좌표가 곧 무대 좌표
			Vector2 anchor = sky.SkyToLocal(popupStar.SkyPosition);
			float offset = Settings.PopupOffset * Mathf.Max(sky.Zoom, 1f);
			float edge = Settings.PopupEdge;
			float right = anchor.x + offset;
			bool onLeft = right + width > stageSize.x - edge;
			float left = onLeft ? anchor.x - offset - width : right;
			float top = Mathf.Clamp(anchor.y - height * 0.5f, edge, Mathf.Max(edge, stageSize.y - edge - height));
			popup.style.left = Mathf.Max(edge, left);
			popup.style.top = top;
			popup.EnableInClassList(USS_POPUP_LEFT, onLeft);
		}
	}
}
