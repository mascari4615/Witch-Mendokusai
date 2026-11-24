using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 몬스터 몸에 붙는 효과 (MonsterBodyFxStyle) 를 그림. MonsterObject 가 켜질 때 스타일을 걸고 종이 바뀌면 다시 걺
	// 프리팹 하나를 여러 종이 나눠 쓰는 풀 몸이라 만든 조각은 재사용
	public sealed class MonsterBodyFx : MonoBehaviour
	{
		private static readonly int COLOR_ID = Shader.PropertyToID("_Color");
		private static readonly int SHAPE_ID = Shader.PropertyToID("_Shape");
		// GroundPuff 셰이더의 안쪽 빈 비율. 음수면 가운데까지 참
		private const float FILLED = -1f;
		private const float GOLDEN = 0.618034f;

		private static Mesh discMesh;

		private readonly List<SpriteRenderer> pieces = new();
		private MonsterBodyFxStyle style;
		private MeshRenderer glow;
		private MeshRenderer ground;
		private MaterialPropertyBlock properties;
		private float seed;

		public void Show(MonsterBodyFxStyle newStyle)
		{
			style = newStyle;
			seed = Random.value;
			enabled = true;

			if (style.HasGlow && glow == null)
				glow = CreateDisc("BodyGlow");
			SetShown(glow, style.HasGlow);
			if (style.HasGround && ground == null)
				ground = CreateDisc("BodyGround");
			SetShown(ground, style.HasGround);

			int count = style.HasPieces ? style.PieceCount : 0;
			while (pieces.Count < count)
				pieces.Add(CreatePiece(pieces.Count));
			for (int i = 0; i < pieces.Count; i++)
				pieces[i].gameObject.SetActive(i < count);
			LateUpdate();
		}

		public void Hide()
		{
			style = null;
			SetShown(glow, false);
			SetShown(ground, false);
			foreach (SpriteRenderer piece in pieces)
				piece.gameObject.SetActive(false);
			enabled = false;
		}

		private void LateUpdate()
		{
			if (style == null)
				return;
			Transform view = Camera.main.transform;
			float time = Time.time;
			if (style.HasGlow)
			{
				float pulse = 1f - style.GlowPulseDepth * 0.5f * (1f + Mathf.Sin((time * style.GlowPulseRate + seed) * Mathf.PI * 2f));
				Vector3 center = transform.position + Vector3.up * style.GlowHeight + view.forward * style.LayerDepth;
				DrawDisc(glow, center, view.rotation, style.GlowRadius, style.GlowColor, pulse);
			}
			if (style.HasGround)
				DrawDisc(ground, transform.position + Vector3.up * style.GroundLift, Quaternion.Euler(90f, 0f, 0f), style.GroundRadius, style.GroundColor, 1f);
			if (style.HasPieces)
				DrawPieces(view, time);
		}

		private void DrawDisc(MeshRenderer disc, Vector3 center, Quaternion rotation, float radius, Color color, float alphaScale)
		{
			disc.transform.SetPositionAndRotation(center, rotation);
			SetWorldScale(disc.transform, radius * 2f);
			disc.sharedMaterial = style.DiscMaterial;
			color.a *= alphaScale;
			properties ??= new MaterialPropertyBlock();
			properties.SetColor(COLOR_ID, color);
			properties.SetVector(SHAPE_ID, new Vector4(FILLED, style.Softness, 0f, 0f));
			disc.SetPropertyBlock(properties);
		}

		private void DrawPieces(Transform view, float time)
		{
			float spriteSize = Mathf.Max(0.01f, style.PieceSprite.bounds.size.y);
			for (int i = 0; i < style.PieceCount; i++)
			{
				(int cycle, float progress) = style.PieceCycle(i, time, seed);
				SpriteRenderer piece = pieces[i];
				float alpha = style.PieceAlpha(progress);
				piece.enabled = alpha > 0f;
				if (piece.enabled == false)
					continue;
				// 주기마다 자리를 바꿈. 난수 상태 없이 번호에서 계산
				float across = Mathf.Repeat((cycle * 2 + i) * GOLDEN + seed, 1f) - 0.5f;
				float upward = Mathf.Repeat((cycle * 3 + i * 5) * GOLDEN + seed * 0.5f, 1f);
				Vector3 position = transform.position + view.right * (across * style.PieceArea.x) + view.up * (upward * style.PieceArea.y) - view.forward * style.LayerDepth;
				piece.transform.SetPositionAndRotation(position, view.rotation);
				SetWorldScale(piece.transform, style.PieceSize / spriteSize);
				piece.sprite = style.PieceSprite;
				piece.sharedMaterial = style.PieceMaterial;
				Color color = style.PieceColor;
				color.a *= alpha;
				piece.color = color;
			}
		}

		// 몸 루트 배율이 달라도 효과 크기는 스타일 값 그대로
		private static void SetWorldScale(Transform target, float size)
		{
			Vector3 parent = target.parent.lossyScale;
			target.localScale = new Vector3(size / parent.x, size / parent.y, size / parent.z);
		}

		private MeshRenderer CreateDisc(string discName)
		{
			GameObject discObject = new(discName);
			discObject.transform.SetParent(transform, false);
			discObject.AddComponent<MeshFilter>().sharedMesh = DiscMesh();
			MeshRenderer disc = discObject.AddComponent<MeshRenderer>();
			disc.shadowCastingMode = ShadowCastingMode.Off;
			disc.receiveShadows = false;
			return disc;
		}

		private SpriteRenderer CreatePiece(int index)
		{
			GameObject pieceObject = new($"BodyPiece {index}");
			pieceObject.transform.SetParent(transform, false);
			SpriteRenderer piece = pieceObject.AddComponent<SpriteRenderer>();
			piece.shadowCastingMode = ShadowCastingMode.Off;
			piece.receiveShadows = false;
			return piece;
		}

		private static void SetShown(MeshRenderer disc, bool shown)
		{
			if (disc != null)
				disc.gameObject.SetActive(shown);
		}

		private static Mesh DiscMesh()
		{
			if (discMesh != null)
				return discMesh;
			discMesh = new Mesh { name = "MonsterBodyFxDisc", hideFlags = HideFlags.HideAndDontSave };
			discMesh.SetVertices(new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) });
			discMesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
			discMesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
			discMesh.RecalculateBounds();
			return discMesh;
		}
	}
}
