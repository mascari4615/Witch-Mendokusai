using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 뒤 게임 화면만 흐리게 보이는 젖빛 유리 칸. 흐림 계산은 CustomBlurFeature 의 화면 전체 한 번 재사용
	// 사용: UXML <WitchMendokusai.BlurPanel class="wm-blur" /> 또는 BlurPanel.AttachBehind(panel) (판 맨 뒤 자식)
	// 색 덮개와 세기는 USS 변수 --wm-blur-tint, --wm-blur-strength, --wm-blur-plain (BlurPanel.uss)
	[UxmlElement]
	public partial class BlurPanel : VisualElement
	{
		public const string USS_CLASS = "wm-blur";
		public const string USS_BEHIND = "wm-blur--behind";

		private static readonly CustomStyleProperty<Color> TINT = new("--wm-blur-tint");
		private static readonly CustomStyleProperty<float> STRENGTH = new("--wm-blur-strength");
		private static readonly CustomStyleProperty<Color> PLAIN = new("--wm-blur-plain");

		// 값 정본은 BlurPanel.uss. 못 받으면 덮개 없이 흐림만
		private Color tint = Color.clear;
		private float strength = 1f;
		private Color plain = Color.clear;

		// 붙어 있고 켜진 동안만 흐림 요청과 매 프레임 재그리기. 기본 켜짐
		private bool isActive = true;
		private bool isAttached;
		private bool isRequesting;
		private IVisualElementScheduledItem repaintItem;

		public BlurPanel()
		{
			pickingMode = PickingMode.Ignore;
			AddToClassList(USS_CLASS);
			generateVisualContent += Draw;
			RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
			RegisterCallback<AttachToPanelEvent>(OnAttach);
			RegisterCallback<DetachFromPanelEvent>(OnDetach);
			RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
		}

		/// <summary>판 맨 뒤 자식으로 흐림 칸을 넣어 판 크기 전체를 덮음. 판 배경색은 투명으로 둘 것</summary>
		public static BlurPanel AttachBehind(VisualElement panel)
		{
			BlurPanel blurPanel = new BlurPanel();
			blurPanel.AddToClassList(USS_BEHIND);
			panel.Insert(0, blurPanel);
			return blurPanel;
		}

		/// <summary>흐림 요청 켜기와 끄기. 투명도로만 숨기는 판은 숨길 때 끌 것. 켤 때마다 UI 연출 설정 다시 읽음</summary>
		public void SetActive(bool active)
		{
			if (isActive == active)
				return;
			isActive = active;
			RefreshRequest();
		}

		private void OnStyleResolved(CustomStyleResolvedEvent evt)
		{
			ICustomStyle style = evt.customStyle;
			if (style.TryGetValue(TINT, out Color tintValue))
				tint = tintValue;
			if (style.TryGetValue(STRENGTH, out float strengthValue))
				strength = Mathf.Clamp01(strengthValue);
			if (style.TryGetValue(PLAIN, out Color plainValue))
				plain = plainValue;
			MarkDirtyRepaint();
		}

		private void OnAttach(AttachToPanelEvent evt)
		{
			isAttached = true;
			RefreshRequest();
		}

		private void OnDetach(DetachFromPanelEvent evt)
		{
			isAttached = false;
			RefreshRequest();
		}

		// Add 와 Remove 는 isRequesting 으로 짝. UI 연출 설정은 요청 켜는 순간마다 읽음
		private void RefreshRequest()
		{
			bool shouldRequest = isAttached && isActive && UIEffects.Enabled;
			if (shouldRequest && isRequesting == false)
			{
				isRequesting = true;
				BlurRequest.Add();
			}
			else if (shouldRequest == false && isRequesting)
			{
				isRequesting = false;
				BlurRequest.Remove();
			}

			// 흐린 화면은 매 프레임 바뀌고 조상 이동도 GeometryChanged 로 안 옴. 요청 중에만 매 프레임 다시 그림
			if (isRequesting)
			{
				if (repaintItem == null)
					repaintItem = schedule.Execute(MarkDirtyRepaint).Every(0);
				else
					repaintItem.Resume();
			}
			else
			{
				repaintItem?.Pause();
			}
			MarkDirtyRepaint();
		}

		private void Draw(MeshGenerationContext context)
		{
			float width = layout.width;
			float height = layout.height;
			if (width <= 0f || height <= 0f || panel == null)
				return;

			RenderTexture output = CustomBlurFeature.ActiveOutput;
			if (isRequesting == false || output == null)
			{
				// 흐림 없음 (UI 연출 끔, 출력 미준비). 단색 막만
				DrawQuad(context, width, height, null, Rect.zero, plain);
				return;
			}

			Rect screen = panel.visualTree.worldBound;
			Rect self = worldBound;
			if (screen.width <= 0f || screen.height <= 0f)
				return;

			// 자기 화면 위치만큼 흐린 화면을 잘라 씀. 텍스처 v 는 아래가 0
			float uMin = (self.xMin - screen.xMin) / screen.width;
			float uMax = (self.xMax - screen.xMin) / screen.width;
			float vTop = 1f - (self.yMin - screen.yMin) / screen.height;
			float vBottom = 1f - (self.yMax - screen.yMin) / screen.height;
			Rect uvRect = Rect.MinMaxRect(uMin, vBottom, uMax, vTop);

			DrawQuad(context, width, height, output, uvRect, new Color(1f, 1f, 1f, strength));
			if (tint.a > 0f)
				DrawQuad(context, width, height, null, Rect.zero, tint);
		}

		private static void DrawQuad(MeshGenerationContext context, float width, float height, Texture texture, Rect uvRect, Color color)
		{
			MeshWriteData mesh = context.Allocate(4, 6, texture);
			mesh.SetNextVertex(Corner(0f, 0f, uvRect.xMin, uvRect.yMax, color));
			mesh.SetNextVertex(Corner(width, 0f, uvRect.xMax, uvRect.yMax, color));
			mesh.SetNextVertex(Corner(width, height, uvRect.xMax, uvRect.yMin, color));
			mesh.SetNextVertex(Corner(0f, height, uvRect.xMin, uvRect.yMin, color));
			mesh.SetNextIndex(0);
			mesh.SetNextIndex(1);
			mesh.SetNextIndex(2);
			mesh.SetNextIndex(0);
			mesh.SetNextIndex(2);
			mesh.SetNextIndex(3);
		}

		private static Vertex Corner(float x, float y, float u, float v, Color color) => new()
		{
			position = new Vector3(x, y, Vertex.nearZ),
			tint = color,
			uv = new Vector2(u, v),
		};
	}
}
