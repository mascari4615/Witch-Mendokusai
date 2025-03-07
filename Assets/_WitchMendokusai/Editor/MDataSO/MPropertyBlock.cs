using System;
using System.Reflection;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// PropertyField가 있는줄 모르고 만들었던 PropertyBlock
	/// </summary>
	public class PropertyBlock : VisualElement
	{
		private readonly DataSO dataSO;
		private readonly PropertyInfo propertyInfo;

		public Label PropertyName { get; private set; }
		public VisualElement PropertyValue { get; private set; }

		public PropertyBlock(DataSO dataSO, PropertyInfo propertyInfo)
		{
			this.dataSO = dataSO;
			this.propertyInfo = propertyInfo;

			PropertyName = new Label(propertyInfo.Name);
			PropertyName.AddToClassList("property-name");
			Add(PropertyName);

			SetPropertyValue();

			AddToClassList("property-block");
		}

		private void SetPropertyValueWithType<T, U>() where U : BaseField<T>, new()
		{
			T value = (T)propertyInfo.GetValue(dataSO);
			PropertyValue = new U();
			(PropertyValue as U).value = value;
		}

		private void SetPropertyValue()
		{
			Type propertyType = propertyInfo.PropertyType;
			switch (propertyType)
			{
				case Type intType when intType == typeof(int):
					SetPropertyValueWithType<int, IntegerField>();
					break;
				case Type floatType when floatType == typeof(float):
					SetPropertyValueWithType<float, FloatField>();
					break;
				case Type boolType when boolType == typeof(bool):
					SetPropertyValueWithType<bool, Toggle>();
					break;
				case Type stringType when stringType == typeof(string):
					SetPropertyValueWithType<string, TextField>();
					bool isDescription = propertyInfo.Name == nameof(DataSO.Description);
					if (isDescription)
					{
						(PropertyValue as TextField).multiline = true;
						(PropertyValue as TextField).style.minHeight = 100;
					}

					PropertyValue.RegisterCallback<ChangeEvent<string>>(evt =>
					{
						propertyInfo.SetValue(dataSO, evt.newValue);
					});
					break;
				case Type enumType when enumType.IsEnum:
					Enum enumValue = (Enum)propertyInfo.GetValue(dataSO);
					PropertyValue = new EnumField();
					(PropertyValue as EnumField).Init(enumValue);
					break;
				case Type spriteType when spriteType == typeof(Sprite):
					Sprite spriteValue = (Sprite)propertyInfo.GetValue(dataSO);
					PropertyValue = new VisualElement();

					ObjectField objectField = new()
					{
						objectType = typeof(Sprite),
						value = spriteValue
					};
					PropertyValue.Add(objectField);

					Image image = new()
					{
						image = (spriteValue != null) ? spriteValue.texture : null
					};
					image.AddToClassList("property-sprite");
					PropertyValue.Add(image);
					break;
				default:
					PropertyValue = new Label("Unsupported Type");
					break;
			}
			PropertyValue.AddToClassList("property-value");
			Add(PropertyValue);
		}
	}
}