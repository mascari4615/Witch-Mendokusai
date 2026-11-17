using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 화면을 덮고 걷는 막. 씬 교체 (SceneFader) 와 씬 안 전환 (TransitionView) 공용
	/// - 무늬는 덮기 시작 때 wipeSource 의 UIWipe 에셋. 걷기는 덮은 무늬 그대로 (RandomUIWipe 도 한 쌍은 같은 무늬)
	/// - 한 번 만든 무늬는 보관해 다시 씀 (Random 전환마다 다시 계산하는 멈칫 방지). 수치를 바꾸면 다음 덮기에 다시 만듦
	/// - 진행은 실제 시간, 시간 정지 중에도 동작. 무늬, 색, 길이는 UIWipe 에셋
	/// - 주인이 사라질 때 Dispose (무늬 텍스처 해제)
	/// </summary>
	public sealed class ScreenWipe : VisualElement
	{
		// 멈칫 뒤 첫 프레임 dt 로 한 번에 끝나지 않게 하는 한 프레임 상한
		private const float MAX_STEP_SECONDS = 1f / 30f;
		private const float MILLISECONDS_PER_SECOND = 1000f;

		private sealed class Built
		{
			public IWipeRenderer Renderer;
			public Vector2 Size;
			public int SettingsVersion;
		}

		private readonly Func<UIWipe> wipeSource;
		private readonly Dictionary<SingleUIWipe, Built> built = new();
		private Built current;
		private SingleUIWipe currentWipe;
		private Vector2 size;
		private IVisualElementScheduledItem running;
		private Action pendingDone;
		private int generation;
		private float lastProgress = 1f;
		private bool lastCover;

		/// <summary>0 은 비어 있음, 1 은 완전히 덮음</summary>
		public float Coverage { get; private set; }

		/// <summary>지금 그리는 전환 에셋 (묶음은 고른 결과)</summary>
		public SingleUIWipe CurrentWipe => currentWipe;

		public ScreenWipe(Func<UIWipe> wipeSource)
		{
			this.wipeSource = wipeSource;
			name = "screen-wipe";
			pickingMode = PickingMode.Ignore;
			style.position = Position.Absolute;
			style.left = 0;
			style.top = 0;
			style.right = 0;
			style.bottom = 0;
			style.overflow = Overflow.Hidden;
			RegisterCallback<GeometryChangedEvent>(_ => OnGeometryChanged());
			UseWipe(Resolve(wipeSource()));
			Apply(1f, false);
		}

		/// <summary>덮기 (cover true) 또는 걷기. 끝나거나 다음 Play 에 밀리면 onDone. overrideWipe 를 주면 기본 칸 대신 그 에셋</summary>
		public void Play(bool cover, Action onDone = null, UIWipe overrideWipe = null)
		{
			int myGeneration = Interrupt();
			pendingDone = onDone;
			if (cover)
				UseWipe(Resolve(overrideWipe != null ? overrideWipe : wipeSource()));
			float seconds = cover ? currentWipe.coverSeconds : currentWipe.revealSeconds;
			float elapsed = 0f;
			bool done = false;

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
				Action finished = pendingDone;
				pendingDone = null;
				finished?.Invoke();
			}).Every(0).Until(() => done);
			Apply(0f, cover);
		}

		/// <summary>애니메이션 없이 덮음 또는 비움. 이미 덮여 있으면 그 무늬를 유지</summary>
		public void SetInstant(bool covered)
		{
			Interrupt();
			if (covered && Coverage <= 0f)
				UseWipe(Resolve(wipeSource()));
			Apply(1f, covered);
		}

		/// <summary>보관한 무늬와 텍스처 해제. 주인 OnDestroy 에서</summary>
		public void Dispose()
		{
			Interrupt();
			foreach (Built entry in built.Values)
				entry.Renderer.Detach();
			built.Clear();
			current = null;
			RemoveFromHierarchy();
		}

		// 진행 중 Play 를 멈추고 그 완료 콜백은 바로 부름 (기다리던 쪽이 멈춰 서지 않게)
		private int Interrupt()
		{
			int myGeneration = ++generation;
			running?.Pause();
			running = null;
			Action interrupted = pendingDone;
			pendingDone = null;
			interrupted?.Invoke();
			return myGeneration;
		}

		private static SingleUIWipe Resolve(UIWipe wipe)
		{
			if (wipe == null)
				throw new InvalidOperationException("전환 에셋 칸이 비어 있음 (UIFxSettings 또는 호출한 자리)");
			return wipe.Pick();
		}

		private void UseWipe(SingleUIWipe wipe)
		{
			if (built.TryGetValue(wipe, out Built entry) == false)
			{
				entry = new Built { Renderer = wipe.CreateRenderer(), SettingsVersion = -1 };
				entry.Renderer.Attach(this);
				built[wipe] = entry;
			}
			if (current != null && current != entry)
				current.Renderer.Root.style.display = DisplayStyle.None;
			current = entry;
			currentWipe = wipe;
			entry.Renderer.Root.style.display = DisplayStyle.Flex;
			BuildIfStale(entry);
			entry.Renderer.Draw(lastProgress, lastCover);
		}

		private void BuildIfStale(Built entry)
		{
			if (size.x <= 0f)
				return;
			if (entry.Size == size && entry.SettingsVersion == UIWipe.Version)
				return;
			entry.Size = size;
			entry.SettingsVersion = UIWipe.Version;
			entry.Renderer.Resize(size);
		}

		private void OnGeometryChanged()
		{
			Vector2 newSize = new(layout.width, layout.height);
			if (float.IsNaN(newSize.x) || newSize.x <= 0f || newSize.y <= 0f || newSize == size)
				return;
			size = newSize;
			BuildIfStale(current);
			current.Renderer.Draw(lastProgress, lastCover);
		}

		private void Apply(float progress, bool cover)
		{
			lastProgress = progress;
			lastCover = cover;
			float eased = Mathf.SmoothStep(0f, 1f, progress);
			Coverage = cover ? eased : 1f - eased;
			current.Renderer.Draw(progress, cover);

			bool visible = cover || progress < 1f;
			style.display = visible || Coverage > 0f ? DisplayStyle.Flex : DisplayStyle.None;
			pickingMode = Coverage > 0f ? PickingMode.Position : PickingMode.Ignore;
		}
	}
}
