package com.example.appquanlychitieu.data.remote;

import android.content.Context;
import android.content.Intent;

/** Process-wide notification used when refresh can no longer recover a 401. */
public final class SessionEvents {
    public static final String ACTION_SESSION_EXPIRED =
            "com.example.appquanlychitieu.SESSION_EXPIRED";

    private SessionEvents() {}

    public static void notifyExpired(Context context) {
        Intent intent = new Intent(ACTION_SESSION_EXPIRED)
                .setPackage(context.getPackageName());
        context.getApplicationContext().sendBroadcast(intent);
    }
}
