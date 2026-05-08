using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
#if UNITY_EDITOR
	// TransformChain에서, TransformChainData 필드를 커스텀
	[CustomPropertyDrawer(typeof(TransformChainData))]
	public class TransformChainDataEditor : PropertyDrawer
	{
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			SerializedProperty transformFlagsProperty = property.FindPropertyRelative(nameof(TransformChainData.TransformFlags));
			SerializedProperty durationProperty = property.FindPropertyRelative(nameof(TransformChainData.Duration));
			int fieldCount = 0;
			int labelCount = 0;

			if (!string.IsNullOrEmpty(label.text))
			{
				labelCount++; // Element 섹션 라벨
			}

			if ((TransformFlag)transformFlagsProperty.intValue != TransformFlag.None)
			{
				labelCount++; // Common 섹션 라벨
				fieldCount += 4; // IsLocal, IsRelative, Duration, TransformFlags

				if (((TransformFlag)transformFlagsProperty.intValue & TransformFlag.DoPosition) != 0)
				{
					labelCount++; // Position 섹션 라벨

					if (durationProperty.floatValue > 0)
					{
						fieldCount++; // MoveEaseType
						if (property.FindPropertyRelative(nameof(TransformChainData.MoveEaseType)).intValue == (int)MoveEaseType.Ease)
						{
							fieldCount++; // EaseType
						}
					}
					fieldCount++; // MovePosType
					if (property.FindPropertyRelative(nameof(TransformChainData.MovePosType)).intValue == (int)MovePosType.Random)
					{
						fieldCount++; // RandomRange
					}
					else
					{
						fieldCount++; // EndPosition
					}
				}

				if (((TransformFlag)transformFlagsProperty.intValue & TransformFlag.DoScale) != 0)
				{
					labelCount++; // Scale 섹션 라벨
					fieldCount++; // EndScale
				}
			}

			float height = EditorGUIUtility.singleLineHeight;
			float space = EditorGUIUtility.standardVerticalSpacing;

			int totalCount = fieldCount + labelCount;
			return totalCount * height + (totalCount - 1) * space;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			SerializedProperty transformFlagsProperty = property.FindPropertyRelative(nameof(TransformChainData.TransformFlags));
			SerializedProperty endPositionProperty = property.FindPropertyRelative(nameof(TransformChainData.EndPosition));
			SerializedProperty endScaleProperty = property.FindPropertyRelative(nameof(TransformChainData.EndScale));
			SerializedProperty isLocalProperty = property.FindPropertyRelative(nameof(TransformChainData.IsLocal));
			SerializedProperty isRelativeProperty = property.FindPropertyRelative(nameof(TransformChainData.IsRelative));
			SerializedProperty durationProperty = property.FindPropertyRelative(nameof(TransformChainData.Duration));
			SerializedProperty easeTypeProperty = property.FindPropertyRelative(nameof(TransformChainData.EaseType));
			SerializedProperty moveEaseTypeProperty = property.FindPropertyRelative(nameof(TransformChainData.MoveEaseType));
			SerializedProperty movePosTypeProperty = property.FindPropertyRelative(nameof(TransformChainData.MovePosType));
			SerializedProperty randomRangeProperty = property.FindPropertyRelative(nameof(TransformChainData.RandomRange));

			EditorGUI.BeginProperty(position, label, property);

			float y = position.y;
			float sectionPadding = 16f;
			Rect GetRect(float indent = 0f)
			{
				return new Rect(position.x + indent, y, position.width - indent, EditorGUIUtility.singleLineHeight);
			}

			float lineHeight = EditorGUIUtility.singleLineHeight;
			float space = EditorGUIUtility.standardVerticalSpacing;

			void AddLabel(string text)
			{
				EditorGUI.LabelField(GetRect(), text, EditorStyles.boldLabel);
				y += lineHeight + space;
			}

			void AddField(SerializedProperty property, GUIContent label)
			{
				EditorGUI.PropertyField(GetRect(sectionPadding), property, label);
				y += lineHeight + space;
			}

			// Label
			if (!string.IsNullOrEmpty(label.text))
			{
				EditorGUI.LabelField(new Rect(position.x, y, position.width, lineHeight), label.text, EditorStyles.boldLabel);
				y += lineHeight + space;
			}

			// Common Section
			{
				AddLabel("Common");
				AddField(isLocalProperty, new GUIContent(nameof(TransformChainData.IsLocal)));
				AddField(isRelativeProperty, new GUIContent(nameof(TransformChainData.IsRelative)));
				AddField(durationProperty, new GUIContent(nameof(TransformChainData.Duration)));
				AddField(transformFlagsProperty, new GUIContent(nameof(TransformChainData.TransformFlags)));
			}

			// Position Section
			if ((transformFlagsProperty.intValue & (int)TransformFlag.DoPosition) != 0)
			{
				AddLabel("Position");

				if (durationProperty.floatValue > 0)
				{
					AddField(moveEaseTypeProperty, new GUIContent(nameof(TransformChainData.MoveEaseType)));

					if (moveEaseTypeProperty.intValue == (int)MoveEaseType.Ease)
					{
						AddField(easeTypeProperty, new GUIContent(nameof(TransformChainData.EaseType)));
					}
				}

				AddField(movePosTypeProperty, new GUIContent(nameof(TransformChainData.MovePosType)));

				switch (movePosTypeProperty.intValue)
				{
					case (int)MovePosType.Random:
						AddField(randomRangeProperty, new GUIContent(nameof(TransformChainData.RandomRange)));
						break;
					case (int)MovePosType.Manual:
						AddField(endPositionProperty, new GUIContent(nameof(TransformChainData.EndPosition)));
						break;
				}
			}

			// Scale Section
			if ((transformFlagsProperty.intValue & (int)TransformFlag.DoScale) != 0)
			{
				AddLabel("Scale");
				AddField(endScaleProperty, new GUIContent(nameof(TransformChainData.EndScale)));
			}

			EditorGUI.EndProperty();
		}
	}
#endif
}