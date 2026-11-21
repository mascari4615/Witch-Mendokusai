using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 공개 한 번의 시간축. 스킨이 사건과 움직임을 넣고 감독이 돌림
	// - 모든 움직임은 하나의 실제 시간 Sequence. 건너뛰기는 끝까지 감은 뒤 끝 상태로
	// - 소리와 입자는 건너뛰는 동안 내지 않음
	// - 시간은 감독이 Tick 으로 넣음 (RevealClock, 녹화 중 고정 간격)
	public sealed class CardRevealRun
	{
		private readonly Sequence sequence;
		private readonly List<Action> releases = new();
		private Action finished;
		private Vector3 contentHome;

		public CardRevealSkin Skin { get; }
		public UICardRevealStage Stage { get; }
		public Transform Content { get; }
		public IReadOnlyList<CardRevealCard> Cards { get; }
		public CardRevealTier Best { get; }
		// 상자 그림. 없으면 카드 꾸러미
		public Sprite ChestSprite { get; }
		// 꾸러미나 상자 자리 (무대 좌표)
		public Vector2 Origin { get; }
		public bool Skipping { get; private set; }
		// 연속 레벨업으로 이어 열린 공개. 스킨이 짧은 길을 고름
		public bool Chained { get; }
		public float End { get; private set; }

		public CardRevealRun(CardRevealSkin skin, UICardRevealStage stage, Transform content, IReadOnlyList<CardRevealCard> cards, CardRevealTier best, Sprite chestSprite, Vector2 origin, bool chained = false)
		{
			Chained = chained;
			Skin = skin;
			Stage = stage;
			Content = content;
			Cards = cards;
			Best = best;
			ChestSprite = chestSprite;
			Origin = origin;
			contentHome = content.localPosition;
			sequence = DOTween.Sequence().SetUpdate(UpdateType.Manual, true).SetAutoKill(false).Pause();
		}

		public Color TierColor(CardRevealTier tier) => Skin.TierColor(tier);

		public void At(float time, Action action)
		{
			sequence.InsertCallback(time, () => action());
			End = Mathf.Max(End, time);
		}

		public void Play(float time, Tween tween)
		{
			sequence.Insert(time, tween.SetUpdate(true));
			End = Mathf.Max(End, time + tween.Duration());
		}

		public void Sfx(float time, UiSfxEvent sfx) => At(time, () =>
		{
			if (Skipping == false)
				UiSfx.Play(sfx);
		});

		// 입자, 일회성 효과. 건너뛰기 중이면 생략
		public void Emit(float time, Action emit) => At(time, () =>
		{
			if (Skipping == false)
				emit();
		});

		public void Mark(float time) => End = Mathf.Max(End, time);

		// 스킨 값으로 입자 터짐. angle 은 가운데 방향, spread 는 퍼짐 (라디안)
		public void Burst(float time, Vector2 position, RevealBurst burst, Color tint, bool front = true, float angle = 0f, float spread = Mathf.PI * 2f) => Emit(time, () =>
			(front ? Stage.FrontParticles : Stage.BackParticles).Burst(position, burst.Count, tint, burst.Speed, burst.Gravity, burst.Life, burst.Size, angle, spread, 2f, burst.SparkRatio));

		// 카드 층 흔들림. 끝으로 갈수록 약해짐
		public void Shake(float time, float strength, float duration)
		{
			if (strength <= 0f)
				return;
			Play(time, DOVirtual.Float(1f, 0f, duration, weight =>
			{
				Vector2 offset = UnityEngine.Random.insideUnitCircle * strength * weight * weight;
				Content.localPosition = contentHome + (Vector3)offset;
			}).OnComplete(() => Content.localPosition = contentHome));
		}

		// 가운데만 밝은 빛 번짐. 기본은 카드 뒤 층이라 카드 앞면을 덮지 않고 가장자리로 번짐
		public Image Bloom(float time, Vector2 position, float radius, Color color, float peak, float duration, bool front = false)
		{
			Image bloom = Stage.NewImage(front, RevealTextures.Bloom, new Color(color.r, color.g, color.b, 0f), position, Vector2.one * radius * 2f, "[Reveal] Bloom");
			float rise = duration * 0.12f;
			Play(time, bloom.DOFade(peak, rise).SetEase(Ease.OutQuad));
			Play(time + rise, bloom.DOFade(0f, duration - rise).SetEase(Ease.OutQuad));
			return bloom;
		}

		// 퍼지는 고리. yScale 이 1 보다 작으면 바닥 파동
		public Image Ring(float time, Vector2 position, float size, float scaleTo, Color color, float duration, float yScale = 1f, bool front = false)
		{
			Image ring = Stage.NewImage(front, RevealTextures.Ring, new Color(color.r, color.g, color.b, 0f), position, new Vector2(size, size * yScale), "[Reveal] Ring");
			ring.rectTransform.localScale = Vector3.one * 0.2f;
			Play(time, ring.rectTransform.DOScale(scaleTo, duration).SetEase(Ease.OutCubic));
			Play(time, DOVirtual.Float(1f, 0f, duration, alpha => ring.color = new Color(color.r, color.g, color.b, alpha)).SetEase(Ease.OutQuad));
			return ring;
		}

		// 전투 카메라를 더 당김. 끝나면 감독이 풂
		public void CameraPunch(float time, float distance, float fieldOfView, float inSeconds)
		{
			At(time, () =>
			{
				if (Skipping == false && CameraManager.TryGetExistingInstance(out CameraManager cameraManager))
					releases.Add(cameraManager.PunchZoom(distance, fieldOfView, inSeconds, float.PositiveInfinity, Skin.CameraOut, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)));
			});
		}

		public void AddRelease(Action release) => releases.Add(release);

		// 끝 알림은 Sequence 콜백이 아니라 갱신이 끝난 뒤 바깥에서 부름
		// - finished 는 이 Sequence 를 Kill 하고 새 tween 을 만든다. 수동 갱신 콜백 안에서 자기 Sequence 를 Kill 하면
		//   DOTween 이 갱신 끝에 같은 tween 을 한 번 더 지워 활성 목록이 깨지고 (IndexOutOfRangeException), 그 뒤 만든 tween 이 돌지 않음
		//   (2026-11-21 MOIS3 Play 재현, 편집 모드에서는 재현 안 됨)
		public void Start(Action finished)
		{
			this.finished = finished;
			// Sequence 길이를 마지막 Mark 까지 늘리는 빈 표시
			sequence.InsertCallback(End, () => { });
			sequence.Play();
		}

		// 공개 중에만 읽음. Kill 뒤에는 부르지 않음
		public float Elapsed => sequence.Elapsed(false);

		public void Tick(float deltaTime)
		{
			if (finished == null)
				return;
			sequence.ManualUpdate(deltaTime, deltaTime);
			if (sequence.Elapsed(false) >= End)
				RaiseFinished();
		}

		public void Skip()
		{
			if (finished == null)
				return;
			Skipping = true;
			sequence.Complete(true);
			RaiseFinished();
		}

		private void RaiseFinished()
		{
			Action callback = finished;
			finished = null;
			callback?.Invoke();
		}

		public void Kill()
		{
			finished = null;
			sequence.Kill();
			Content.localPosition = contentHome;
			foreach (Action release in releases)
				release();
			releases.Clear();
		}
	}
}
