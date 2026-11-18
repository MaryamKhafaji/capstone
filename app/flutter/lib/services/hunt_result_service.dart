import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:firebase_auth/firebase_auth.dart';

class HuntResultService {
  final FirebaseFirestore _db =
      FirebaseFirestore.instance;

  final FirebaseAuth _auth =
      FirebaseAuth.instance;

  Future<void> saveCompletedHunt({
    required String huntId,
    required int steps,
    required int crystalsEarned,
    required int energyEarned,
    required String legend,
    required String startTime,
    required String endTime,
  }) async {
    final user =
        _auth.currentUser;

    if (user == null) {
      throw Exception(
        'No user is signed in.',
      );
    }

    final cleanHuntId =
        huntId.trim();

    final cleanLegend =
        legend.trim();

    if (cleanHuntId.isEmpty) {
      throw Exception(
        'huntId cannot be empty.',
      );
    }

    if (cleanLegend.isEmpty) {
      throw Exception(
        'legend cannot be empty.',
      );
    }

    if (steps < 0 ||
        crystalsEarned < 0 ||
        energyEarned < 0) {
      throw Exception(
        'Hunt result values cannot be negative.',
      );
    }

    final parsedStartTime =
        DateTime.tryParse(
      startTime,
    );

    final parsedEndTime =
        DateTime.tryParse(
      endTime,
    );

    if (parsedStartTime == null ||
        parsedEndTime == null) {
      throw Exception(
        'Invalid hunt startTime or endTime.',
      );
    }

    final userRef =
        _db
            .collection('users')
            .doc(user.uid);

    final sessionRef =
        _db
            .collection(
              'walking_sessions',
            )
            .doc(cleanHuntId);

    final leaderboardRef =
        _db
            .collection(
              'leaderboard',
            )
            .doc(user.uid);

    // ===================================================
    // FIND MOST RECENT SESSION
    // ===================================================

    // No orderBy = no composite index requirement.
    final previousSessions =
        await _db
            .collection(
              'walking_sessions',
            )
            .where(
              'userId',
              isEqualTo: user.uid,
            )
            .get();

    DateTime? previousSessionDate;

    for (
      final document
      in previousSessions.docs
    ) {
      final data =
          document.data();

      final end =
          data['endTime'];

      if (end is! Timestamp) {
        continue;
      }

      final sessionEnd =
          end.toDate();

      if (previousSessionDate == null ||
          sessionEnd.isAfter(
            previousSessionDate!,
          )) {
        previousSessionDate =
            sessionEnd;
      }
    }

    // ===================================================
    // ATOMIC TRANSACTION
    // ===================================================

    await _db.runTransaction(
      (transaction) async {
        // -----------------------------------------------
        // DUPLICATE PROTECTION
        // -----------------------------------------------

        final existingSession =
            await transaction.get(
          sessionRef,
        );

        if (existingSession.exists) {
          return;
        }

        // -----------------------------------------------
        // USER
        // -----------------------------------------------

        final userSnapshot =
            await transaction.get(
          userRef,
        );

        if (!userSnapshot.exists) {
          throw Exception(
            'User profile does not exist.',
          );
        }

        final userData =
            userSnapshot.data()!;

        final currentSteps =
            _toInt(
          userData['totalSteps'],
        );

        final currentCrystals =
            _toInt(
          userData['crystals'],
        );

        final currentStreak =
            _toInt(
          userData['streak'],
        );

        final name =
            (userData['name'] ??
                    'Unknown User')
                .toString();

        final newTotalSteps =
            currentSteps +
                steps;

        final newTotalCrystals =
            currentCrystals +
                crystalsEarned;

        // -----------------------------------------------
        // STREAK
        // -----------------------------------------------

        final huntDay =
            _dateOnly(
          parsedEndTime.toLocal(),
        );

        int newStreak;

        if (
          previousSessionDate == null
        ) {
          newStreak = 1;
        } else {
          final previousDay =
              _dateOnly(
            previousSessionDate!
                .toLocal(),
          );

          final yesterday =
              DateTime(
            huntDay.year,
            huntDay.month,
            huntDay.day - 1,
          );

          if (
            previousDay ==
            huntDay
          ) {
            newStreak =
                currentStreak > 0
                    ? currentStreak
                    : 1;
          } else if (
            previousDay ==
            yesterday
          ) {
            newStreak =
                currentStreak + 1;
          } else {
            newStreak = 1;
          }
        }

        // -----------------------------------------------
        // WALKING SESSION
        // -----------------------------------------------

        transaction.set(
          sessionRef,
          {
            'userId':
                user.uid,

            'footwearId':
                '',

            'startTime':
                Timestamp.fromDate(
              parsedStartTime.toUtc(),
            ),

            'endTime':
                Timestamp.fromDate(
              parsedEndTime.toUtc(),
            ),

            'numSteps':
                steps,

            'crystalsEarned':
                crystalsEarned,

            'energyEarned':
                energyEarned,

            'legend':
                cleanLegend,

            'distance':
                0.0,

            'destination':
                '',
          },
        );

        // -----------------------------------------------
        // USERS
        // -----------------------------------------------

        transaction.update(
          userRef,
          {
            'totalSteps':
                newTotalSteps,

            'crystals':
                newTotalCrystals,

            'streak':
                newStreak,

            'lastHuntId':
                cleanHuntId,
          },
        );

        // -----------------------------------------------
        // LEADERBOARD MIRROR
        // -----------------------------------------------

        transaction.set(
          leaderboardRef,
          {
            'userId':
                user.uid,

            'name':
                name,

            'totalSteps':
                newTotalSteps,

            'streak':
                newStreak,
          },
          SetOptions(
            merge: true,
          ),
        );
      },
    );
  }

  int _toInt(
    dynamic value,
  ) {
    if (value is int) {
      return value;
    }

    if (value is num) {
      return value.toInt();
    }

    return int.tryParse(
          value?.toString() ??
              '',
        ) ??
        0;
  }

  DateTime _dateOnly(
    DateTime date,
  ) {
    return DateTime(
      date.year,
      date.month,
      date.day,
    );
  }
}
