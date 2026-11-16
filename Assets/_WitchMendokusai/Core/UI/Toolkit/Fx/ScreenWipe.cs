using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 화면을 덮고 걷는 막. 씬 교체 (SceneFader) 와 씬 안 전환 (TransitionView) 공용
	/// - Fade: 단색 막의 투명도
	/// - Diamond: 마름모 칸이 왼쪽 위에서 대각선으로 커지며 덮고, 같은 방향으로 작아지며 걷힘
	/// 진행은 실제 시간, 시간 정지 중에도 동작. 수치는 UIFxSettings
	/// </summary>
	public sealed class ScreenWipe : VisualElement
	{
		private const float DIAMOND_ROTATION_DEGREES = 45f;
		// 멈칫 뒤 첫 프레임 dt 로 한 번에 끝나지 않게 하는 한 프레임 상한
		private const float MAX_STEP_SECONDS = 1f / 30f;
		private const float MILLISECONDS_PER_SECOND = 1000f;

		private readonly List<VisualElement> tiles = new();
		private readonly List<float> tileOrder = new();
		private Vector2 builtSize;
		private IVisualElementScheduledItem running;
		private int generation;

		/// <summary>0 은 비어 있음, 1 은 완전히 덮음</summary>
		public float Coverage { get; private set; }

		private static UIFxSettings Settings => UIFxSettings.Instance;

		public ScreenWipe()
		{
			name = "screen-wipe";
			pickingMode = PickingMode.Ignore;
			style.position = Position.Absolute;
			style.left = 0;
			style.top = 0;
			style.right = 0;
			style.bottom = 0;
			style.overflow = Overflow.Hidden;
			RegisterCallback<GeometryChangedEvent>(_ => RebuildIfNeeded());
			Apply(0f, true);
		}

		/// <summary>덮기 (cover true) 또는 걷기. 끝나면 onDone</summary>
		public void Play(bool cover, Action onDone = null)
		{
			int myGeneration = ++generation;
			running?.Pause();
			float seconds = cover ? Settings.wipeCoverSeconds : Settings.wipeRevealSeconds;
			float elapsed = 0f;
			bool done = false;
			RebuildIfNeeded();

			running = schedule.Execute(timer =>
			{
				if (myGeneration != generation)
				{
					done = true;
					return;
				}
				elapsed += Mathf.Min(timer.deltaTime / MILLISECONDS_PER_SECOND, MAX_STEP_SECONDS);
				float progress = seconds <= 0f ? 1f : Mathf.Clamp01(elapsed / seconds);
				Apply(progress, cover);
				if (progress < 1f)
					return;
				done = true;
				running = null;
				onDone?.Invoke();
			}).Every(0).Until(() => done);
			Apply(0f, cover);
		}

		/// <summary>애니메이션 없이 덮음 또는 비움</summary>
		public void SetInstant(bool covered)
		{
			generation++;
			running?.Pause();
			running = null;
			RebuildIfNeeded();
			Apply(1f, covered);
		}

		private void Apply(float progress, bool cover)
		{
			Color color = Settings.wipeColor;
			bool diamond = Settings.wipeStyle == ScreenWipeStyle.Diamond && tiles.Count > 0;
			float eased = Mathf.SmoothStep(0f, 1f, progress);
			Coverage = cover ? eased : 1f - eased;

			if (diamond)
			{
				style.backgroundColor = Color.clear;
				float spread = Settings.wipeSpread;
				for (int i = 0; i < tiles.Count; i++)
				{
					float local = Mathf.Clamp01(progress * (1f + spread) - tileOrder[i] * spread);
					float grown = cover ? EaseOutBack(local) : 1f - EaseInQuad(local);
					float scale = Mathf.Max(0f, grown) * Settings.wipeTileGrowth;
					tiles[i].style.scale = new Scale(new Vector3(scale, scale, 1f));
					tiles[i].style.backgroundColor = color;
				}
			}
			else
			{
				color.a *= Coverage;
				style.backgroundColor = color;
				foreach (VisualElement tile in tiles)
					tile.style.scale = new Scale(Vector3.zero);
			}

			bool visible = cover || progress < 1f;
			style.display = visible || Coverage > 0f ? DisplayStyle.Flex : DisplayStyle.None;
			pickingMode = Coverage > 0f ? PickingMode.Position : PickingMode.Ignore;
		}

		private void RebuildIfNeeded()
		{
			Vector2 size = new(layout.width, layout.height);
			if (float.IsNaN(size.x) || size.x <= 0f || size.y <= 0f || size == builtSize)
				return;
			builtSize = size;

			foreach (VisualElement tile in tiles)
				tile.RemoveFromHierarchy();
			tiles.Clear();
			tileOrder.Clear();

			float cell = Mathf.Max(8f, Settings.wipeCellSize);
			int columns = Mathf.CeilToInt(size.x / cell) + 1;
			int rows = Mathf.CeilToInt(size.y / cell) + 1;
			float maxOrder = Mathf.Max(1, columns + rows - 2);
			for (int row = 0; row < rows; row++)
			{
				for (int column = 0; column < columns; column++)
				{
					VisualElement tile = new() { pickingMode = PickingMode.Ignore };
					tile.style.position = Position.Absolute;
					tile.style.width = cell;
					tile.style.height = cell;
					tile.style.left = column * cell - cell * 0.5f;
					tile.style.top = row * cell - cell * 0.5f;
					tile.style.rotate = new UnityEngine.UIElements.Rotate(new UnityEngine.UIElements.Angle(DIAMOND_ROTATION_DEGREES, AngleUnit.Degree));
					tile.style.scale = new Scale(Vector3.zero);
					Add(tile);
					tiles.Add(tile);
					tileOrder.Add((column + row) / maxOrder);
				}
			}
		}

		private static float EaseOutBack(float progress)
		{
			const float OVERSHOOT = 1.4f;
			float shifted = progress - 1f;
			return 1f + (OVERSHOOT + 1f) * shifted * shifted * shifted + OVERSHOOT * shifted * shifted;
		}

		private static float EaseInQuad(float progress) => progress * progress;
	}
}
