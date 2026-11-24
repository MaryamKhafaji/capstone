package com.example.masar_plus;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Intent;
import android.content.Context;
import android.content.pm.ServiceInfo;
import android.location.Location;
import android.location.LocationListener;
import android.location.LocationManager;
import android.hardware.Sensor;
import android.hardware.SensorEvent;
import android.hardware.SensorEventListener;
import android.hardware.SensorManager;
import android.hardware.GeomagneticField;
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
public final class HuntLocationService extends Service implements LocationListener, SensorEventListener {
    static final String USER_ID = "masar.player_id";
    static final String SPAWN_CATALOG = "masar.spawn_catalog";
    private static final String CHANNEL = "ar_hunt_location";
    private static final int NOTIFICATION = 702;
    // A rough network fix is enough to choose a nearby destination, including
    // indoors. Arrival still requires the much tighter site-specific accuracy.
    private static final float MAX_SELECTION_ACCURACY = 500f;
    // Indoor Wi-Fi location is often around 100 m even with precise access.
    // Still require the reported position itself to be inside the site's radius.
    private static final float MAX_ARRIVAL_ACCURACY = 100f;
    private static final long MAX_FIX_AGE_MS = 120_000;
    private static final Random RANDOM = new Random();

    private record Site(String monsterId, String name, Location point, float arrivalRadius, double maxSearchDistance) {}
    private record Candidate(Site site, float distance) {}

    private static HuntLocationService active;
    private LocationManager locations;
    private String playerId;
    private final List<Site> sites = new ArrayList<>();
    private Location latestFix;
    private Site selectedSite;
    private String error;
    private SensorManager sensors;
    private float magneticHeading;
    private boolean hasHeading;

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
                    // GPS alone can remain silent indoors. Use all available Android
                    // providers, still enforcing freshness and accuracy on every fix.
                    for (String provider : locations.getProviders(true)) {
                        if (LocationManager.PASSIVE_PROVIDER.equals(provider)) continue;
                        locations.requestLocationUpdates(provider, 1_000L, 1f, this, Looper.getMainLooper());
                        Location cached = locations.getLastKnownLocation(provider);
                        if (usable(cached) && (latestFix == null || cached.getElapsedRealtimeNanos() > latestFix.getElapsedRealtimeNanos())) latestFix = cached;
                    }
                    sensors = getSystemService(SensorManager.class);
                    Sensor rotation = sensors.getDefaultSensor(Sensor.TYPE_ROTATION_VECTOR);
                    if (rotation != null) sensors.registerListener(this, rotation, SensorManager.SENSOR_DELAY_UI);
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
        sites.addAll(readSites(this, remoteCatalog));
    }

    private static List<Site> readSites(Context context, String remoteCatalog) throws IOException, JSONException {
        List<Site> sites = new ArrayList<>();
        try (InputStream input = context.getAssets().open("hunt_sites.json")) {
            ByteArrayOutputStream bytes = new ByteArrayOutputStream();
            byte[] buffer = new byte[4096];
            int count;
            while ((count = input.read(buffer)) != -1) bytes.write(buffer, 0, count);
            JSONObject catalog = new JSONObject(new String(bytes.toByteArray(), StandardCharsets.UTF_8));
            double maxDistance = catalog.getDouble("maxSearchMeters");
            if (!Double.isFinite(maxDistance) || maxDistance <= 0)
                throw new JSONException("maxSearchMeters must be positive.");
            JSONArray entries = catalog.getJSONArray("sites");
            JSONObject managed = new JSONObject(remoteCatalog == null ? "{}" : remoteCatalog);
            JSONArray combined = new JSONArray();
            for (int i = 0; i < entries.length(); i++) {
                JSONObject entry = entries.getJSONObject(i);
                if (!managed.has(entry.getString("monsterId"))) combined.put(entry);
            }
            for (String monster : List.of("bu_daryah", "om_el_saaf", "tantal", "hamaret_al_qayla")) {
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
                // Older locations inherit the existing one-hour search limit.
                double searchDistance = entry.optDouble("maxSearchMeters", maxDistance);
                if ((!monster.equals("bu_daryah") && !monster.equals("om_el_saaf") && !monster.equals("tantal") && !monster.equals("hamaret_al_qayla")) ||
                    name.isEmpty() || !Double.isFinite(latitude) || Math.abs(latitude) > 90 ||
                    !Double.isFinite(longitude) || Math.abs(longitude) > 180 ||
                    !(radius >= 10 && radius <= 250) ||
                    !Double.isFinite(searchDistance) || searchDistance <= radius || searchDistance > maxDistance) {
                    throw new JSONException("Invalid hunt site at index " + i);
                }
                Location point = new Location("hunt_site");
                point.setLatitude(latitude);
                point.setLongitude(longitude);
                sites.add(new Site(monster, name, point, (float) radius, searchDistance));
            }
        }
        return sites;
    }

    @Override
    public void onLocationChanged(Location location) {
        synchronized (HuntLocationService.class) {
            if (active == this && usable(location)
                && (latestFix == null || !usable(latestFix)
                    || location.getAccuracy() <= latestFix.getAccuracy()
                    || location.getElapsedRealtimeNanos() - latestFix.getElapsedRealtimeNanos() > 30_000_000_000L)) {
                latestFix = location;
                Log.d("MasarHunt", "Location updated from " + location.getProvider() + "; accuracy=" + location.getAccuracy());
            }
        }
    }

    @Override public void onSensorChanged(SensorEvent event) {
        float[] rotation = new float[9];
        float[] orientation = new float[3];
        SensorManager.getRotationMatrixFromVector(rotation, event.values);
        SensorManager.getOrientation(rotation, orientation);
        synchronized (HuntLocationService.class) {
            magneticHeading = (float) Math.toDegrees(orientation[0]);
            hasHeading = true;
        }
    }
    @Override public void onAccuracyChanged(Sensor sensor, int accuracy) { }

    @Override
    public void onDestroy() {
        if (locations != null) locations.removeUpdates(this);
        if (sensors != null) sensors.unregisterListener(this);
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
        if (!locations.isLocationEnabled()) return siteState("gps_off");
        if (!usable(latestFix)) return siteState("locating");

        if (selectedSite == null) {
            selectedSite = chooseNearbySite(latestFix);
            if (selectedSite == null) return state("no_nearby_sites");
        }

        float distance = latestFix.distanceTo(selectedSite.point());
        // A usable fix inside the zone is enough. Requiring the entire accuracy
        // circle to fit made small sites impossible even when standing on the pin.
        boolean arrived = distance <= selectedSite.arrivalRadius()
            && latestFix.getAccuracy() <= Math.max(MAX_ARRIVAL_ACCURACY, selectedSite.arrivalRadius());
        return siteState(arrived ? "arrived" : "en_route")
            .put("playerLatitude", latestFix.getLatitude())
            .put("playerLongitude", latestFix.getLongitude())
            .put("accuracyMeters", latestFix.getAccuracy())
            .put("hasHeading", hasHeading)
            .put("headingDegrees", magneticHeading + new GeomagneticField(
                (float) latestFix.getLatitude(), (float) latestFix.getLongitude(), 0f,
                System.currentTimeMillis()).getDeclination())
            .put("distanceMeters", Math.round(distance))
            .put("bearingDegrees", latestFix.bearingTo(selectedSite.point()))
            .put("accuracyLimited", latestFix.getAccuracy() > selectedSite.arrivalRadius() / 2);
    }

    private static Map<String, List<Candidate>> nearbyCandidates(List<Site> sites, Location player) {
        Map<String, List<Candidate>> byMonster = new TreeMap<>();
        for (Site site : sites) {
            float distance = player.distanceTo(site.point());
            if (distance <= site.maxSearchDistance()) {
                byMonster.computeIfAbsent(site.monsterId(), key -> new ArrayList<>())
                    .add(new Candidate(site, distance));
            }
        }
        for (List<Candidate> choices : byMonster.values()) {
            choices.sort(Comparator.comparingDouble(Candidate::distance));
            if (choices.size() > 3) choices.subList(3, choices.size()).clear();
        }
        return byMonster;
    }

    private Site chooseNearbySite(Location player) {
        Map<String, List<Candidate>> byMonster = nearbyCandidates(sites, player);
        if (byMonster.isEmpty()) return null;

        List<List<Candidate>> groups = new ArrayList<>(byMonster.values());
        List<Candidate> choices = groups.get(RANDOM.nextInt(groups.size()));
        return choices.get(RANDOM.nextInt(choices.size())).site();
    }

    static JSONArray previewNearby(Context context, String catalog, Location location) throws IOException, JSONException {
        JSONArray result = new JSONArray();
        for (List<Candidate> group : nearbyCandidates(readSites(context, catalog), location).values()) {
            for (Candidate candidate : group) {
                result.put(new JSONObject().put("monsterId", candidate.site().monsterId())
                    .put("siteName", candidate.site().name())
                    .put("distanceMeters", Math.round(candidate.distance())));
            }
        }
        return result;
    }

    static boolean usable(Location fix) {
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
