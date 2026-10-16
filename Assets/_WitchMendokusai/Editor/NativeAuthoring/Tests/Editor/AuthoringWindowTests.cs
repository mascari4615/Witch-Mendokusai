using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace WitchMendokusai.ContentAuthoring.Tests
{
    public sealed class AuthoringWindowTests
    {
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
            finally { Object.DestroyImmediate(window); }
        }
    }
}
