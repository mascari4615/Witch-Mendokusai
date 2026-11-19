using UnityEngine;

namespace WitchMendokusai
{
	// 한 장짜리 그림 묶음 (남은 시간 테두리, 숫자) 을 칸마다 Sprite 로 자름
	// - 칸 그림마다 .meta 를 손으로 자르지 않게 런타임에 Sprite.Create 로. 묶음 그림이 바뀌면 다시 자름
	// - 칸 한 변이 곧 한 단위 (pixelsPerUnit 은 칸 화소 수). 표시는 한 변을 원하는 크기로 맞춰 그림
	internal static class UnitStatusSheets
	{
		private static Texture2D ringTexture;
		private static Sprite[] ring;
		private static Texture2D digitTexture;
		private static Sprite[] digits;

		// 번호 0 이 가장 짧음, 마지막이 가득. 칸은 왼쪽 위부터 행 순서
		public static Sprite[] Ring(UnitStatusConfig config)
		{
			if (config.RingSheet == null)
				return System.Array.Empty<Sprite>();
			if (ring == null || ringTexture != config.RingSheet || ring.Length != config.RingFrames || ring[0] == null)
			{
				ringTexture = config.RingSheet;
				int columns = Mathf.Max(1, config.RingColumns);
				int cell = ringTexture.width / columns;
				ring = new Sprite[Mathf.Max(1, config.RingFrames)];
				for (int i = 0; i < ring.Length; i++)
				{
					int row = i / columns;
					Rect rect = new((i % columns) * cell, ringTexture.height - (row + 1) * cell, cell, cell);
					ring[i] = Sprite.Create(ringTexture, rect, new Vector2(0.5f, 0.5f), cell, 0, SpriteMeshType.FullRect);
				}
			}
			return ring;
		}

		// 0부터 9, 가로 한 줄
		public static Sprite[] Digits(UnitStatusConfig config)
		{
			if (config.DigitSheet == null)
				return System.Array.Empty<Sprite>();
			if (digits == null || digitTexture != config.DigitSheet || digits[0] == null)
			{
				digitTexture = config.DigitSheet;
				int cell = digitTexture.height;
				digits = new Sprite[10];
				for (int i = 0; i < digits.Length; i++)
					digits[i] = Sprite.Create(digitTexture, new Rect(i * cell, 0, cell, cell), new Vector2(0.5f, 0.5f), cell, 0, SpriteMeshType.FullRect);
			}
			return digits;
		}
	}
}
