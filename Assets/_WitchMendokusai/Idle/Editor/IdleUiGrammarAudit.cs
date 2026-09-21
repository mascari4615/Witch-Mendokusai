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
		private static readonly string[] KOREAN_ALLOWED = { "doll-name", "tooltip", "note", "feedback", "hero-", "codex", "discovery", "pickup", "row-", "appraise", "advice", "battle-note", "scene-cover" };

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
			int visited = 0;
			Walk(root, element =>
			{
				if (element.resolvedStyle.display == DisplayStyle.None || element.resolvedStyle.visibility == Visibility.Hidden)
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
				}

				return true;
			});

			string line = "{\"state\":\"" + stateName + "\",\"visited\":" + visited
				+ ",\"overflow\":" + Json(overflow) + ",\"offscreen\":" + Json(offscreen)
				+ ",\"gold\":" + Json(gold) + ",\"korean\":" + Json(korean) + ",\"scrolled\":" + Json(scrolled) + "}";
			System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath));
			System.IO.File.AppendAllText(outPath, line + "\n");
			return stateName + " visited=" + visited + " overflow=" + overflow.Count + " offscreen=" + offscreen.Count + " gold=" + gold.Count + " korean=" + korean.Count + " scrolled=" + scrolled.Count;
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
