using UnityEngine;

namespace Ps5Maintenance
{
    [CreateAssetMenu(menuName = "PS5 XR/Equipment layout")]
    public class EquipmentLayout : ScriptableObject
    {
        [Tooltip("Enable only after checking each target on the actual console and marker.")]
        public bool calibratedForRealConsole;
        public ConsoleModel calibratedModel;
        public string exactCfiModel = "";
        [Tooltip("One position per instruction step; metres in the marker coordinate system.")]
        public Vector3[] targets;
        public bool CanShowRealAr(ConsoleModel selected, int count) =>
            calibratedForRealConsole && selected != ConsoleModel.Unknown &&
            calibratedModel == selected && !string.IsNullOrWhiteSpace(exactCfiModel) &&
            targets != null && targets.Length == count;
    }
}
