package ru.student.ps5xr;

import android.content.Context;

/** JNI entry point called by Unity's AndroidJavaObject. */
public final class MaintenanceBridge {
    private final SessionRepository repository;
    private long activeId = -1;
    public MaintenanceBridge(Context context) { repository = new SqliteSessionRepository(context); }
    public void begin(String equipment, String model, int count) { activeId = repository.begin(equipment, model, count); }
    private void requireSession() {
        if (activeId < 0) throw new IllegalStateException("Start a session first");
    }
    public void recordStep(int number) { requireSession(); repository.recordStep(activeId, number); }
    public void finish() { requireSession(); repository.finish(activeId); activeId = -1; }
    public int completedSessions() { return repository.completedSessions(); }
    public void close() { repository.close(); }
}
