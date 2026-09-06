using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	/// <summary>
	/// 뽑기 연출 (사용자 2026-09-05: 버튼 딸깍하고 끝나는 식은 절대 안 됨).
	///
	/// ★ 네 박자. 모으기 (빛이 커짐) - 터짐 (최고 등급 색으로 화면이 번쩍) - 공개 (카드가 한 장씩)
	///   - 마무리 (요약과 닫기). 누르면 남은 것을 한 번에 보여주고 끝냄
	/// ★ 색이 곧 기대. 터지는 빛 색이 그 판 최고 등급이라 카드를 보기 전에 이미 앎
	/// ★ 판정은 이미 끝난 뒤. 여기는 <b>보여주기만</b> 함 (결과를 다시 굴리지 않음)
	/// </summary>
	public sealed class GachaRevealController
	{
		private enum Beat
		{
			Idle = 0,
			Charge = 1,
			Flash = 2,
			Reveal = 3,
			Done = 4,
		}

		private readonly VisualElement popup;
		private readonly ModalController modalController;
		private readonly UIContentSO content;
		private readonly HeroVisualPresenter heroVisualPresenter;
		private readonly VisualTreeAsset cardAsset;
		private readonly RuntimeSettingsSO settings;
		private readonly VisualElement burst;
		private readonly VisualElement grid;
		private readonly Label title;
		private readonly Label summary;
		private readonly Button skip;
		private readonly Button close;
		private readonly List<IdleHeroPull> pulls = new List<IdleHeroPull>();
		private readonly List<VisualElement> cards = new List<VisualElement>();
		private Beat beat = Beat.Idle;
		private float beatLeft;
		private int shown;

		public GachaRevealController(VisualElement popup, ModalController modalController, UIContentSO content,
			HeroVisualPresenter heroVisualPresenter, VisualTreeAsset cardAsset, RuntimeSettingsSO settings)
		{
			this.popup = popup;
			this.modalController = modalController;
			this.content = content;
			this.heroVisualPresenter = heroVisualPresenter;
			this.cardAsset = cardAsset;
			this.settings = settings;

			burst = popup.RequireQ<VisualElement>("gacha-burst");
			grid = popup.RequireQ<VisualElement>("gacha-grid");
			title = popup.RequireQ<Label>("gacha-title");
			summary = popup.RequireQ<Label>("gacha-summary");
			skip = popup.RequireQ<Button>("gacha-skip");
			close = popup.RequireQ<Button>("gacha-close");

			skip.text = content.GachaSkipText;
			skip.clicked += RevealAll;
			close.text = content.GachaCloseText;
			close.clicked += Close;
			modalController.Register(popup, Close);
		}

		public bool IsOpen => beat != Beat.Idle;

		/// <summary>그 판 결과를 연출로. 목록은 뽑힌 차례대로</summary>
		public void Show(IReadOnlyList<IdleHeroPull> result)
		{
			if (result == null || result.Count == 0)
			{
				return;
			}

			pulls.Clear();
			for (int index = 0; index < result.Count; index++)
			{
				pulls.Add(result[index]);
			}

			BuildCards();
			title.text = content.GachaTitleText(pulls.Count);
			summary.text = string.Empty;
			skip.style.display = DisplayStyle.Flex;
			close.style.display = DisplayStyle.None;
			burst.style.opacity = 0f;
			SetBurstGrade(BestGrade());

			shown = 0;
			beat = Beat.Charge;
			beatLeft = settings.GachaChargeSeconds;
			modalController.Show(popup);
		}

		public void Tick(float delta)
		{
			if (beat == Beat.Idle || beat == Beat.Done)
			{
				return;
			}

			beatLeft -= delta;

			if (beat == Beat.Charge)
			{
				float ratio = 1f - Mathf.Clamp01(beatLeft / settings.GachaChargeSeconds);
				burst.style.opacity = ratio * 0.6f;
				burst.style.scale = new StyleScale(new Scale(new Vector2(0.3f + ratio * 0.8f, 0.3f + ratio * 0.8f)));

				if (beatLeft <= 0f)
				{
					beat = Beat.Flash;
					beatLeft = settings.GachaFlashSeconds;
				}

				return;
			}

			if (beat == Beat.Flash)
			{
				float ratio = Mathf.Clamp01(beatLeft / settings.GachaFlashSeconds);
				burst.style.opacity = ratio;
				burst.style.scale = new StyleScale(new Scale(new Vector2(1.1f + (1f - ratio) * 1.4f, 1.1f + (1f - ratio) * 1.4f)));

				if (beatLeft <= 0f)
				{
					beat = Beat.Reveal;
					beatLeft = 0f;
					burst.style.opacity = 0f;
				}

				return;
			}

			if (beatLeft <= 0f && shown < cards.Count)
			{
				RevealOne(shown);
				shown++;
				beatLeft = settings.GachaCardStepSeconds;
			}

			if (shown >= cards.Count)
			{
				Finish();
			}
		}

		/// <summary>남은 카드를 한 번에. 사람이 기다릴 뜻이 없을 때</summary>
		private void RevealAll()
		{
			while (shown < cards.Count)
			{
				RevealOne(shown);
				shown++;
			}

			burst.style.opacity = 0f;
			Finish();
		}

		private void Finish()
		{
			if (beat == Beat.Done)
			{
				return;
			}

			beat = Beat.Done;
			skip.style.display = DisplayStyle.None;
			close.style.display = DisplayStyle.Flex;

			int legend = 0;
			int epic = 0;
			int newFaces = 0;
			for (int index = 0; index < pulls.Count; index++)
			{
				legend += pulls[index].Grade == IdleHeroGrade.Legend ? 1 : 0;
				epic += pulls[index].Grade == IdleHeroGrade.Epic ? 1 : 0;
				newFaces += pulls[index].IsNew ? 1 : 0;
			}

			summary.text = content.GachaSummaryText(pulls.Count, legend, epic, newFaces);
		}

		public void Close()
		{
			beat = Beat.Idle;
			modalController.Hide(popup);
		}

		/// <summary>카드 자리를 결과 수에 맞춰 세움. 앞면은 아직 안 보임</summary>
		private void BuildCards()
		{
			grid.Clear();
			cards.Clear();

			for (int index = 0; index < pulls.Count; index++)
			{
				TemplateContainer tree = cardAsset.Instantiate();
				VisualElement card = tree.RequireQ<VisualElement>("gacha-card");
				card.RemoveFromHierarchy();
				card.AddToClassList("idle-gacha-card--back");
				grid.Add(card);
				cards.Add(card);
			}
		}

		/// <summary>한 장 뒤집기. 등급 색과 이름, 처음 본 얼굴 표시</summary>
		private void RevealOne(int index)
		{
			IdleHeroPull pull = pulls[index];
			VisualElement card = cards[index];
			IdleHeroKind kind = IdleHeroes.KindOf(pull.Id);

			card.RemoveFromClassList("idle-gacha-card--back");
			card.AddToClassList(GradeClass(pull.Grade));

			VisualElement portrait = card.RequireQ<VisualElement>("gacha-card-portrait");
			heroVisualPresenter.SetPortrait(portrait, pull.Id);

			card.RequireQ<Label>("gacha-card-name").text = kind.Name;
			card.RequireQ<Label>("gacha-card-grade").text = content.GradeName(pull.Grade);

			Label badge = card.RequireQ<Label>("gacha-card-badge");
			bool marked = pull.IsNew || pull.ByPity;
			badge.text = pull.IsNew ? content.GachaNewBadge : content.GachaPityBadge;
			badge.style.display = marked ? DisplayStyle.Flex : DisplayStyle.None;
		}

		/// <summary>그 판 최고 등급. 터지는 빛 색이 이것</summary>
		private IdleHeroGrade BestGrade()
		{
			IdleHeroGrade best = IdleHeroGrade.Common;
			for (int index = 0; index < pulls.Count; index++)
			{
				if (pulls[index].Grade > best)
				{
					best = pulls[index].Grade;
				}
			}

			return best;
		}

		private void SetBurstGrade(IdleHeroGrade grade)
		{
			burst.RemoveFromClassList("idle-gacha-burst--legend");
			burst.RemoveFromClassList("idle-gacha-burst--epic");
			burst.RemoveFromClassList("idle-gacha-burst--rare");
			burst.AddToClassList(BurstClass(grade));
		}

		private static string GradeClass(IdleHeroGrade grade)
		{
			switch (grade)
			{
				case IdleHeroGrade.Legend: return "idle-gacha-card--legend";
				case IdleHeroGrade.Epic: return "idle-gacha-card--epic";
				case IdleHeroGrade.Rare: return "idle-gacha-card--rare";
				default: return "idle-gacha-card--common";
			}
		}

		private static string BurstClass(IdleHeroGrade grade)
		{
			switch (grade)
			{
				case IdleHeroGrade.Legend: return "idle-gacha-burst--legend";
				case IdleHeroGrade.Epic: return "idle-gacha-burst--epic";
				case IdleHeroGrade.Rare: return "idle-gacha-burst--rare";
				default: return "idle-gacha-burst--common";
			}
		}
	}
}
