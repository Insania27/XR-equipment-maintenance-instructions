using System;
using System.IO;
using UnityEngine;

namespace Ps5Maintenance
{
    public interface IProgressRepository : IDisposable
    {
        string StorageName { get; }
        void Begin(string equipment, string model, int stepCount);
        void RecordStep(int stepNumber);
        void Finish();
        int CompletedSessions();
    }

    [Serializable]
    public class LocalSessionRecord
    {
        public string equipment;
        public string model;
        public string startedAt;
        public string finishedAt;
        public bool[] completed;
    }

    // Desktop preview deliberately uses JSON; SQLite/Java runs in the Android build.
    public class JsonProgressRepository : IProgressRepository
    {
        private LocalSessionRecord record;
        private string path;
        private readonly string folder = Path.Combine(Application.persistentDataPath, "sessions");
        public string StorageName => "JSON · режим ПК";
        public void Begin(string equipment, string model, int stepCount)
        {
            Directory.CreateDirectory(folder);
            record = new LocalSessionRecord { equipment = equipment, model = model,
                startedAt = DateTime.UtcNow.ToString("o"), completed = new bool[stepCount] };
            path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".json");
            Save();
        }
        private void Save() => File.WriteAllText(path, JsonUtility.ToJson(record, true));
        public void RecordStep(int stepNumber)
        {
            if (record == null || stepNumber < 0 || stepNumber >= record.completed.Length)
                throw new InvalidOperationException("Недопустимый шаг сеанса.");
            record.completed[stepNumber] = true;
            Save();
        }
        public void Finish()
        {
            if (record == null || Array.Exists(record.completed, value => !value))
                throw new InvalidOperationException("Не все шаги выполнены.");
            record.finishedAt = DateTime.UtcNow.ToString("o");
            Save();
        }
        public int CompletedSessions()
        {
            if (!Directory.Exists(folder)) return 0;
            int count = 0;
            foreach (var file in Directory.GetFiles(folder, "*.json"))
            {
                var saved = JsonUtility.FromJson<LocalSessionRecord>(File.ReadAllText(file));
                if (!string.IsNullOrEmpty(saved.finishedAt)) count++;
            }
            return count;
        }
        public void Dispose() { }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    public sealed class JavaProgressRepository : IProgressRepository
    {
        private readonly AndroidJavaObject bridge;
        public string StorageName => "Java · SQLite";
        public JavaProgressRepository()
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            bridge = new AndroidJavaObject("ru.student.ps5xr.MaintenanceBridge", activity);
        }
        public void Begin(string equipment, string model, int count) => bridge.Call("begin", equipment, model, count);
        public void RecordStep(int number) => bridge.Call("recordStep", number);
        public void Finish() => bridge.Call("finish");
        public int CompletedSessions() => bridge.Call<int>("completedSessions");
        public void Dispose() { bridge.Call("close"); bridge.Dispose(); }
    }
#endif
}
