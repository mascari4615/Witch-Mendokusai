using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 완성 도장. 금판에 그 몬스터의 그림을 금 양각으로 찍은 텍스처를 처음 쓸 때 한 번 굽고 들고 있음.
	/// 종마다 그림 자산을 만들지 않으므로 몬스터가 늘거나 그림이 바뀌어도 따로 할 일이 없음.
	/// 색, 크기, 가장자리 두께는 재질 (DiscoverySettings.StampMaterial). 도감을 닫을 때 놓고 다음에 열 때 그때 값으로 다시 구움.
	/// 그림의 색은 버리고 밝기만 쓰므로 변종의 색 곱 (Monster.Tint) 은 넣지 않음
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
			// 그래픽 장치를 잃어 내용이 사라졌으면 다시 구움
			if (stamp.IsCreated() == false)
			{
				Bake(monster.Sprite, stamp);
			}
			return stamp;
		}

		/// <summary>구운 것을 모두 놓음. 화면에서 뗀 뒤에 부름</summary>
		public void Clear()
		{
			foreach (RenderTexture stamp in stamps.Values)
			{
				stamp.Release();
				UnityEngine.Object.Destroy(stamp);
			}
			stamps.Clear();
		}

		public void Dispose()
		{
			Clear();
			UnityEngine.Object.Destroy(material);
		}

		private void Bake(Sprite sprite, RenderTexture target)
		{
			Texture2D texture = sprite.texture;
			Rect rect = UvBoundsOf(sprite);
			material.CopyPropertiesFromMaterial(settings.StampMaterial);
			material.SetVector(SPRITE_RECT_ID, new Vector4(rect.x, rect.y, rect.width, rect.height));
			Vector2 pixels = new(rect.width * texture.width, rect.height * texture.height);
			material.SetVector(SPRITE_SIZE_ID, pixels * (material.GetFloat(FIT_ID) / Mathf.Max(pixels.x, pixels.y)));

			// 선형 색 공간이라 쓸 때 sRGB 로 바꾸게 함. 앞 그리기가 남긴 상태에 밝기가 흔들리지 않게
			RenderTexture previous = RenderTexture.active;
			bool previousSrgbWrite = GL.sRGBWrite;
			GL.sRGBWrite = true;
			Graphics.Blit(texture, target, material);
			GL.sRGBWrite = previousSrgbWrite;
			RenderTexture.active = previous;
		}

		// textureRect 는 빈틈없이 묶인 아틀라스에서 던지므로 메시의 uv 범위를 씀 (SpriteColorSampler 와 같음). 투명한 여백도 빠짐
		private static Rect UvBoundsOf(Sprite sprite)
		{
			Vector2 min = Vector2.one;
			Vector2 max = Vector2.zero;
			foreach (Vector2 point in sprite.uv)
			{
				min = Vector2.Min(min, point);
				max = Vector2.Max(max, point);
			}
			return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
		}
	}
}
