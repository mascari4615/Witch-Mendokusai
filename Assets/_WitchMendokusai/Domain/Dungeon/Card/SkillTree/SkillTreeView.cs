using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	[Serializable]
	public class SkillTreeViewSettings
	{
		public int MaxNodesPerRow = 4;
		public float NodeSize = 84f;
		public float ColumnPitch = 132f;
		public float RowPitch = 118f;
		public float GraphPadding = 14f;
		public long AppearDelayMs = 60;
		public long PulseIntervalMs = 520;
		public string LockedLabel = "잠김";
		public string OpenLabel = "해금됨";
		public string MaxedLabel = "최대";
		public string OfferedLabel = "이번 후보";
		public string ConditionLabel = "해금 조건";
		public string OtherEquipmentLabel = "다른 장비";
		public string TotalFormat = "강화 {0}회";
		public string StackFormat = "강화 {0} / {1}회";
	}

	// 스킬 트리 탭 화면. 색과 크기 값은 SkillTree.uss
	public class SkillTreeView : VisualElement
	{
		private const string ROOT_CLASS = "wm-skilltree";

		private readonly SkillTreeViewSettings settings;
		private readonly VisualElement bandIcon = new();
		private readonly Label bandName = new();
		private readonly Label bandTotal = new();
		private readonly VisualElement graph = new();
		private readonly SkillTreeEdges edges = new();
		private readonly Label infoName = new();
		private readonly Label infoState = new();
		private readonly Label infoDescription = new();
		private readonly Label infoCondition = new();
		private readonly Dictionary<string, SkillTreeNodeElement> elements = new();
		private readonly IVisualElementScheduledItem pulse;

		private SkillTree tree;
		private Dictionary<string, SkillTreeCell> graphCells;
		private IReadOnlyList<CardData> selectedCards = Array.Empty<CardData>();
		private HashSet<int> offeredCardIds = new();
		private string focusedId;
		private bool isPulseOn;

		public event Action<string> NodePointerEntered = delegate { };

		public SkillTreeView(SkillTreeViewSettings settings)
		{
			this.settings = settings;
			AddToClassList(ROOT_CLASS);
			pickingMode = PickingMode.Ignore;

			VisualElement band = NewChild(this, "wm-skilltree__band");
			band.Add(bandIcon);
			bandIcon.AddToClassList("wm-skilltree__band-icon");
			band.Add(bandName);
			bandName.AddToClassList("wm-skilltree__band-name");
			band.Add(bandTotal);
			bandTotal.AddToClassList("wm-skilltree__value");

			graph.AddToClassList("wm-skilltree__graph");
			graph.pickingMode = PickingMode.Ignore;
			Add(graph);
			edges.AddToClassList("wm-skilltree__edges");
			graph.Add(edges);

			VisualElement info = NewChild(this, "wm-skilltree__info");
			NewChild(info, "wm-skilltree__info-line");
			VisualElement head = NewChild(info, "wm-skilltree__info-head");
			head.Add(infoName);
			infoName.AddToClassList("wm-skilltree__info-name");
			head.Add(infoState);
			infoState.AddToClassList("wm-skilltree__value");
			info.Add(infoDescription);
			infoDescription.AddToClassList("wm-skilltree__info-text");
			info.Add(infoCondition);
			infoCondition.AddToClassList("wm-skilltree__info-text");

			pulse = schedule.Execute(TogglePulse).Every(settings.PulseIntervalMs);
			pulse.Pause();
		}

		public IReadOnlyDictionary<string, SkillTreeNodeElement> Elements => elements;

		public void SetTree(SkillTree newTree, EquipmentData equipment, Dictionary<string, SkillTreeCell> cells, IReadOnlyList<CardData> selected, IReadOnlyList<CardData> offered)
		{
			tree = newTree;
			selectedCards = selected;
			offeredCardIds = new HashSet<int>(offered.Where(card => card != null).Select(card => card.ID));

			bandIcon.style.backgroundImage = new StyleBackground(equipment.Sprite);
			bandName.text = equipment.Name;
			bandTotal.text = string.Format(settings.TotalFormat, tree.Nodes.Sum(node => SkillTree.Count(node.Card, selected)));

			foreach (SkillTreeNodeElement element in elements.Values)
				element.RemoveFromHierarchy();
			elements.Clear();

			foreach (SkillTreeNode node in tree.Nodes)
			{
				SkillTreeNodeElement element = new(node, settings.NodeSize);
				string nodeId = node.Id;
				element.RegisterCallback<PointerEnterEvent>(_ => NodePointerEntered(nodeId));
				elements.Add(node.Id, element);
				graph.Add(element);
			}

			graph.UnregisterCallback<GeometryChangedEvent>(OnGraphGeometryChanged);
			graph.RegisterCallback<GeometryChangedEvent>(OnGraphGeometryChanged);
			graphCells = cells;
			Refresh();
			PlaceNodes();
			PlayAppear();
			pulse.Resume();
		}

		public void SetFocused(string nodeId)
		{
			if (focusedId != null && elements.TryGetValue(focusedId, out SkillTreeNodeElement previous))
				previous.SetFocused(false);
			focusedId = nodeId;
			if (nodeId != null)
				elements[nodeId].SetFocused(true);
			ShowInfo(nodeId != null ? tree.Nodes.First(node => node.Id == nodeId) : null);
		}

		public void Stop() => pulse.Pause();

		private void OnGraphGeometryChanged(GeometryChangedEvent geometryEvent) => PlaceNodes();

		private void Refresh()
		{
			foreach (SkillTreeNode node in tree.Nodes)
				elements[node.Id].SetState(node, node.State(selectedCards), offeredCardIds.Contains(node.Card.ID), selectedCards);
		}

		private void PlaceNodes()
		{
			Rect area = graph.contentRect;
			if (tree == null || area.width <= 0f || area.height <= 0f)
				return;

			int rows = SkillTreeLayout.RowCount(graphCells);
			float usableHeight = area.height - settings.NodeSize - settings.GraphPadding * 2f;
			float rowPitch = rows > 1 ? Mathf.Min(settings.RowPitch, usableHeight / (rows - 1)) : 0f;
			float totalHeight = (rows - 1) * rowPitch;
			float top = (area.height - totalHeight) / 2f;

			Dictionary<string, Vector2> centers = new();
			foreach (SkillTreeNode node in tree.Nodes)
			{
				SkillTreeCell cell = graphCells[node.Id];
				Vector2 center = new(area.width / 2f + cell.Column * settings.ColumnPitch, top + cell.Row * rowPitch);
				centers.Add(node.Id, center);
				SkillTreeNodeElement element = elements[node.Id];
				element.style.left = center.x - settings.NodeSize / 2f;
				element.style.top = center.y - settings.NodeSize / 2f;
			}

			edges.SetEdges(tree, centers, settings.NodeSize, Mathf.Max(0f, rowPitch - settings.NodeSize) / 2f, selectedCards);
		}

		private void PlayAppear()
		{
			foreach (SkillTreeNode node in tree.Nodes)
			{
				SkillTreeNodeElement element = elements[node.Id];
				element.AddToClassList("wm-skilltree__node--hidden");
				long delay = settings.AppearDelayMs * (graphCells[node.Id].Row + 1);
				element.schedule.Execute(() => element.RemoveFromClassList("wm-skilltree__node--hidden")).ExecuteLater(delay);
			}
		}

		private void TogglePulse()
		{
			isPulseOn = isPulseOn == false;
			foreach (SkillTreeNodeElement element in elements.Values)
				element.SetPulse(isPulseOn);
		}

		private void ShowInfo(SkillTreeNode node)
		{
			if (node == null)
			{
				infoName.text = string.Empty;
				infoState.text = string.Empty;
				infoDescription.text = string.Empty;
				infoCondition.text = string.Empty;
				return;
			}

			infoName.text = node.Card.Name;
			infoState.text = StateText(node);
			infoDescription.text = node.Card.Description;
			infoCondition.text = ConditionText(node);
		}

		private string StateText(SkillTreeNode node)
		{
			int count = SkillTree.Count(node.Card, selectedCards);
			string text = node.State(selectedCards) switch
			{
				SkillTreeNodeState.Locked => settings.LockedLabel,
				SkillTreeNodeState.Maxed => settings.MaxedLabel,
				SkillTreeNodeState.Open => settings.OpenLabel,
				_ => string.Format(settings.StackFormat, count, node.Card.MaxStack),
			};
			return offeredCardIds.Contains(node.Card.ID) ? $"{settings.OfferedLabel}  {text}" : text;
		}

		private string ConditionText(SkillTreeNode node)
		{
			if (node.Requirements.Count == 0)
				return string.Empty;

			StringBuilder builder = new(settings.ConditionLabel);
			builder.Append("  ");
			for (int i = 0; i < node.Requirements.Count; i++)
			{
				SkillTreeRequirement requirement = node.Requirements[i];
				string owner = requirement.IsExternal ? $" ({settings.OtherEquipmentLabel})" : string.Empty;
				string separator = i > 0 ? ", " : string.Empty;
				builder.Append($"{separator}{requirement.Card.Name}{owner} {Mathf.Min(requirement.Current(selectedCards), requirement.Stack)}/{requirement.Stack}");
			}
			return builder.ToString();
		}

		private static VisualElement NewChild(VisualElement parent, string className)
		{
			VisualElement child = new();
			child.AddToClassList(className);
			child.pickingMode = PickingMode.Ignore;
			parent.Add(child);
			return child;
		}
	}

	public class SkillTreeNodeElement : VisualElement
	{
		private static readonly string[] KIND_CLASSES =
		{
			"wm-skilltree__node--count",
			"wm-skilltree__node--damage",
			"wm-skilltree__node--speed",
			"wm-skilltree__node--size",
			"wm-skilltree__node--advanced",
		};

		private static readonly string[] STATE_CLASSES =
		{
			"wm-skilltree__node--locked",
			"wm-skilltree__node--open",
			"wm-skilltree__node--owned",
			"wm-skilltree__node--maxed",
		};

		private readonly VisualElement burst = new();
		private readonly VisualElement focusFrame = new();
		private readonly Label stack = new();
		private bool isOffered;

		public string NodeId { get; }
		public SkillTreeNodeState State { get; private set; }

		public SkillTreeNodeElement(SkillTreeNode node, float size)
		{
			NodeId = node.Id;
			AddToClassList("wm-skilltree__node");
			AddToClassList(KIND_CLASSES[(int)node.Card.UIKind]);
			style.width = size;
			style.height = size;

			burst.AddToClassList("wm-skilltree__burst");
			burst.pickingMode = PickingMode.Ignore;
			Add(burst);
			focusFrame.AddToClassList("wm-skilltree__focus");
			focusFrame.pickingMode = PickingMode.Ignore;
			Add(focusFrame);

			VisualElement diamond = new();
			diamond.AddToClassList("wm-skilltree__diamond");
			diamond.pickingMode = PickingMode.Ignore;
			Add(diamond);
			VisualElement line = new();
			line.AddToClassList("wm-skilltree__diamond-line");
			line.pickingMode = PickingMode.Ignore;
			diamond.Add(line);

			VisualElement icon = new();
			icon.AddToClassList("wm-skilltree__icon");
			icon.pickingMode = PickingMode.Ignore;
			icon.style.backgroundImage = new StyleBackground(node.Card.Sprite);
			Add(icon);

			stack.AddToClassList("wm-skilltree__stack");
			stack.pickingMode = PickingMode.Ignore;
			Add(stack);
		}

		public void SetState(SkillTreeNode node, SkillTreeNodeState state, bool offered, IReadOnlyList<CardData> selectedCards)
		{
			State = state;
			isOffered = offered;
			foreach (string stateClass in STATE_CLASSES)
				RemoveFromClassList(stateClass);
			AddToClassList(STATE_CLASSES[(int)state]);
			EnableInClassList("wm-skilltree__node--offered", offered);
			int count = SkillTree.Count(node.Card, selectedCards);
			stack.text = count > 0 ? $"{count}/{node.Card.MaxStack}" : string.Empty;
		}

		public void SetFocused(bool isFocused)
		{
			EnableInClassList("wm-skilltree__node--focused", isFocused);
			burst.RemoveFromClassList("wm-skilltree__burst--play");
			if (isFocused)
				burst.schedule.Execute(() => burst.AddToClassList("wm-skilltree__burst--play"));
		}

		public void SetPulse(bool isOn) => EnableInClassList("wm-skilltree__node--pulse", isOn && isOffered);
	}

	// 노드 연결선. 조건 충족 여부로 색 구분
	public class SkillTreeEdges : VisualElement
	{
		private static readonly CustomStyleProperty<Color> LOCKED_COLOR = new("--wm-skilltree-edge");
		private static readonly CustomStyleProperty<Color> OPEN_COLOR = new("--wm-skilltree-edge-open");
		private static readonly CustomStyleProperty<float> WIDTH = new("--wm-skilltree-edge-width");

		private readonly List<(Vector2 from, Vector2 to, bool isOpen)> lines = new();
		private Color lockedColor = Color.gray;
		private Color openColor = Color.white;
		private float width = 2f;
		private float elbow;

		public SkillTreeEdges()
		{
			pickingMode = PickingMode.Ignore;
			generateVisualContent += Draw;
			RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
		}

		public int LineCount => lines.Count;

		public void SetEdges(SkillTree tree, Dictionary<string, Vector2> centers, float nodeSize, float elbowOffset, IReadOnlyList<CardData> selectedCards)
		{
			lines.Clear();
			float gap = nodeSize / 2f;
			elbow = elbowOffset;
			foreach (SkillTreeNode node in tree.Nodes)
			{
				foreach (SkillTreeRequirement requirement in node.Requirements.Where(requirement => requirement.IsExternal == false))
				{
					Vector2 from = centers[requirement.Node.Id] + Vector2.up * gap;
					Vector2 to = centers[node.Id] - Vector2.up * gap;
					lines.Add((from, to, requirement.IsMet(selectedCards)));
				}
			}
			MarkDirtyRepaint();
		}

		private void OnStyleResolved(CustomStyleResolvedEvent styleEvent)
		{
			ICustomStyle resolved = styleEvent.customStyle;
			if (resolved.TryGetValue(LOCKED_COLOR, out Color locked))
				lockedColor = locked;
			if (resolved.TryGetValue(OPEN_COLOR, out Color open))
				openColor = open;
			if (resolved.TryGetValue(WIDTH, out float newWidth))
				width = newWidth;
			MarkDirtyRepaint();
		}

		private void Draw(MeshGenerationContext context)
		{
			Painter2D painter = context.painter2D;
			painter.lineWidth = width;
			foreach ((Vector2 from, Vector2 to, bool isOpen) in lines)
			{
				float middleY = to.y - elbow;
				painter.strokeColor = isOpen ? openColor : lockedColor;
				painter.BeginPath();
				painter.MoveTo(from);
				painter.LineTo(new Vector2(from.x, middleY));
				painter.LineTo(new Vector2(to.x, middleY));
				painter.LineTo(to);
				painter.Stroke();
			}
		}
	}
}
