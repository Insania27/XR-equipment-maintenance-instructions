using System;
using System.IO;
using UnityEngine;

namespace Ps5Maintenance
{
    [Serializable]
    public sealed class ArrowCalibration
    {
        public string instructionId;
        public ConsoleModel model;
        public Vector3[] targets;
        public bool[] adjusted;

        public static ArrowCalibration Create(InstructionData data, EquipmentLayout layout, ConsoleModel model)
        {
            var profile = new ArrowCalibration { instructionId = data.id, model = model,
                targets = new Vector3[data.steps.Length], adjusted = new bool[data.steps.Length] };
            bool layoutMatches = layout != null && layout.CanShowRealAr(model, data.steps.Length);
            for (int i = 0; i < data.steps.Length; i++)
            {
                profile.targets[i] = layoutMatches ? layout.targets[i] : ApproximateTarget(data.steps[i], model);
                profile.adjusted[i] = layoutMatches && i < 6;
            }
            return profile;
        }

        // A deliberately approximate horizontal console, centred 50 cm along marker +Z (TOP).
        // These are schematic envelopes, not measured locations of real PS5 components.
        public static Vector3 ApproximateTarget(InstructionStep step, ConsoleModel model)
        {
            float width = model == ConsoleModel.Slim || model == ConsoleModel.Pro ? .22f : .27f;
            float length = model == ConsoleModel.Slim ? .36f : .40f;
            float height = model == ConsoleModel.Pro ? .09f : model == ConsoleModel.Slim ? .10f : .11f;
            float x = (step.target.x - .18f) / .27f * width;
            float z = step.target.z / .41f * length;
            return new Vector3(x, height, .50f + z);
        }

        public bool Matches(InstructionData data, ConsoleModel selected)
        {
            if (instructionId != data.id || model != selected || targets == null || adjusted == null ||
                targets.Length != data.steps.Length || adjusted.Length != data.steps.Length) return false;
            foreach (var target in targets)
                if (!float.IsFinite(target.x) || !float.IsFinite(target.y) || !float.IsFinite(target.z) || target.magnitude > 3f) return false;
            return true;
        }

        public static ArrowCalibration Load(string folder, InstructionData data, EquipmentLayout layout, ConsoleModel model)
        {
            var path = ProfilePath(folder, model);
            try
            {
                if (File.Exists(path))
                {
                    var saved = JsonUtility.FromJson<ArrowCalibration>(File.ReadAllText(path));
                    if (saved != null && saved.Matches(data, model)) return saved;
                }
            }
            catch (Exception e) { Debug.LogWarning("Arrow calibration load: " + e.Message); }
            return Create(data, layout, model);
        }

        public void Move(int step, Vector3 delta)
        {
            targets[step] = Vector3.ClampMagnitude(targets[step] + delta, 2f);
            adjusted[step] = false;
        }

        public void SaveStep(string folder, int step)
        {
            Directory.CreateDirectory(folder);
            bool previous = adjusted[step];
            adjusted[step] = true;
            try { File.WriteAllText(ProfilePath(folder, model), JsonUtility.ToJson(this, true)); }
            catch { adjusted[step] = previous; throw; }
        }

        private static string ProfilePath(string folder, ConsoleModel model) => Path.Combine(folder, "ar-arrows-" + model + ".json");
    }
}
