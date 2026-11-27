import 'dart:async';
import 'freej_weekly_service.dart';
import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:firebase_auth/firebase_auth.dart';
import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Independent, device-local hourly counters. Never modifies rewards or totals.
class WalkingInsightsService extends ChangeNotifier {
  WalkingInsightsService._();
  static final instance = WalkingInsightsService._();
  Timer? _refresh;
  Future<SharedPreferences>? _storage;

  Future<SharedPreferences> _prefs() async {
    try {
      return await (_storage ??= _initialize());
    } catch (_) {
      _storage = null;
      rethrow;
    }
  }

  Future<SharedPreferences> _initialize() async {
    final prefs = await SharedPreferences.getInstance();
    if (prefs.getString('${_prefix}device') == null) {
      final id = FirebaseFirestore.instance.collection('users').doc().id;
      if (!await prefs.setString('${_prefix}device', id)) {
        throw StateError('Installation ID storage failed');
      }
    }
    return prefs;
  }

  void _changed() {
    _refresh ??= Timer(const Duration(milliseconds: 500), () {
      _refresh = null;
      notifyListeners();
    });
  }

  Timer? _timer;
  Future<void> _queue = Future.value();
  static const _prefix = 'masar_insights_v1_';

  void recordStep() {
    final uid = FirebaseAuth.instance.currentUser?.uid;
    if (uid == null) return;
    final now = DateTime.now().toUtc();
    final hour = DateTime.utc(now.year, now.month, now.day, now.hour);
    final key = '$_prefix${uid}_${hour.millisecondsSinceEpoch}';
    _queue = _queue.then((_) async {
      final prefs = await _prefs();
      final count = (prefs.getInt(key) ?? 0) + 1;
      if (!await prefs.setInt(key, count))
        throw StateError('Step storage failed');
      _changed();
    }).catchError((Object e) {
      debugPrint('Insights storage: $e');
    });
    _timer ??= Timer.periodic(const Duration(minutes: 5), (_) => flush());
  }

  bool _syncing = false;
  Future<void> flush() async {
    if (_syncing) return;
    final uid = FirebaseAuth.instance.currentUser?.uid;
    if (uid == null) return;
    _syncing = true;
    try {
      await _queue;
      final prefs = await _prefs();
      final prefix = '$_prefix${uid}_';
      // Stable installation ID keeps independent devices from overwriting counts.
      final device = prefs.getString('${_prefix}device')!;
      final batch = FirebaseFirestore.instance.batch();
      final sent = <String, int>{};
      for (final key in prefs.getKeys().where((k) => k.startsWith(prefix))) {
        final count = prefs.getInt(key) ?? 0;
        if (count <= (prefs.getInt('synced_$key') ?? 0)) continue;
        final millis = int.tryParse(key.substring(prefix.length));
        if (millis == null) continue;
        final ref = FirebaseFirestore.instance
            .collection('users')
            .doc(uid)
            .collection('walking_insights')
            .doc('${millis}_$device');
        batch.set(ref, {
          'steps': count,
          'hourStart': Timestamp.fromMillisecondsSinceEpoch(millis),
          'updatedAt': FieldValue.serverTimestamp()
        });
        sent[key] = count;
        if (sent.length >= 400) break;
      }
      if (FirebaseAuth.instance.currentUser?.uid != uid) return;
      if (sent.isNotEmpty) await batch.commit();
      for (final entry in sent.entries) {
        await prefs.setInt('synced_${entry.key}', entry.value);
      }
      _changed();
      try {
        await FreejWeeklyService.publish(uid);
      } catch (e) {
        debugPrint('Freej weekly sync: $e');
      }
    } catch (e) {
      debugPrint('Insights sync: $e');
    } finally {
      _syncing = false;
    }
  }

  // Kuwait Sunday midnight, represented as UTC.
  static DateTime weekStart() {
    final k = DateTime.now().toUtc().add(const Duration(hours: 3));
    return DateTime.utc(k.year, k.month, k.day - k.weekday % 7)
        .subtract(const Duration(hours: 3));
  }

  static String weekId() => weekStart().millisecondsSinceEpoch.toString();

  void start() {
    _timer ??= Timer.periodic(const Duration(minutes: 5), (_) => flush());
    unawaited(flush());
  }

  void stop() {
    _timer?.cancel();
    _timer = null;
    unawaited(flush());
  }

  Stream<QuerySnapshot<Map<String, dynamic>>> history(
          String uid, DateTime start, DateTime end) =>
      FirebaseFirestore.instance
          .collection('users')
          .doc(uid)
          .collection('walking_insights')
          .where('hourStart', isGreaterThanOrEqualTo: Timestamp.fromDate(start))
          .where('hourStart', isLessThan: Timestamp.fromDate(end))
          .snapshots();

  /// Merge local absolute counts with cloud counts for this installation.
  Future<Map<DateTime, int>> totals(
      String uid,
      List<QueryDocumentSnapshot<Map<String, dynamic>>> docs,
      DateTime start,
      DateTime end, {
      List<QueryDocumentSnapshot<Map<String, dynamic>>> hunts = const [],
      }) async {
    await _queue;
    final prefs = await _prefs();
    final device = prefs.getString('${_prefix}device')!;
    final counts = <String, int>{};
    final hours = <String, DateTime>{};
    for (final doc in docs) {
      final data = doc.data();
      final time = data['hourStart'];
      final steps = data['steps'];
      if (time is! Timestamp || steps is! int || steps < 0) continue;
      final hour = time.toDate().toUtc();
      if (hour.isBefore(start) || !hour.isBefore(end)) continue;
      counts[doc.id] = steps;
      hours[doc.id] = hour;
    }
    final prefix = '$_prefix${uid}_';
    for (final key in prefs.getKeys().where((k) => k.startsWith(prefix))) {
      final millis = int.tryParse(key.substring(prefix.length));
      if (millis == null) continue;
      final hour = DateTime.fromMillisecondsSinceEpoch(millis, isUtc: true);
      if (hour.isBefore(start) || !hour.isBefore(end)) continue;
      final id = '${millis}_$device';
      final local = prefs.getInt(key) ?? 0;
      if (local > (counts[id] ?? 0)) counts[id] = local;
      hours[id] = hour;
    }
    final result = <DateTime, int>{};
    for (final entry in counts.entries) {
      final hour = hours[entry.key]!;
      result[hour] = (result[hour] ?? 0) + entry.value;
    }
    for (final doc in hunts) {
      if (doc.data()['userId'] != uid) continue;
      final hunt = FreejWeeklyService.completedHunt(doc.data(), start, end);
      if (hunt != null) {
        result[hunt.key] = (result[hunt.key] ?? 0) + hunt.value;
      }
    }
    return result;
  }
}
