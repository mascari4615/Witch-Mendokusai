using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using SdkHeaderAttribute = WitchMendokusai.Numerics.HeaderAttribute;
using SdkTooltipAttribute = WitchMendokusai.Numerics.TooltipAttribute;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// 판정 층(DomainSDK) 구조체의 SDK Header, Tooltip 속성을 인스펙터에 Unity 것처럼 표시
	/// SDK 는 UnityEngine 을 모르므로 PropertyAttribute 불가. 타입 단위 등록 + reflection 으로 읽음
	/// 새 SDK 구조체가 Header, Tooltip 을 쓰면 CustomPropertyDrawer 줄 하나 추가
	/// </summary>
	[CustomPropertyDrawer(typeof(TowerDefenseDifficulty))]
	[CustomPropertyDrawer(typeof(TowerDefenseMapParameters))]
	public sealed class SdkAnnotatedStructDrawer : PropertyDrawer
	{
		// 헤더 줄 높이: 한 줄 + 위 여백 (Unity Header 와 같은 비율)
		private static float HeaderHeight => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing * 4f;

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			float height = EditorGUIUtility.singleLineHeight;
			if (property.isExpanded == false)
			{
				return height;
			}

			foreach (SerializedProperty child in Children(property))
			{
				FieldInfo field = FieldOf(property, child);
				if (field != null && field.GetCustomAttribute<SdkHeaderAttribute>() != null)
				{
					height += HeaderHeight;
				}
				height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
			}
			return height;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
			property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
			if (property.isExpanded == false)
			{
				return;
			}

			EditorGUI.indentLevel++;
			float y = line.yMax;
			foreach (SerializedProperty child in Children(property))
			{
				FieldInfo field = FieldOf(property, child);
				SdkHeaderAttribute header = field?.GetCustomAttribute<SdkHeaderAttribute>();
				if (header != null)
				{
					float headerHeight = HeaderHeight;
					Rect headerRect = new Rect(position.x, y + headerHeight - EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight);
					GUI.Label(EditorGUI.IndentedRect(headerRect), header.header, EditorStyles.boldLabel);
					y += headerHeight;
				}

				GUIContent childLabel = new GUIContent(child.displayName);
				SdkTooltipAttribute tooltip = field?.GetCustomAttribute<SdkTooltipAttribute>();
				if (tooltip != null)
				{
					childLabel.tooltip = tooltip.tooltip;
				}

				float childHeight = EditorGUI.GetPropertyHeight(child, true);
				EditorGUI.PropertyField(new Rect(position.x, y, position.width, childHeight), child, childLabel, true);
				y += childHeight + EditorGUIUtility.standardVerticalSpacing;
			}
			EditorGUI.indentLevel--;
		}

		private static System.Collections.Generic.IEnumerable<SerializedProperty> Children(SerializedProperty parent)
		{
			SerializedProperty iterator = parent.Copy();
			SerializedProperty end = parent.GetEndProperty();
			bool enterChildren = true;
			while (iterator.NextVisible(enterChildren) && SerializedProperty.EqualContents(iterator, end) == false)
			{
				enterChildren = false;
				yield return iterator.Copy();
			}
		}

		private FieldInfo FieldOf(SerializedProperty parent, SerializedProperty child)
		{
			Type structType = fieldInfo.FieldType;
			return structType.GetField(child.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}
	}
}
