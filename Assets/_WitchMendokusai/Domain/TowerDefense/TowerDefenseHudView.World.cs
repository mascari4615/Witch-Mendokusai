using UnityEngine;
using Vector2 = WitchMendokusai.Numerics.Vector2;
// ★ 좌표는 판정 쪽 (TASK-WM-214) — 엔진으로 나갈 땐 자동, 엔진에서 받을 땐 캐스트.
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// TowerDefenseHudView 의 World 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseHudView.cs 를 본다.
	public partial class TowerDefenseHudView
	{
		// 인형 이름표 — 노드 배수표와 같은 방식(월드→화면 투영)이지만 대상이 다르므로 목록을 나눈다.
		private readonly System.Collections.Generic.List<Label> dollLabelViews = new();

		/// <summary> 「다음 웨이브: 돌진 3 · 방패 1」 — 매치가 실제 스폰에 쓰는 그 계산을 그대로 부른다. </summary>
		private string BuildWavePreview(TowerDefenseMatch match)
		{
			int previewWave = match.Phase == TowerDefensePhase.Assault ? match.WaveIndex + 1 : match.WaveIndex;
			match.ComposeWave(previewWave, compositionBuffer);

			// 웨이브 성격은 색까지 바꿔 예고한다 — 「무엇이 오는가」를 한눈에 알아야 대비가 성립한다.
			TowerDefenseWaveEventKind previewEvent = match.WaveEventAt(previewWave);
			// 판정 색 → 엔진 색 → 스타일 색. 사용자 정의 변환은 두 번 연달아 안 걸리므로 가운데를 명시한다 (TASK-WM-214).
			nextWaveValue.style.color = TowerDefenseWaveEvent.DisplayColor(previewEvent).ToUnity();
			string eventPrefix = previewEvent == TowerDefenseWaveEventKind.None
				? string.Empty
				: "《" + TowerDefenseWaveEvent.DisplayName(previewEvent) + "》 ";

			// 적응은 반드시 보여야 한다 — 안 보이면 플레이어는 자기 포탑이 고장 났다고 여긴다.
			string adaptationNote = TowerDefenseAdaptation.Describe(match.Adaptation);
			if (adaptationNote.Length > 0)
				eventPrefix += "[" + adaptationNote + "] ";

			int archetypeCount = match.EnemyArchetypeCount;
			if (archetypeCount <= 0)
				return eventPrefix + compositionBuffer.Count + "기";

			if (archetypeCountBuffer.Length < archetypeCount)
				archetypeCountBuffer = new int[archetypeCount];

			TowerDefenseWaveComposer.CountByArchetype(compositionBuffer, archetypeCount, archetypeCountBuffer);

			string preview = string.Empty;
			for (int index = 0; index < archetypeCount; index++)
			{
				if (archetypeCountBuffer[index] <= 0)
					continue;
				TowerDefenseEnemyArchetype archetype = match.EnemyArchetypeAt(index);
				if (archetype == null)
					continue;
				if (preview.Length > 0)
					preview += " · ";
				preview += archetype.DisplayName + " " + archetypeCountBuffer[index];
			}

			return eventPrefix + (preview.Length > 0 ? preview : compositionBuffer.Count + "기");
		}

		/// <summary>
		/// 월드에 붙는 UI 를 그린다 — **카메라가 그 프레임의 자리로 옮겨간 뒤에** 불려야 한다.
		///
		/// ★ 사용자 실증: "WASD로 움직이면 특히 심하다 위치 차이가". 카메라는 LateUpdate 에 움직이는데
		///   UI 를 Update 에서 그리면 *한 프레임 전 카메라*로 자리를 계산한다 — 멈춰 있으면 안 보이고,
		///   화면을 밀수록 그 한 프레임만큼 이름표가 뒤로 끌린다. 좌표 변환을 아무리 고쳐도
		///   **읽는 시점이 틀리면** 계속 어긋난다(그래서 앞 고침만으로는 안 잡혔다).
		/// </summary>
		public void TickWorldAnchored()
		{
			if (worldTickMatch == null)
				return;

			UpdateNodeLabels(worldTickMatch, worldTickStage);
			UpdateDollLabels(worldTickMatch);
			UpdateInvasionWarning(worldTickMatch);
			UpdateAlerts(worldTickMatch);
		}

		// 화면 밖에서 난 일을 가장자리에 붙여 알리는 표식.
		private readonly System.Collections.Generic.List<Label> alertMarks = new();

		/// <summary>
		/// 「지금 어디서 무슨 일이 났나」를 화면에 세운다 (TASK-WM-194).
		///
		/// ★ 이 장르 최대 불만이 「무슨 일이 났는지 안 알려준다」였다(레퍼런스 조사). 자리가 화면 밖이면
		///   가장자리에 붙인다 — 그래야 「그쪽을 봐야 한다」가 전달된다. 안 붙이면 카메라를 이미 그쪽에
		///   두고 있던 사람만 알림을 본다 = 알림이 필요 없는 사람만 본다.
		/// </summary>
		private void UpdateAlerts(TowerDefenseMatch match)
		{
			Camera camera = ViewCameraResolver.Current;
			System.Collections.Generic.IReadOnlyList<TowerDefenseAlerts.Alert> active = match.Alerts;

			while (alertMarks.Count < TowerDefenseAlerts.MAX_ALERTS)
			{
				Label mark = new Label(string.Empty);
				mark.name = "AlertMark"; // 하네스가 「칸은 있는데 글자가 없나」를 가릴 수 있어야 한다.
				mark.style.position = Position.Absolute;
				mark.style.fontSize = TEXT_SMALL;
				mark.style.unityFontStyleAndWeight = FontStyle.Bold;
				mark.style.unityTextAlign = TextAnchor.MiddleCenter;
				mark.style.color = new Color(1f, 0.9f, 0.6f, 1f);
				mark.style.backgroundColor = new Color(0.45f, 0.1f, 0.12f, 0.92f);
				mark.style.paddingLeft = 8;
				mark.style.paddingRight = 8;
				mark.style.paddingTop = 3;
				mark.style.paddingBottom = 3;
				mark.pickingMode = PickingMode.Ignore;
				worldLabelLayer.Add(mark);
				alertMarks.Add(mark);
			}

			Rect panelBox = worldLabelLayer.panel != null && worldLabelLayer.panel.visualTree != null
				? worldLabelLayer.panel.visualTree.worldBound
				: Rect.zero;

			for (int index = 0; index < alertMarks.Count; index++)
			{
				Label mark = alertMarks[index];
				if (camera == null || index >= active.Count)
				{
					mark.style.display = DisplayStyle.None;
					continue;
				}

				Vector3 screenPosition = camera.WorldToScreenPoint(active[index].Position.ToUnity()).ToSim();
				if (screenPosition.z <= 0f)
				{
					// 카메라 뒤쪽이면 그대로 두면 반대편에 찍힌다 — 좌우만 뒤집어 가장자리에 붙인다.
					screenPosition.x = Screen.width - screenPosition.x;
					screenPosition.y = Screen.height - screenPosition.y;
				}

				Vector2 point = ClampToPanel(ToPanel(mark, screenPosition), panelBox, 70f);
				mark.style.display = DisplayStyle.Flex;
				mark.text = "❗ " + active[index].Label;
				mark.style.left = point.x - 60f;
				mark.style.top = point.y - 10f;
			}
		}

		// 다음 파도가 들어올 자리에 세우는 경고 표식 + 그 방향을 말하는 글자 하나.
		private readonly System.Collections.Generic.List<Label> invasionMarks = new();
		private readonly System.Collections.Generic.List<Vector3> invasionPoints = new();
		private Label invasionDirectionLabel;

		/// <summary>
		/// 다음 파도가 **어디로** 오는지 미리 보여준다 (TASK-WM-194, 데아빌 레퍼런스).
		///
		/// ★ 왜 이 방식인가: 파도 번호·남은 시간 같은 숫자를 판 위에 늘어놓지 않기로 했으므로
		///   (사용자 지시), 예고는 *자리*와 *말*로만 한다 — 들어올 테두리에 표식이 서고 방위를 말한다.
		///   표식이 서는 자리는 스폰과 **같은 함수**가 계산하므로 화면과 실제가 갈라질 수 없다.
		/// ★ 화면 밖으로 나가면 가장자리에 붙인다 — 안 그러면 카메라를 그쪽으로 돌린 사람만 예고를 본다.
		/// </summary>
		private void UpdateInvasionWarning(TowerDefenseMatch match)
		{
			Camera camera = ViewCameraResolver.Current;
			match.CollectNextInvasionPoints(invasionPoints);

			bool show = camera != null && invasionPoints.Count > 0 && match.Outcome == TowerDefenseOutcome.InProgress;

			while (invasionMarks.Count < invasionPoints.Count)
			{
				Label mark = new Label("▼");
				mark.style.position = Position.Absolute;
				mark.style.fontSize = 26;
				mark.style.unityTextAlign = TextAnchor.MiddleCenter;
				mark.pickingMode = PickingMode.Ignore;
				worldLabelLayer.Add(mark);
				invasionMarks.Add(mark);
			}

			if (invasionDirectionLabel == null)
			{
				invasionDirectionLabel = new Label(string.Empty);
				invasionDirectionLabel.style.position = Position.Absolute;
				invasionDirectionLabel.style.fontSize = TEXT_TITLE;
				invasionDirectionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
				invasionDirectionLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
				invasionDirectionLabel.pickingMode = PickingMode.Ignore;
				worldLabelLayer.Add(invasionDirectionLabel);
			}

			// 숨쉬듯 밝아졌다 어두워진다 — 가만히 있는 표식은 지형 장식으로 읽혀 경고가 안 된다.
			float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f));
			Color warning = new Color(1f, 0.35f, 0.32f, pulse);

			Rect panelBox = worldLabelLayer.panel != null && worldLabelLayer.panel.visualTree != null
				? worldLabelLayer.panel.visualTree.worldBound
				: Rect.zero;

			for (int index = 0; index < invasionMarks.Count; index++)
			{
				Label mark = invasionMarks[index];
				if (show == false || index >= invasionPoints.Count)
				{
					mark.style.display = DisplayStyle.None;
					continue;
				}

				Vector3 screenPosition = camera.WorldToScreenPoint(invasionPoints[index].ToUnity()).ToSim();
				if (screenPosition.z <= 0f)
				{
					mark.style.display = DisplayStyle.None;
					continue;
				}

				Vector2 point = ClampToPanel(ToPanel(mark, screenPosition), panelBox, 24f);
				mark.style.display = DisplayStyle.Flex;
				mark.style.color = warning;
				mark.style.left = point.x - 13f;
				mark.style.top = point.y - 13f;
			}

			if (show == false)
			{
				invasionDirectionLabel.style.display = DisplayStyle.None;
				return;
			}

			// 글자는 토막 한가운데에 하나만 — 표식마다 붙이면 판이 글자로 덮인다.
			Vector3 middleScreen = camera.WorldToScreenPoint(invasionPoints[invasionPoints.Count / 2].ToUnity()).ToSim();
			if (middleScreen.z <= 0f)
			{
				invasionDirectionLabel.style.display = DisplayStyle.None;
				return;
			}

			Vector2 middle = ClampToPanel(ToPanel(invasionDirectionLabel, middleScreen), panelBox, 60f);
			invasionDirectionLabel.style.display = DisplayStyle.Flex;
			// ★ 「무엇이」 + 「어디서」 — 방향만으로는 어떤 대비를 할지 못 정한다(떼거리는 광역, 정예는 관통).
			invasionDirectionLabel.text = match.NextWaveEventPhrase() + match.NextInvasionDirectionName() + "에서 온다";
			invasionDirectionLabel.style.color = warning;
			invasionDirectionLabel.style.left = middle.x - 60f;
			invasionDirectionLabel.style.top = middle.y - 44f;
		}

		/// <summary> 판 밖으로 나간 자리를 가장자리에 붙인다 — 경고는 카메라를 어디로 돌리든 보여야 한다. </summary>
		private static Vector2 ClampToPanel(Vector2 point, Rect panelBox, float margin)
		{
			if (panelBox.width <= 0f || panelBox.height <= 0f)
				return point;

			return new Vector2(
				Mathf.Clamp(point.x, margin, panelBox.width - margin),
				Mathf.Clamp(point.y, margin, panelBox.height - margin));
		}

		private void UpdateNodeLabels(TowerDefenseMatch match, TowerDefenseStageSO stage)
		{
			System.Collections.Generic.IReadOnlyList<Vector3> nodes = match.ActiveResourceNodeLocalPositions;
			Camera camera = ViewCameraResolver.Current;
			Transform stageRoot = match.StageRoot;

			while (worldLabels.Count < nodes.Count)
			{
				Label label = new Label(string.Empty);
				label.style.position = Position.Absolute;
				label.style.fontSize = 15;
				label.style.color = new Color(1f, 0.86f, 0.35f, 1f);
				label.style.unityTextAlign = TextAnchor.MiddleCenter;
				label.pickingMode = PickingMode.Ignore;
				worldLabelLayer.Add(label);
				worldLabels.Add(label);
			}

			for (int index = 0; index < worldLabels.Count; index++)
			{
				Label label = worldLabels[index];
				if (index >= nodes.Count || camera == null || stageRoot == null)
				{
					label.style.display = DisplayStyle.None;
					continue;
				}

				Vector3 worldPosition = stageRoot.TransformPoint(nodes[index].ToUnity()).ToSim();

				// 아직 못 가본 자리의 벌이를 알려주면 시야가 무의미해진다 — 밝혔던 곳만 숫자를 보여준다.
				if (match.IsExploredAt(worldPosition) == false)
				{
					label.style.display = DisplayStyle.None;
					continue;
				}

				Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition.ToUnity()).ToSim();
				if (screenPosition.z <= 0f)
				{
					label.style.display = DisplayStyle.None;
					continue;
				}

				label.style.display = DisplayStyle.Flex;
				label.text = "×" + match.NodeIncomeMultiplierAt(index).ToString("0.0");
				Vector2 panelPoint = ToPanel(label, screenPosition);
				label.style.left = panelPoint.x - 22f;
				label.style.top = panelPoint.y - 34f;
			}
		}

		/// <summary>
		/// 화면 좌표(카메라가 준 것)를 *UI 판 좌표*로 옮긴다.
		///
		/// ★ 이걸 안 거치면 월드 UI 가 유닛에서 어긋난다 (사용자 실증: "영웅 유닛 위치랑 영웅 유닛
		///   UI 위치 다르다고"). 카메라는 **화면 픽셀**(1920×1080)로 답하는데 UI 는 자기
		///   **논리 픽셀**(배율이 1 이 아니면 1422×800 같은 값)로 자리를 잡는다. 두 자를 섞으면
		///   배율만큼 밀리고, 원점에서 멀수록 더 벌어진다 — 「전체적으로 밀렸다」가 그 그림이다.
		///   화면 크기로 y 를 뒤집던 것도 여기서 함께 처리된다(판이 알아서 뒤집는다).
		/// </summary>
		private static Vector2 ToPanel(VisualElement element, Vector3 screenPosition)
		{
			// ★ 판의 크기로 재고 세로는 뒤집는다. 카메라는 **아래가 0**인 화면 좌표를 주는데
			//   `style.top` 은 **위가 0**이다. 뒤집지 않으면 이름표가 세로로 거울처럼 반대편에 붙는다.
			//   실측(같은 점): 화면 985,561 → 뒤집기 전 729,416 / 뒤집은 뒤 729,384 — 416+384 = 800(판 높이).
			//   즉 딱 판 높이만큼 대칭된 자리였다. 가로는 맞으니 「전체적으로 밀렸다」로 보였다.
			// ★ 변환 함수(ScreenToPanel)도 아래가 0인 값을 돌려준다 — 그걸 그대로 top 에 넣은 것이 병이었다.
			Rect box = element?.panel?.visualTree != null ? element.panel.visualTree.worldBound : Rect.zero;
			float panelWidth = box.width > 0f ? box.width : Screen.width;
			float panelHeight = box.height > 0f ? box.height : Screen.height;
			float screenWidth = Mathf.Max(1f, Screen.width);
			float screenHeight = Mathf.Max(1f, Screen.height);

			return new Vector2(
				screenPosition.x * panelWidth / screenWidth,
				(screenHeight - screenPosition.y) * panelHeight / screenHeight);
		}

		/// <summary>
		/// 인형 머리 위 이름표 — 「광역 포탑」이 아니라 「비올라」가 서 있어야 판다·잃는다에 무게가 생긴다.
		/// 안 밝힌 자리는 띄우지 않는다(시야 밖의 것을 화면이 알려주면 시야가 무의미해진다).
		/// </summary>
		private void UpdateDollLabels(TowerDefenseMatch match)
		{
			System.Collections.Generic.IReadOnlyList<TowerDefenseDollLabel> dolls = match.DollLabels;
			Camera camera = ViewCameraResolver.Current;

			while (dollLabelViews.Count < dolls.Count)
			{
				Label label = new Label(string.Empty);
				label.style.position = Position.Absolute;
				label.style.fontSize = 12;
				label.style.unityTextAlign = TextAnchor.MiddleCenter;
				label.pickingMode = PickingMode.Ignore;
				worldLabelLayer.Add(label);
				dollLabelViews.Add(label);

				VisualElement bar = TowerDefenseProgressBar.Create();
				worldLabelLayer.Add(bar);
				dollBarViews.Add(bar);
			}

			for (int index = 0; index < dollLabelViews.Count; index++)
			{
				Label label = dollLabelViews[index];
				VisualElement bar = dollBarViews[index];
				if (index >= dolls.Count || camera == null)
				{
					label.style.display = DisplayStyle.None;
					bar.style.display = DisplayStyle.None;
					continue;
				}

				TowerDefenseDollLabel doll = dolls[index];
				Vector3 screenPosition = camera.WorldToScreenPoint(doll.Anchor.position).ToSim();
				if (screenPosition.z <= 0f || match.IsExploredAt(doll.Anchor.position.ToSim()) == false)
				{
					label.style.display = DisplayStyle.None;
					bar.style.display = DisplayStyle.None;
					continue;
				}

				label.style.display = DisplayStyle.Flex;
				label.text = doll.Text;
				label.style.color = doll.Tint;
				Vector2 panelPoint = ToPanel(label, screenPosition);
				label.style.left = panelPoint.x - 40f;
				label.style.top = panelPoint.y + 12f;
				label.style.width = 80;

				bar.style.display = DisplayStyle.Flex;
				Vector2 barPoint = ToPanel(bar, screenPosition);
				bar.style.left = barPoint.x - TowerDefenseProgressBar.WIDTH * 0.5f;
				bar.style.top = barPoint.y + 4f;
				TowerDefenseProgressBar.SetRatio(bar, doll.ReadyRatio, doll.Working);
			}
		}
	}
}
