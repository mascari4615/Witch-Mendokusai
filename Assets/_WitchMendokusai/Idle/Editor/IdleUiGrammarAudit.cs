using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.Idle.Editor
{
	/// <summary>
	/// 방치형 UI 문법 검사기 (memo wm/systems/idle-ui-grammar-audit.md).
	///
	/// Play 중인 BattleScreen 의 UI Toolkit 트리를 훑어 넷을 잰다.
	/// 1. 글자 넘침: Label 의 자연 크기가 칸보다 크다
	/// 2. 화면 밖: 보이는 요소가 1920x1080 밖으로 나간다
	/// 3. 금색: 옛 문법의 금색 계열이 색, 배경, 테두리에 남아 있다
	/// 4. 한글: 라벨에 한글이 있다 (이름과 본문은 예외 목록으로)
	/// 5. 겹침: 형제끼리 겹친다 (absolute 는 라벨끼리만). 사용자 2026-09-21 "겹치고 꽉 차고 안 보이는 건 왜 못 잡나"
	/// 6. 꽉 참: 라벨 글자가 칸 폭의 94% 를 넘거나 안쪽 여백이 4px 미만
	/// 7. 안 보임: 글자색 알파 .3 미만, 또는 뒤에 그려지는 불투명 형제가 라벨을 덮는다
	/// 8. 붙음: 흐름 배치 형제 사이 틈이 4px 미만 (개수만)
	///
	/// 상태 바꾸기 (탭, 빈 자리) 는 <see cref="Press"/>. 결과는 JSON 줄 하나로 파일에 덧붙인다.
	/// 진입은 `unity command eval` 에서 정적 호출. 메뉴는 현재 탭 한 번만 잰다
	/// </summary>
	public static class IdleUiGrammarAudit
	{
		private static readonly Color32[] GOLDS =
		{
			new Color32(245, 175, 16, 255), new Color32(245, 196, 0, 255), new Color32(247, 210, 140, 255),
			new Color32(242, 206, 137, 255), new Color32(255, 229, 142, 255), new Color32(255, 222, 128, 255),
			new Color32(234, 207, 155, 255), new Color32(66, 52, 29, 255), new Color32(76, 56, 27, 255),
		};

		/// <summary>문턱은 EditorPrefs 로 만진다 (WM 룰: 수치 하드코딩 금지). 기본 28, 1920, 1080</summary>
		private static float GoldDistance => EditorPrefs.GetFloat("WM.Idle.UiAudit.GoldDistance", 28f);
		private static float ScreenWidth => EditorPrefs.GetFloat("WM.Idle.UiAudit.ScreenWidth", 1920f);
		private static float ScreenHeight => EditorPrefs.GetFloat("WM.Idle.UiAudit.ScreenHeight", 1080f);

		/// <summary>한글이어도 되는 요소 이름 조각. 고유명사와 본문</summary>
		private static readonly string[] KOREAN_ALLOWED = { "doll-name", "tooltip", "note", "feedback", "doll-", "codex", "discovery", "pickup", "row-", "appraise", "advice", "battle-note", "scene-cover" };

		[MenuItem("WM/Idle/UI Grammar Audit (current state)")]
		public static void AuditCurrent()
		{
			string path = System.IO.Path.Combine(Application.dataPath, "..", "Logs", "ui-grammar-audit.jsonl");
			Inspect("menu", path);
			Debug.Log("[ui-audit] " + path);
		}

		/// <summary>버튼 이름으로 누른다. 탭 전환과 자리 클릭용</summary>
		public static string Press(string buttonName)
		{
			VisualElement root = Root();
			if (root == null)
			{
				return "no root";
			}

			Button button = root.Q<Button>(buttonName);
			if (button == null)
			{
				return "no button " + buttonName;
			}

			using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
			{
				submit.target = button;
				button.SendEvent(submit);
			}

			return "pressed " + buttonName;
		}

		/// <summary>지금 상태를 재서 JSON 한 줄을 파일에 덧붙인다. 반환은 요약</summary>
		public static string Inspect(string stateName, string outPath)
		{
			VisualElement root = Root();
			if (root == null)
			{
				return "no root";
			}

			List<string> overflow = new List<string>();
			List<string> offscreen = new List<string>();
			List<string> gold = new List<string>();
			List<string> korean = new List<string>();
			List<string> scrolled = new List<string>();
			List<string> overlap = new List<string>();
			List<string> cramped = new List<string>();
			List<string> hidden = new List<string>();
			List<string> tight = new List<string>();
			List<string> rects = new List<string>();
			List<string> dense = new List<string>();
			int visited = 0;
			Walk(root, element =>
			{
				if (element.resolvedStyle.display == DisplayStyle.None || element.resolvedStyle.visibility == Visibility.Hidden || element.ClassListContains("wm-floating-text"))
				{
					return false;
				}

				visited++;
				Rect bound = element.worldBound;
				if (bound.width > 0f && bound.height > 0f && element.resolvedStyle.opacity > 0.01f)
				{
					if (bound.xMin < -1f || bound.yMin < -1f || bound.xMax > ScreenWidth + 1f || bound.yMax > ScreenHeight + 1f)
					{
						// 스크롤 안은 화면 밖이 정상. 판 높이를 넘긴 양만 scrolled 로 센다
						if (InsideScroll(element))
						{
							scrolled.Add(Describe(element) + " " + Round(bound));
						}
						else
						{
							offscreen.Add(Describe(element) + " " + Round(bound));
						}
					}

					CheckGold(element, gold);
				}

				Label label = element as Label;
				if (label != null && string.IsNullOrEmpty(label.text) == false)
				{
					CheckText(label, overflow, korean);
					CheckCramped(label, cramped);
					CheckHidden(label, hidden);
					rects.Add("L " + Round(label.worldBound));
				}
				else if (element is Button)
				{
					rects.Add("B " + Round(element.worldBound));
				}

				CheckSiblings(element, overlap, tight);
				CheckDense(element, dense);
				return true;
			});

			string line = "{\"state\":\"" + stateName + "\",\"visited\":" + visited
				+ ",\"overflow\":" + Json(overflow) + ",\"offscreen\":" + Json(offscreen)
				+ ",\"gold\":" + Json(gold) + ",\"korean\":" + Json(korean) + ",\"scrolled\":" + Json(scrolled)
				+ ",\"overlap\":" + Json(overlap) + ",\"cramped\":" + Json(cramped) + ",\"hidden\":" + Json(hidden) + ",\"tight\":" + Json(tight) + ",\"rects\":" + Json(rects) + ",\"dense\":" + Json(dense) + "}";
			System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath));
			System.IO.File.AppendAllText(outPath, line + "\n");
			return stateName + " visited=" + visited + " overflow=" + overflow.Count + " offscreen=" + offscreen.Count + " gold=" + gold.Count + " korean=" + korean.Count + " scrolled=" + scrolled.Count + " overlap=" + overlap.Count + " cramped=" + cramped.Count + " hidden=" + hidden.Count + " tight=" + tight.Count;
		}

		private static void CheckText(Label label, List<string> overflow, List<string> korean)
		{
			Vector2 natural = label.MeasureTextSize(label.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);
			float width = label.resolvedStyle.width - label.resolvedStyle.paddingLeft - label.resolvedStyle.paddingRight;
			float height = label.resolvedStyle.height - label.resolvedStyle.paddingTop - label.resolvedStyle.paddingBottom;
			bool wraps = label.resolvedStyle.whiteSpace == WhiteSpace.Normal;
			if (width > 0f && wraps == false && natural.x > width + 2f)
			{
				overflow.Add(Describe(label) + " text=" + Short(label.text) + " need=" + Mathf.Round(natural.x) + " have=" + Mathf.Round(width));
			}
			else if (height > 0f && natural.y > height + 2f && label.style.overflow.value == Overflow.Hidden)
			{
				overflow.Add(Describe(label) + " text=" + Short(label.text) + " needH=" + Mathf.Round(natural.y) + " haveH=" + Mathf.Round(height));
			}

			if (HasHangul(label.text) && IsKoreanAllowed(label) == false)
			{
				korean.Add(Describe(label) + " text=" + Short(label.text));
			}
		}

		private static float CrampedRatio => EditorPrefs.GetFloat("WM.Idle.UiAudit.CrampedRatio", 0.94f);
		private static float MinPadding => EditorPrefs.GetFloat("WM.Idle.UiAudit.MinPadding", 4f);
		private static float MinGap => EditorPrefs.GetFloat("WM.Idle.UiAudit.MinGap", 4f);

		/// <summary>글자가 칸을 거의 다 채우거나 안쪽 여백이 없다. 버튼 안 라벨은 버튼 여백을 본다</summary>
		private static void CheckCramped(Label label, List<string> cramped)
		{
			if (label.resolvedStyle.whiteSpace == WhiteSpace.Normal)
			{
				return;
			}

			Vector2 natural = label.MeasureTextSize(label.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);
			VisualElement box = label.parent is Button ? label.parent : label;
			float width = box.resolvedStyle.width;
			float padding = box.resolvedStyle.paddingLeft + box.resolvedStyle.paddingRight;
			if (label.parent is Button)
			{
				padding += label.resolvedStyle.paddingLeft + label.resolvedStyle.paddingRight + label.resolvedStyle.marginLeft + label.resolvedStyle.marginRight;
			}

			if (width <= 0f || natural.x <= 0f)
			{
				return;
			}

			bool autoSized = label.parent is Button == false && Mathf.Abs(width - natural.x - label.resolvedStyle.paddingLeft - label.resolvedStyle.paddingRight) <= 2f;
			if (autoSized)
			{
				return;
			}

			bool fillsBox = natural.x > width * CrampedRatio && natural.x <= width + 2f;
			bool noPadding = width - natural.x < MinPadding * 2f && natural.x <= width + 2f;
			if (fillsBox || noPadding)
			{
				cramped.Add(Describe(label) + " text=" + Short(label.text) + " need=" + Mathf.Round(natural.x) + " box=" + Mathf.Round(width) + " pad=" + Mathf.Round(padding));
			}
		}

		/// <summary>글자색이 거의 투명하거나, 뒤에 그려지는 불투명 형제 (또는 조상의 뒤 형제) 가 라벨을 다 덮는다</summary>
		private static void CheckHidden(Label label, List<string> hidden)
		{
			if (label.resolvedStyle.color.a < 0.3f)
			{
				hidden.Add(Describe(label) + " text=" + Short(label.text) + " alpha=" + label.resolvedStyle.color.a.ToString("0.00"));
				return;
			}

			Rect bound = label.worldBound;
			for (VisualElement cursor = label; cursor != null && cursor.parent != null; cursor = cursor.parent)
			{
				VisualElement parent = cursor.parent;
				int index = parent.IndexOf(cursor);
				for (int i = index + 1; i < parent.childCount; i++)
				{
					VisualElement later = parent[i];
					if (later.resolvedStyle.display == DisplayStyle.None || later.resolvedStyle.visibility == Visibility.Hidden || later.resolvedStyle.opacity < 0.5f)
					{
						continue;
					}

					bool opaque = later.resolvedStyle.backgroundColor.a > 0.85f || later.resolvedStyle.backgroundImage.texture != null || later.resolvedStyle.backgroundImage.sprite != null;
					if (opaque && Contains(later.worldBound, bound))
					{
						hidden.Add(Describe(label) + " text=" + Short(label.text) + " under=" + Describe(later));
						return;
					}
				}
			}
		}

		/// <summary>형제끼리 겹침과 붙음. absolute 는 라벨끼리 겹칠 때만 (배지가 얼굴 위에 얹히는 건 의도)</summary>
		private static void CheckSiblings(VisualElement parent, List<string> overlap, List<string> tight)
		{
			for (int i = 0; i < parent.childCount; i++)
			{
				VisualElement a = parent[i];
				if (Skip(a))
				{
					continue;
				}

				for (int j = i + 1; j < parent.childCount; j++)
				{
					VisualElement b = parent[j];
					if (Skip(b))
					{
						continue;
					}

					Rect ra = a.worldBound;
					Rect rb = b.worldBound;
					bool aAbs = a.resolvedStyle.position == Position.Absolute;
					bool bAbs = b.resolvedStyle.position == Position.Absolute;
					float ix = Mathf.Min(ra.xMax, rb.xMax) - Mathf.Max(ra.xMin, rb.xMin);
					float iy = Mathf.Min(ra.yMax, rb.yMax) - Mathf.Max(ra.yMin, rb.yMin);
					if (ix > 2f && iy > 2f)
					{
						bool bothFlow = aAbs == false && bAbs == false;
						bool textOnText = a is Label && b is Label;
						if (bothFlow || textOnText)
						{
							overlap.Add(Describe(a) + " x " + Describe(b) + " by " + Mathf.Round(ix) + "x" + Mathf.Round(iy));
						}
					}
					else if (aAbs == false && bAbs == false && j == i + 1 && a is Button && b is Button)
					{
						float gap = Mathf.Max(-ix, -iy);
						if (gap >= 0f && gap < MinGap)
						{
							tight.Add(Describe(a) + " | " + Describe(b) + " gap=" + Mathf.Round(gap));
						}
					}
				}
			}
		}

		private static float DenseTextHeight => EditorPrefs.GetFloat("WM.Idle.UiAudit.DenseTextHeight", 0.7f);

		/// <summary>빽빽함. 고정 높이 줄 안의 라벨 글자가 줄 높이의 70% 를 넘는다 (위아래 여백 없음). 자식에 맞춰 줄어든 줄은 제외</summary>
		private static void CheckDense(VisualElement parent, List<string> dense)
		{
			if (parent.resolvedStyle.flexDirection != FlexDirection.Row || parent.childCount < 2 || parent is ScrollView)
			{
				return;
			}

			for (int i = 0; i < parent.childCount; i++)
			{
				Label label = parent[i] as Label;
				if (label == null || Skip(label) || string.IsNullOrEmpty(label.text))
				{
					continue;
				}

				float boxHeight = parent.worldBound.height;
				bool hugs = Mathf.Abs(boxHeight - label.worldBound.height) <= 2f;
				if (hugs == false && boxHeight > 0f && label.resolvedStyle.fontSize > boxHeight * DenseTextHeight)
				{
					dense.Add(Describe(label) + " text=" + Short(label.text) + " font=" + Mathf.Round(label.resolvedStyle.fontSize) + " rowH=" + Mathf.Round(boxHeight));
				}
			}
		}

		private static bool Skip(VisualElement element)
		{
			Rect bound = element.worldBound;
			return element.resolvedStyle.display == DisplayStyle.None || element.resolvedStyle.visibility == Visibility.Hidden
				|| element.resolvedStyle.opacity < 0.05f || bound.width <= 1f || bound.height <= 1f
				|| element.ClassListContains("idle-layer") || element.ClassListContains("wm-floating-text") || element is ScrollView;
		}

		private static bool Contains(Rect outer, Rect inner)
		{
			return outer.xMin <= inner.xMin + 1f && outer.yMin <= inner.yMin + 1f && outer.xMax >= inner.xMax - 1f && outer.yMax >= inner.yMax - 1f;
		}

		private static void CheckGold(VisualElement element, List<string> gold)
		{
			if (IsGold(element.resolvedStyle.color))
			{
				gold.Add(Describe(element) + " color");
			}

			if (IsGold(element.resolvedStyle.backgroundColor))
			{
				gold.Add(Describe(element) + " background");
			}

			if (element.resolvedStyle.borderLeftWidth > 0f && IsGold(element.resolvedStyle.borderLeftColor))
			{
				gold.Add(Describe(element) + " border");
			}
		}

		private static bool IsGold(Color color)
		{
			if (color.a < 0.05f)
			{
				return false;
			}

			Color32 c = color;
			foreach (Color32 g in GOLDS)
			{
				float d = Mathf.Sqrt((c.r - g.r) * (c.r - g.r) + (c.g - g.g) * (c.g - g.g) + (c.b - g.b) * (c.b - g.b));
				if (d <= GoldDistance)
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsKoreanAllowed(VisualElement element)
		{
			for (VisualElement cursor = element; cursor != null; cursor = cursor.parent)
			{
				string name = cursor.name ?? string.Empty;
				foreach (string allowed in KOREAN_ALLOWED)
				{
					if (name.Contains(allowed))
					{
						return true;
					}
				}

				if (cursor.ClassListContains("idle-row-note") || cursor.ClassListContains("idle-doll-name") || cursor.ClassListContains("idle-tooltip"))
				{
					return true;
				}
			}

			return false;
		}

		private static bool InsideScroll(VisualElement element)
		{
			for (VisualElement cursor = element.parent; cursor != null; cursor = cursor.parent)
			{
				if (cursor is ScrollView)
				{
					return true;
				}
			}

			return false;
		}

		private static bool HasHangul(string text)
		{
			foreach (char ch in text)
			{
				if (ch >= '\uAC00' && ch <= '\uD7A3')
				{
					return true;
				}
			}

			return false;
		}

		private static VisualElement Root()
		{
			BattleScreen screen = Object.FindFirstObjectByType<BattleScreen>();
			if (screen == null)
			{
				return null;
			}

			FieldInfo field = typeof(BattleScreen).GetField("panelRoot", BindingFlags.NonPublic | BindingFlags.Instance);
			return field == null ? null : field.GetValue(screen) as VisualElement;
		}

		private static void Walk(VisualElement element, System.Func<VisualElement, bool> visit)
		{
			if (visit(element) == false)
			{
				return;
			}

			for (int i = 0; i < element.childCount; i++)
			{
				Walk(element[i], visit);
			}
		}

		private static string Describe(VisualElement element)
		{
			StringBuilder sb = new StringBuilder();
			for (VisualElement cursor = element; cursor != null && sb.Length < 90; cursor = cursor.parent)
			{
				string piece = string.IsNullOrEmpty(cursor.name) ? FirstClass(cursor) : "#" + cursor.name;
				sb.Insert(0, piece + (sb.Length == 0 ? string.Empty : ">"));
			}

			return sb.ToString();
		}

		private static string FirstClass(VisualElement element)
		{
			foreach (string cls in element.GetClasses())
			{
				return "." + cls;
			}

			return element.GetType().Name;
		}

		private static string Short(string text)
		{
			string flat = text.Replace("\n", "\\n").Replace("\"", "'");
			return flat.Length > 24 ? flat.Substring(0, 24) : flat;
		}

		private static string Round(Rect rect)
		{
			return "(" + Mathf.Round(rect.xMin) + "," + Mathf.Round(rect.yMin) + "," + Mathf.Round(rect.xMax) + "," + Mathf.Round(rect.yMax) + ")";
		}

		private static string Json(List<string> items)
		{
			StringBuilder sb = new StringBuilder("[");
			for (int i = 0; i < items.Count; i++)
			{
				sb.Append(i == 0 ? "\"" : ",\"").Append(items[i].Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\"");
			}

			return sb.Append("]").ToString();
		}
	}
}
