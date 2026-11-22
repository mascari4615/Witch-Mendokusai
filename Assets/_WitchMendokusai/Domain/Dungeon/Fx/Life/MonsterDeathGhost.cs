using UnityEngine;

namespace WitchMendokusai
{
	/// <summary> 사망 순간의 겉모습. 본체가 꺼지거나 풀로 돌아가도 잔상이 이 값으로 그려짐 </summary>
	public struct MonsterDeathGhostPose
	{
		public Sprite Sprite;
		public bool FlipX;
		public bool FlipY;
		public Color Color;
		public Vector3 Position;
		public Quaternion Rotation;
		/// <summary> 월드 기준 크기 (lossyScale) </summary>
		public Vector3 Scale;
		/// <summary> 본체 재질의 빌보드 방식 (0 끔, 1 카메라 위치 기준, 2 카메라 회전 기준) </summary>
		public float BillboardMode;
		public int SortingLayerId;
		public int SortingOrder;
	}

	/// <summary>
	/// 사망 잔상의 진입점. 스프라이트 렌더러의 지금 모습을 복사한 가벼운 오브젝트가 대신 녹음
	/// - 본체는 건드리지 않음. 부른 뒤 바로 꺼도 됨
	/// - 본체가 이미 원상 복구된 뒤 (풀 반환 등) 재생해야 하면 Capture 를 먼저 해 두고 그 값으로 Play
	/// - 상한 (스타일의 MaxGhosts) 을 넘으면 재생하지 않고 false
	/// </summary>
	public static class MonsterDeathGhost
	{
		private static readonly int BILLBOARD_MODE_ID = Shader.PropertyToID("_BillboardMode");
		private static readonly int COLOR_ID = Shader.PropertyToID("_Color");

		public static MonsterDeathGhostPose Capture(SpriteRenderer source)
		{
			Transform sourceTransform = source.transform;
			Material sourceMaterial = source.sharedMaterial;
			Color color = source.color;
			if (sourceMaterial.HasProperty(COLOR_ID))
				color *= sourceMaterial.GetColor(COLOR_ID);
			// 젤 출렁임 중이면 그 순간의 늘어난 모양을 크기에 담음. 잔상 재질엔 출렁임이 없어서 안 담으면 툭 돌아감
			Vector2 wobbleScale = JellyWobbleMath.ScaleFactor(sourceMaterial, sourceTransform.position);
			Vector3 scale = sourceTransform.lossyScale;
			scale.x *= wobbleScale.x;
			scale.y *= wobbleScale.y;

			return new MonsterDeathGhostPose
			{
				Sprite = source.sprite,
				FlipX = source.flipX,
				FlipY = source.flipY,
				Color = color,
				Position = sourceTransform.position,
				Rotation = sourceTransform.rotation,
				Scale = scale,
				BillboardMode = sourceMaterial.HasProperty(BILLBOARD_MODE_ID) ? sourceMaterial.GetFloat(BILLBOARD_MODE_ID) : 0f,
				SortingLayerId = source.sortingLayerID,
				SortingOrder = source.sortingOrder,
			};
		}

		public static bool Play(SpriteRenderer source, MonsterDeathStyle style) => Play(Capture(source), style);

		public static bool Play(in MonsterDeathGhostPose pose, MonsterDeathStyle style)
			=> MonsterLifeFxDriver.Instance.AddGhost(pose, style, style.Seconds) != MonsterLifeFxDriver.NONE;
	}
}
