package com.example.masar_plus

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.os.Handler
import android.os.Looper
import android.util.Log
import com.google.firebase.Timestamp
import com.google.firebase.auth.FirebaseAuth
import com.google.firebase.auth.FirebaseUser
import com.google.firebase.firestore.FirebaseFirestore
import com.google.firebase.firestore.SetOptions
import java.text.SimpleDateFormat
import java.util.Calendar
import java.util.Date
import java.util.Locale
import java.util.concurrent.atomic.AtomicBoolean

class HuntResultReceiver : BroadcastReceiver() {

    companion object {
        private const val TAG = "MasAR_HuntReceiver"
        private const val PREFS_NAME = "masar_hunt_results"
        private const val HUNT_ACTION = "com.masar.plus.HUNT_COMPLETE"
    }

    override fun onReceive(
        context: Context,
        intent: Intent
    ) {
        if (intent.action != HUNT_ACTION) {
            return
        }

        val pendingResult = goAsync()

        val huntId =
            intent.getStringExtra("huntId")
                ?.trim()
                ?: ""

        val monsterId =
            intent.getStringExtra("monsterId")
                ?.trim()
                ?: ""

        val huntSteps =
            intent.getIntExtra(
                "huntSteps",
                0
            )

        val crystalsEarned =
            intent.getIntExtra(
                "crystalsEarned",
                0
            )

        val energyEarned =
            intent.getIntExtra(
                "energyEarned",
                0
            )

        val legend =
            intent.getStringExtra("legend")
                ?.trim()
                ?: ""

        val startTimeString =
            intent.getStringExtra("startTime")
                ?: ""

        val endTimeString =
            intent.getStringExtra("endTime")
                ?: ""

        val prefs =
            context.getSharedPreferences(
                PREFS_NAME,
                Context.MODE_PRIVATE
            )

        // -------------------------------------------------
        // ALWAYS SAVE A LOCAL BACKUP FIRST
        // -------------------------------------------------

        prefs.edit()
            .putBoolean(
                "hasPendingResult",
                true
            )
            .putString(
                "huntId",
                huntId
            )
            .putString(
                "monsterId",
                monsterId
            )
            .putInt(
                "huntSteps",
                huntSteps
            )
            .putInt(
                "crystalsEarned",
                crystalsEarned
            )
            .putInt(
                "energyEarned",
                energyEarned
            )
            .putString(
                "legend",
                legend
            )
            .putString(
                "startTime",
                startTimeString
            )
            .putString(
                "endTime",
                endTimeString
            )
            .putString(
                "debugStatus",
                "broadcast_received"
            )
            .remove(
                "debugError"
            )
            .putLong(
                "debugUpdatedAt",
                System.currentTimeMillis()
            )
            .apply()

        Log.d(
            TAG,
            "Received hunt $huntId, steps=$huntSteps"
        )

        if (huntId.isEmpty()) {
            saveDebugFailure(
                prefs,
                "invalid_hunt_id",
                "huntId was empty"
            )

            pendingResult.finish()
            return
        }

        val startDate =
            parseUnityDate(
                startTimeString
            )

        val endDate =
            parseUnityDate(
                endTimeString
            )

        if (
            startDate == null ||
            endDate == null
        ) {
            saveDebugFailure(
                prefs,
                "invalid_timestamps",
                "start=$startTimeString end=$endTimeString"
            )

            pendingResult.finish()
            return
        }

        // -------------------------------------------------
        // FIREBASE AUTH MAY NOT BE READY YET
        // -------------------------------------------------

        waitForFirebaseUser(
            prefs = prefs,
            pendingResult = pendingResult
        ) { user ->

            saveHuntToFirestore(
                prefs = prefs,
                pendingResult = pendingResult,
                user = user,
                huntId = huntId,
                monsterId = monsterId,
                huntSteps = huntSteps,
                crystalsEarned = crystalsEarned,
                energyEarned = energyEarned,
                legend = legend,
                startDate = startDate,
                endDate = endDate
            )
        }
    }

    // =====================================================
    // WAIT FOR FIREBASE AUTH INITIALIZATION
    // =====================================================

    private fun waitForFirebaseUser(
        prefs: android.content.SharedPreferences,
        pendingResult: PendingResult,
        onUserReady: (FirebaseUser) -> Unit
    ) {
        val auth =
            FirebaseAuth.getInstance()

        val immediateUser =
            auth.currentUser

        if (immediateUser != null) {
            prefs.edit()
                .putString(
                    "debugStatus",
                    "auth_ready"
                )
                .putLong(
                    "debugUpdatedAt",
                    System.currentTimeMillis()
                )
                .apply()

            onUserReady(
                immediateUser
            )

            return
        }

        prefs.edit()
            .putString(
                "debugStatus",
                "waiting_for_auth"
            )
            .putLong(
                "debugUpdatedAt",
                System.currentTimeMillis()
            )
            .apply()

        val handled =
            AtomicBoolean(false)

        lateinit var listener:
            FirebaseAuth.AuthStateListener

        listener =
            FirebaseAuth.AuthStateListener {
                firebaseAuth ->

                val user =
                    firebaseAuth.currentUser

                if (
                    user != null &&
                    handled.compareAndSet(
                        false,
                        true
                    )
                ) {
                    auth.removeAuthStateListener(
                        listener
                    )

                    prefs.edit()
                        .putString(
                            "debugStatus",
                            "auth_ready_after_wait"
                        )
                        .putLong(
                            "debugUpdatedAt",
                            System.currentTimeMillis()
                        )
                        .apply()

                    onUserReady(
                        user
                    )
                }
            }

        auth.addAuthStateListener(
            listener
        )

        // Give Firebase Auth a few seconds to restore
        // the persisted signed-in user.
        Handler(
            Looper.getMainLooper()
        ).postDelayed(
            {
                if (
                    handled.compareAndSet(
                        false,
                        true
                    )
                ) {
                    auth.removeAuthStateListener(
                        listener
                    )

                    val lateUser =
                        auth.currentUser

                    if (lateUser != null) {
                        prefs.edit()
                            .putString(
                                "debugStatus",
                                "auth_ready_at_timeout"
                            )
                            .putLong(
                                "debugUpdatedAt",
                                System.currentTimeMillis()
                            )
                            .apply()

                        onUserReady(
                            lateUser
                        )
                    } else {
                        saveDebugFailure(
                            prefs,
                            "auth_unavailable",
                            "FirebaseAuth.currentUser stayed null"
                        )

                        pendingResult.finish()
                    }
                }
            },
            3000L
        )
    }

    // =====================================================
    // SAVE COMPLETE HUNT TO FIRESTORE
    // =====================================================

    private fun saveHuntToFirestore(
        prefs: android.content.SharedPreferences,
        pendingResult: PendingResult,
        user: FirebaseUser,
        huntId: String,
        monsterId: String,
        huntSteps: Int,
        crystalsEarned: Int,
        energyEarned: Int,
        legend: String,
        startDate: Date,
        endDate: Date
    ) {
        prefs.edit()
            .putString(
                "debugStatus",
                "loading_previous_sessions"
            )
            .putLong(
                "debugUpdatedAt",
                System.currentTimeMillis()
            )
            .apply()

        val db =
            FirebaseFirestore.getInstance()

        val userRef =
            db.collection("users")
                .document(user.uid)

        val leaderboardRef =
            db.collection("leaderboard")
                .document(user.uid)

        val sessionRef =
            db.collection("walking_sessions")
                .document(huntId)

        // No orderBy here.
        // This avoids requiring a composite Firestore index.
        db.collection("walking_sessions")
            .whereEqualTo(
                "userId",
                user.uid
            )
            .get()
            .addOnSuccessListener {
                previousSessions ->

                var previousSessionDate:
                    Date? = null

                for (
                    document
                    in previousSessions.documents
                ) {
                    val sessionEnd =
                        document
                            .getTimestamp(
                                "endTime"
                            )
                            ?.toDate()
                            ?: continue

                    val currentPrevious =
                        previousSessionDate

                    if (
                        currentPrevious == null ||
                        sessionEnd.after(
                            currentPrevious
                        )
                    ) {
                        previousSessionDate =
                            sessionEnd
                    }
                }

                prefs.edit()
                    .putString(
                        "debugStatus",
                        "starting_transaction"
                    )
                    .putLong(
                        "debugUpdatedAt",
                        System.currentTimeMillis()
                    )
                    .apply()

                db.runTransaction {
                    transaction ->

                    // -------------------------------------
                    // PREVENT DUPLICATE HUNT
                    // -------------------------------------

                    val existingSession =
                        transaction.get(
                            sessionRef
                        )

                    if (
                        existingSession.exists()
                    ) {
                        return@runTransaction true
                    }

                    // -------------------------------------
                    // CURRENT USER DATA
                    // -------------------------------------

                    val userSnapshot =
                        transaction.get(
                            userRef
                        )

                    if (
                        !userSnapshot.exists()
                    ) {
                        throw Exception(
                            "User profile does not exist."
                        )
                    }

                    val currentSteps =
                        userSnapshot
                            .getLong(
                                "totalSteps"
                            )
                            ?: 0L

                    val currentCrystals =
                        userSnapshot
                            .getLong(
                                "crystals"
                            )
                            ?: 0L

                    val currentStreak =
                        userSnapshot
                            .getLong(
                                "streak"
                            )
                            ?: 0L

                    val name =
                        userSnapshot
                            .getString(
                                "name"
                            )
                            ?: "Unknown User"

                    val newTotalSteps =
                        currentSteps +
                            huntSteps.toLong()

                    val newTotalCrystals =
                        currentCrystals +
                            crystalsEarned.toLong()

                    val newStreak =
                        calculateStreak(
                            currentStreak =
                                currentStreak,
                            previousSessionDate =
                                previousSessionDate,
                            completedHuntDate =
                                endDate
                        )

                    // -------------------------------------
                    // WALKING SESSION
                    // -------------------------------------

                    val sessionData =
                        hashMapOf<String, Any>(
                            "userId" to
                                user.uid,

                            "footwearId" to
                                "",

                            "startTime" to
                                Timestamp(
                                    startDate
                                ),

                            "endTime" to
                                Timestamp(
                                    endDate
                                ),

                            "numSteps" to
                                huntSteps,

                            "crystalsEarned" to
                                crystalsEarned,

                            "energyEarned" to
                                energyEarned,

                            "legend" to
                                if (
                                    legend.isNotEmpty()
                                ) {
                                    legend
                                } else {
                                    monsterId
                                },

                            "distance" to
                                0.0,

                            "destination" to
                                ""
                        )

                    transaction.set(
                        sessionRef,
                        sessionData
                    )

                    // -------------------------------------
                    // USERS - SOURCE OF TRUTH
                    // -------------------------------------

                    transaction.update(
                        userRef,
                        mapOf(
                            "totalSteps" to
                                newTotalSteps,

                            "crystals" to
                                newTotalCrystals,

                            "streak" to
                                newStreak
                        )
                    )

                    // -------------------------------------
                    // LEADERBOARD MIRROR
                    // -------------------------------------

                    transaction.set(
                        leaderboardRef,
                        mapOf(
                            "userId" to
                                user.uid,

                            "name" to
                                name,

                            "totalSteps" to
                                newTotalSteps,

                            "crystals" to
                                newTotalCrystals,

                            "streak" to
                                newStreak
                        ),
                        SetOptions.merge()
                    )

                    true
                }
                    .addOnSuccessListener {

                        Log.d(
                            TAG,
                            "Firestore saved hunt $huntId"
                        )

                        // Keep debug information so we can
                        // verify success later with ADB,
                        // but remove the pending payload.
                        prefs.edit()
                            .putBoolean(
                                "hasPendingResult",
                                false
                            )
                            .putString(
                                "debugStatus",
                                "firestore_saved"
                            )
                            .putString(
                                "debugLastHuntId",
                                huntId
                            )
                            .remove(
                                "debugError"
                            )
                            .remove(
                                "huntId"
                            )
                            .remove(
                                "monsterId"
                            )
                            .remove(
                                "huntSteps"
                            )
                            .remove(
                                "crystalsEarned"
                            )
                            .remove(
                                "energyEarned"
                            )
                            .remove(
                                "legend"
                            )
                            .remove(
                                "startTime"
                            )
                            .remove(
                                "endTime"
                            )
                            .putLong(
                                "debugUpdatedAt",
                                System.currentTimeMillis()
                            )
                            .apply()

                        pendingResult.finish()
                    }
                    .addOnFailureListener {
                        exception ->

                        Log.e(
                            TAG,
                            "Firestore transaction failed",
                            exception
                        )

                        saveDebugFailure(
                            prefs,
                            "transaction_failed",
                            "${exception.javaClass.simpleName}: ${exception.message}"
                        )

                        // Keep the pending result for
                        // Flutter to retry later.
                        pendingResult.finish()
                    }
            }
            .addOnFailureListener {
                exception ->

                Log.e(
                    TAG,
                    "Previous sessions query failed",
                    exception
                )

                saveDebugFailure(
                    prefs,
                    "session_query_failed",
                    "${exception.javaClass.simpleName}: ${exception.message}"
                )

                pendingResult.finish()
            }
    }

    // =====================================================
    // STREAK
    // =====================================================

    private fun calculateStreak(
        currentStreak: Long,
        previousSessionDate: Date?,
        completedHuntDate: Date
    ): Long {

        if (
            previousSessionDate == null
        ) {
            return 1L
        }

        val previousDay =
            dateKey(
                previousSessionDate
            )

        val currentDay =
            dateKey(
                completedHuntDate
            )

        if (
            previousDay ==
            currentDay
        ) {
            return if (
                currentStreak > 0L
            ) {
                currentStreak
            } else {
                1L
            }
        }

        val calendar =
            Calendar.getInstance()

        calendar.time =
            completedHuntDate

        calendar.add(
            Calendar.DAY_OF_YEAR,
            -1
        )

        val yesterday =
            dateKey(
                calendar.time
            )

        if (
            previousDay ==
            yesterday
        ) {
            return currentStreak + 1L
        }

        return 1L
    }

    private fun dateKey(
        date: Date
    ): String {
        return SimpleDateFormat(
            "yyyy-MM-dd",
            Locale.US
        ).format(
            date
        )
    }

    // =====================================================
    // UNITY DATE PARSER
    // =====================================================

    private fun parseUnityDate(
        value: String
    ): Date? {

        if (
            value.isBlank()
        ) {
            return null
        }

        return try {

            val normalized =
                value.replace(
                    Regex(
                        "(\\.\\d{3})\\d+(Z|[+-]\\d{2}:\\d{2})$"
                    ),
                    "$1$2"
                )

            val format =
                SimpleDateFormat(
                    "yyyy-MM-dd'T'HH:mm:ss.SSSXXX",
                    Locale.US
                )

            format.isLenient =
                false

            format.parse(
                normalized
            )

        } catch (
            exception: Exception
        ) {
            null
        }
    }

    // =====================================================
    // DEBUG FAILURE
    // =====================================================

    private fun saveDebugFailure(
        prefs: android.content.SharedPreferences,
        status: String,
        error: String
    ) {
        prefs.edit()
            .putString(
                "debugStatus",
                status
            )
            .putString(
                "debugError",
                error
            )
            .putLong(
                "debugUpdatedAt",
                System.currentTimeMillis()
            )
            .apply()
    }
}