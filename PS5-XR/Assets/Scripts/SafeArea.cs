using UnityEngine;

namespace Ps5Maintenance
{
    public class SafeArea : MonoBehaviour
    {
        private Rect last;
        private void Update()
        {
            var area = Screen.safeArea;
            if (area == last || Screen.width == 0 || Screen.height == 0) return;
            last = area;
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            rect.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        }
    }
}
