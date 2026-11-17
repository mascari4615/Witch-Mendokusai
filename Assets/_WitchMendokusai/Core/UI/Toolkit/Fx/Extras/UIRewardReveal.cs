using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 보상 정체를 보이기 전에 등급 색으로 먼저 빛나고 뜸 들임. 최고 등급은 빛 고리 한 겹 더, 가끔 한 단계 아래 색에서 올라감
	/// Play(host, 등급, 공개) 에서 host 는 보상 칸, 공개 콜백에서 그림과 이름을 채움
	/// </summary>
	[CreateAssetMenu(fileName = "Reveal_0_Rarity", menuName = "WM/UI/Reward Reveal")]
	public sealed class UIRewardReveal : ScriptableObject
	{
		[Tooltip("등급별 색. 0 번이 가장 낮음")]
		public Color[] rarityColors =
		{
			new(0.44f, 0.71f, 1f, 1f),
			new(0.76f, 0.55f, 1f, 1f),
			new(1f, 0.83f, 0.42f, 1f),
		};
		[Tooltip("누르면 움츠러드는 시간")]
		public float anticipationSeconds = 0.22f;
		[Range(0.5f, 1f)] public float squash = 0.9f;
		[Tooltip("빛이 피어나는 시간과 뜸 들이는 시간")]
		public float bloomSeconds = 0.26f;
		public float holdSeconds = 0.9f;
		[Tooltip("최고 등급이 아래 색에서 올라갈 확률")]
		[Range(0f, 1f)] public float upgradeChance = 0.5f;
		public float upgradeFlashSeconds = 0.22f;
		[Tooltip("올라갈 때 빛이 한 번 부푸는 정도")]
		public float upgradeFlashScale = 0.6f;
		[Tooltip("최고 등급 고리 퍼짐 시간과 배율")]
		public float ringSeconds = 0.52f;
		public float ringScale = 2.4f;
		public float ringStartScale = 0.8f;
		public float ringWidth = 3f;
		[Tooltip("빛 크기, 칸 크기 배율")]
		public float glowScale = 2.2f;
		[Tooltip("빛이 피어나기 시작할 때 크기")]
		public float glowStartScale = 0.6f;
		public float fadeSeconds = 0.3f;

		/// <summary>rarity 는 rarityColors 의 번호. 정체를 보일 때 reveal, 다 끝나면 onDone</summary>
		public void Play(VisualElement host, int rarity, Action reveal, Action onDone = null)
		{
			if (rarity < 0 || rarity >= rarityColors.Length)
				throw new ArgumentOutOfRangeException(nameof(rarity), rarity, $"{name}: 등급 색 칸 수 {rarityColors.Length}");
			bool top = rarity == rarityColors.Length - 1;
			bool upgrade = top && rarity > 0 && UnityEngine.Random.value < upgradeChance;

			VisualElement glow = new() { name = "reveal-glow", pickingMode = PickingMode.Ignore };
			glow.style.position = Position.Absolute;
			float inset = -(glowScale - 1f) * 50f;
			glow.style.left = Length.Percent(inset);
			glow.style.right = Length.Percent(inset);
			glow.style.top = Length.Percent(inset);
			glow.style.bottom = Length.Percent(inset);
			glow.style.backgroundImage = new StyleBackground(UIFxTextures.Glow);
			glow.style.opacity = 0f;
			host.Add(glow);
			glow.SendToBack();

			VisualElement ring = new() { name = "reveal-ring", pickingMode = PickingMode.Ignore };
			ring.style.position = Position.Absolute;
			ring.style.left = 0;
			ring.style.right = 0;
			ring.style.top = 0;
			ring.style.bottom = 0;
			UIFxTween.SetRadius(ring, Length.Percent(50f));
			ring.style.opacity = 0f;
			host.Add(ring);

			void Tint(int index) => glow.style.unityBackgroundImageTintColor = rarityColors[index];

			UIFxTween.Run(host, anticipationSeconds, 0f, progress =>
			{
				float scale = Mathf.Lerp(1f, squash, Mathf.Sin(progress * Mathf.PI));
				host.style.scale = new Scale(new Vector2(scale, scale));
			}, () =>
			{
				host.style.scale = StyleKeyword.Null;
				Tint(upgrade ? rarity - 1 : rarity);
				UIFxTween.Run(glow, bloomSeconds, 0f, progress =>
				{
					glow.style.opacity = UIFxTween.OutCubic(progress);
					float scale = Mathf.Lerp(glowStartScale, 1f, UIFxTween.OutCubic(progress));
					glow.style.scale = new Scale(new Vector2(scale, scale));
				}, () => UIFxTween.After(host, holdSeconds, () =>
				{
					if (upgrade)
					{
						Tint(rarity);
						UIFxTween.Run(glow, upgradeFlashSeconds, 0f, progress =>
						{
							float flash = 1f + upgradeFlashScale * Mathf.Sin(progress * Mathf.PI);
							glow.style.scale = new Scale(new Vector2(flash, flash));
						}, () => Finish(host, glow, ring, rarity, top, reveal, onDone));
						return;
					}
					Finish(host, glow, ring, rarity, top, reveal, onDone);
				}));
			});
		}

		private void Finish(VisualElement host, VisualElement glow, VisualElement ring, int rarity, bool top, Action reveal, Action onDone)
		{
			reveal?.Invoke();
			if (top)
			{
				UIFxTween.SetBorder(ring, ringWidth, rarityColors[rarity]);
				UIFxTween.Run(ring, ringSeconds, 0f, progress =>
				{
					float scale = Mathf.Lerp(ringStartScale, ringScale, UIFxTween.OutCubic(progress));
					ring.style.scale = new Scale(new Vector2(scale, scale));
					ring.style.opacity = 1f - progress;
				});
			}
			UIFxTween.After(host, holdSeconds, () => UIFxTween.Run(glow, fadeSeconds, 0f, progress => glow.style.opacity = 1f - progress, () =>
			{
				glow.RemoveFromHierarchy();
				ring.RemoveFromHierarchy();
				onDone?.Invoke();
			}));
		}
	}
}
