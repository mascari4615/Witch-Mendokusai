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

		private const float NODE_MAJOR = 58f;
		private const float NODE_MINOR = 30f;
		private const float ZOOM_MIN = 0.45f;
		private const float ZOOM_MAX = 1.8f;

		public bool IsOpen => root != null && root.style.display == DisplayStyle.Flex;

		/// <summary> 지금 화면에서 실제로 차지한 사각형 — 「전체화면으로 만들어달라」를 잴 유일한 근거다. </summary>
		public Rect ScreenRect => root != null ? root.worldBound : Rect.zero;

		/// <summary> 마디를 찍었다 — 값 치르기·효과 적용은 바깥(매치)이 한다. </summary>
		public event System.Action<int> NodeChosen = delegate { };

		public void Build(VisualElement parent, int branchCount, int ringCount, float majorAmount, float minorAmount,
			int nodeCost, string[] branchNames = null)
		{
			TowerDefenseResearchGraph.Build(branchCount, ringCount, majorAmount, minorAmount, nodeCost, nodes);
			this.branchNames = branchNames;
			taken.Clear();
			taken.Add(TowerDefenseResearchGraph.CORE_ID); // 코어는 이미 있는 것 — 여기서 길이 시작한다.

			root = new VisualElement { name = "ResearchScreen" };
			root.style.position = Position.Absolute;
			// 전체화면 — 네 변에 붙인다(크기를 숫자로 박으면 창 크기가 바뀔 때마다 어긋난다).
			root.style.left = 0;
			root.style.top = 0;
			root.style.right = 0;
			root.style.bottom = 0;
			root.style.backgroundColor = new Color(0.03f, 0.04f, 0.07f, 1f); // 완전 불투명 (사용자 지시).
			root.style.display = DisplayStyle.None;
			parent.Add(root);

			// ★ 캔버스를 *먼저* 붙인다 — 나중에 붙는 글자가 위로 온다. 앞서는 반대라 마디가 제목을
			//   덮어써서 「연구 성좌」가 읽히지 않았다(픽셀 확인에서 드러남).
			canvas = new VisualElement { name = "ResearchCanvas" };
			canvas.style.position = Position.Absolute;
			canvas.style.left = 0;
			canvas.style.top = 0;
			canvas.style.right = 0;
			canvas.style.bottom = 0;
			canvas.generateVisualContent += DrawEdges;
			root.Add(canvas);

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

			essenceLabel = new Label(string.Empty);
			essenceLabel.style.position = Position.Absolute;
			essenceLabel.style.right = 36;
			essenceLabel.style.top = 30;
			essenceLabel.style.fontSize = 20;
			essenceLabel.style.color = new Color(0.72f, 0.62f, 1f, 1f);
			root.Add(essenceLabel);

			BuildNodeElements();
			BuildBranchLabels();
			RegisterPanZoom();
		}

		private Label essenceLabel;
		private readonly List<(Label Label, Vector2 Position)> branchLabels = new();

		/// <summary>
		/// 갈래 끝에 그 갈래의 이름을 붙인다.
		///
		/// ★ 픽셀 확인에서 드러난 것: 마디가 전부 똑같이 생겨서 **어느 방향이 무엇을 주는지 화면에
		///   한 글자도 없었다**. 마우스를 올려야만 알 수 있으면 「어디로 뚫을까」를 눈으로 못 고른다 —
		///   성좌의 값어치가 그 한눈에 있는데 그게 없던 셈이다.
		/// </summary>
		private void BuildBranchLabels()
		{
			branchLabels.Clear();
			foreach (TowerDefenseResearchGraph.Node node in nodes)
			{
				if (node.IsMajor == false || node.Id == TowerDefenseResearchGraph.CORE_ID)
					continue;

				Label label = new Label(DisplayNameOf(node.Effect));
				label.style.position = Position.Absolute;
				label.style.fontSize = 15;
				label.style.color = new Color(0.82f, 0.86f, 0.95f, 1f);
				canvas.Add(label);
				// 이름은 마디보다 *한 걸음 더 바깥*에 둔다 — 마디 위에 겹치면 둘 다 안 읽힌다.
				branchLabels.Add((label, node.Position * 1.16f));
			}
		}

		private string[] branchNames;

		/// <summary> 화면에 쓸 갈래 이름 — 자산이 정한 것이 있으면 그것, 없으면 규칙층의 기본 이름. </summary>
		private string DisplayNameOf(TowerDefenseResearchEffect effect)
		{
			int index = (int)effect;
			if (branchNames != null && index >= 0 && index < branchNames.Length
				&& string.IsNullOrWhiteSpace(branchNames[index]) == false)
				return branchNames[index];
			return TowerDefenseResearchGraph.NameOf(effect);
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
				detailLabel.text = DisplayNameOf(node.Effect) + " — " + node.Description + "  ·  값 " + node.Cost + "  ·  " + state;
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

			foreach ((Label label, Vector2 position) in branchLabels)
			{
				Vector2 center = ToScreen(position);
				label.style.left = center.x - 34f;
				label.style.top = center.y - 10f;
			}

			RefreshEssence();
		}

		private System.Func<int> essenceProvider;
		private IVisualElementScheduledItem essenceTicker;

		/// <summary>
		/// 정수 잔량을 다시 적는다.
		///
		/// ★ 왜 따로 도나: 예전엔 마디를 누르거나 화면을 끌 때만 갱신됐다. 성좌를 열어둔 채로도
		///   판은 계속 돌아 정수가 들어오는데, 화면 숫자는 열었을 때 그대로 굳어 있었다 —
		///   「모자란 줄 알고 안 찍는」 상태가 생긴다. 열려 있는 동안은 스스로 따라가야 한다.
		/// </summary>
		private void RefreshEssence()
		{
			if (essenceLabel == null)
				return;
			essenceLabel.text = essenceProvider != null ? "정수 " + essenceProvider() : string.Empty;
		}

		/// <summary> 지금 정수가 얼마인지 묻는 통로 — 값을 치르는 화면인데 잔량이 안 보이면 고를 수가 없다. </summary>
		public void SetEssenceProvider(System.Func<int> provider)
		{
			essenceProvider = provider;
		}

		private void PlaceNode(in TowerDefenseResearchGraph.Node node, VisualElement dot)
		{
			float size = (node.IsMajor ? NODE_MAJOR : NODE_MINOR) * zoom;
			Vector2 center = ToScreen(node.Position);
			dot.style.width = size;
			dot.style.height = size;
			dot.style.left = center.x - size * 0.5f;
			dot.style.top = center.y - size * 0.5f;

			// ★ 모서리 둥글기도 *지금 크기* 기준으로 다시 준다. 세울 때 한 번만 주면 확대·축소에 따라
			//   비율이 어긋나, 각져야 할 큰 마디가 동그랗게 보인다(픽셀 확인에서 드러남 — 화면에서
			//   「길 끝」과 「중간」이 구분되지 않았다).
			float radius = node.IsMajor ? size * 0.22f : size * 0.5f;
			dot.style.borderTopLeftRadius = radius;
			dot.style.borderTopRightRadius = radius;
			dot.style.borderBottomLeftRadius = radius;
			dot.style.borderBottomRightRadius = radius;
			float border = node.IsMajor ? 3f : 2f;
			dot.style.borderTopWidth = border;
			dot.style.borderBottomWidth = border;
			dot.style.borderLeftWidth = border;
			dot.style.borderRightWidth = border;
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
				FitToView();
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
			{
				essenceTicker?.Pause(); // 닫힌 화면을 계속 갱신할 이유가 없다.
				return;
			}

			// 열려 있는 동안만 잔량을 따라간다.
			essenceTicker ??= root.schedule.Execute(RefreshEssence).Every(250);
			essenceTicker.Resume();

			// 열 때마다 코어를 화면 가운데로 되돌린다 — 지난번에 끌어둔 자리에서 열리면 「어디지」가 된다.
			panOffset = Vector2.zero;
			FitToView();
			RefreshNodes();
			canvas.MarkDirtyRepaint();
		}

		/// <summary>
		/// 성좌 전체가 화면에 들어오도록 배율을 맞춘다.
		///
		/// ★ 픽셀 확인에서 드러난 것: 기본 배율이 1이라 성좌가 화면 밖으로 넘쳤다. 전체 모양이
		///   안 보이면 「어디로 뚫을까」를 고를 수가 없다 — 성좌의 값어치가 그 한눈에 있다.
		/// </summary>
		private void FitToView()
		{
			if (canvas == null || nodes.Count == 0)
				return;

			Rect area = canvas.contentRect;
			if (area.width <= 1f || area.height <= 1f)
				return; // 아직 자리가 안 잡혔다 — 자리가 잡히면 다시 부른다.

			float reach = 0f;
			foreach (TowerDefenseResearchGraph.Node node in nodes)
				reach = Mathf.Max(reach, node.Position.magnitude);
			if (reach <= 0f)
				return;

			// 가장 먼 마디가 화면 짧은 변의 절반 안쪽으로 들어오게. 여백은 마디 하나 크기만큼.
			float half = Mathf.Min(area.width, area.height) * 0.5f - NODE_MAJOR;
			zoom = Mathf.Clamp(half / reach, ZOOM_MIN, ZOOM_MAX);
		}

		public void Toggle() => SetOpen(IsOpen == false);
	}
}
