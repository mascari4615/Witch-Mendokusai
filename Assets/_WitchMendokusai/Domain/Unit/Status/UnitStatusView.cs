using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 유닛 하나의 상태이상 표시 묶음. 머리 위 칸 줄 (RoR2식 칸), 작은 효과 (불티, 거품, 별), 둔화 잔상
	// - 칸 하나: 어두운 칸, 줄어드는 테두리 (시계 방향), 상태색 실루엣, 쌓임 숫자 (오른쪽 아래). 같은 자리라 LayerBias 로 앞뒤를 가름
	// - 칸은 종류마다가 아니라 자리 (IconSlots) 마다. 걸린 순서 (Styles 위 줄 먼저) 로 앞 자리부터 채움
	// - 정렬: 그림 정렬값은 몸과 같고, 정렬용 경계 중심을 몸 정렬점에서 카메라 쪽으로 SortBias 만큼
	//   몸 그림은 정렬점이 발밑 (Sprite Sort Point 는 Pivot, UnitBase.prefab) 이라 몸 위치 기준
	//   투명 물체는 먼 것부터 그림. 표시는 자기 몸 다음 차례라 몸 위, 더 가까운 앞 몬스터 차례보다 먼저라 그 아래
	//   잔상은 카메라 반대쪽이라 자기 몸보다 먼저, 몸 아래
	// - 정렬값 +1 은 모든 몬스터 위에 그려서 쓰지 않음
	// - 구동기 풀에서 재사용. 만들 때 칸을 다 만들고 이후 할당 없음
	internal sealed class UnitStatusView
	{
		private static readonly int BILLBOARD_MODE_ID = Shader.PropertyToID("_BillboardMode");
		// 그림마다 실제로 칠해진 영역의 위 끝 (그림 좌표). 그림 틀의 빈 여백 위로 아이콘이 뜨지 않게
		private static readonly Dictionary<Sprite, Vector2> paintedRange = new();

		private readonly GameObject root;
		private readonly Slot[] slots;
		private readonly float[] iconShown;
		private readonly SpriteRenderer[][] effects;
		private readonly UnitStatusOrbit3D[] orbits;
		private readonly Sprite[] ringFrames;
		private readonly Sprite[] digitSprites;
		private readonly SpriteRenderer ghost;
		private Vector3 ghostPosition;
		private bool ghostPlaced;

		// 칸 하나의 그림 넷. 아래부터 칸, 테두리, 실루엣, 숫자
		private readonly struct Slot
		{
			public readonly SpriteRenderer Back;
			public readonly SpriteRenderer Ring;
			public readonly SpriteRenderer Glyph;
			public readonly SpriteRenderer Digit;

			public Slot(SpriteRenderer back, SpriteRenderer ring, SpriteRenderer glyph, SpriteRenderer digit)
			{
				Back = back;
				Ring = ring;
				Glyph = glyph;
				Digit = digit;
			}

			public void Hide()
			{
				Back.enabled = false;
				Ring.enabled = false;
				Glyph.enabled = false;
				Digit.enabled = false;
			}
		}

		public UnitStatusView(Transform parent, UnitStatusConfig config)
		{
			root = new GameObject(nameof(UnitStatusView));
			root.transform.SetParent(parent, false);
			int count = UnitStatusRules.Count;
			ringFrames = UnitStatusSheets.Ring(config);
			digitSprites = UnitStatusSheets.Digits(config);
			slots = new Slot[Mathf.Max(1, config.IconSlots)];
			for (int i = 0; i < slots.Length; i++)
				slots[i] = new Slot(
					CreateRenderer($"Slot {i} back", config.OverlayMaterial),
					CreateRenderer($"Slot {i} ring", config.OverlayMaterial),
					CreateRenderer($"Slot {i} glyph", config.OverlayMaterial),
					CreateRenderer($"Slot {i} digit", config.OverlayMaterial));
			iconShown = new float[count];
			orbits = new UnitStatusOrbit3D[count];
			for (int i = 0; i < count; i++)
				orbits[i] = new UnitStatusOrbit3D(root.transform);
			effects = new SpriteRenderer[count][];
			for (int i = 0; i < count; i++)
			{
				UnitStatusType type = UnitStatusRules.ALL[i];
				UnitStatusStyle style = config.Style(type);
				effects[i] = new SpriteRenderer[style.EffectCount];
				for (int j = 0; j < style.EffectCount; j++)
					effects[i][j] = CreateRenderer($"Effect {type} {j}", config.OverlayMaterial);
			}
			ghost = CreateRenderer("Ghost", null);
			root.SetActive(false);
		}

		private SpriteRenderer CreateRenderer(string rendererName, Material material)
		{
			GameObject child = new(rendererName);
			child.transform.SetParent(root.transform, false);
			SpriteRenderer spriteRenderer = child.AddComponent<SpriteRenderer>();
			spriteRenderer.shadowCastingMode = ShadowCastingMode.Off;
			spriteRenderer.receiveShadows = false;
			if (material != null)
				spriteRenderer.sharedMaterial = material;
			spriteRenderer.enabled = false;
			return spriteRenderer;
		}

		public void Begin()
		{
			root.SetActive(true);
			ghostPlaced = false;
			for (int i = 0; i < iconShown.Length; i++)
				iconShown[i] = -1f;
		}

		public void End()
		{
			foreach (Slot slot in slots)
				slot.Hide();
			for (int i = 0; i < effects.Length; i++)
				foreach (SpriteRenderer effect in effects[i])
					effect.enabled = false;
			foreach (UnitStatusOrbit3D orbit in orbits)
				orbit.Hide();
			ghost.enabled = false;
			root.SetActive(false);
		}

		public void Draw(UnitStatus status, SpriteRenderer body, Camera camera, UnitStatusConfig config, float time, float deltaTime)
		{
			UnitObject unit = status.Unit;
			Bounds bodyBounds = body.bounds;
			Transform cameraTransform = camera.transform;
			Vector3 right = cameraTransform.right;
			Vector3 up = cameraTransform.up;
			Vector3 sortPoint = BodySortPoint(body, bodyBounds);
			Vector3 toCamera = (cameraTransform.position - sortPoint).normalized;
			// 눈에 보이는 그림의 위 끝과 아래 끝. 경계 상자의 max.y 는 기운 그림에서 위 끝이 아님
			BodyEdges(body, right, up, out Vector3 top, out Vector3 bottom, out float bodyWidth);
			float bodyHeight = Vector3.Distance(top, bottom);

			// 정렬과 화면 밖 판정에 쓰는 경계. 중심은 정렬점, 크기는 몸과 머리 위 아이콘을 다 덮게. 화면 밖 판정이 표시를 자르지 않게 넉넉히
			float cover = (Vector3.Distance(sortPoint, bodyBounds.center) + bodyBounds.extents.magnitude + config.IconGap + config.IconSize * 2f) * 2f;
			Bounds frontBounds = LayerBounds(sortPoint, toCamera, config, cover, 0);
			Bounds backBounds = new(sortPoint - toCamera * config.SortBias, Vector3.one * cover);

			int activeIcons = 0;
			bool orbiting = false;
			float orbit3DLift = 0f;
			foreach (UnitStatusType type in UnitStatusRules.ALL)
			{
				if (UnitStatus.Has(unit, type) == false)
					continue;
				activeIcons++;
				UnitStatusStyle activeStyle = config.Style(type);
				orbiting |= activeStyle.EffectMotion == UnitStatusEffectMotion.Orbit;
				if (activeStyle.OrbitStars > 0)
					orbit3DLift = Mathf.Max(orbit3DLift, activeStyle.OrbitHeight + activeStyle.OrbitBob + activeStyle.OrbitStarSize * 0.5f + config.OrbitIconGap);
			}

			// 별이 머리를 돌면 아이콘은 그 위로. 3D 궤도는 별 높이만큼
			float orbitLift = Mathf.Max(orbiting ? config.IconSize * config.OrbitIconLift : 0f, orbit3DLift);
			Vector3 iconCenter = top + up * (config.IconGap + orbitLift + config.IconSize * 0.5f);
			float step = config.IconSize * config.IconSpacing;
			float offset = -(Mathf.Min(activeIcons, slots.Length) - 1) * 0.5f * step;

			int order = 0;
			for (int i = 0; i < config.Styles.Count; i++)
			{
				UnitStatusStyle style = config.Styles[i];
				int index = (int)style.Type;
				bool active = UnitStatus.Has(unit, style.Type);
				if (active == false)
				{
					iconShown[index] = -1f;
					HideEffects(index);
					orbits[index].Hide();
					continue;
				}
				if (iconShown[index] < 0f)
					iconShown[index] = time;

				float pop = 0f;
				if (config.IconPopSeconds > 0f)
				{
					float popProgress = Mathf.Clamp01((time - iconShown[index]) / config.IconPopSeconds);
					pop = Mathf.Sin(popProgress * Mathf.PI) * config.IconPopScale;
				}
				if (order < slots.Length)
				{
					Vector3 center = iconCenter + right * (offset + order * step);
					DrawSlot(slots[order], status, style, config, body, center, right, up, config.IconSize * (1f + pop), sortPoint, toCamera, cover);
					order++;
				}

				DrawEffects(index, style, config, status, body, top, bottom, right, up, bodyHeight, bodyWidth, time, frontBounds);
				orbits[index].Draw(style, config, top, bodyWidth, toCamera, time, status.PhaseSeed);
			}

			for (int i = order; i < slots.Length; i++)
				slots[i].Hide();

			DrawGhost(unit, body, config, deltaTime, backBounds);
		}

		private void DrawSlot(Slot slot, UnitStatus status, UnitStatusStyle style, UnitStatusConfig config, SpriteRenderer body, Vector3 center, Vector3 right, Vector3 up, float size,
			Vector3 sortPoint, Vector3 toCamera, float cover)
		{
			int index = (int)style.Type;
			UnitObject unit = status.Unit;
			Place(slot.Back, body, config.CellBack, config.CellColor, center, size, LayerBounds(sortPoint, toCamera, config, cover, 0));

			Sprite ring = null;
			if (ringFrames.Length > 0)
				ring = ringFrames[UnitStatusRules.RingFrame(unit.UnitStat[UnitStatusRules.StatType(style.Type)], status.TotalMilliseconds[index], ringFrames.Length)];
			Place(slot.Ring, body, ring, style.IconColor, center, size, LayerBounds(sortPoint, toCamera, config, cover, 1));

			Place(slot.Glyph, body, style.Icon, style.IconColor, center, size * config.SilhouetteScale, LayerBounds(sortPoint, toCamera, config, cover, 2));

			int stacks = style.MaxStacks > 1 ? status.Stacks[index] : 0;
			if (stacks >= 2 && digitSprites.Length > 0)
			{
				float digitSize = size * config.StackDigitScale;
				Vector3 digitCenter = center + right * (size * 0.5f - digitSize * 0.5f) - up * (size * 0.5f - digitSize * 0.5f);
				Place(slot.Digit, body, digitSprites[Mathf.Min(stacks, 9)], Color.white, digitCenter, digitSize, LayerBounds(sortPoint, toCamera, config, cover, 3));
			}
			else
				slot.Digit.enabled = false;
		}

		// 같은 자리 겹침의 앞뒤. 몸 정렬점에서 카메라 쪽으로 SortBias + 층 * LayerBias. 층이 클수록 앞
		private static Bounds LayerBounds(Vector3 sortPoint, Vector3 toCamera, UnitStatusConfig config, float cover, int layer) =>
			new(sortPoint + toCamera * (config.SortBias + layer * config.LayerBias), Vector3.one * cover);

		private void DrawEffects(int index, UnitStatusStyle style, UnitStatusConfig config, UnitStatus status, SpriteRenderer body, Vector3 top, Vector3 bottom, Vector3 right, Vector3 up,
			float bodyHeight, float bodyWidth, float time, Bounds frontBounds)
		{
			SpriteRenderer[] list = effects[index];
			int count = list.Length;
			if (style.EffectMotion == UnitStatusEffectMotion.None || style.EffectSprite == null || count == 0)
			{
				HideEffects(index);
				return;
			}
			float seed = status.PhaseSeed;
			for (int j = 0; j < count; j++)
			{
				float share = (float)j / count;
				Vector3 position;
				float size = style.EffectSize;
				Color color = style.EffectColor;
				if (style.EffectMotion == UnitStatusEffectMotion.Orbit)
				{
					float angle = (time * style.EffectRate + share + seed) * Mathf.PI * 2f;
					float radius = bodyWidth * style.EffectSpread;
					position = top + right * (Mathf.Cos(angle) * radius) + up * (Mathf.Sin(angle) * radius * config.OrbitSquash + size * 0.5f);
					size *= 1f - config.OrbitSizeWobble + config.OrbitSizeWobble * Mathf.Sin(angle);
				}
				else
				{
					float phase = Mathf.Repeat(time * style.EffectRate + share + seed, 1f);
					float lane = Mathf.Repeat((j + 1) * 0.381966f + seed * 0.5f, 1f) - 0.5f;
					position = bottom + up * (bodyHeight * (config.RiseStart + config.RiseHeight * phase)) + right * (lane * bodyWidth * style.EffectSpread);
					color.a *= Mathf.Sin(phase * Mathf.PI);
					size *= 1f - config.RiseShrink * phase;
				}
				Place(list[j], body, style.EffectSprite, color, position, size, frontBounds);
			}
		}

		private void HideEffects(int index)
		{
			foreach (SpriteRenderer effect in effects[index])
				effect.enabled = false;
		}

		private void DrawGhost(UnitObject unit, SpriteRenderer body, UnitStatusConfig config, float deltaTime, Bounds backBounds)
		{
			UnitStatusStyle ghostStyle = null;
			foreach (UnitStatusType type in UnitStatusRules.ALL)
			{
				if (UnitStatus.Has(unit, type) == false)
					continue;
				UnitStatusStyle style = config.Style(type);
				if (style.GhostAlpha > 0f)
				{
					ghostStyle = style;
					break;
				}
			}
			if (ghostStyle == null || body.sprite == null)
			{
				ghost.enabled = false;
				ghostPlaced = false;
				return;
			}
			Transform bodyTransform = body.transform;
			if (ghostPlaced == false)
			{
				ghostPosition = bodyTransform.position;
				ghostPlaced = true;
			}
			ghostPosition = Vector3.Lerp(ghostPosition, bodyTransform.position, 1f - Mathf.Exp(-deltaTime / ghostStyle.GhostLagSeconds));

			ghost.sprite = body.sprite;
			ghost.flipX = body.flipX;
			ghost.flipY = body.flipY;
			ghost.sharedMaterial = body.sharedMaterial;
			ghost.sortingLayerID = body.sortingLayerID;
			ghost.sortingOrder = body.sortingOrder;
			Color color = ghostStyle.Tint;
			color.a = ghostStyle.GhostAlpha;
			ghost.color = color;
			Transform ghostTransform = ghost.transform;
			ghostTransform.SetPositionAndRotation(ghostPosition, bodyTransform.rotation);
			ghostTransform.localScale = bodyTransform.lossyScale;
			ghost.enabled = true;
			ghost.bounds = backBounds;
		}

		// 그림 재질이 카메라를 보면 (_BillboardMode 1, 2) 카메라 위 방향, 아니면 그림 자체 회전
		private static void BodyEdges(SpriteRenderer body, Vector3 cameraRight, Vector3 cameraUp, out Vector3 top, out Vector3 bottom, out float width)
		{
			Transform bodyTransform = body.transform;
			Bounds spriteBounds = body.sprite.bounds;
			Vector2 painted = PaintedRange(body.sprite);
			Vector3 scale = bodyTransform.lossyScale;
			Material material = body.sharedMaterial;
			bool facesCamera = material.HasFloat(BILLBOARD_MODE_ID) && material.GetFloat(BILLBOARD_MODE_ID) > 0.5f;
			width = spriteBounds.size.x * Mathf.Abs(scale.x);
			if (facesCamera)
			{
				Vector3 pivot = bodyTransform.position;
				Vector3 center = cameraRight * (spriteBounds.center.x * Mathf.Abs(scale.x));
				top = pivot + center + cameraUp * (painted.y * Mathf.Abs(scale.y));
				bottom = pivot + center + cameraUp * (painted.x * Mathf.Abs(scale.y));
				return;
			}
			top = bodyTransform.TransformPoint(new Vector3(spriteBounds.center.x, painted.y, 0f));
			bottom = bodyTransform.TransformPoint(new Vector3(spriteBounds.center.x, painted.x, 0f));
		}

		// x 아래 끝, y 위 끝. 꼭 맞춤 그림 망 (Tight) 의 꼭짓점 기준, 처음 한 번만 계산
		private static Vector2 PaintedRange(Sprite sprite)
		{
			if (paintedRange.TryGetValue(sprite, out Vector2 range))
				return range;
			float minimum = float.MaxValue;
			float maximum = float.MinValue;
			foreach (Vector2 vertex in sprite.vertices)
			{
				minimum = Mathf.Min(minimum, vertex.y);
				maximum = Mathf.Max(maximum, vertex.y);
			}
			range = new Vector2(minimum, maximum);
			paintedRange[sprite] = range;
			return range;
		}

		// 몸 그림이 발밑 정렬이면 몸 위치, 아니면 경계 중심
		private static Vector3 BodySortPoint(SpriteRenderer body, Bounds bodyBounds) => body.spriteSortPoint == SpriteSortPoint.Pivot ? body.transform.position : bodyBounds.center;

		private static void Place(SpriteRenderer spriteRenderer, SpriteRenderer body, Sprite sprite, Color color, Vector3 position, float size, Bounds sortBounds)
		{
			if (sprite == null)
			{
				spriteRenderer.enabled = false;
				return;
			}
			spriteRenderer.sprite = sprite;
			spriteRenderer.color = color;
			spriteRenderer.sortingLayerID = body.sortingLayerID;
			spriteRenderer.sortingOrder = body.sortingOrder;
			Transform rendererTransform = spriteRenderer.transform;
			rendererTransform.position = position;
			// 그림 한 변을 size 로. 회전은 재질이 카메라를 봄
			float spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
			rendererTransform.localScale = Vector3.one * (size / spriteSize);
			spriteRenderer.enabled = true;
			spriteRenderer.bounds = sortBounds;
		}
	}
}
