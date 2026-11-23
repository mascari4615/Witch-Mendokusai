using UnityEngine;

namespace WitchMendokusai
{
	// 상자 위에 얹힌 소품 (약병, 책). 상자 메시에 같이 그려 두고, 부서질 때 같은 모양 조각이 따로 높이 튀어 떨어짐
	// 크기는 미터 그대로 (조각으로 띄울 때 크기 배율 1)
	public static class BreakableTopProp
	{
		public enum Kind
		{
			Bottle = 0,
			Book = 1,
		}

		// origin 은 소품 바닥 가운데. 회전은 y 축만
		public static void Add(BreakableMeshBuilder builder, Kind kind, Vector3 origin, float yaw, float scale, int mainRow, int accentRow)
		{
			switch (kind)
			{
				case Kind.Bottle:
					AddBottle(builder, origin, scale, mainRow, accentRow);
					break;
				case Kind.Book:
					AddBook(builder, origin, yaw, scale, mainRow, accentRow);
					break;
				default:
					throw new System.ArgumentOutOfRangeException(nameof(kind), kind, null);
			}
		}

		// 둥근 몸통, 가는 목, 마개
		private static void AddBottle(BreakableMeshBuilder builder, Vector3 origin, float scale, int glassRow, int corkRow)
		{
			float height = .22f * scale;
			float radius = .065f * scale;
			int sides = 7;
			float[] heights = { 0f, height * .5f, height * .66f, height * .86f };
			float[] radii = { radius, radius, radius * .38f, radius * .38f };
			builder.AddLathe(origin, heights, radii, sides, new[] { glassRow, glassRow, glassRow }, glassRow, glassRow, 0f);
			builder.AddLathe(origin + Vector3.up * heights[3], new[] { 0f, height * .12f }, new[] { radius * .42f, radius * .4f }, sides, new[] { corkRow }, -1, corkRow, 0f);
		}

		// 눕힌 책 한 권. 표지와 종이
		private static void AddBook(BreakableMeshBuilder builder, Vector3 origin, float yaw, float scale, int coverRow, int pageRow)
		{
			Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
			Vector3 size = new Vector3(.22f, .06f, .16f) * scale;
			float cover = size.y * .2f;
			builder.AddBox(origin + Vector3.up * size.y * .5f, new Vector3(size.x * .96f, size.y - cover * 2f, size.z * .94f), rotation, pageRow);
			builder.AddBox(origin + Vector3.up * cover * .5f, new Vector3(size.x, cover, size.z), rotation, coverRow);
			builder.AddBox(origin + Vector3.up * (size.y - cover * .5f), new Vector3(size.x, cover, size.z), rotation, coverRow);
			builder.AddBox(origin + Vector3.up * size.y * .5f + rotation * new Vector3(-size.x * .5f, 0f, 0f), new Vector3(cover, size.y, size.z), rotation, coverRow);
		}
	}
}
