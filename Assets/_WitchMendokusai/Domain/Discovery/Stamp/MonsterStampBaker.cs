using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 완성 도장. 금판에 그 몬스터의 그림을 금 양각으로 찍은 텍스처를 처음 쓸 때 한 번 굽고 들고 있음.
	/// 종마다 그림 자산을 만들지 않으므로 몬스터가 늘거나 그림이 바뀌어도 따로 할 일이 없음.
	/// 색, 크기, 가장자리 두께는 재질 (DiscoverySettings.StampMaterial)
	/// </summary>
	public sealed class MonsterStampBaker : IDisposable
	{
		private static readonly int SPRITE_RECT_ID = Shader.PropertyToID("_SpriteRect");
		private static readonly int SPRITE_SIZE_ID = Shader.PropertyToID("_SpriteSize");
		private static readonly int FIT_ID = Shader.PropertyToID("_Fit");

		private readonly Material material;
		private readonly DiscoverySettings settings;
		private readonly Dictionary<Monster, RenderTexture> stamps = new();

		public MonsterStampBaker(DiscoverySettings settings)
		{
			// 굽는 동안 값을 넣으므로 자산이 아닌 사본에
			material = new Material(settings.StampMaterial);
			this.settings = settings;
		}

		public RenderTexture StampOf(Monster monster)
		{
			if (stamps.TryGetValue(monster, out RenderTexture stamp) == false)
			{
				stamp = new RenderTexture(settings.StampTextureSize, settings.StampTextureSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
				{
					name = $"DiscoveryStamp {monster.ID}",
					useMipMap = true,
					autoGenerateMips = true,
					filterMode = FilterMode.Trilinear,
				};
				stamps[monster] = stamp;
			}
			// 그래픽 장치를 잃으면 내용이 사라짐. 그때는 다시 구움
			if (stamp.IsCreated() == false)
			{
				Bake(monster.Sprite, stamp);
			}
			return stamp;
		}

		public void Dispose()
		{
			foreach (RenderTexture stamp in stamps.Values)
			{
				stamp.Release();
				UnityEngine.Object.Destroy(stamp);
			}
			stamps.Clear();
			UnityEngine.Object.Destroy(material);
		}

		private void Bake(Sprite sprite, RenderTexture target)
		{
			Texture2D texture = sprite.texture;
			Rect rect = sprite.textureRect;
			material.SetVector(SPRITE_RECT_ID, new Vector4(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height));
			float scale = material.GetFloat(FIT_ID) / Mathf.Max(rect.width, rect.height);
			material.SetVector(SPRITE_SIZE_ID, new Vector2(rect.width, rect.height) * scale);

			RenderTexture previous = RenderTexture.active;
			Graphics.Blit(texture, target, material);
			RenderTexture.active = previous;
		}
	}
}
