import 'dart:convert';

import 'package:firebase_auth/firebase_auth.dart';
import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:flutter/services.dart';
import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:permission_handler/permission_handler.dart';

import 'hunt_result_service.dart';
import 'hunt_achievements.dart';

class UnityHuntBridge {
  static const MethodChannel _channel = MethodChannel(
    'com.example.masar_plus/hunt_result',
  );

  final HuntResultService _huntResultService = HuntResultService();
  static const _debugModeKey = 'ar_close_range_debug';
  static Future<void>? _sharedPendingSync;

  static Future<bool> closeRangeDebugEnabled() async =>
      (await SharedPreferences.getInstance()).getBool(_debugModeKey) ?? false;

  static Future<void> setCloseRangeDebug(bool enabled) async {
    await (await SharedPreferences.getInstance())
        .setBool(_debugModeKey, enabled);
  }

  static Future<String> _loadSpawnCatalog() async {
    final entries = await Future.wait(['bu_daryah', 'om_el_saaf', 'tantal', 'hamaret_al_qayla'].map((id) async {
      final ref = FirebaseFirestore.instance.collection('monsters').doc(id);
      final monster = await ref.get(const GetOptions(source: Source.server));
      if (monster.data()?['spawnCatalogEnabled'] != true) return null;
      final sites = await ref.collection('spawn_locations').get(const GetOptions(source: Source.server));
      return MapEntry(id, sites.docs.map((site) {
        final data = site.data();
        return {
          'monsterId': id,
          'name': data['name'] as String,
          'latitude': data['latitude'] as num,
          'longitude': data['longitude'] as num,
          'arrivalRadiusMeters': data['arrivalRadiusMeters'] as num,
          if (data['maxSearchMeters'] is num) 'maxSearchMeters': data['maxSearchMeters'] as num,
        };
      }).toList());
    }));
    return jsonEncode(Map.fromEntries(entries.nonNulls));
  }

  static Future<List<Map<String, dynamic>>> previewNearbyHunts() async {
    if (FirebaseAuth.instance.currentUser == null) {
      throw PlatformException(code: 'auth_required', message: 'Sign in to view nearby hunts.');
    }
    if (defaultTargetPlatform != TargetPlatform.android) {
      throw PlatformException(code: 'unsupported', message: 'Nearby hunts are available on Android.');
    }
    final permission = await Permission.locationWhenInUse.request();
    if (!permission.isGranted) {
      throw PlatformException(code: 'location_permission', message: 'Allow precise location to see nearby hunts.');
    }
    final catalog = await _loadSpawnCatalog();
    final response = await _channel.invokeMethod<String>('previewNearbyHunts', {'spawnCatalog': catalog});
    return (jsonDecode(response ?? '[]') as List)
        .map((entry) => Map<String, dynamic>.from(entry as Map)).toList();
  }

  static Future<void> launch() async {
    final user = FirebaseAuth.instance.currentUser;
    if (user == null) {
      throw PlatformException(
          code: 'auth_required', message: 'Sign in to start a hunt.');
    }
    await UnityHuntBridge().consumePendingResult();
    final debug = await closeRangeDebugEnabled();
    var crystals = 0;
    var spawnCatalog = '{}';
    if (!debug) {
      final pending = await _channel.invokeListMethod<String>('getEmbeddedHuntResults') ?? [];
      if (pending.any((entry) => (jsonDecode(entry) as Map<String, dynamic>)['userId'] == user.uid)) {
        throw PlatformException(
          code: 'pending_hunts',
          message: 'Previous hunt results could not be saved. Check your connection and try again.',
        );
      }
      late final DocumentSnapshot<Map<String, dynamic>> profile;
      try {
        profile = await FirebaseFirestore.instance
            .collection('users')
            .doc(user.uid)
            .get(const GetOptions(source: Source.server));
      } on FirebaseException catch (error) {
        throw PlatformException(
          code: error.code,
          message: 'Could not load your crystal balance. Check your connection and try again.',
        );
      }
      if (!profile.exists) {
        throw PlatformException(code: 'profile_missing', message: 'Your profile could not be found.');
      }
      crystals = (profile.data()?['crystals'] as num?)?.toInt() ?? 0;
      try {
        spawnCatalog = await _loadSpawnCatalog();
      } catch (error) {
        throw PlatformException(
          code: 'spawn_catalog_unavailable',
          message: 'Could not load hunt locations. Check your connection and try again.',
        );
      }
    }
    await _channel.invokeMethod<void>('launchEmbeddedGame', {
      'userId': user.uid,
      'debug': debug,
      'crystals': crystals,
      'spawnCatalog': spawnCatalog,
    });
  }

  static Future<bool> isGameRunning() async =>
      defaultTargetPlatform == TargetPlatform.android &&
      (await _channel.invokeMethod<bool>('isEmbeddedGameRunning') ?? false);

  Future<void> consumePendingResult() =>
      _sharedPendingSync ??= _sync().whenComplete(() => _sharedPendingSync = null);

  Future<void> _sync() async {
    final uid = FirebaseAuth.instance.currentUser?.uid;
    if (uid == null) return;
    final queue =
        await _channel.invokeListMethod<String>('getEmbeddedHuntResults') ?? [];
    final hunts = queue
        .map((entry) => jsonDecode(entry) as Map<String, dynamic>)
        .where((hunt) => hunt['userId'] == uid)
        .toList()
      ..sort(
          (a, b) => (a['endTime'] as String).compareTo(b['endTime'] as String));
    for (final hunt in hunts) {
      if (FirebaseAuth.instance.currentUser?.uid != uid) return;
      try {
        await _huntResultService.saveCompletedHunt(
          huntId: hunt['huntId'] as String,
          steps: (hunt['huntSteps'] as num).toInt(),
          crystalsEarned: (hunt['crystalsEarned'] as num).toInt(),
          energyEarned: (hunt['energyEarned'] as num).toInt(),
          legend: hunt['legend'] as String,
          startTime: hunt['startTime'] as String,
          endTime: hunt['endTime'] as String,
        );
        await _channel.invokeMethod<void>(
            'clearEmbeddedHuntResult', {'huntId': hunt['huntId']});
        await HuntAchievements().awardForHunt(uid, hunt['huntId'] as String);
      } catch (error) {
        debugPrint('Could not sync completed AR hunt: $error');
        break;
      }
    }
    if (FirebaseAuth.instance.currentUser?.uid == uid) {
      await _consumeLegacyResult();
    }
  }

  Future<void> _consumeLegacyResult() async {
    try {
      final rawResult = await _channel.invokeMethod<dynamic>(
        'getPendingHuntResult',
      );

      if (rawResult == null) {
        debugPrint('UNITY BRIDGE: no pending hunt result');
        return;
      }

      final result = Map<String, dynamic>.from(rawResult as Map);

      debugPrint('========================================');
      debugPrint('UNITY → FLUTTER HUNT RESULT RECEIVED');
      debugPrint('huntId: ${result['huntId']}');
      debugPrint('monsterId: ${result['monsterId']}');
      debugPrint('huntSteps: ${result['huntSteps']}');
      debugPrint('crystalsEarned: ${result['crystalsEarned']}');
      debugPrint('energyEarned: ${result['energyEarned']}');
      debugPrint('legend: ${result['legend']}');
      debugPrint('startTime: ${result['startTime']}');
      debugPrint('endTime: ${result['endTime']}');
      debugPrint('========================================');

      // Save the actual Unity hunt result to Firebase.
      await _huntResultService.saveCompletedHunt(
        huntId: result['huntId'].toString(),
        steps: (result['huntSteps'] as num).toInt(),
        crystalsEarned: (result['crystalsEarned'] as num).toInt(),
        energyEarned: (result['energyEarned'] as num).toInt(),
        legend: result['legend'].toString(),
        startTime: result['startTime'].toString(),
        endTime: result['endTime'].toString(),
      );

      debugPrint('UNITY BRIDGE: Firebase hunt save succeeded');

      // Clear only after the Firebase save succeeded.
      await _channel.invokeMethod<void>(
        'clearPendingHuntResult',
      );

      debugPrint('UNITY BRIDGE: pending result cleared');
      final uid = _authUid;
      if (uid != null) await HuntAchievements().awardForHunt(uid, result['huntId'].toString());
    } catch (e, stackTrace) {
      debugPrint('UNITY BRIDGE: SAVE FAILED: $e');
      debugPrintStack(stackTrace: stackTrace);

      // Do not clear the pending hunt if saving fails.
      // This allows us to retry it later.
    }
  }

  String? get _authUid => FirebaseAuth.instance.currentUser?.uid;
}
