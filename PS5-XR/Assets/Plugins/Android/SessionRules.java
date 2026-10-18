package ru.student.ps5xr;

/** Platform-independent checks used by the Android repository. */
public final class SessionRules {
    private SessionRules() { }
    public static void validateCount(int count) {
        if (count <= 0) throw new IllegalArgumentException("Step count must be positive");
    }
    public static void validateStep(int step, int count) {
        validateCount(count);
        if (step < 0 || step >= count) throw new IllegalArgumentException("Step outside instruction");
    }
    public static void requireComplete(int completed, int total) {
        validateCount(total);
        if (completed != total) throw new IllegalStateException("Instruction is incomplete");
    }
}
