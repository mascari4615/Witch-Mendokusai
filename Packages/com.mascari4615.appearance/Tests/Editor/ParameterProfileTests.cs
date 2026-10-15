using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Appearance.Tests
{
    public sealed class ParameterProfileTests
    {
        private static ParameterProfile Profile()
        {
            return new ParameterProfile
            {
                Id = "shape",
                Properties = new[] { new ParameterProperty { Id = "property", Baseline = 1, Minimum = 0, Maximum = 2 } },
                Controls = new[] { new ParameterControl { Id = "size", Label = "Size", Minimum = 0, Maximum = 1, Default = .5f, Value = .5f,
                    Influences = new[] { new ParameterInfluence { Property = "property", Weight = 1 } } } }
            };
        }

        [Test]
        public void ValuesAreEvaluatedFromCalibrationWithoutAccumulatingPreviousEdits()
        {
            ParameterProfile original = Profile();
            ParameterProfile first = ParameterProfiles.WithValues(original, new Dictionary<string, float> { { "size", .75f } });
            ParameterProfile second = ParameterProfiles.WithValues(first, new Dictionary<string, float> { { "size", 1 } });
            Assert.That(ParameterProfiles.Evaluate(first)["property"], Is.EqualTo(1.25f));
            Assert.That(ParameterProfiles.Evaluate(second)["property"], Is.EqualTo(1.5f));
            Assert.That(original.Controls[0].Value, Is.EqualTo(.5f));
            second.Controls[0].Influences[0].Weight = 5;
            Assert.That(original.Controls[0].Influences[0].Weight, Is.EqualTo(1));
        }

        [Test]
        public void MultipleControlsCombineBeforePropertyBoundsAreChecked()
        {
            ParameterProfile profile = Profile();
            ParameterControl first = profile.Controls[0];
            first.Value = 1;
            profile.Controls = new[] { first, new ParameterControl { Id = "balance", Label = "Balance", Minimum = -1, Maximum = 1, Default = 0, Value = -.5f,
                Influences = new[] { new ParameterInfluence { Property = "property", Weight = 1 } } } };
            Assert.That(ParameterProfiles.Evaluate(profile)["property"], Is.EqualTo(1));
            profile.Controls[1].Value = 1;
            Assert.Throws<ArgumentException>(() => ParameterProfiles.Evaluate(profile));
        }

        [Test]
        public void InvalidDefinitionsAndValuesAreRejectedBeforePublishingAChangedProfile()
        {
            ParameterProfile profile = Profile();
            Assert.Throws<ArgumentException>(() => ParameterProfiles.WithValues(profile, new Dictionary<string, float> { { "unknown", .5f } }));
            Assert.Throws<ArgumentException>(() => ParameterProfiles.WithValues(profile, new Dictionary<string, float> { { "size", float.NaN } }));
            Assert.That(profile.Controls[0].Value, Is.EqualTo(.5f));
            profile.Controls[0].Influences[0].Property = "missing";
            Assert.Throws<ArgumentException>(() => ParameterProfiles.Evaluate(profile));
            profile = Profile();
            profile.Controls[0].Influences = new[] { profile.Controls[0].Influences[0], profile.Controls[0].Influences[0] };
            Assert.Throws<ArgumentException>(() => ParameterProfiles.Evaluate(profile));
        }
    }
}
