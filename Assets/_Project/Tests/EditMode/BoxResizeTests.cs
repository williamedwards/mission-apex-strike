using GravBoots.Editor;
using NUnit.Framework;
using UnityEngine;

namespace GravBoots.Tests
{
    public class BoxResizeTests
    {
        static readonly Vector3 Size = new Vector3(2f, 2f, 2f);

        // A bounds handle dragged by `delta` on one face reports this centre and size.
        static Vector3 Drag(int axis, float faceSign, float delta, out Vector3 handleCenter)
        {
            Vector3 size = Size;
            size[axis] += delta;
            handleCenter = Vector3.zero;
            handleCenter[axis] = faceSign * delta * 0.5f;
            return size;
        }

        [Test]
        public void GrowingPositiveFaceKeepsNegativeFacePut()
        {
            Vector3 handleSize = Drag(0, +1f, 1.3f, out Vector3 centre);
            Vector3 size = BoxResize.Snap(Size, centre, handleSize, 0.5f, out Vector3 shift);
            Assert.That(size.x, Is.EqualTo(3.5f).Within(1e-5f));
            Assert.That(shift.x, Is.EqualTo(0.75f).Within(1e-5f));
            Assert.That(-Size.x * 0.5f, Is.EqualTo(shift.x - size.x * 0.5f).Within(1e-5f), "the -X face stays at -1");
        }

        [Test]
        public void ShrinkingNegativeFaceKeepsPositiveFacePut()
        {
            Vector3 handleSize = Drag(2, -1f, -0.8f, out Vector3 centre);
            Vector3 size = BoxResize.Snap(Size, centre, handleSize, 0.5f, out Vector3 shift);
            Assert.That(size.z, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(shift.z + size.z * 0.5f, Is.EqualTo(Size.z * 0.5f).Within(1e-5f), "the +Z face stays at +1");
        }

        [Test]
        public void NeverShrinksBelowOneStep()
        {
            Vector3 handleSize = Drag(1, +1f, -5f, out Vector3 centre);
            Vector3 size = BoxResize.Snap(Size, centre, handleSize, 0.5f, out _);
            Assert.That(size.y, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void UntouchedAxesDontChange()
        {
            Vector3 handleSize = Drag(0, +1f, 1f, out Vector3 centre);
            Vector3 size = BoxResize.Snap(Size, centre, handleSize, 0.5f, out Vector3 shift);
            Assert.That(size.y, Is.EqualTo(Size.y));
            Assert.That(size.z, Is.EqualTo(Size.z));
            Assert.That(shift.y, Is.EqualTo(0f));
            Assert.That(shift.z, Is.EqualTo(0f));
        }
    }
}
