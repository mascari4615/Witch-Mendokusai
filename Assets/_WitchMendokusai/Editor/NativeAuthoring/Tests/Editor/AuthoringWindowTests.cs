using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace WitchMendokusai.ContentAuthoring.Tests
{
    public sealed class AuthoringWindowTests
    {
        [Test]
        public void ClosingWorkspaceReleasesCachedWardrobeThumbnails()
        {
            ContentAuthoringWindow window = ScriptableObject.CreateInstance<ContentAuthoringWindow>();
            Texture2D thumbnail = new Texture2D(2, 2);
            thumbnail.hideFlags = HideFlags.HideAndDontSave;
            WardrobePanel wardrobe = window.Wardrobe;
            FieldInfo thumbnailsField = typeof(WardrobePanel).GetField("thumbs", BindingFlags.Instance | BindingFlags.NonPublic);
            Dictionary<string, Texture2D> thumbnails = (Dictionary<string, Texture2D>)thumbnailsField.GetValue(wardrobe);
            try
            {
                thumbnails.Add("outfit", thumbnail);
                thumbnails.Add("missing", null);
                UnityEngine.Object.DestroyImmediate(window);
                Assert.That(thumbnail == null, Is.True);
                Assert.That(thumbnails, Is.Empty);
                Assert.DoesNotThrow(() => ((IDisposable)wardrobe).Dispose());
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (window != null) UnityEngine.Object.DestroyImmediate(window);
                if (thumbnail != null) UnityEngine.Object.DestroyImmediate(thumbnail);
            }
        }

        [Test]
        public void WindowConstructionInitializesLocalizedStateAfterUnityCreatesTheWindow()
        {
            ContentAuthoringWindow window = ScriptableObject.CreateInstance<ContentAuthoringWindow>();
            try
            {
                Assert.That(window.maxSize.x, Is.GreaterThanOrEqualTo(window.minSize.x));
                Assert.That(window.maxSize.y, Is.GreaterThanOrEqualTo(window.minSize.y));
                Assert.That(new SerializedObject(window).FindProperty("libraryTitle").stringValue, Is.Not.Empty);
                LogAssert.NoUnexpectedReceived();
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }
    }
}
