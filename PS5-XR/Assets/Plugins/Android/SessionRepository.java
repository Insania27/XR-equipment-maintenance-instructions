package ru.student.ps5xr;

public interface SessionRepository {
    long begin(String equipment, String model, int stepCount);
    void recordStep(long sessionId, int stepNumber);
    void finish(long sessionId);
    int completedSessions();
    void close();
}
