using UnityEditor;
using UnityEngine.UIElements;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// 연구 성좌 미리보기 창 — 게임을 켜지 않고 성좌를 *눈으로* 본다.
	///
	/// ★ 왜 EditorWindow 인가: 게임 화면(UIDocument)은 Play 중에만 그려진다. 성좌 모양을 손볼 때마다
	///   판을 열어야 하면 확인이 느려서 결국 안 보게 되고, 「컴파일은 됐는데 화면은 아무도 안 본」
	///   상태가 쌓인다. 편집 중에 바로 그려지는 창이면 모양·색·선을 그 자리에서 고칠 수 있다.
	/// ★ 값은 여기서 지어낸다(스테이지 자산을 안 읽는다) — 미리보기가 자산을 건드리면 「보기만 했는데
	///   판이 바뀌는」 일이 생긴다. 모양을 보는 것이 목적이므로 대표값이면 충분하다.
	/// </summary>
	public sealed class TowerDefenseResearchWindow : EditorWindow
	{
		private const int PREVIEW_BRANCHES = 6;
		private const int PREVIEW_RINGS = 5;
		private const float PREVIEW_MAJOR = 0.35f;
		private const float PREVIEW_MINOR = 0.08f;
		private const int PREVIEW_COST = 2;
		private const int PREVIEW_ESSENCE_FROM_RING = 2; // 미리보기도 「안쪽은 자원, 바깥은 정수」를 그대로 보여준다.
		private const int PREVIEW_RESOURCE_COST = 45;

		[MenuItem("WM/TowerDefense/Research Constellation Preview")]
		public static void Open()
		{
			TowerDefenseResearchWindow window = GetWindow<TowerDefenseResearchWindow>();
			window.titleContent = new UnityEngine.GUIContent("연구 성좌");
			window.minSize = new UnityEngine.Vector2(900f, 620f);
			window.Show();
		}

		private void CreateGUI()
		{
			VisualElement host = rootVisualElement;
			host.style.flexGrow = 1;

			TowerDefenseResearchView view = new TowerDefenseResearchView();
			view.Build(host, PREVIEW_BRANCHES, PREVIEW_RINGS, PREVIEW_MAJOR, PREVIEW_MINOR, PREVIEW_COST,
				PREVIEW_ESSENCE_FROM_RING, PREVIEW_RESOURCE_COST);
			view.SetOpen(true);
		}
	}
}
