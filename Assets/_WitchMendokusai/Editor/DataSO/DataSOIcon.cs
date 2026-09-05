using UnityEngine;
using UnityEditor;
using System.IO;

namespace WitchMendokusai
{
	[InitializeOnLoad]
	public class DataSOIcon
	{
		static DataSOIcon()
		{
			EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
		}

		private static void OnProjectWindowItemGUI(string guid, Rect rect)
		{
			string path = AssetDatabase.GUIDToAssetPath(guid);

			// 폴더와 방금 지워진 stale 항목은 건너뜀. File.GetAttributes 는 없는 경로에 던진다 (2026-09-05 Resource 폴더 이동 뒤 실측)
			if (path == "" ||
				Event.current.type != EventType.Repaint ||
				AssetDatabase.IsValidFolder(path) ||
				File.Exists(path) == false)
			{
				return;
			}

			Object obj = AssetDatabase.LoadAssetAtPath<Object>(path);

			if (obj is DataSO dataSO)
			{
				Rect imageRect;
				if (rect.height > 20)
				{
					imageRect = new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.width + 2);
				}
				else if (rect.x > 20)
				{
					imageRect = new Rect(rect.x - 1, rect.y - 1, rect.height + 2, rect.height + 2);
				}
				else
				{
					imageRect = new Rect(rect.x + 2, rect.y - 1, rect.height + 2, rect.height + 2);
				}

				if (dataSO.Sprite == null)
					return;

				// 배경 칠하기
				EditorGUI.DrawRect(imageRect, new Color(0.1f, 0.1f, 0.1f));

				// Sprite의 UV 설정
				Rect uvRect = new Rect(
					dataSO.Sprite.textureRect.x / dataSO.Sprite.texture.width,
					dataSO.Sprite.textureRect.y / dataSO.Sprite.texture.height,
					dataSO.Sprite.textureRect.width / dataSO.Sprite.texture.width,
					dataSO.Sprite.textureRect.height / dataSO.Sprite.texture.height
				);

				Rect spriteRect = imageRect;
				// 원본 비율 유지
				float aspectRatio = dataSO.Sprite.textureRect.width / dataSO.Sprite.textureRect.height;
				if (aspectRatio > 1)
				{
					spriteRect.height /= aspectRatio;
				}
				else
				{
					spriteRect.width *= aspectRatio;
				}

				// 그만큼 중앙에 위치 (ImageRect의 중앙에 위치)
				spriteRect.x += (imageRect.width - spriteRect.width) / 2;
				spriteRect.y += (imageRect.height - spriteRect.height) / 2;
			
				// Sprite의 특정 영역을 잘라내어 표시
				GUI.DrawTextureWithTexCoords(spriteRect, dataSO.Sprite.texture, uvRect);
			}
		}
	}
}