package com.example.masar_plus;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Intent;
import android.content.pm.ServiceInfo;
import android.location.Location;
import android.location.LocationListener;
import android.location.LocationManager;
import android.os.IBinder;
import android.os.Looper;
import android.os.SystemClock;
import android.util.Log;
import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.io.IOException;
import java.io.InputStream;
import java.io.ByteArrayOutputStream;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.Map;
import java.util.Random;
import java.util.TreeMap;

/** Android owns location and hunt selection, even when the phone screen is off. */
public final class HuntLocationService extends Service implements LocationListener {
    static final String USER_ID = "masar.player_id";
    static final String SPAWN_CATALOG = "masar.spawn_catalog";
    private static final String CHANNEL = "ar_hunt_location";
    private static final int NOTIFICATION = 702;
    private static final float MAX_SELECTION_ACCURACY = 50f;
    private static final long MAX_FIX_AGE_MS = 30_000;
    private static final Random RANDOM = new Random();

    private record Site(String monsterId, String name, Location point, float arrivalRadius) {}
    private record Candidate(Site site, float distance) {}

    private static HuntLocationService active;
    private LocationManager locations;
    private String playerId;
    private final List<Site> sites = new ArrayList<>();
    private Location latestFix;
    private Site selectedSite;
    private double maxDistance;
    private String error;

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        String user = intent == null ? null : intent.getStringExtra(USER_ID);
        if (user == null || user.trim().isEmpty()) {
            stopSelf();
            return START_NOT_STICKY;
        }

        startForeground(NOTIFICATION, notification(), ServiceInfo.FOREGROUND_SERVICE_TYPE_LOCATION);
        synchronized (HuntLocationService.class) {
            if (user.equals(playerId) && active == this) return START_NOT_STICKY;
            playerId = user;
            active = this;
            selectedSite = null;
            latestFix = null;
            error = null;
            try {
                loadSites(intent.getStringExtra(SPAWN_CATALOG));
            } catch (IOException | JSONException e) {
                Log.e("MasarHunt", "Invalid spawn catalog", e);
                error = "catalog_error";
                return START_NOT_STICKY;
            }
            locations = getSystemService(LocationManager.class);
            locations.removeUpdates(this);
            if (!sites.isEmpty()) {
                try {
                    locations.requestLocationUpdates(LocationManager.GPS_PROVIDER, 1_000L,
                        1f, this, Looper.getMainLooper());
                    Location cached = locations.getLastKnownLocation(LocationManager.GPS_PROVIDER);
                    if (usable(cached)) latestFix = cached;
                } catch (SecurityException e) {
                    error = "no_permission";
                }
            }
        }
        return START_NOT_STICKY;
    }

    private Notification notification() {
        getSystemService(NotificationManager.class).createNotificationChannel(
            new NotificationChannel(CHANNEL, "AR hunt location", NotificationManager.IMPORTANCE_LOW));
        Intent stop = new Intent(this, EndHuntReceiver.class);
        PendingIntent stopIntent = PendingIntent.getBroadcast(this, 0, stop, PendingIntent.FLAG_IMMUTABLE);
        return new Notification.Builder(this, CHANNEL)
            .setSmallIcon(android.R.drawable.ic_menu_mylocation)
            .setContentTitle("MasAR+ hunt is active")
            .setContentText("Using GPS to guide you to the monster's location.")
            .setOnlyAlertOnce(true)
            .setOngoing(true)
            .addAction(new Notification.Action.Builder(null, "End hunt", stopIntent).build())
            .build();
    }

    private void loadSites(String remoteCatalog) throws IOException, JSONException {
        sites.clear();
        try (InputStream input = getAssets().open("hunt_sites.json")) {
            ByteArrayOutputStream bytes = new ByteArrayOutputStream();
            byte[] buffer = new byte[4096];
            int count;
            while ((count = input.read(buffer)) != -1) bytes.write(buffer, 0, count);
            JSONObject catalog = new JSONObject(new String(bytes.toByteArray(), StandardCharsets.UTF_8));
            maxDistance = catalog.getDouble("maxSearchMeters");
            if (!Double.isFinite(maxDistance) || maxDistance <= 0)
                throw new JSONException("maxSearchMeters must be positive.");
            JSONArray entries = catalog.getJSONArray("sites");
            JSONObject managed = new JSONObject(remoteCatalog == null ? "{}" : remoteCatalog);
            JSONArray combined = new JSONArray();
            for (int i = 0; i < entries.length(); i++) {
                JSONObject entry = entries.getJSONObject(i);
                if (!managed.has(entry.getString("monsterId"))) combined.put(entry);
            }
            for (String monster : List.of("bu_daryah", "om_el_saaf")) {
                if (!managed.has(monster)) continue;
                JSONArray managedSites = managed.getJSONArray(monster);
                for (int i = 0; i < managedSites.length(); i++) combined.put(managedSites.getJSONObject(i));
            }
            entries = combined;
            for (int i = 0; i < entries.length(); i++) {
                JSONObject entry = entries.getJSONObject(i);
                String monster = entry.getString("monsterId");
                String name = entry.getString("name").trim();
                double latitude = entry.getDouble("latitude");
                double longitude = entry.getDouble("longitude");
                double radius = entry.getDouble("arrivalRadiusMeters");
                if ((!monster.equals("bu_daryah") && !monster.equals("om_el_saaf")) ||
                    name.isEmpty() || !Double.isFinite(latitude) || Math.abs(latitude) > 90 ||
                    !Double.isFinite(longitude) || Math.abs(longitude) > 180 ||
                    !(radius >= 10 && radius < maxDistance)) {
                    throw new JSONException("Invalid hunt site at index " + i);
                }
                Location point = new Location("hunt_site");
                point.setLatitude(latitude);
                point.setLongitude(longitude);
                sites.add(new Site(monster, name, point, (float) radius));
            }
        }
    }

    @Override
    public void onLocationChanged(Location location) {
        synchronized (HuntLocationService.class) {
            if (active == this && usable(location)) latestFix = location;
        }
    }

    @Override
    public void onDestroy() {
        if (locations != null) locations.removeUpdates(this);
        synchronized (HuntLocationService.class) {
            if (active == this) active = null;
        }
        super.onDestroy();
    }

    static synchronized void requestNextHunt() {
        if (active != null) {
            active.selectedSite = null;
        }
    }

    static synchronized String huntState() throws JSONException {
        return active == null ? state("locating").toString() : active.describeHunt().toString();
    }

    private JSONObject describeHunt() throws JSONException {
        if (error != null) return state(error);
        if (sites.isEmpty()) return state("no_sites");
        if (!locations.isProviderEnabled(LocationManager.GPS_PROVIDER)) return siteState("gps_off");
        if (!usable(latestFix)) return siteState("locating");

        if (selectedSite == null) {
            selectedSite = chooseNearbySite(latestFix);
            if (selectedSite == null) return state("no_nearby_sites");
        }

        float distance = latestFix.distanceTo(selectedSite.point());
        // Require the GPS confidence circle to fit inside the arrival zone.
        boolean arrived = distance + latestFix.getAccuracy() <= selectedSite.arrivalRadius();
        return siteState(arrived ? "arrived" : "en_route")
            .put("distanceMeters", Math.round(distance))
            .put("bearingDegrees", latestFix.bearingTo(selectedSite.point()))
            .put("accuracyLimited", latestFix.getAccuracy() > selectedSite.arrivalRadius() / 2);
    }

    private Site chooseNearbySite(Location player) {
        Map<String, List<Candidate>> byMonster = new TreeMap<>();
        for (Site site : sites) {
            float distance = player.distanceTo(site.point());
            if (distance <= maxDistance) {
                byMonster.computeIfAbsent(site.monsterId(), key -> new ArrayList<>())
                    .add(new Candidate(site, distance));
            }
        }
        if (byMonster.isEmpty()) return null;

        List<List<Candidate>> groups = new ArrayList<>(byMonster.values());
        List<Candidate> choices = groups.get(RANDOM.nextInt(groups.size()));
        choices.sort(Comparator.comparingDouble(Candidate::distance));
        return choices.get(RANDOM.nextInt(Math.min(3, choices.size()))).site();
    }

    private static boolean usable(Location fix) {
        return fix != null && fix.hasAccuracy() && fix.getAccuracy() <= MAX_SELECTION_ACCURACY
            && SystemClock.elapsedRealtime() - fix.getElapsedRealtimeNanos() / 1_000_000 <= MAX_FIX_AGE_MS;
    }

    private static JSONObject state(String status) throws JSONException {
        return new JSONObject().put("status", status);
    }

    private JSONObject siteState(String status) throws JSONException {
        JSONObject data = state(status);
        if (selectedSite != null) {
            data.put("monsterId", selectedSite.monsterId());
            data.put("siteName", selectedSite.name());
        }
        return data;
    }
}
