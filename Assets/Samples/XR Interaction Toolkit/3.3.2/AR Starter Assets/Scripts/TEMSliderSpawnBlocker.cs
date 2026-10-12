#if AR_FOUNDATION_PRESENT
using UnityEngine;
using UnityEngine.EventSystems;

namespace UnityEngine.XR.Interaction.Toolkit.Samples.ARStarterAssets
{
    /// <summary>
    /// Prevents AR placement while the diameter slider owns the current pointer gesture,
    /// including the frame in which the pointer is released.
    /// </summary>
    public sealed class TEMSliderSpawnBlocker : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, ICancelHandler
    {
        static bool s_PointerDownOnSlider;
        static int s_BlockThroughFrame = -1;

        public static bool IsBlockingSpawn => s_PointerDownOnSlider || Time.frameCount <= s_BlockThroughFrame;

        public void OnPointerDown(PointerEventData eventData)
        {
            s_PointerDownOnSlider = true;
            s_BlockThroughFrame = Time.frameCount + 1;
        }

        public void OnPointerUp(PointerEventData eventData) => ReleasePointer();

        public void OnCancel(BaseEventData eventData) => ReleasePointer();

        void OnDisable() => ReleasePointer();

        static void ReleasePointer()
        {
            s_PointerDownOnSlider = false;
            s_BlockThroughFrame = Time.frameCount + 1;
        }
    }
}
#endif
