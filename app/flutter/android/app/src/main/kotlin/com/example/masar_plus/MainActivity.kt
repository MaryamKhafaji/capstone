package com.example.masar_plus

import android.Manifest
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.util.AtomicFile
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

class MainActivity : FlutterActivity() {

    private data class HuntLaunch(
        val userId: String,
        val debug: Boolean,
        val crystals: Int,
        val spawnCatalog: String,
        val result: MethodChannel.Result
    )

    private var pendingLaunch: HuntLaunch? = null
    private val locationRequest = 2001

    private val channelName =
        "com.example.masar_plus/hunt_result"

    override fun configureFlutterEngine(
        flutterEngine: FlutterEngine
    ) {
        super.configureFlutterEngine(
            flutterEngine
        )

        MethodChannel(
            flutterEngine.dartExecutor.binaryMessenger,
            channelName
        ).setMethodCallHandler { call, result ->

            when (call.method) {

                "launchEmbeddedGame" -> {
                    val uid = call.argument<String>("userId")
                    if (uid.isNullOrBlank() || pendingLaunch != null) {
                        result.error("launch_failed", "Unable to start this hunt.", null)
                        return@setMethodCallHandler
                    }
                    val runningGame = EmbeddedHuntBridge.gameTasks(this).firstOrNull()
                    if (runningGame != null) {
                        val resumed = runCatching {
                            if (!EmbeddedHuntBridge.ownedBy(this, uid)) return@runCatching false
                            runningGame.moveToFront()
                            true
                        }.getOrDefault(false)
                        if (resumed) result.success(null)
                        else result.error("hunt_active", "End the active hunt before starting another.", null)
                        return@setMethodCallHandler
                    }
                    val launch = HuntLaunch(uid, call.argument<Boolean>("debug") == true,
                        call.argument<Int>("crystals") ?: 0,
                        call.argument<String>("spawnCatalog") ?: "{}", result)
                    if (!launch.debug && checkSelfPermission(Manifest.permission.ACCESS_FINE_LOCATION)
                        != PackageManager.PERMISSION_GRANTED
                    ) {
                        pendingLaunch = launch
                        requestPermissions(arrayOf(Manifest.permission.ACCESS_FINE_LOCATION,
                            Manifest.permission.ACCESS_COARSE_LOCATION), locationRequest)
                    } else {
                        startHunt(launch)
                    }
                }

                "isEmbeddedGameRunning" ->
                    result.success(EmbeddedHuntBridge.gameTasks(this).isNotEmpty())

                "getEmbeddedHuntResults" -> {
                    val entries = EmbeddedHuntBridge.resultDirectory(this)
                        .listFiles { file -> file.extension == "json" }
                        .orEmpty()
                        .mapNotNull { file ->
                            runCatching { String(AtomicFile(file).readFully(), Charsets.UTF_8) }
                                .getOrNull()
                        }
                    result.success(entries)
                }

                "clearEmbeddedHuntResult" -> {
                    val file = call.argument<String>("huntId")
                        ?.let { EmbeddedHuntBridge.resultFile(this, it) }
                    if (file != null) AtomicFile(file).delete()
                    result.success(null)
                }

                "getPendingHuntResult" -> {
                    val prefs =
                        getSharedPreferences(
                            "masar_hunt_results",
                            Context.MODE_PRIVATE
                        )

                    val hasPending =
                        prefs.getBoolean(
                            "hasPendingResult",
                            false
                        )

                    if (!hasPending) {
                        result.success(null)
                        return@setMethodCallHandler
                    }

                    val huntResult =
                        mapOf(
                            "huntId" to
                                prefs.getString(
                                    "huntId",
                                    ""
                                ),

                            "monsterId" to
                                prefs.getString(
                                    "monsterId",
                                    ""
                                ),

                            "huntSteps" to
                                prefs.getInt(
                                    "huntSteps",
                                    0
                                ),

                            "crystalsEarned" to
                                prefs.getInt(
                                    "crystalsEarned",
                                    0
                                ),

                            "energyEarned" to
                                prefs.getInt(
                                    "energyEarned",
                                    0
                                ),

                            "legend" to
                                prefs.getString(
                                    "legend",
                                    ""
                                ),

                            "startTime" to
                                prefs.getString(
                                    "startTime",
                                    ""
                                ),

                            "endTime" to
                                prefs.getString(
                                    "endTime",
                                    ""
                                )
                        )

                    result.success(
                        huntResult
                    )
                }

                "clearPendingHuntResult" -> {
                    val prefs =
                        getSharedPreferences(
                            "masar_hunt_results",
                            Context.MODE_PRIVATE
                        )

                    prefs.edit()
                        .clear()
                        .apply()

                    result.success(true)
                }

                else -> {
                    result.notImplemented()
                }
            }
        }
    }

    override fun onRequestPermissionsResult(
        requestCode: Int, permissions: Array<out String>, grantResults: IntArray
    ) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        if (requestCode != locationRequest) return
        val launch = pendingLaunch ?: return
        pendingLaunch = null
        if (checkSelfPermission(Manifest.permission.ACCESS_FINE_LOCATION) ==
            PackageManager.PERMISSION_GRANTED) {
            startHunt(launch)
        } else {
            launch.result.error("location_permission",
                "Enable precise location for MasAR+ to play a real-world hunt.", null)
        }
    }

    private fun startHunt(launch: HuntLaunch) {
        val service = Intent(this, HuntLocationService::class.java)
            .putExtra(HuntLocationService.USER_ID, launch.userId)
            .putExtra(HuntLocationService.SPAWN_CATALOG, launch.spawnCatalog)
        try {
            EmbeddedHuntBridge.endHunt(this)
            EmbeddedHuntBridge.prepareLaunch(this, launch.userId, launch.debug, launch.crystals)
            if (!launch.debug) startForegroundService(service)
            startActivity(Intent(Intent.ACTION_MAIN)
                .setClassName(packageName, EmbeddedHuntBridge.XR_ACTIVITY)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
            launch.result.success(null)
        } catch (e: Exception) {
            stopService(service)
            android.util.Log.e("MasarHunt", "Cannot start embedded game", e)
            launch.result.error("launch_failed", "Unable to open the AR game.", null)
        }
    }
}
