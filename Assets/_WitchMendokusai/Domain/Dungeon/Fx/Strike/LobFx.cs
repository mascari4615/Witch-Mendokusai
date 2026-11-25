using System;
using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	// 시전자 몸에서 던져 포물선으로 날아가 앉는 물체. 예고 시작에 재생하고 Seconds 가 때리는 순간
	// 날아감 -> 앉음 (납작) -> 부풀어 떨림 -> 사라짐 (터짐은 StrikeFx 몫)
	[CreateAssetMenu(fileName = "Lob_0", menuName = "WM/CombatFx/" + nameof(LobFx))]
	public class LobFx : CombatFx
	{
		[SerializeField, Tooltip("날 때 번갈아 쓰는 그림")] private Sprite[] flySprites;
		[SerializeField, Min(1f)] private float flyFps = 12f;
		[SerializeField] private Sprite landedSprite;
		[SerializeField] private Sprite swellSprite;
		[SerializeField, Tooltip("카메라를 보는 재질")] private Material material;
		[SerializeField, Tooltip("던지는 높이 (m)"), Min(0f)] private float launchHeight = 1.4f;
		[SerializeField, Tooltip("포물선 높이 (m)"), Min(0f)] private float arcHeight = 3f;
		[SerializeField, Tooltip("앉은 뒤 터지기까지 (초)"), Min(0f)] private float fuseSeconds = 0.45f;
		[SerializeField, Min(0.05f)] private float scale = 1f;
		[SerializeField, Min(1f)] private float swellScale = 1.35f;
		[SerializeField, Tooltip("앉을 때 먼지. 비우면 없음")] private MonsterSpawnStyle landPuff;

		protected override Action Play(CombatFxRunner runner, CombatFxContext context)
		{
			SpriteRenderer body = FxSpritePool.Rent(runner, flySprites[0], material);
			return runner.Run(Routine(body, context), () => FxSpritePool.Return(runner, body));
		}

		private IEnumerator Routine(SpriteRenderer body, CombatFxContext context)
		{
			Vector3 from = context.Origin + Vector3.up * launchHeight;
			Vector3 to = context.Position + Vector3.up * 0.25f;
			float flySeconds = Mathf.Max(0.1f, context.Seconds - fuseSeconds);
			CombatSfx.Play(CombatSfxEvent.WeaponThrow, context.Origin);
			for (float time = 0f; time < flySeconds; time += Time.deltaTime)
			{
				body.sprite = flySprites[(int)(time * flyFps) % flySprites.Length];
				body.transform.position = SlimeKingPatternMath.ArcPoint(from, to, arcHeight, time / flySeconds);
				body.transform.localScale = Vector3.one * scale;
				yield return null;
			}
			if (landPuff != null)
				landPuff.PlayPuff(context.Position, 0.6f);
			body.transform.position = to;
			for (float time = 0f; time < fuseSeconds; time += Time.deltaTime)
			{
				float progress = time / fuseSeconds;
				// 앞 1/4 은 납작하게 눌렸다가, 뒤는 점점 빠르게 깜빡이며 부풂
				bool isSquashed = progress < 0.25f;
				bool isLit = isSquashed == false && Mathf.Sin(progress * progress * 60f) > -0.2f;
				body.sprite = isSquashed ? landedSprite : isLit ? swellSprite : flySprites[0];
				body.transform.localScale = Vector3.one * (scale * (isSquashed ? 1f : Mathf.Lerp(1f, swellScale, progress)));
				yield return null;
			}
		}
	}
}
