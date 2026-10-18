import ru.student.ps5xr.SessionRules;

public class SessionRulesTest {
    private static void fails(Runnable action) {
        try { action.run(); } catch (IllegalArgumentException | IllegalStateException expected) { return; }
        throw new AssertionError("Expected rejection");
    }
    public static void main(String[] args) {
        SessionRules.validateStep(0, 10);
        SessionRules.validateStep(9, 10);
        SessionRules.requireComplete(10, 10);
        fails(() -> SessionRules.validateCount(0));
        fails(() -> SessionRules.validateStep(-1, 10));
        fails(() -> SessionRules.validateStep(10, 10));
        fails(() -> SessionRules.requireComplete(9, 10));
        System.out.println("PS5_XR_JAVA_RULES_PASSED");
    }
}
