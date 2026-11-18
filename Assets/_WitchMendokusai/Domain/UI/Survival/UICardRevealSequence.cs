using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// 카드 등장 슬롯 연출 묶음. 왼쪽부터 차례로 멈춤, 아무 입력이면 전부 즉시 멈춤
	public sealed class UICardRevealSequence : MonoBehaviour
	{
		private readonly List<CardReel> reels = new();
		private readonly Dictionary<Image, UISlotRays> raysByIcon = new();
		private CardRevealStyle style;
		private float nextTickAt;
		private float choiceUnlockAt;
		private int startedFrame;
		private bool waitPointerRelease;
		private int pointerReleasedFrame = -1;

		public bool IsRevealing { get; private set; }
		// 회전 중, 건너뛰기 직후, 건너뛴 클릭을 떼는 프레임까지 고르기 입력 무시
		public bool BlocksChoice => IsRevealing || Time.unscaledTime < choiceUnlockAt || waitPointerRelease || Time.frameCount <= pointerReleasedFrame;
		// 회전이 끝난 순간. 자연 멈춤과 건너뛰기 모두
		public event Action Finished = delegate { };

		public static UICardRevealSequence AttachTo(GameObject panel) => panel.AddComponent<UICardRevealSequence>();

		// 회전 후보 추가. 같은 카드 중복 제외
		public static void AddSymbol(List<CardRevealSymbol> symbols, HashSet<int> addedIds, CardData card)
		{
			if (card != null && card.Sprite != null && addedIds.Add(card.ID))
				symbols.Add(new CardRevealSymbol(card.Sprite, card.Name));
		}

		// 등급 규칙 한 곳. 영웅이나 진화 카드는 최고 등급
		public static CardRevealTier TierOf(CardData card, bool isEvolution = false)
		{
			if (isEvolution || card.Rarity == CardRarity.Epic || card.UIKind == SurvivalUIKind.Advanced)
				return CardRevealTier.Epic;
			return card.Rarity == CardRarity.Rare ? CardRevealTier.Rare : CardRevealTier.Common;
		}

		public void Play(IReadOnlyList<CardRevealEntry> entries, IReadOnlyList<CardRevealSymbol> candidates, CardRevealStyle revealStyle)
		{
			Cancel();
			if (UIEffects.Enabled == false || entries.Count == 0)
				return;
			style = revealStyle;
			PruneDestroyedRays();
			List<CardRevealSymbol> symbols = BuildSymbols(candidates);
			float now = Time.unscaledTime;
			for (int i = 0; i < entries.Count; i++)
			{
				CardRevealEntry entry = entries[i];
				float stopAt = now + style.FirstStopDelay + i * style.StopInterval + (i == entries.Count - 1 ? style.LastStopExtraDelay : 0f);
				int offset = symbols.Count > 0 ? i * symbols.Count / entries.Count : 0;
				reels.Add(new CardReel(entry, style, symbols, offset, now, stopAt, RaysFor(entry.Icon)));
			}
			IsRevealing = true;
			choiceUnlockAt = 0f;
			nextTickAt = now;
			startedFrame = Time.frameCount;
		}

		// 연출 없이 원래 값 복구. 다음 Set 전, 패널 닫힘
		public void Cancel()
		{
			foreach (CardReel reel in reels)
			{
				if (reel.IsAlive == false)
					continue;
				reel.ResetInstant();
				reel.HideRays();
			}
			reels.Clear();
			IsRevealing = false;
		}

		// 고르기 버튼 입력 관문. 막힌 입력은 건너뛰기로 소비
		public bool ConsumeBlockedChoice()
		{
			if (BlocksChoice == false)
				return false;
			SkipAll();
			return true;
		}

		public void SkipAll()
		{
			if (IsRevealing == false)
				return;
			CardRevealTier best = CardRevealTier.Common;
			bool stoppedAny = false;
			foreach (CardReel reel in reels)
			{
				if (reel.IsSpinning == false)
					continue;
				stoppedAny = true;
				if (reel.Tier > best)
					best = reel.Tier;
				reel.Stop(true, false);
			}
			if (stoppedAny)
				UiSfx.Play(style.TierSfx(best));
			IsRevealing = false;
			choiceUnlockAt = Time.unscaledTime + style.SkipChoiceLock;
			waitPointerRelease = InputManager.TryGetExistingInstance(out InputManager inputManager) && inputManager.IsPointerPressed;
			Finished.Invoke();
		}

		private void Update()
		{
			float now = Time.unscaledTime;
			bool hasInput = InputManager.TryGetExistingInstance(out InputManager inputManager);
			// 연 입력과 같은 프레임은 건너뛰기 제외
			if (IsRevealing && Time.frameCount > startedFrame && hasInput && inputManager.IsAnyKeyPressedThisFrame)
				SkipAll();
			if (waitPointerRelease && (hasInput == false || inputManager.IsPointerPressed == false))
			{
				waitPointerRelease = false;
				pointerReleasedFrame = Time.frameCount;
			}

			bool spinning = false;
			foreach (CardReel reel in reels)
			{
				reel.Tick(now);
				spinning |= reel.IsSpinning;
			}
			if (IsRevealing == false)
				return;
			if (spinning == false)
			{
				IsRevealing = false;
				Finished.Invoke();
				return;
			}
			if (now >= nextTickAt)
			{
				UiSfx.Play(style.SpinTickSfx);
				nextTickAt = now + style.SpinTickInterval;
			}
		}

		private void OnDisable() => Cancel();

		// 상자 칸처럼 매번 새로 만드는 칸의 빛줄기 정리
		private void PruneDestroyedRays()
		{
			List<Image> destroyed = new();
			foreach (KeyValuePair<Image, UISlotRays> pair in raysByIcon)
				if (pair.Key == null || pair.Value == null)
					destroyed.Add(pair.Key);
			foreach (Image icon in destroyed)
				raysByIcon.Remove(icon);
		}

		private UISlotRays RaysFor(Image icon)
		{
			if (raysByIcon.TryGetValue(icon, out UISlotRays rays) == false)
			{
				rays = UISlotRays.Create(icon.transform.parent, icon.transform.GetSiblingIndex());
				raysByIcon[icon] = rays;
			}
			return rays;
		}

		// 후보 섞기와 상한
		private List<CardRevealSymbol> BuildSymbols(IReadOnlyList<CardRevealSymbol> candidates)
		{
			List<CardRevealSymbol> pool = new(candidates);
			for (int i = pool.Count - 1; i > 0; i--)
			{
				int swapIndex = Random.Range(0, i + 1);
				(pool[i], pool[swapIndex]) = (pool[swapIndex], pool[i]);
			}
			if (pool.Count > style.MaxSymbols)
				pool.RemoveRange(style.MaxSymbols, pool.Count - style.MaxSymbols);
			return pool;
		}
	}
}
