using System;
using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	// 시전자 몸에서 던져 포물선으로 날아가 앉는 물체. 예고 시작에 재생하고 Seconds 가 때리는 순간
	// 날아감 -> 앉음 (납작) -> 부풀어 떨림 -> 사라짐 (터짐은 StrikeFx 몫). Hold 가 있으면 선 채 그 수명대로 남음
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
		[SerializeField, Tooltip("터질 때까지 더 남는 초"), Min(0f)] private float lingerSeconds = 0.06f;
		[SerializeField, Tooltip("나는 초 상한. 남는 초는 던지기 전 기다림 (몸이 던지는 자세로 바뀔 때까지). 0 이면 예고 내내 낢"), Min(0f)] private float maxFlySeconds;
		[SerializeField, Min(0.05f)] private float scale = 1f;
		[SerializeField, Min(1f)] private float swellScale = 1.35f;
		[SerializeField, Tooltip("앉아서 납작한 동안 (앉은 뒤 초의 비율)"), Range(0f, 1f)] private float squashRatio = 0.25f;
		[SerializeField, Tooltip("이 뒤로는 켜진 채"), Range(0f, 1f)] private float litRatio = 0.6f;
		[SerializeField, Min(0f)] private float blinkSpeed = 60f;
		[SerializeField, Tooltip("앉을 때 먼지. 비우면 없음")] private MonsterSpawnStyle landPuff;

		[Header("남음 (Hold)")]
		[SerializeField, Tooltip("서서 남는 그림. 비우면 날던 그림")] private Sprite standSprite;
		[SerializeField, Tooltip("식어서 안 아픈 그림. 비우면 그대로 색만")] private Sprite coolSprite;
		[SerializeField] private Color hotTint = Color.white;
		[SerializeField] private Color coolTint = new(0.5f, 0.5f, 0.5f, 1f);
		[SerializeField, Min(0f)] private float hotPulseSpeed = 7f;
		[SerializeField, Min(0.05f)] private float sinkSeconds = 0.25f;

		protected override Action Play(CombatFxRunner runner, CombatFxContext context)
		{
			SpriteRenderer body = FxSpritePool.Rent(runner, flySprites[0], material);
			bool stands = false;
			Action stop = runner.Run(Routine(body, context, () => stands = true), () => FxSpritePool.Return(runner, body));
			// 땅에 서서 남은 뒤에는 공격이 끊겨도 수명대로
			return () =>
			{
				if (stands == false)
					stop();
			};
		}

		private IEnumerator Routine(SpriteRenderer body, CombatFxContext context, Action onStand)
		{
			Vector3 from = context.Origin + Vector3.up * launchHeight;
			Vector3 to = context.Position + Vector3.up * 0.25f;
			float flySeconds = Mathf.Max(0.1f, context.Seconds - fuseSeconds);
			float waitSeconds = maxFlySeconds > 0f ? Mathf.Max(0f, flySeconds - maxFlySeconds) : 0f;
			flySeconds -= waitSeconds;
			float launchTime = Time.time + waitSeconds;
			body.enabled = false;
			while (Time.time < launchTime)
				yield return null;
			body.enabled = true;
			CombatSfx.Play(CombatSfxEvent.WeaponThrow, context.Origin);
			float startTime = Time.time;
			for (float time = 0f; time < flySeconds; time = Time.time - startTime)
			{
				body.sprite = flySprites[(int)(time * flyFps) % flySprites.Length];
				body.transform.position = SlimeKingPatternMath.ArcPoint(from, to, arcHeight, time / flySeconds);
				body.transform.localScale = Vector3.one * scale;
				yield return null;
			}
			if (landPuff != null)
				landPuff.PlayPuff(context.Position, 0.6f);
			body.transform.position = to;
			if (context.Hold != null)
			{
				onStand();
				yield return Stand(body, context.Hold);
				yield break;
			}
			// 터짐 (StrikeFx) 은 행동 틱에 맞춰 최대 0.1초 늦음. 그동안 부푼 채 남음
			for (float time = 0f; time < fuseSeconds + lingerSeconds; time += Time.deltaTime)
			{
				float progress = Mathf.Clamp01(time / Mathf.Max(0.01f, fuseSeconds));
				// 앞은 납작하게 눌렸다가, 뒤는 점점 빠르게 깜빡이며 부풂
				bool isSquashed = progress < squashRatio;
				// 뒤는 켜진 채. 터지기 직전이 가장 밝게 읽힘
				bool isLit = progress > litRatio || (isSquashed == false && Mathf.Sin(progress * progress * blinkSpeed) > 0f);
				body.sprite = isSquashed ? landedSprite : isLit ? swellSprite : flySprites[0];
				body.transform.localScale = Vector3.one * (scale * (isSquashed ? 1f : Mathf.Lerp(1f, swellScale, progress)));
				yield return null;
			}
		}

		private IEnumerator Stand(SpriteRenderer body, FxHold hold)
		{
			float sinkTime = 0f;
			Sprite plain = standSprite != null ? standSprite : flySprites[0];
			while (hold.IsGone == false && sinkTime < sinkSeconds)
			{
				// 때리기 직전에 앉으면 잠깐 안 뜨거움. 식은 것과 구별
				bool isCool = Time.time >= hold.HarmlessTime;
				body.sprite = hold.IsCracked ? swellSprite : isCool && coolSprite != null ? coolSprite : plain;
				body.color = isCool ? coolTint : Color.Lerp(Color.white, hotTint, Mathf.PingPong(Time.time * hotPulseSpeed / Mathf.PI, 1f));
				if (Time.time >= hold.EndTime)
					sinkTime += Time.deltaTime;
				body.transform.localScale = Vector3.one * (scale * (1f - Mathf.Clamp01(sinkTime / sinkSeconds)));
				yield return null;
			}
		}
	}
}
