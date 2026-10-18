using System;
using UnityEngine;

namespace Ps5Maintenance
{
    public enum ConsoleModel { Unknown, Original, Slim, Pro }

    [Serializable]
    public class InstructionStep
    {
        public string id;
        public string title;
        public string description;
        public string original;
        public string slim;
        public string pro;
        public string caution;
        public bool requiresModel;
        public bool showArrow;
        public Vector3 target;
        public string TextFor(ConsoleModel model)
        {
            string specific = model == ConsoleModel.Original ? original :
                model == ConsoleModel.Slim ? slim : model == ConsoleModel.Pro ? pro : null;
            return description + (string.IsNullOrEmpty(specific) ? "" : "\n\n" + specific);
        }
    }

    [Serializable]
    public class InstructionData
    {
        public string id;
        public string equipment;
        public string sourceUrl;
        public InstructionStep[] steps;

        public static InstructionData Load()
        {
            var file = Resources.Load<TextAsset>("ps5-instruction");
            if (file == null) throw new InvalidOperationException("Не найден файл инструкции.");
            var data = JsonUtility.FromJson<InstructionData>(file.text);
            if (data == null || data.steps == null || data.steps.Length == 0)
                throw new InvalidOperationException("Инструкция не содержит шагов.");
            return data;
        }
    }

}
