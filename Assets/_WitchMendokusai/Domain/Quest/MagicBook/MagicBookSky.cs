using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// 마도서 하늘. 가운데 마도서에서 장마다 별자리가 뻗고, 깬 길만 빛남. 별 자리는 MagicBookSkyLayout,
	/// 카메라 (끌기, 휠, 고른 별 따라가기) 는 MagicBookSky.Camera.cs. 색은 MagicBook.uss (선 색은 사용자 속성)
	/// </summary>
	public sealed partial class MagicBookSky : VisualElement
	{
		public const string USS_CLASS = "wm-mb-sky";
		private const string USS_DUST = "wm-mb-sky__dust";
		private const string USS_NEBULA = "wm-mb-sky__nebula";
		private const string USS_WORLD = "wm-mb-world";
		private const string USS_LINES = "wm-mb-lines";
		private const string USS_HUB = "wm-mb-hub";
		private const string USS_HUB_GLOW = "wm-mb-hub__glow";
		private const string USS_HUB_CORE = "wm-mb-hub__core";
		private const string USS_HUB_ICON = "wm-mb-hub__icon";
		private const string USS_HUB_NAME = "wm-mb-hub__name";
		private const string USS_CHAPTER_NAME = "wm-mb-chapter-name";
		private const string USS_CHAPTER_LOCKED = "wm-mb-chapter-name--locked";
		private const string HUB_TITLE = "마도서";
		private const int NEBULA_COUNT = 3;

		private static readonly CustomStyleProperty<Color> DONE_LINE = new("--wm-mb-line-done");
		private static readonly CustomStyleProperty<Color> DONE_GLOW = new("--wm-mb-line-done-glow");
		private static readonly CustomStyleProperty<Color> OPEN_LINE = new("--wm-mb-line-open");
		private static readonly CustomStyleProperty<Color> DIM_LINE = new("--wm-mb-line-dim");
		private static readonly CustomStyleProperty<Color> DUST = new("--wm-mb-dust");

		private readonly struct Edge
		{
			public readonly MagicBookStar From;
			public readonly MagicBookStar To;

			public Edge(MagicBookStar from, MagicBookStar to)
			{
				From = from;
				To = to;
			}
		}

		private readonly MagicBookSettings settings;
		private readonly Texture2D glowTexture;
		private readonly VisualElement dust;
		private readonly VisualElement world;
		private readonly VisualElement lines;
		private readonly List<MagicBookStar> stars = new();
		private readonly List<Edge> edges = new();
		private readonly Dictionary<ChapterSO, Label> chapterNames = new();
		private readonly Dictionary<ChapterSO, Rect> chapterBounds = new();
		private Rect linesRect;
		private Color doneLineColor = Color.white;
		private Color doneGlowColor = Color.clear;
		private Color openLineColor = Color.white;
		private Color dimLineColor = Color.gray;
		private Color dustColor = Color.white;

		public IReadOnlyList<MagicBookStar> Stars => stars;

		/// <summary>별을 확인 (클릭, 확인 키) 했을 때</summary>
		public event Action<MagicBookStar> StarSubmitted = delegate { };

		public MagicBookSky(MagicBookSettings settings, Texture2D glowTexture)
		{
			this.settings = settings;
			this.glowTexture = glowTexture;
			AddToClassList(USS_CLASS);
			pickingMode = PickingMode.Position;

			for (int i = 1; i <= NEBULA_COUNT; i++)
			{
				VisualElement nebula = new() { pickingMode = PickingMode.Ignore };
				nebula.AddToClassList(USS_NEBULA);
				nebula.AddToClassList(USS_NEBULA + "--" + i);
				nebula.style.backgroundImage = glowTexture;
				Add(nebula);
			}
			dust = new VisualElement { pickingMode = PickingMode.Ignore };
			dust.AddToClassList(USS_DUST);
			dust.generateVisualContent += DrawDust;
			Add(dust);

			world = new VisualElement { pickingMode = PickingMode.Ignore };
			world.AddToClassList(USS_WORLD);
			Add(world);
			lines = new VisualElement { pickingMode = PickingMode.Ignore };
			lines.AddToClassList(USS_LINES);
			lines.generateVisualContent += DrawLines;
			world.Add(lines);

			RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyle);
			RegisterCameraCallbacks();
		}

		/// <summary>장 목록으로 하늘을 새로 짬. 장이 바뀔 때만 (열 때마다 상태는 Refresh)</summary>
		public void Build(IReadOnlyList<ChapterSO> chapters)
		{
			stars.Clear();
			edges.Clear();
			chapterNames.Clear();
			chapterBounds.Clear();
			world.Clear();
			world.Add(lines);
			BuildHub();

			Rect all = new(Vector2.zero, Vector2.zero);
			for (int chapterIndex = 0; chapterIndex < chapters.Count; chapterIndex++)
			{
				Rect bounds = BuildChapter(chapters[chapterIndex], chapterIndex);
				all = Encapsulate(all, bounds);
			}
			// 선 그림이 잘리지 않게 선 칸을 모든 별보다 넓게
			linesRect = all;
			lines.style.left = all.xMin;
			lines.style.top = all.yMin;
			lines.style.width = all.width;
			lines.style.height = all.height;
			skyBounds = all;
		}

		private void BuildHub()
		{
			VisualElement hub = new() { pickingMode = PickingMode.Ignore };
			hub.AddToClassList(USS_HUB);
			VisualElement glow = new() { pickingMode = PickingMode.Ignore };
			glow.AddToClassList(USS_HUB_GLOW);
			glow.style.backgroundImage = glowTexture;
			hub.Add(glow);
			VisualElement core = new() { pickingMode = PickingMode.Ignore };
			core.AddToClassList(USS_HUB_CORE);
			hub.Add(core);
			VisualElement icon = new() { pickingMode = PickingMode.Ignore };
			icon.AddToClassList(USS_HUB_ICON);
			if (settings.HubIcon != null)
			{
				icon.style.backgroundImage = new StyleBackground(settings.HubIcon);
			}
			core.Add(icon);
			Label title = new(HUB_TITLE) { pickingMode = PickingMode.Ignore };
			title.AddToClassList(USS_HUB_NAME);
			hub.Add(title);
			world.Add(hub);
		}

		private Rect BuildChapter(ChapterSO chapter, int chapterIndex)
		{
			IReadOnlyList<NodeBase> nodes = chapter.Nodes;
			IReadOnlyList<NodeConnection> connections = chapter.Connections;
			MagicBookSkyLayout.Options options = new(Vector2.zero, chapter.SkyAngle, chapter.SkyDepthStep, settings.BranchStep, settings.FirstOffset, settings.Jitter, chapterIndex * 3);
			Dictionary<string, Vector2> positions = MagicBookSkyLayout.Place(nodes, connections, options);

			HashSet<string> hasParent = new();
			foreach (NodeConnection connection in connections)
			{
				hasParent.Add(connection.TargetNodeId);
			}

			// 잎 별 이름은 장이 뻗는 바깥쪽, 갈래 별 이름은 아래. 들어오고 나가는 선을 피함
			MagicBookLabelSide outward = Mathf.Cos(chapter.SkyAngle * Mathf.Deg2Rad) < 0f ? MagicBookLabelSide.Left : MagicBookLabelSide.Right;
			HashSet<string> hasChild = new();
			foreach (NodeConnection connection in connections)
			{
				hasChild.Add(connection.SourceNodeId);
			}
			Dictionary<string, MagicBookStar> starsById = new();
			Rect bounds = new(Vector2.zero, Vector2.zero);
			foreach (NodeBase node in nodes)
			{
				if (node is QuestNode questNode == false || questNode.Target == null)
				{
					continue;
				}
				Vector2 position = positions[node.Id];
				MagicBookStar star = new(questNode.Target, chapter, position, hasParent.Contains(node.Id) == false, hasChild.Contains(node.Id) ? MagicBookLabelSide.Below : outward, glowTexture);
				star.clicked += () => StarSubmitted(star);
				// 키로 고를 때만 따라감. 클릭 중에 카메라가 움직이면 별이 커서에서 빠져나가 클릭이 안 됨
				star.RegisterCallback<FocusInEvent>(_ =>
				{
					if (IsPointerHeld == false)
					{
						Follow(star.SkyPosition);
					}
				});
				world.Add(star);
				stars.Add(star);
				starsById[node.Id] = star;
				bounds = Encapsulate(bounds, new Rect(position, Vector2.zero));
			}

			foreach (NodeConnection connection in connections)
			{
				if (starsById.TryGetValue(connection.SourceNodeId, out MagicBookStar from) && starsById.TryGetValue(connection.TargetNodeId, out MagicBookStar to))
				{
					edges.Add(new Edge(from, to));
				}
			}

			// 장 이름은 별자리의 가운데에서 먼 쪽 (위로 뻗는 장은 위, 아래로 뻗는 장은 아래) 가운데. 별 이름이 놓이는 옆을 피함
			Label name = new(ChapterTitle(chapter)) { pickingMode = PickingMode.Ignore };
			name.AddToClassList(USS_CHAPTER_NAME);
			bool below = Mathf.Sin(chapter.SkyAngle * Mathf.Deg2Rad) > 0.1f;
			Vector2 namePosition = below
				? new Vector2(bounds.center.x, bounds.yMax + settings.ChapterNameGap + settings.BelowLabelHeight)
				: new Vector2(bounds.center.x, bounds.yMin - settings.ChapterNameGap);
			name.style.left = namePosition.x;
			name.style.top = namePosition.y;
			world.Add(name);
			chapterNames[chapter] = name;

			bounds = Encapsulate(bounds, new Rect(namePosition, Vector2.zero));
			chapterBounds[chapter] = bounds;
			return bounds;
		}

		public static string ChapterTitle(ChapterSO chapter) => string.IsNullOrEmpty(chapter.Name) ? chapter.name : chapter.Name;

		/// <summary>퀘스트 진행을 별 상태로. 열 때와 퀘스트가 바뀔 때</summary>
		public void Refresh(QuestManager questManager)
		{
			Dictionary<int, QuestState> gateStates = questManager.GetQuestStates();
			Dictionary<ChapterSO, bool> anyOpen = new();
			foreach (MagicBookStar star in stars)
			{
				RuntimeQuest quest = questManager.GetQuest(star.Quest);
				QuestState gate = gateStates != null && gateStates.TryGetValue(star.Quest.ID, out QuestState found) ? found : QuestState.Locked;
				MagicBookStarState state = StateOf(gate, quest);
				star.SetState(state, state == MagicBookStarState.Doing || state == MagicBookStarState.Ready ? questManager.ProgressTextOf(quest) : string.Empty);
				anyOpen.TryGetValue(star.Chapter, out bool open);
				anyOpen[star.Chapter] = open || state != MagicBookStarState.Locked;
			}
			foreach (KeyValuePair<ChapterSO, Label> pair in chapterNames)
			{
				anyOpen.TryGetValue(pair.Key, out bool open);
				pair.Value.EnableInClassList(USS_CHAPTER_LOCKED, open == false);
			}
			lines.MarkDirtyRepaint();
		}

		public static MagicBookStarState StateOf(QuestState gate, RuntimeQuest quest)
		{
			if (gate == QuestState.Completed || (quest != null && quest.State == RuntimeQuestState.Completed))
			{
				return MagicBookStarState.Done;
			}
			if (quest == null || gate == QuestState.Locked)
			{
				return MagicBookStarState.Locked;
			}
			return quest.State == RuntimeQuestState.CanComplete ? MagicBookStarState.Ready : MagicBookStarState.Doing;
		}

		public bool IsChapterLocked(ChapterSO chapter) => chapterNames.TryGetValue(chapter, out Label name) && name.ClassListContains(USS_CHAPTER_LOCKED);

		public int CountDone()
		{
			int done = 0;
			foreach (MagicBookStar star in stars)
			{
				if (star.State == MagicBookStarState.Done)
				{
					done++;
				}
			}
			return done;
		}

		/// <summary>장에서 처음 고를 별: 깬 별 다음의 열린 별, 없으면 입구</summary>
		public MagicBookStar FirstStarOf(ChapterSO chapter)
		{
			MagicBookStar first = null;
			foreach (MagicBookStar star in stars)
			{
				if (star.Chapter != chapter)
				{
					continue;
				}
				if (star.State == MagicBookStarState.Ready || star.State == MagicBookStarState.Doing)
				{
					return star;
				}
				first ??= star;
			}
			return first;
		}

		private void OnCustomStyle(CustomStyleResolvedEvent evt)
		{
			ICustomStyle custom = evt.customStyle;
			custom.TryGetValue(DONE_LINE, out doneLineColor);
			custom.TryGetValue(DONE_GLOW, out doneGlowColor);
			custom.TryGetValue(OPEN_LINE, out openLineColor);
			custom.TryGetValue(DIM_LINE, out dimLineColor);
			custom.TryGetValue(DUST, out dustColor);
			lines.MarkDirtyRepaint();
			dust.MarkDirtyRepaint();
		}

		// 다 깬 길은 보라 번짐 위 보라 선, 깬 별에서 열린 별로는 흰 점선, 나머지는 흐린 선
		private void DrawLines(MeshGenerationContext context)
		{
			Painter2D painter = context.painter2D;
			painter.lineCap = LineCap.Round;
			Vector2 offset = -linesRect.min;
			foreach (Edge edge in edges)
			{
				Vector2 from = edge.From.SkyPosition + offset;
				Vector2 to = edge.To.SkyPosition + offset;
				bool fromDone = edge.From.State == MagicBookStarState.Done;
				if (fromDone && edge.To.State == MagicBookStarState.Done)
				{
					Stroke(painter, from, to, doneGlowColor, settings.DoneGlowWidth);
					Stroke(painter, from, to, doneLineColor, settings.DoneLineWidth);
				}
				else if (fromDone && edge.To.State != MagicBookStarState.Locked)
				{
					StrokeDashed(painter, from, to, openLineColor, settings.OpenLineWidth, settings.OpenDash);
				}
				else
				{
					Stroke(painter, from, to, dimLineColor, settings.DimLineWidth);
				}
			}
		}

		private static void Stroke(Painter2D painter, Vector2 from, Vector2 to, Color color, float width)
		{
			painter.strokeColor = color;
			painter.lineWidth = width;
			painter.BeginPath();
			painter.MoveTo(from);
			painter.LineTo(to);
			painter.Stroke();
		}

		private static void StrokeDashed(Painter2D painter, Vector2 from, Vector2 to, Color color, float width, Vector2 dash)
		{
			float length = Vector2.Distance(from, to);
			float period = dash.x + dash.y;
			if (length <= 0f || period <= 0f)
			{
				return;
			}
			Vector2 direction = (to - from) / length;
			painter.strokeColor = color;
			painter.lineWidth = width;
			painter.BeginPath();
			for (float start = 0f; start < length; start += period)
			{
				painter.MoveTo(from + direction * start);
				painter.LineTo(from + direction * Mathf.Min(start + dash.x, length));
			}
			painter.Stroke();
		}

		// 고운 별 가루. 같은 자리에 늘 같은 가루 (난수 대신 사인 해시)
		private void DrawDust(MeshGenerationContext context)
		{
			Rect rect = dust.contentRect;
			if (rect.width <= 0f || rect.height <= 0f)
			{
				return;
			}
			Painter2D painter = context.painter2D;
			for (int i = 0; i < settings.DustCount; i++)
			{
				float x = Fraction(Mathf.Sin(i * 12.9898f) * 43758.547f) * rect.width;
				float y = Fraction(Mathf.Sin(i * 78.233f) * 12345.679f) * rect.height;
				float alpha = Mathf.Lerp(settings.DustAlpha.x, settings.DustAlpha.y, (i * 37 % 10) / 9f);
				float size = settings.DustBigEvery > 0 && i % settings.DustBigEvery == 0 ? 2f : 1f;
				painter.fillColor = new Color(dustColor.r, dustColor.g, dustColor.b, dustColor.a * alpha);
				painter.BeginPath();
				painter.MoveTo(new Vector2(x, y));
				painter.LineTo(new Vector2(x + size, y));
				painter.LineTo(new Vector2(x + size, y + size));
				painter.LineTo(new Vector2(x, y + size));
				painter.ClosePath();
				painter.Fill();
			}
		}

		private static float Fraction(float value) => value - Mathf.Floor(value);

		private static Rect Encapsulate(Rect rect, Rect other) => Rect.MinMaxRect(
			Mathf.Min(rect.xMin, other.xMin), Mathf.Min(rect.yMin, other.yMin),
			Mathf.Max(rect.xMax, other.xMax), Mathf.Max(rect.yMax, other.yMax));
	}
}
