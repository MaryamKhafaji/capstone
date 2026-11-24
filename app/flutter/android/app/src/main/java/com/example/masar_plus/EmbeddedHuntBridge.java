package com.example.masar_plus;

import android.app.Activity;
import android.app.ActivityManager;
import android.content.Context;
import android.content.Intent;
import android.util.AtomicFile;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.ThreadLocalRandom;
import org.json.JSONException;
import org.json.JSONObject;

/** Called by Unity in MasAR+'s private game process. */
public final class EmbeddedHuntBridge {
    static final String XR_ACTIVITY = "ai.nreal.activitylife.NRXRActivity";
    private static final String UNITY_ACTIVITY = "com.unity3d.player.UnityPlayerActivity";

    private static String debugLaunchId;
    private static String debugMonster;

    private EmbeddedHuntBridge() {}

    static void prepareLaunch(Context context, String uid, boolean debug, int crystals)
        throws JSONException, IOException {
        JSONObject config = new JSONObject()
            .put("launchId", UUID.randomUUID().toString())
            .put("userId", uid)
            .put("debug", debug)
            .put("crystals", crystals);
        write(launchFile(context), config);
    }

    private static AtomicFile launchFile(Context context) {
        return new AtomicFile(new File(context.getFilesDir(), "ar_launch.json"));
    }

    private static JSONObject launch(Context context) throws JSONException, IOException {
        return new JSONObject(new String(launchFile(context).readFully(), StandardCharsets.UTF_8));
    }

    static boolean ownedBy(Context context, String uid) throws JSONException, IOException {
        return uid.equals(launch(context).getString("userId"));
    }

    public static int getInitialCrystals(Activity activity) throws JSONException, IOException {
        return Math.max(0, launch(activity).optInt("crystals", 0));
    }

    public static synchronized String getHuntState(Activity activity) throws JSONException, IOException {
        JSONObject config = launch(activity);
        if (!config.getBoolean("debug")) return HuntLocationService.huntState();

        String launchId = config.getString("launchId");
        if (!launchId.equals(debugLaunchId) || debugMonster == null) {
            debugLaunchId = launchId;
            String[] monsters = {"bu_daryah", "om_el_saaf", "tantal", "hamaret_al_qayla"};
            debugMonster = monsters[ThreadLocalRandom.current().nextInt(monsters.length)];
        }
        return new JSONObject()
            .put("status", "arrived")
            .put("closeRangeDebug", true)
            .put("monsterId", debugMonster)
            .toString();
    }

    public static synchronized void requestNextHunt() {
        debugMonster = null;
        HuntLocationService.requestNextHunt();
    }

    public static void queueResult(Activity activity, String result) throws JSONException, IOException {
        String uid = launch(activity).getString("userId");
        JSONObject hunt = new JSONObject(result);
        File path = resultFile(activity, hunt.getString("huntId"));
        if (path == null) throw new JSONException("Invalid hunt ID.");
        hunt.put("userId", uid);

        write(new AtomicFile(path), hunt);
    }

    private static void write(AtomicFile file, JSONObject json) throws IOException {
        FileOutputStream stream = file.startWrite();
        try {
            stream.write(json.toString().getBytes(StandardCharsets.UTF_8));
            file.finishWrite(stream);
        } catch (IOException e) {
            file.failWrite(stream);
            throw e;
        }
    }

    static File resultDirectory(Context context) {
        File directory = new File(context.getFilesDir(), "completed_hunts");
        directory.mkdirs();
        return directory;
    }

    static File resultFile(Context context, String id) {
        return id.matches("[a-f0-9]{32}") ? new File(resultDirectory(context), id + ".json") : null;
    }

    public static void returnToApp(Activity activity) {
        activity.runOnUiThread(() -> {
            Intent home = new Intent(activity, MainActivity.class)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP |
                    Intent.FLAG_ACTIVITY_SINGLE_TOP);
            activity.startActivity(home);
            endHunt(activity);
            activity.finish();
        });
    }

    public static void showLocationError(Activity activity, String message) {
        activity.runOnUiThread(() -> android.widget.Toast.makeText(activity, message,
            android.widget.Toast.LENGTH_LONG).show());
    }

    static void endHunt(Context context) {
        context.stopService(new Intent(context, HuntLocationService.class));
        for (ActivityManager.AppTask task : gameTasks(context)) task.finishAndRemoveTask();
    }

    static List<ActivityManager.AppTask> gameTasks(Context context) {
        ActivityManager manager = context.getSystemService(ActivityManager.class);
        List<ActivityManager.AppTask> gameTasks = new ArrayList<>();
        for (ActivityManager.AppTask task : manager.getAppTasks()) {
            android.content.ComponentName root = task.getTaskInfo().baseActivity;
            if (root != null && (XR_ACTIVITY.equals(root.getClassName()) ||
                    UNITY_ACTIVITY.equals(root.getClassName()))) {
                gameTasks.add(task);
            }
        }
        return gameTasks;
    }
}
