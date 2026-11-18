package com.example.masar_plus;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

public final class EndHuntReceiver extends BroadcastReceiver {
    @Override
    public void onReceive(Context context, Intent intent) {
        EmbeddedHuntBridge.endHunt(context);
    }
}
