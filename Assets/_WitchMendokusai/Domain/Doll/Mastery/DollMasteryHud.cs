using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 던전 숙련 막대 HUD (사용자 2026-11-22 22:26 '조각 얼마나 모였는지 보이는 전용 UI', 23:14 '게임답게')
	// 좌우 대칭 두 갈래: [아이콘 마름모] 보석 칸 x 문턱 | 가운데 갈림 표식 | 보석 칸 [아이콘 마름모]. 뒤에 어두운 평행사변형 판
	// 칸 모양은 스킬창 마름모 (RunEquipmentHud) 와 같은 말. 막대 색은 숙련 자료, 나머지 모양과 시간은 DollMasteryHud.uss
	// 먹으면 그 칸이 튐, 차면 그쪽이 맥박과 '변신 가능', 확정하면 반대쪽이 흐려짐
	public class DollMasteryHud : VisualElement
	{
		public const string USS_ROOT = "wm-mastery";
		public const string USS_HIDDEN = USS_ROOT + "--hidden";
		public const string USS_PLATE = USS_ROOT + "__plate";
		public const string USS_SIDE = USS_ROOT + "__side";
		public const string USS_SIDE_MIRROR = USS_SIDE + "--mirror";
		public const string USS_SIDE_READY = USS_SIDE + "--ready";
		public const string USS_SIDE_PULSE = USS_SIDE + "--pulse";
		public const string USS_SIDE_DIM = USS_SIDE + "--dim";
		public const string USS_SIDE_CONFIRMED = USS_SIDE + "--confirmed";
		public const string USS_EMBLEM = USS_ROOT + "__emblem";
		public const string USS_EMBLEM_FRAME = USS_EMBLEM + "-frame";
		public const string USS_ICON_FRAME = USS_ROOT + "__icon-frame";
		public const string USS_ICON = USS_ROOT + "__icon";
		public const string USS_ICON_FLASH = USS_ICON_FRAME + "--flash";
		public const string USS_BODY = USS_ROOT + "__body";
		public const string USS_PIPS = USS_ROOT + "__pips";
		public const string USS_PIP = USS_ROOT + "__pip";
		public const string USS_PIP_FILLED = USS_PIP + "--filled";
		public const string USS_PIP_POP = USS_PIP + "--pop";
		public const string USS_PIP_CORE = USS_PIP + "-core";
		public const string USS_CAPTION = USS_ROOT + "__caption";
		public const string USS_NAME = USS_ROOT + "__name";
		public const string USS_COUNT = USS_ROOT + "__count";
		public const string USS_READY_TAG = USS_ROOT + "__ready-tag";

		private static readonly CustomStyleProperty<int> POP_MILLISECONDS_PROPERTY = new("--wm-mastery-pop-milliseconds");
		private static readonly CustomStyleProperty<int> PULSE_MILLISECONDS_PROPERTY = new("--wm-mastery-pulse-milliseconds");
		private static readonly CustomStyleProperty<Color> PLATE_FILL_PROPERTY = new("--wm-mastery-plate-fill");
		private static readonly CustomStyleProperty<Color> PLATE_EDGE_PROPERTY = new("--wm-mastery-plate-edge");
		private static readonly CustomStyleProperty<float> PLATE_SLANT_PROPERTY = new("--wm-mastery-plate-slant");

		private readonly VisualElement plate = new() { name = "Plate", pickingMode = PickingMode.Ignore };
		private readonly VisualElement emblem = new() { name = "Emblem", pickingMode = PickingMode.Ignore };
		private readonly VisualElement emblemFrame = new() { pickingMode = PickingMode.Ignore };
		private readonly List<Side> sides = new();
		private DollMasteryData shownData;
		private int shownVersion = -1;
		private int[] shownProgress = new int[0];
		private int popMilliseconds;
		private int pulseMilliseconds;
		private Color plateFill;
		private Color plateEdge;
		private float plateSlant;
		private IVisualElementScheduledItem pulseItem;
		private bool pulseOn;

		public DollMasteryHud()
		{
			name = "DollMasteryHud";
			pickingMode = PickingMode.Ignore;
			AddToClassList(USS_ROOT);
			plate.AddToClassList(USS_PLATE);
			plate.generateVisualContent += DrawPlate;
			emblem.AddToClassList(USS_EMBLEM);
			emblemFrame.AddToClassList(USS_EMBLEM_FRAME);
			emblem.Add(emblemFrame);
			RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
		}

		private void OnCustomStyleResolved(CustomStyleResolvedEvent styleEvent)
		{
			// USS 에 값이 없으면 바로 실패. 시간과 판 색을 코드에 박지 않음
			if (styleEvent.customStyle.TryGetValue(POP_MILLISECONDS_PROPERTY, out popMilliseconds) == false)
				throw new System.InvalidOperationException("DollMasteryHud.uss 에 --wm-mastery-pop-milliseconds 없음");
			if (styleEvent.customStyle.TryGetValue(PULSE_MILLISECONDS_PROPERTY, out pulseMilliseconds) == false)
				throw new System.InvalidOperationException("DollMasteryHud.uss 에 --wm-mastery-pulse-milliseconds 없음");
			if (styleEvent.customStyle.TryGetValue(PLATE_FILL_PROPERTY, out plateFill) == false)
				throw new System.InvalidOperationException("DollMasteryHud.uss 에 --wm-mastery-plate-fill 없음");
			if (styleEvent.customStyle.TryGetValue(PLATE_EDGE_PROPERTY, out plateEdge) == false)
				throw new System.InvalidOperationException("DollMasteryHud.uss 에 --wm-mastery-plate-edge 없음");
			if (styleEvent.customStyle.TryGetValue(PLATE_SLANT_PROPERTY, out plateSlant) == false)
				throw new System.InvalidOperationException("DollMasteryHud.uss 에 --wm-mastery-plate-slant 없음");
			plate.MarkDirtyRepaint();
		}

		public void SetVisible(bool isVisible) => EnableInClassList(USS_HIDDEN, isVisible == false);

		// 판 상태 버전이 바뀔 때만 다시 그림. 늘어난 칸은 튀게
		public void Refresh(DollMasteryData data, DollMastery mastery, int version)
		{
			if (version == shownVersion && data == shownData)
				return;
			bool isNewData = data != shownData;
			shownVersion = version;
			EnsureLayout(data, mastery);
			bool anyReady = false;
			for (int track = 0; track < sides.Count; track++)
			{
				int progress = mastery.Progress(track);
				int before = isNewData ? progress : shownProgress[track];
				Side side = sides[track];
				side.Show(progress, mastery.Threshold(track), before, popMilliseconds);
				bool isReady = mastery.IsReady(track);
				bool isConfirmed = mastery.ConfirmedTrack == track;
				bool isOtherConfirmed = mastery.ConfirmedTrack != DollMastery.NO_TRACK && isConfirmed == false;
				side.Root.EnableInClassList(USS_SIDE_READY, isReady && mastery.AcceptsShards);
				side.Root.EnableInClassList(USS_SIDE_CONFIRMED, isConfirmed);
				side.Root.EnableInClassList(USS_SIDE_DIM, isOtherConfirmed);
				anyReady |= isReady && mastery.AcceptsShards;
				shownProgress[track] = progress;
			}
			SetPulse(anyReady);
		}

		public void ResetShown()
		{
			shownVersion = -1;
			shownData = null;
			SetPulse(false);
		}

		// 찬 쪽만 맥박. 켜고 끄는 박자는 USS 시간, 모양은 --pulse 클래스
		private void SetPulse(bool isOn)
		{
			// USS 를 읽기 전 (박자 0) 에는 맥박 없음. 다음 갱신에 다시
			if (isOn && pulseItem == null && pulseMilliseconds > 0)
				pulseItem = schedule.Execute(TogglePulse).Every(pulseMilliseconds);
			else if (isOn == false && pulseItem != null)
			{
				pulseItem.Pause();
				pulseItem = null;
				pulseOn = false;
				foreach (Side side in sides)
					side.Root.RemoveFromClassList(USS_SIDE_PULSE);
			}
		}

		private void TogglePulse()
		{
			pulseOn = pulseOn == false;
			foreach (Side side in sides)
				side.Root.EnableInClassList(USS_SIDE_PULSE, pulseOn && side.Root.ClassListContains(USS_SIDE_READY));
		}

		// 막대가 둘이면 왼쪽, 가운데 표식, 오른쪽 (거울). 그 밖의 수는 차례로
		private void EnsureLayout(DollMasteryData data, DollMastery mastery)
		{
			if (data == shownData)
				return;
			shownData = data;
			Clear();
			sides.Clear();
			Add(plate);
			shownProgress = new int[data.Tracks.Count];
			for (int track = 0; track < data.Tracks.Count; track++)
			{
				if (track == 1)
					Add(emblem);
				Side side = new(data.Tracks[track], mastery.Threshold(track), track % 2 == 1);
				sides.Add(side);
				Add(side.Root);
			}
		}

		// 뒤판: 어두운 평행사변형 (WM 판 결). 기울기와 색은 USS
		private void DrawPlate(MeshGenerationContext context)
		{
			Rect rect = plate.contentRect;
			if (rect.width <= 0f || rect.height <= 0f)
				return;
			Painter2D painter = context.painter2D;
			float slant = plateSlant;
			painter.BeginPath();
			painter.MoveTo(new Vector2(rect.xMin + slant, rect.yMin));
			painter.LineTo(new Vector2(rect.xMax, rect.yMin));
			painter.LineTo(new Vector2(rect.xMax - slant, rect.yMax));
			painter.LineTo(new Vector2(rect.xMin, rect.yMax));
			painter.ClosePath();
			painter.fillColor = plateFill;
			painter.Fill();
			painter.strokeColor = plateEdge;
			painter.lineWidth = 1.5f;
			painter.Stroke();
		}

		private sealed class Side
		{
			public readonly VisualElement Root = new() { pickingMode = PickingMode.Ignore };
			private readonly VisualElement iconFrame = new() { pickingMode = PickingMode.Ignore };
			private readonly VisualElement pipRow = new() { pickingMode = PickingMode.Ignore };
			private readonly List<VisualElement> pips = new();
			private readonly Label count = new() { pickingMode = PickingMode.Ignore };

			public Side(DollMasteryTrackData track, int threshold, bool isMirror)
			{
				Root.AddToClassList(USS_SIDE);
				Root.EnableInClassList(USS_SIDE_MIRROR, isMirror);
				// 막대 색은 자료. 칸과 테두리, 이름이 같은 색을 USS 변수로 받음
				Root.style.color = track.Color;

				iconFrame.AddToClassList(USS_ICON_FRAME);
				iconFrame.style.borderTopColor = track.Color;
				iconFrame.style.borderBottomColor = track.Color;
				iconFrame.style.borderLeftColor = track.Color;
				iconFrame.style.borderRightColor = track.Color;
				VisualElement icon = new() { pickingMode = PickingMode.Ignore };
				icon.AddToClassList(USS_ICON);
				if (track.Icon != null)
					icon.style.backgroundImage = new StyleBackground(track.Icon);
				icon.style.unityBackgroundImageTintColor = track.Color;
				iconFrame.Add(icon);

				VisualElement body = new() { pickingMode = PickingMode.Ignore };
				body.AddToClassList(USS_BODY);
				pipRow.AddToClassList(USS_PIPS);
				for (int i = 0; i < threshold; i++)
				{
					VisualElement pip = new() { pickingMode = PickingMode.Ignore };
					pip.AddToClassList(USS_PIP);
					VisualElement core = new() { pickingMode = PickingMode.Ignore };
					core.AddToClassList(USS_PIP_CORE);
					core.style.backgroundColor = track.Color;
					pip.Add(core);
					pips.Add(pip);
					pipRow.Add(pip);
				}
				VisualElement caption = new() { pickingMode = PickingMode.Ignore };
				caption.AddToClassList(USS_CAPTION);
				Label trackName = new(track.DisplayName) { pickingMode = PickingMode.Ignore };
				trackName.AddToClassList(USS_NAME);
				count.AddToClassList(USS_COUNT);
				Label readyTag = new(DollMasteryHudText.READY) { pickingMode = PickingMode.Ignore };
				readyTag.AddToClassList(USS_READY_TAG);
				caption.Add(trackName);
				caption.Add(count);
				caption.Add(readyTag);
				body.Add(pipRow);
				body.Add(caption);

				Root.Add(iconFrame);
				Root.Add(body);
			}

			public void Show(int progress, int threshold, int before, int popMilliseconds)
			{
				count.text = $"{progress}/{threshold}";
				for (int i = 0; i < pips.Count; i++)
				{
					bool isFilled = i < progress;
					pips[i].EnableInClassList(USS_PIP_FILLED, isFilled);
					if (isFilled && i >= before)
						Pop(pips[i], USS_PIP_POP, popMilliseconds);
				}
				if (progress > before)
					Pop(iconFrame, USS_ICON_FLASH, popMilliseconds);
			}

			// 클래스를 잠깐 켰다 끔. 커지고 줄어드는 움직임은 USS transition
			private static void Pop(VisualElement element, string className, int milliseconds)
			{
				element.AddToClassList(className);
				element.schedule.Execute(() => element.RemoveFromClassList(className)).StartingIn(milliseconds);
			}
		}
	}

	// HUD 글자. 언어 설정 붙이기 전 한 곳에
	public static class DollMasteryHudText
	{
		public const string READY = "변신 가능";
	}
}
