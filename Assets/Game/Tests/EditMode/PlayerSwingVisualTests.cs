using System.Reflection;
using NUnit.Framework;
using SpiderSwing.Gameplay;
using UnityEngine;

namespace SpiderSwing.Tests
{
    public sealed class PlayerSwingVisualTests
    {
        [Test]
        public void ConfigureResolvesRightArmBonesAndCreatesOneSocket()
        {
            var rig = CreateRig(out var player, out var upperArm, out var lowerArm);
            var line = player.AddComponent<LineRenderer>();
            var visual = player.AddComponent<PlayerSwingVisual>();

            visual.Configure(rig.transform, upperArm, lowerArm, null, line);
            visual.Configure(rig.transform, upperArm, lowerArm, null, line);

            Assert.That(visual.RightUpperArm, Is.SameAs(upperArm));
            Assert.That(visual.RightLowerArm, Is.SameAs(lowerArm));
            Assert.That(visual.WebOrigin, Is.Not.Null);
            Assert.That(visual.WebOrigin.parent, Is.SameAs(lowerArm));
            Assert.That(CountNamedChildren(lowerArm.transform, "RightWebOrigin"), Is.EqualTo(1));

            Object.DestroyImmediate(player);
        }

        [Test]
        public void SwingAimAlignsBothArmAxesWithAnchor()
        {
            var rig = CreateRig(out var player, out var upperArm, out var lowerArm);
            var line = player.AddComponent<LineRenderer>();
            var visual = player.AddComponent<PlayerSwingVisual>();
            visual.Configure(rig.transform, upperArm, lowerArm, null, line);
            var upperRestY = upperArm.localEulerAngles.y;
            var lowerRestY = lowerArm.localEulerAngles.y;

            var anchor = new Vector3(2f, 2f, 5f);
            visual.SetSwingState(true, anchor);
            for (var frame = 0; frame < 12; frame++)
            {
                InvokePrivate(visual, "LateUpdate");
            }

            var expected = (anchor - upperArm.position).normalized;
            // The swing pose aims toward the anchor while deliberately
            // preserving the authored local-Y rotation to avoid arm twisting.
            // Exact pole alignment is therefore not possible for every rig
            // orientation; the threshold verifies a strong visual aim.
            Assert.That(Vector3.Dot(upperArm.TransformDirection(Vector3.up).normalized, expected), Is.GreaterThan(0.85f));
            expected = (anchor - lowerArm.position).normalized;
            Assert.That(Vector3.Dot(lowerArm.TransformDirection(Vector3.up).normalized, expected), Is.GreaterThan(0.85f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(upperArm.localEulerAngles.y, upperRestY)), Is.LessThan(0.01f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(lowerArm.localEulerAngles.y, lowerRestY)), Is.LessThan(0.01f));
            Assert.That(line.enabled, Is.True);
            Assert.That(line.GetPosition(0), Is.EqualTo(visual.WebOrigin.position));
            Assert.That(line.GetPosition(1), Is.EqualTo(anchor));

            Object.DestroyImmediate(player);
        }

        [Test]
        public void DisableRestoresAuthoredArmRotationsAndHidesWeb()
        {
            var rig = CreateRig(out var player, out var upperArm, out var lowerArm);
            var line = player.AddComponent<LineRenderer>();
            var visual = player.AddComponent<PlayerSwingVisual>();
            visual.Configure(rig.transform, upperArm, lowerArm, null, line);
            var upperRest = upperArm.localRotation;
            var lowerRest = lowerArm.localRotation;

            visual.SetSwingState(true, new Vector3(3f, 2f, 4f));
            for (var frame = 0; frame < 12; frame++)
            {
                InvokePrivate(visual, "LateUpdate");
            }
            visual.enabled = false;
            InvokePrivate(visual, "OnDisable");

            Assert.That(line.enabled, Is.False);
            Assert.That(upperArm.localRotation, Is.EqualTo(upperRest));
            Assert.That(lowerArm.localRotation, Is.EqualTo(lowerRest));

            Object.DestroyImmediate(player);
        }

        [Test]
        public void MissingArmRigDoesNotThrowAndUsesFallbackOrigin()
        {
            var player = new GameObject("Player");
            var line = player.AddComponent<LineRenderer>();
            var visual = player.AddComponent<PlayerSwingVisual>();

            Assert.DoesNotThrow(() => visual.Configure(player.transform, null, null, null, line));
            Assert.DoesNotThrow(() => visual.SetSwingState(true, new Vector3(0f, 4f, 5f)));
            Assert.That(line.enabled, Is.True);

            Object.DestroyImmediate(player);
        }

        private static GameObject CreateRig(
            out GameObject player,
            out Transform upperArm,
            out Transform lowerArm)
        {
            player = new GameObject("Player");
            var rig = new GameObject("base_rig");
            rig.transform.SetParent(player.transform, false);
            upperArm = new GameObject("ArmR1").transform;
            upperArm.SetParent(rig.transform, false);
            lowerArm = new GameObject("ArmR2").transform;
            lowerArm.SetParent(upperArm, false);
            lowerArm.localPosition = Vector3.up;
            return rig;
        }

        private static int CountNamedChildren(Transform parent, string name)
        {
            var count = 0;
            for (var index = 0; index < parent.childCount; index++)
            {
                if (parent.GetChild(index).name == name)
                {
                    count++;
                }
            }

            return count;
        }

        private static void InvokePrivate(object target, string methodName)
        {
            typeof(PlayerSwingVisual)
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, null);
        }
    }
}
