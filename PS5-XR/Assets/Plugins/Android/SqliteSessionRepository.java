package ru.student.ps5xr;

import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.database.sqlite.SQLiteDatabase;
import android.database.sqlite.SQLiteOpenHelper;

/** Local storage: no server, account or network permission required. */
public final class SqliteSessionRepository extends SQLiteOpenHelper implements SessionRepository {
    public SqliteSessionRepository(Context context) {
        super(context.getApplicationContext(), "ps5-maintenance.db", null, 1);
    }
    @Override public void onConfigure(SQLiteDatabase db) { db.setForeignKeyConstraintsEnabled(true); }
    @Override public void onCreate(SQLiteDatabase db) {
        db.execSQL("CREATE TABLE sessions (id INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "equipment TEXT NOT NULL, model TEXT NOT NULL, step_count INTEGER NOT NULL CHECK(step_count > 0), " +
            "started_at INTEGER NOT NULL, finished_at INTEGER)");
        db.execSQL("CREATE TABLE completed_steps (session_id INTEGER NOT NULL REFERENCES sessions(id) ON DELETE CASCADE, " +
            "step_number INTEGER NOT NULL CHECK(step_number >= 0), completed_at INTEGER NOT NULL, " +
            "PRIMARY KEY(session_id, step_number))");
    }
    @Override public void onUpgrade(SQLiteDatabase db, int oldVersion, int newVersion) {
        throw new IllegalStateException("No migration for database version " + newVersion);
    }
    @Override public long begin(String equipment, String model, int count) {
        SessionRules.validateCount(count);
        if (equipment == null || equipment.isEmpty() || model == null || model.isEmpty())
            throw new IllegalArgumentException("Equipment and model are required");
        ContentValues values = new ContentValues();
        values.put("equipment", equipment); values.put("model", model);
        values.put("step_count", count); values.put("started_at", System.currentTimeMillis());
        return getWritableDatabase().insertOrThrow("sessions", null, values);
    }
    private int activeCount(SQLiteDatabase db, long id) {
        try (Cursor row = db.rawQuery("SELECT step_count, finished_at FROM sessions WHERE id = ?", new String[] { Long.toString(id) })) {
            if (!row.moveToFirst()) throw new IllegalArgumentException("Unknown session");
            if (!row.isNull(1)) throw new IllegalStateException("Session is already finished");
            return row.getInt(0);
        }
    }
    @Override public void recordStep(long id, int step) {
        SQLiteDatabase db = getWritableDatabase();
        db.beginTransaction();
        try {
            int total = activeCount(db, id);
            SessionRules.validateStep(step, total);
            try (Cursor rows = db.rawQuery("SELECT COUNT(*) FROM completed_steps WHERE session_id = ? AND step_number < ?",
                    new String[] { Long.toString(id), Integer.toString(step) })) {
                rows.moveToFirst();
                if (rows.getInt(0) != step) throw new IllegalStateException("Previous steps must be completed first");
            }
            ContentValues values = new ContentValues();
            values.put("session_id", id); values.put("step_number", step);
            values.put("completed_at", System.currentTimeMillis());
            db.insertWithOnConflict("completed_steps", null, values, SQLiteDatabase.CONFLICT_IGNORE);
            db.setTransactionSuccessful();
        } finally { db.endTransaction(); }
    }
    @Override public void finish(long id) {
        SQLiteDatabase db = getWritableDatabase();
        db.beginTransaction();
        try {
            int total = activeCount(db, id);
            try (Cursor row = db.rawQuery("SELECT COUNT(*) FROM completed_steps WHERE session_id = ?", new String[] { Long.toString(id) })) {
                row.moveToFirst(); SessionRules.requireComplete(row.getInt(0), total);
            }
            ContentValues values = new ContentValues();
            values.put("finished_at", System.currentTimeMillis());
            db.update("sessions", values, "id = ?", new String[] { Long.toString(id) });
            db.setTransactionSuccessful();
        } finally { db.endTransaction(); }
    }
    @Override public int completedSessions() {
        try (Cursor row = getReadableDatabase().rawQuery("SELECT COUNT(*) FROM sessions WHERE finished_at IS NOT NULL", null)) {
            row.moveToFirst(); return row.getInt(0);
        }
    }
}
