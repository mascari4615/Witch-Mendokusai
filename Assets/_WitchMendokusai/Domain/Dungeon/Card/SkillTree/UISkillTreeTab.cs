using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 카드 선택 패널 안 트리 탭. 표시는 UI Toolkit, 키 입력은 패널의 EventSystem 선택을 공유
	public class UISkillTreeTab : Selectable
	{
		[SerializeField] private StyleSheet styleSheet;
		[SerializeField] private RectTransform area;
		[SerializeField] private SkillTreeViewSettings settings = new();

		private readonly Vector3[] corners = new Vector3[4];
		private SkillTreeView view;
		private SkillTree tree;
		private EquipmentData equipment;
		private IReadOnlyList<CardData> selectedCards;
		private IReadOnlyList<CardData> offeredCards;
		private Dictionary<string, SkillTreeCell> cells;
		private string focusedId;
		private Canvas canvas;

		public string FocusedId => focusedId;
		public SkillTreeView View => view;

		public void Show(SkillTree newTree, EquipmentData newEquipment, IReadOnlyList<CardData> selected, IReadOnlyList<CardData> offered)
		{
			tree = newTree;
			equipment = newEquipment;
			selectedCards = selected;
			offeredCards = offered;
			interactable = tree != null;
			cells = tree != null ? SkillTreeLayout.Compute(tree, settings.MaxNodesPerRow) : null;
			focusedId = tree != null && tree.Nodes.Count > 0 ? DefaultFocus() : null;
			Rebuild();
		}

		public void SetNeighbors(Selectable up, Selectable left)
		{
			navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnLeft = left };
		}

		// 카드 칸에 포커스가 가면 같은 노드를 표시
		public void Preview(CardData card)
		{
			SkillTreeNode node = tree?.NodeOf(card);
			if (node != null)
				SetFocus(node.Id);
		}

		public override void OnMove(AxisEventData eventData)
		{
			Vector2Int direction = eventData.moveDir switch
			{
				MoveDirection.Up => Vector2Int.up,
				MoveDirection.Down => Vector2Int.down,
				MoveDirection.Left => Vector2Int.left,
				MoveDirection.Right => Vector2Int.right,
				_ => Vector2Int.zero,
			};

			string next = cells != null && focusedId != null && direction != Vector2Int.zero ? SkillTreeLayout.Next(cells, focusedId, direction) : null;
			if (next == null)
			{
				base.OnMove(eventData);
				return;
			}

			SetFocus(next);
			eventData.Use();
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			if (Application.isPlaying == false)
				return;
			transition = Transition.None;
			Mount();
			Rebuild();
		}

		protected override void OnDisable()
		{
			if (view != null)
			{
				view.Stop();
				view.RemoveFromHierarchy();
			}
			base.OnDisable();
		}

		private void LateUpdate()
		{
			if (view == null || view.panel == null)
				return;

			area.GetWorldCorners(corners);
			Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
			Vector2 topLeft = ToPanel(RectTransformUtility.WorldToScreenPoint(camera, corners[1]));
			Vector2 topRight = ToPanel(RectTransformUtility.WorldToScreenPoint(camera, corners[2]));
			float width = area.rect.width;
			float scale = width > 0f ? (topRight.x - topLeft.x) / width : 0f;

			view.style.left = topLeft.x;
			view.style.top = topLeft.y;
			view.style.width = width;
			view.style.height = area.rect.height;
			view.style.scale = new Scale(new Vector3(scale, scale, 1f));
		}

		private Vector2 ToPanel(Vector2 screenPoint) =>
			RuntimePanelUtils.ScreenToPanel(view.panel, new Vector2(screenPoint.x, Screen.height - screenPoint.y));

		private void Mount()
		{
			if (view == null)
			{
				canvas = GetComponentInParent<Canvas>(true).rootCanvas;
				view = new SkillTreeView(settings);
				view.styleSheets.Add(styleSheet);
				view.NodePointerEntered += OnNodePointerEntered;
			}
			UIRoot.Instance.ScreenLayer.Add(view);
		}

		private void Rebuild()
		{
			if (view == null || view.panel == null)
				return;
			view.style.display = tree != null ? DisplayStyle.Flex : DisplayStyle.None;
			if (tree == null)
				return;
			view.SetTree(tree, equipment, cells, selectedCards, offeredCards);
			view.SetFocused(focusedId);
		}

		private void SetFocus(string nodeId)
		{
			focusedId = nodeId;
			if (view != null && view.panel != null)
				view.SetFocused(nodeId);
		}

		private void OnNodePointerEntered(string nodeId)
		{
			SetFocus(nodeId);
			EventSystem.current.SetSelectedGameObject(gameObject);
		}

		private string DefaultFocus()
		{
			foreach (SkillTreeNode node in tree.Nodes)
				foreach (CardData offered in offeredCards)
					if (offered != null && offered.ID == node.Card.ID)
						return node.Id;
			return tree.Nodes[0].Id;
		}
	}
}
