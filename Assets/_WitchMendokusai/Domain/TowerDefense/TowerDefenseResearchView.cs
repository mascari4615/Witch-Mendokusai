using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 연구 성좌 화면(TASK-WM-194) — **전체화면** · 선으로 이어진 그래프 · 끌어서 보기.
	///
	/// ★ 사용자 지시 두 가지를 한 화면에서 만족시킨다: 「연구 UI 전체화면」 + 「리니어 말고
	///   여러 갈래로 뻗는 그래프 형식」. 예전 연구 표시는 작은 패널에 한 줄짜리 목록이었다 —
	///   갈래가 없으니 고를 게 없고, 작으니 전체 모양이 안 보였다. 둘 다 「연구가 판의 중심」이라는
	///   감각을 못 준다.
	/// ★ 배치 좌표는 규칙층(TowerDefenseResearchGraph)이 준 것을 그대로 옮긴다 — 여기서 다시
	///   배치를 정하면 두 곳이 갈라져서, 규칙이 이웃이라 말하는 마디가 화면에선 멀어진다.
	/// ★ 선은 mesh 로 직접 긋는다(생성 API 로 사각형을 회전시키지 않는다) — 갈래가 늘면 요소 수가
	///   폭발하고, 회전한 사각형은 굵기가 배율에 따라 어긋난다.
	/// </summary>
	public sealed class TowerDefenseResearchView
	{
		private readonly List<TowerDefenseResearchGraph.Node> nodes = new();
		private readonly HashSet<int> taken = new();
		private readonly Dictionary<int, VisualElement> nodeElements = new();

		private VisualElement root;
		private VisualElement canvas;
		private Label titleLabel;
		private Label detailLabel;
		private Vector2 panOffset;
		private float zoom = 1f;
		private bool dragging;
		private Vector2 dragStart;
		private Vector2 panAtDragStart;

		private const float NODE_MAJOR = 46f;
		private const float NODE_MINOR = 30f;
		private const float ZOOM_MIN = 0.45f;
		private const float ZOOM_MAX = 1.8f;

		public bool IsOpen => root != null && root.style.display == DisplayStyle.Flex;

		/// <summary> 마디를 찍었다 — 값 치르기·효과 적용은 바깥(매치)이 한다. </summary>
		public event System.Action<int> NodeChosen = delegate { };

		public void Build(VisualElement parent, int branchCount, int ringCount, float majorAmount, float minorAmount)
		{
			TowerDefenseResearchGraph.Build(branchCount, ringCount, majorAmount, minorAmount, nodes);
			taken.Clear();
			taken.Add(TowerDefenseResearchGraph.CORE_ID); // 코어는 이미 있는 것 — 여기서 길이 시작한다.

			root = new VisualElement { name = "ResearchScreen" };
			root.style.position = Position.Absolute;
			// 전체화면 — 네 변에 붙인다(크기를 숫자로 박으면 창 크기가 바뀔 때마다 어긋난다).
			root.style.left = 0;
			root.style.top = 0;
			root.style.right = 0;
			root.style.bottom = 0;
			root.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.97f);
			root.style.display = DisplayStyle.None;
			parent.Add(root);

			titleLabel = new Label("연구 성좌");
			titleLabel.style.position = Position.Absolute;
			titleLabel.style.left = 36;
			titleLabel.style.top = 26;
			titleLabel.style.fontSize = 30;
			titleLabel.style.color = Color.white;
			root.Add(titleLabel);

			Label help = new Label("끌어서 이동 · 휠 확대·축소 · 마디를 눌러 연구 · ESC 닫기");
			help.style.position = Position.Absolute;
			help.style.left = 36;
			help.style.top = 66;
			help.style.fontSize = 14;
			help.style.color = new Color(0.62f, 0.68f, 0.78f, 1f);
			root.Add(help);

			detailLabel = new Label(string.Empty);
			detailLabel.style.position = Position.Absolute;
			detailLabel.style.left = 36;
			detailLabel.style.bottom = 30;
			detailLabel.style.fontSize = 16;
			detailLabel.style.color = new Color(1f, 0.86f, 0.5f, 1f);
			detailLabel.style.whiteSpace = WhiteSpace.Normal;
			detailLabel.style.maxWidth = 520;
			root.Add(detailLabel);

			canvas = new VisualElement { name = "ResearchCanvas" };
			canvas.style.position = Position.Absolute;
			canvas.style.left = 0;
			canvas.style.top = 0;
			canvas.style.right = 0;
			canvas.style.bottom = 0;
			canvas.generateVisualContent += DrawEdges;
			root.Add(canvas);

			BuildNodeElements();
			RegisterPanZoom();
		}

		private void BuildNodeElements()
		{
			nodeElements.Clear();
			foreach (TowerDefenseResearchGraph.Node node in nodes)
			{
				float size = node.IsMajor ? NODE_MAJOR : NODE_MINOR;
				VisualElement dot = new VisualElement { name = "node-" + node.Id };
				dot.style.position = Position.Absolute;
				dot.style.width = size;
				dot.style.height = size;
				// 큰 마디는 각지게, 작은 마디는 동그랗게 — 크기만으로는 한눈에 안 갈린다.
				float radius = node.IsMajor ? size * 0.22f : size * 0.5f;
				dot.style.borderTopLeftRadius = radius;
				dot.style.borderTopRightRadius = radius;
				dot.style.borderBottomLeftRadius = radius;
				dot.style.borderBottomRightRadius = radius;
				dot.style.borderTopWidth = 2;
				dot.style.borderBottomWidth = 2;
				dot.style.borderLeftWidth = 2;
				dot.style.borderRightWidth = 2;

				int id = node.Id;
				dot.RegisterCallback<ClickEvent>(_ => OnNodeClicked(id));
				dot.RegisterCallback<MouseEnterEvent>(_ => ShowDetail(id));

				canvas.Add(dot);
				nodeElements[node.Id] = dot;
			}
			RefreshNodes();
		}

		private void OnNodeClicked(int id)
		{
			foreach (TowerDefenseResearchGraph.Node node in nodes)
			{
				if (node.Id != id)
					continue;
				if (taken.Contains(id))
					return;
				if (TowerDefenseResearchGraph.IsReachable(node, taken) == false)
				{
					detailLabel.text = "아직 못 간다 — 앞 마디를 먼저 뚫어야 한다.";
					return;
				}
				taken.Add(id);
				RefreshNodes();
				canvas.MarkDirtyRepaint();
				NodeChosen(id);
				return;
			}
		}

		private void ShowDetail(int id)
		{
			foreach (TowerDefenseResearchGraph.Node node in nodes)
			{
				if (node.Id != id)
					continue;
				string state = taken.Contains(id)
					? "찍음"
					: TowerDefenseResearchGraph.IsReachable(node, taken) ? "지금 찍을 수 있다" : "잠김";
				detailLabel.text = node.Name + " — " + node.Description + "  ·  값 " + node.Cost + "  ·  " + state;
				return;
			}
		}

		/// <summary> 마디 색·자리 갱신 — 찍은 것/찍을 수 있는 것/잠긴 것이 한눈에 갈려야 한다. </summary>
		private void RefreshNodes()
		{
			foreach (TowerDefenseResearchGraph.Node node in nodes)
			{
				if (nodeElements.TryGetValue(node.Id, out VisualElement dot) == false)
					continue;

				bool isTaken = taken.Contains(node.Id);
				bool reachable = TowerDefenseResearchGraph.IsReachable(node, taken);

				Color fill = isTaken
					? new Color(1f, 0.78f, 0.32f, 1f)
					: reachable ? new Color(0.22f, 0.32f, 0.46f, 1f) : new Color(0.11f, 0.13f, 0.18f, 1f);
				Color line = isTaken
					? new Color(1f, 0.9f, 0.6f, 1f)
					: reachable ? new Color(0.55f, 0.75f, 1f, 1f) : new Color(0.2f, 0.23f, 0.3f, 1f);

				dot.style.backgroundColor = fill;
				dot.style.borderTopColor = line;
				dot.style.borderBottomColor = line;
				dot.style.borderLeftColor = line;
				dot.style.borderRightColor = line;

				PlaceNode(node, dot);
			}
		}

		private void PlaceNode(in TowerDefenseResearchGraph.Node node, VisualElement dot)
		{
			float size = (node.IsMajor ? NODE_MAJOR : NODE_MINOR) * zoom;
			Vector2 center = ToScreen(node.Position);
			dot.style.width = size;
			dot.style.height = size;
			dot.style.left = center.x - size * 0.5f;
			dot.style.top = center.y - size * 0.5f;
		}

		/// <summary> 성좌 좌표 → 화면 좌표. 화면 한가운데가 코어다. </summary>
		private Vector2 ToScreen(Vector2 graphPosition)
		{
			Rect area = canvas.contentRect;
			Vector2 middle = new Vector2(area.width * 0.5f, area.height * 0.5f);
			return middle + panOffset + graphPosition * zoom;
		}

		private void DrawEdges(MeshGenerationContext context)
		{
			Painter2D painter = context.painter2D;
			painter.lineWidth = Mathf.Max(1.5f, 3f * zoom);
			painter.lineCap = LineCap.Round;

			foreach (TowerDefenseResearchGraph.Node node in nodes)
			{
				if (node.Requires == null)
					continue;
				foreach (int requiredId in node.Requires)
				{
					if (TryFind(requiredId, out TowerDefenseResearchGraph.Node from) == false)
						continue;

					// 이미 이어진 길은 밝게 — 「내가 어디까지 뚫었나」가 선으로 읽혀야 성좌가 된다.
					bool lit = taken.Contains(requiredId) && taken.Contains(node.Id);
					painter.strokeColor = lit
						? new Color(1f, 0.8f, 0.4f, 0.95f)
						: new Color(0.35f, 0.42f, 0.55f, 0.55f);
					painter.BeginPath();
					painter.MoveTo(ToScreen(from.Position));
					painter.LineTo(ToScreen(node.Position));
					painter.Stroke();
				}
			}
		}

		private bool TryFind(int id, out TowerDefenseResearchGraph.Node found)
		{
			foreach (TowerDefenseResearchGraph.Node node in nodes)
			{
				if (node.Id != id)
					continue;
				found = node;
				return true;
			}
			found = default;
			return false;
		}

		private void RegisterPanZoom()
		{
			canvas.RegisterCallback<PointerDownEvent>(evt =>
			{
				dragging = true;
				dragStart = evt.position;
				panAtDragStart = panOffset;
			});
			canvas.RegisterCallback<PointerMoveEvent>(evt =>
			{
				if (dragging == false)
					return;
				panOffset = panAtDragStart + ((Vector2)evt.position - dragStart);
				RefreshNodes();
				canvas.MarkDirtyRepaint();
			});
			canvas.RegisterCallback<PointerUpEvent>(_ => dragging = false);
			canvas.RegisterCallback<PointerLeaveEvent>(_ => dragging = false);
			canvas.RegisterCallback<WheelEvent>(evt =>
			{
				zoom = Mathf.Clamp(zoom - evt.delta.y * 0.06f, ZOOM_MIN, ZOOM_MAX);
				RefreshNodes();
				canvas.MarkDirtyRepaint();
				evt.StopPropagation();
			});
			// 창 크기가 바뀌면 한가운데가 옮겨진다 — 다시 앉히지 않으면 성좌가 구석으로 밀린다.
			canvas.RegisterCallback<GeometryChangedEvent>(_ =>
			{
				RefreshNodes();
				canvas.MarkDirtyRepaint();
			});
		}

		/// <summary> 마디 조회 — 바깥이 값·효과를 읽는다. </summary>
		public bool TryGetNode(int id, out TowerDefenseResearchGraph.Node node) => TryFind(id, out node);

		/// <summary> 값을 못 치렀다 — 찍은 것을 도로 지운다. </summary>
		public void Undo(int id)
		{
			if (taken.Remove(id) == false)
				return;
			detailLabel.text = "정수가 모자라다.";
			RefreshNodes();
			canvas.MarkDirtyRepaint();
		}

		/// <summary> 처음으로 — 새 판이 열렸다. 찍은 것을 전부 지운다(코어만 남는다). </summary>
		public void ResetTaken()
		{
			taken.Clear();
			taken.Add(TowerDefenseResearchGraph.CORE_ID);
			if (canvas == null)
				return;
			RefreshNodes();
			canvas.MarkDirtyRepaint();
		}

		/// <summary> 지금 찍혀 있는 마디 번호들을 담아 준다(코어는 뺀다 — 그건 늘 있는 것이다). </summary>
		public void CollectTaken(List<int> into)
		{
			if (into == null)
				return;
			foreach (int id in taken)
			{
				if (id != TowerDefenseResearchGraph.CORE_ID)
					into.Add(id);
			}
		}

		/// <summary> 이어하기 — 적힌 마디를 다시 찍은 것으로 한다. 값은 이미 치른 것이라 다시 안 받는다. </summary>
		public void RestoreTaken(List<int> ids)
		{
			ResetTaken();
			if (ids == null)
				return;
			foreach (int id in ids)
				taken.Add(id);
			if (canvas == null)
				return;
			RefreshNodes();
			canvas.MarkDirtyRepaint();
		}

		public void SetOpen(bool open)
		{
			if (root == null)
				return;
			root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
			if (open == false)
				return;

			// 열 때마다 코어를 화면 가운데로 되돌린다 — 지난번에 끌어둔 자리에서 열리면 「어디지」가 된다.
			panOffset = Vector2.zero;
			RefreshNodes();
			canvas.MarkDirtyRepaint();
		}

		public void Toggle() => SetOpen(IsOpen == false);
	}
}
