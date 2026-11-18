
import 'package:cloud_firestore/cloud_firestore.dart';

class DatabaseSetup {
  static Future<void> createCollections() async {
    final db = FirebaseFirestore.instance;

    // 12 ER-diagram entities + 5 relationship collections.
    final collections = <String, Map<String, dynamic>>{
      'users': {
        'name': '',
        'email': '',
        'xp': 0,
        'level': 1,
        'totalSteps': 0,
        'crystals': 0,
        'createdAt': FieldValue.serverTimestamp(),
      },
      'footwear': {
        'deviceName': '',
        'batteryLevel': 0,
        'connectionStatus': 'Disconnected',
        'description': '',
        'userId': '',
      },
      'walking_sessions': {
        'userId': '',
        'footwearId': '',
        'startTime': null,
        'endTime': null,
        'numSteps': 0,
        'crystalsEarned': 0,
        'energyEarned': 0,
        'distance': 0.0,
        'destination': '',
        'legend': '',
      },
      'session_records': {
        'sessionId': '',
        'rewardId': '',
        'totalSteps': 0,
        'distance': 0.0,
        'caloriesBurned': 0.0,
        'recordDate': null,
        'duration': 0,
      },
      'rewards': {
        'title': '',
        'description': '',
        'expDate': null,
        'availStatus': '',
        'partnerId': '',
        'adminId': '',
      },
      'partners': {
        'businessName': '',
        'description': '',
        'contactInfo': '',
        'verified': false,
      },
      'hunts': {
        'title': '',
        'description': '',
        'difficulty': '',
        'status': '',
        'reward': '',
        'adminId': '',
      },
      'monsters': {
        'name': '',
        'lore': '',
        'boost': 0,
        'questId': '',
        'adminId': '',
      },
      'groups': {
        'description': '',
        'location': '',
        'dateCreated': null,
        'adminId': '',
      },
      'achievements': {
        'name': '',
        'description': '',
        'unlockCondition': '',
      },
      'matches': {
        'dateCreated': null,
        'status': '',
      },
      'admins': {
        'username': '',
        'role': '',
      },
      'user_matches': {
        'userId': '',
        'matchId': '',
      },
      'user_rewards': {
        'userId': '',
        'rewardId': '',
        'status': '',
        'redeemDate': null,
      },
      'user_hunts': {
        'userId': '',
        'questId': '',
        'status': '',
        'progress': 0,
      },
      'user_achievements': {
        'userId': '',
        'achievementId': '',
      },
      'group_members': {
        'userId': '',
        'groupId': '',
        'rank': 0,
        'score': 0,
        'dateCreated': null,
      },
    };

    final batch = db.batch();

    for (final entry in collections.entries) {
      final ref = db.collection(entry.key).doc('_schema_placeholder');

      batch.set(ref, {
        ...entry.value,
        '_isPlaceholder': true,
      });
    }

    await batch.commit();

    print('Created ${collections.length} Firestore collections!');
  }
}
