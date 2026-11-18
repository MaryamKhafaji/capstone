import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:firebase_auth/firebase_auth.dart';
import 'package:flutter/foundation.dart';

class HuntAchievements {
  final _db = FirebaseFirestore.instance;

  Future<void> awardForHunt(String uid, String sessionId) async {
    try {
      final session =
          await _db.collection('walking_sessions').doc(sessionId).get();
      if (session.exists) await _award(uid, [session]);
    } catch (error) {
      // A failed badge check must never prevent Unity results from syncing.
      debugPrint('Could not check hunt achievements: $error');
    }
  }

  Future<void> checkHistory(String uid) async {
    final sessions = await _db
        .collection('walking_sessions')
        .where('userId', isEqualTo: uid)
        .get();
    await _award(uid, sessions.docs);
  }

  Future<void> _award(
    String uid,
    List<DocumentSnapshot<Map<String, dynamic>>> sessions,
  ) async {
    if (FirebaseAuth.instance.currentUser?.uid != uid) {
      return;
    }
    final profile = (await _db.collection('users').doc(uid).get()).data() ?? {};
    final active = await _db
        .collection('achievements')
        .where('active', isEqualTo: true)
        .get();
    if (active.docs.isEmpty) return;
    final earned = _db.collection('users').doc(uid).collection('earned_badges');
    final existingIds = (await earned.get()).docs.map((doc) => doc.id).toSet();
    for (final definition in active.docs) {
      if (existingIds.contains(definition.id)) continue;
      final badge = definition.data();
      final profileValue = switch (badge['metric']) {
        'total_steps' => profile['totalSteps'],
        'active_day_streak' => profile['streak'],
        _ => null,
      };
      final isProfileCondition = badge['metric'] == 'total_steps' ||
          badge['metric'] == 'active_day_streak';
      final eligibleSessions = <DocumentSnapshot<Map<String, dynamic>>?>[];
      if (isProfileCondition) {
        if (profileValue is num &&
            badge['target'] is int &&
            profileValue >= (badge['target'] as int)) {
          eligibleSessions.add(null);
        }
      } else {
        eligibleSessions.addAll(sessions.where(
          (session) => _qualifies(session.data(), badge, uid),
        ));
      }
      for (final session in eligibleSessions) {
        if (FirebaseAuth.instance.currentUser?.uid != uid) return;
        await _db.runTransaction((tx) async {
          final ref = earned.doc(definition.id);
          if ((await tx.get(ref)).exists) return;
          tx.set(ref, {
            'name': badge['name'],
            'description': badge['description'],
            'category': categoryForAchievement(badge),
            'sessionId': session?.id ?? 'profile',
            'earnedAt': FieldValue.serverTimestamp(),
          });
        });
        break;
      }
    }
  }

  bool _qualifies(
      Map<String, dynamic>? session, Map<String, dynamic> badge, String uid) {
    if (session == null || session['userId'] != uid) return false;
    final start = session['startTime'], end = session['endTime'];
    if (start is! Timestamp || end is! Timestamp || end.compareTo(start) < 0) {
      return false;
    }
    final monster = switch (session['legend']) {
      'Bu Daryah' => 'bu_daryah',
      'Om El-Saaf' => 'om_el_saaf',
      _ => null,
    };
    if (monster == null) return false;
    final steps = session['numSteps'];
    final crystals = session['crystalsEarned'];
    final target = badge['target'];
    return switch (badge['metric']) {
      'hunt' => true,
      'monster' => badge['monsterId'] == monster,
      'hunt_steps' => steps is int && target is int && steps >= target,
      'hunt_crystals' => crystals is int && target is int && crystals >= target,
      _ => false,
    };
  }
}

String categoryForAchievement(Map<String, dynamic> badge) {
  final category = badge['category'];
  if (category is String && category.isNotEmpty) return category;
  return switch (badge['metric']) {
    'hunt' || 'monster' => 'monster_hunting',
    'hunt_steps' || 'total_steps' => 'walking',
    'hunt_crystals' => 'crystals_energy',
    'active_day_streak' => 'consistency',
    _ => '',
  };
}
