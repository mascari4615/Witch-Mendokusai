using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 던전 숙련 막대 HUD. 인물 자원이라 왼쪽 아래 체력 띠 위, 인물 이름 바로 위 한 줄 (사용자 2026-11-23 00:26 '하단바 너무 시야를 잡아먹어')
	// 막대마다 [막대 색 아이콘] + 문턱 수만큼 작은 마름모 보석. 글자 없음 (찬 순간은 머리 위 글자, DollMasteryRun.Pickup)
	// 먹으면 그 보석과 아이콘이 튐, 차면 그쪽 아이콘이 빛나며 맥박, 확정하면 반대쪽 흐림. 모양과 시간은 DollMasteryHud.uss
	public class DollMasteryHud : VisualElement
	{
		public const string USS_ROOT = "wm-mastery";
		public const string USS_HIDDEN = USS_ROOT + "--hidden";
		public const string USS_SIDE = USS_ROOT + "__side";
		public const string USS_SIDE_READY = USS_SIDE + "--ready";
		public const string USS_SIDE_PULSE = USS_SIDE + "--pulse";
		public const string USS_SIDE_DIM = USS_SIDE + "--dim";
		public const string USS_SEPARATOR = USS_ROOT + "__separator";
		public const string USS_ICON = USS_ROOT + "__icon";
		public const string USS_ICON_FLASH = USS_ICON + "--flash";
		public const string USS_PIP = USS_ROOT + "__pip";
		public const string USS_PIP_FILLED = USS_PIP + "--filled";
		public const string USS_PIP_POP = USS_PIP + "--pop";

		private static readonly CustomStyleProperty<int> POP_MILLISECONDS_PROPERTY = new("--wm-mastery-pop-milliseconds");
		private static readonly CustomStyleProperty<int> PULSE_MILLISECONDS_PROPERTY = new("--wm-mastery-pulse-milliseconds");

		private readonly List<Side> sides = new();
		private DollMasteryData shownData;
		private int shownVersion = -1;
		private int[] shownProgress = new int[0];
		private int popMilliseconds;
		private int pulseMilliseconds;
		private IVisualElementScheduledItem pulseItem;
		private bool pulseOn;

		public DollMasteryHud()
		{
			name = "DollMasteryHud";
			pickingMode = PickingMode.Ignore;
			AddToClassList(USS_ROOT);
			RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
		}

		private void OnCustomStyleResolved(CustomStyleResolvedEvent styleEvent)
		{
			// USS 에 값이 없으면 바로 실패. 시간을 코드에 박지 않음
			if (styleEvent.customStyle.TryGetValue(POP_MILLISECONDS_PROPERTY, out popMilliseconds) == false)
				throw new System.InvalidOperationException("DollMasteryHud.uss 에 --wm-mastery-pop-milliseconds 없음");
			if (styleEvent.customStyle.TryGetValue(PULSE_MILLISECONDS_PROPERTY, out pulseMilliseconds) == false)
				throw new System.InvalidOperationException("DollMasteryHud.uss 에 --wm-mastery-pulse-milliseconds 없음");
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
				side.Show(progress, before, popMilliseconds);
				bool isReady = mastery.IsReady(track) && mastery.AcceptsShards;
				bool isOtherConfirmed = mastery.ConfirmedTrack != DollMastery.NO_TRACK && mastery.ConfirmedTrack != track;
				side.Root.EnableInClassList(USS_SIDE_READY, isReady);
				side.Root.EnableInClassList(USS_SIDE_DIM, isOtherConfirmed);
				anyReady |= isReady;
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

		// 찬 쪽만 맥박. 켜고 끄는 박자는 USS 시간, 모양은 --pulse 클래스. USS 를 읽기 전 (박자 0) 에는 다음 갱신에
		private void SetPulse(bool isOn)
		{
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

		private void EnsureLayout(DollMasteryData data, DollMastery mastery)
		{
			if (data == shownData)
				return;
			shownData = data;
			Clear();
			sides.Clear();
			shownProgress = new int[data.Tracks.Count];
			for (int track = 0; track < data.Tracks.Count; track++)
			{
				if (track > 0)
				{
					VisualElement separator = new() { pickingMode = PickingMode.Ignore };
					separator.AddToClassList(USS_SEPARATOR);
					Add(separator);
				}
				Side side = new(data.Tracks[track], mastery.Threshold(track));
				sides.Add(side);
				Add(side.Root);
			}
		}

		private sealed class Side
		{
			public readonly VisualElement Root = new() { pickingMode = PickingMode.Ignore };
			private readonly VisualElement icon = new() { pickingMode = PickingMode.Ignore };
			private readonly List<VisualElement> pips = new();
			private readonly Color trackColor;

			public Side(DollMasteryTrackData track, int threshold)
			{
				trackColor = track.Color;
				Root.AddToClassList(USS_SIDE);
				icon.AddToClassList(USS_ICON);
				if (track.Icon != null)
					icon.style.backgroundImage = new StyleBackground(track.Icon);
				// 막대 색은 자료 (아이콘 물들임, 찬 보석). 나머지는 USS
				icon.style.unityBackgroundImageTintColor = trackColor;
				Root.Add(icon);
				for (int i = 0; i < threshold; i++)
				{
					VisualElement pip = new() { pickingMode = PickingMode.Ignore };
					pip.AddToClassList(USS_PIP);
					pips.Add(pip);
					Root.Add(pip);
				}
			}

			public void Show(int progress, int before, int popMilliseconds)
			{
				for (int i = 0; i < pips.Count; i++)
				{
					bool isFilled = i < progress;
					pips[i].EnableInClassList(USS_PIP_FILLED, isFilled);
					// 찬 칸만 막대 색. 빈 칸 색은 USS
					pips[i].style.backgroundColor = isFilled ? new StyleColor(trackColor) : new StyleColor(StyleKeyword.Null);
					if (isFilled && i >= before)
						Pop(pips[i], USS_PIP_POP, popMilliseconds);
				}
				if (progress > before)
					Pop(icon, USS_ICON_FLASH, popMilliseconds);
			}

			// 클래스를 잠깐 켰다 끔. 커지고 줄어드는 움직임은 USS transition
			private static void Pop(VisualElement element, string className, int milliseconds)
			{
				element.AddToClassList(className);
				element.schedule.Execute(() => element.RemoveFromClassList(className)).StartingIn(milliseconds);
			}
		}
	}
}
