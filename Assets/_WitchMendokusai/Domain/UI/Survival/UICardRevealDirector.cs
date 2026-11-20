using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace WitchMendokusai
{
	// 카드 공개 감독. 화면마다 하나, 연출은 테마의 스킨 에셋
	// - 공통: 배경 어둡게, 전투 카메라 당김, 카드 층 확대, 아무 키로 건너뛰기, 고르기 입력 관문
	// - 스킨: 박자, 등급 예고, 터짐 (CardRevealSkin.Build)
	// - 처음 몇 번만 풀 연출: 스킨의 RepeatSkin, FullPlays 와 세이브의 본 횟수
	public sealed class UICardRevealDirector : MonoBehaviour
	{
		private readonly List<CardRevealCard> cards = new();
		private UICardRevealStage stage;
		private Transform content;
		private CardRevealRun run;
		private CardRevealSkin skin;
		private Vector3 contentScale = Vector3.one;
		private bool zoomed;
		private Action releaseCamera;
		private float choiceUnlockAt;
		private int startedFrame;
		private bool waitPointerRelease;
		private int pointerReleasedFrame = -1;

		public bool IsRevealing { get; private set; }
		// 공개 중, 건너뛰기 직후, 건너뛴 클릭을 떼는 프레임까지 고르기 입력 무시
		public bool BlocksChoice => IsRevealing || Time.unscaledTime < choiceUnlockAt || waitPointerRelease || Time.frameCount <= pointerReleasedFrame;
		// 공개가 끝난 순간. 자연 종료와 건너뛰기 모두
		public event Action Finished = delegate { };

		// stageRoot 는 화면 전체를 덮는 패널, cardContent 는 카드가 든 층 (흔들림과 확대)
		public static UICardRevealDirector AttachTo(GameObject panel, RectTransform stageRoot, Transform cardContent)
		{
			UICardRevealDirector director = panel.AddComponent<UICardRevealDirector>();
			director.stage = UICardRevealStage.Ensure(stageRoot);
			director.content = cardContent;
			return director;
		}

		// 등급 규칙 한 곳. 영웅이나 진화 카드는 최고 등급
		public static CardRevealTier TierOf(CardData card, bool isEvolution = false)
		{
			if (isEvolution || card.Rarity == CardRarity.Epic || card.UIKind == SurvivalUIKind.Advanced)
				return CardRevealTier.Epic;
			return card.Rarity == CardRarity.Rare ? CardRevealTier.Rare : CardRevealTier.Common;
		}

		// chestSprite 가 있으면 origin 자리 상자에서 나옴, 없으면 카드 가운데의 꾸러미. chained 는 연속 레벨업
		// 연출을 시작했으면 true. 효과 끔, 빈 목록, 스킨 없음이면 false 이고 부르는 쪽이 기본 공개
		public bool Play(CardRevealSkin revealSkin, IReadOnlyList<CardRevealEntry> entries, Sprite chestSprite = null, RectTransform origin = null, bool chained = false)
		{
			if (UIEffects.Enabled == false || entries.Count == 0)
			{
				Cancel();
				return false;
			}
			return PlayExact(Resolve(revealSkin), entries, chestSprite, origin, chained);
		}

		// 처음 FullPlays 번은 그 스킨, 뒤로는 RepeatSkin. 고른 순간 본 횟수 1 올림 (건너뛰어도 본 것). 빈 스킨은 그대로 넘겨 PlayExact 가 기본 공개로
		public static CardRevealSkin Resolve(CardRevealSkin requested)
		{
			if (requested == null || requested.RepeatSkin == null || DataManager.TryGetExistingInstance(out DataManager dataManager) == false)
				return requested;
			int seen = dataManager.GameStat[GameStatType.CARD_PACK_FULL_REVEAL_SEEN];
			if (seen >= requested.FullPlays)
				return requested.RepeatSkin;
			dataManager.GameStat[GameStatType.CARD_PACK_FULL_REVEAL_SEEN] = seen + 1;
			return requested;
		}

		// 반복 판정 없이 이 스킨 그대로. 개발 창 비교용
		public bool PlayExact(CardRevealSkin revealSkin, IReadOnlyList<CardRevealEntry> entries, Sprite chestSprite = null, RectTransform origin = null, bool chained = false)
		{
			Cancel();
			if (CanPlay(revealSkin, entries) == false)
			{
				// 스킨만 빈 경우는 설정 오류. 판을 멈추는 예외 대신 오류 로그와 기본 공개
				if (revealSkin == null && UIEffects.Enabled && entries != null && entries.Count > 0)
					Debug.LogError($"카드 공개 스킨이 비어 기본 공개로 대체: {name}. SurvivalUITheme.Reveal 슬롯 확인", this);
				return false;
			}
			skin = revealSkin;
			// 화면 맞춤 배율이 바뀌었을 수 있어 매번 기억. Cancel 이 확대를 되돌린 뒤라 원래 크기
			contentScale = content.localScale;
			stage.Arrange();
			// 레이아웃이 자리를 정한 뒤의 위치를 기억해야 날아오기와 떨어지기가 제자리로 감
			Canvas.ForceUpdateCanvases();
			foreach (CardRevealEntry entry in entries)
			{
				CardRevealCard card = new(entry);
				card.Prepare(skin.TierColor(entry.Tier), skin.AuraSizeRatio, skin.ShineAngle, skin.BackSprite(entry.Tier));
				cards.Add(card);
			}
			Vector2 originPosition = origin != null ? stage.ToLocal(origin) : CardsCenter();
			run = new CardRevealRun(skin, stage, content, cards, CardRevealSkin.Best(entries), chestSprite, originPosition, chained);
			BuildScreen(run, chestSprite != null);
			skin.Build(run);
			run.Start(OnRunFinished);
			IsRevealing = true;
			choiceUnlockAt = 0f;
			startedFrame = Time.frameCount;
			return true;
		}

		// 연출 조건. 스킨이 비는 경우: 테마 Reveal 슬롯 미지정, 또는 켜진 에디터가 새 스킨 에셋보다 테마를 먼저 다시 읽어 참조가 비어 고정 (2026-11-21 MOIS2 실측)
		public static bool CanPlay(CardRevealSkin revealSkin, IReadOnlyList<CardRevealEntry> entries)
		{
			return UIEffects.Enabled && revealSkin != null && entries != null && entries.Count > 0;
		}

		// 화면 공통: 어둡게, 카메라, 카드 층 확대
		private void BuildScreen(CardRevealRun reveal, bool chest)
		{
			stage.Dim.color = stage.DimColor(0f);
			reveal.Play(0f, stage.Dim.DOFade(skin.DimAlpha, skin.DimIn).SetEase(Ease.OutQuad));
			zoomed = true;
			reveal.Play(0f, content.DOScale(contentScale * (chest ? skin.ChestContentZoom : skin.ContentZoom), skin.CameraIn).SetEase(Ease.OutCubic));
			if (CameraManager.TryGetExistingInstance(out CameraManager cameraManager))
				releaseCamera = cameraManager.PunchZoom(skin.CameraDistance, skin.CameraFieldOfView, skin.CameraIn, float.PositiveInfinity, skin.CameraOut, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f));
		}

		private Vector2 CardsCenter()
		{
			Vector2 sum = Vector2.zero;
			foreach (CardRevealCard card in cards)
				sum += stage.ToLocal(card.Rect);
			return sum / cards.Count;
		}

		// 연출 없이 원래 값 복구. 다음 Play 전, 패널 닫힘
		public void Cancel()
		{
			run?.Kill();
			run = null;
			foreach (CardRevealCard card in cards)
				card.Dispose();
			cards.Clear();
			ReleaseScreen(true);
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
			if (IsRevealing == false || run == null)
				return;
			UiSfx.Play(skin.TierSfx(run.Best));
			run.Skip();
			choiceUnlockAt = Time.unscaledTime + skin.SkipChoiceLock;
			waitPointerRelease = InputManager.TryGetExistingInstance(out InputManager inputManager) && inputManager.IsPointerPressed;
		}

		private void OnRunFinished()
		{
			if (IsRevealing == false)
				return;
			IsRevealing = false;
			run.Kill();
			// 입자는 수명까지 날게 두고 번짐, 기둥 그림만 치움
			stage.ClearImages();
			foreach (CardRevealCard card in cards)
				card.Finish(skin.IdleAuraAlpha, skin.IdleAuraPeriod);
			ReleaseScreen(false);
			Finished.Invoke();
		}

		private void ReleaseScreen(bool instant)
		{
			releaseCamera?.Invoke();
			releaseCamera = null;
			if (stage == null)
				return;
			stage.Dim.DOKill();
			// 확대한 적이 있으면 복귀 tween 이 도는 중이어도 원래 크기를 정본으로
			if (zoomed)
				content.DOKill();
			if (instant || skin == null)
			{
				stage.Clear();
				stage.Dim.color = stage.DimColor(0f);
				if (zoomed)
					content.localScale = contentScale;
				zoomed = false;
				return;
			}
			if (zoomed == false)
				return;
			// 고르는 동안에도 배경은 조금 어둡게 둠
			stage.Dim.DOFade(skin.DimAlpha * skin.HoldDimRatio, skin.CameraOut).SetUpdate(true);
			content.DOScale(contentScale, skin.CameraOut).SetEase(Ease.OutCubic).SetUpdate(true).OnComplete(() => zoomed = false);
		}

		private void Update()
		{
			if (IsRevealing && run != null)
				run.Tick(RevealClock.Delta);
			bool hasInput = InputManager.TryGetExistingInstance(out InputManager inputManager);
			// 연 입력과 같은 프레임은 건너뛰기 제외
			if (IsRevealing && Time.frameCount > startedFrame && hasInput && inputManager.IsAnyKeyPressedThisFrame)
				SkipAll();
			if (waitPointerRelease && (hasInput == false || inputManager.IsPointerPressed == false))
			{
				waitPointerRelease = false;
				pointerReleasedFrame = Time.frameCount;
			}
		}

		private void OnDisable() => Cancel();
	}
}
