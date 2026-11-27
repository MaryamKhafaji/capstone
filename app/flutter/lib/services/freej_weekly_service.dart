import 'dart:async';
import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:firebase_auth/firebase_auth.dart';

class FreejWeeklyService {
  static final _db = FirebaseFirestore.instance;
  static DateTime weekStart() {
    final k = DateTime.now().toUtc().add(const Duration(hours: 3));
    return DateTime.utc(k.year, k.month, k.day - k.weekday % 7)
        .subtract(const Duration(hours: 3));
  }
  static String weekId(DateTime start) => start.millisecondsSinceEpoch.toString();
  static CollectionReference<Map<String, dynamic>> scores(DateTime start) =>
      _db.collection('freej_weekly').doc(weekId(start)).collection('scores');

  // Both Insights and Freej use this exact filter and completion-hour policy.
  static MapEntry<DateTime, int>? completedHunt(
      Map<String, dynamic> data, DateTime start, DateTime end) {
    final time = data['endTime'];
    final steps = data['numSteps'];
    final legend = data['legend'];
    final hunt = data['sessionMode'] == 'hunt' ||
        (legend is String && legend.trim().isNotEmpty);
    if (data['recordType'] == 'ar_session' || !hunt ||
        time is! Timestamp || steps is! int || steps < 0) return null;
    final t = time.toDate().toUtc();
    if (t.isBefore(start) || !t.isBefore(end)) return null;
    return MapEntry(DateTime.utc(t.year, t.month, t.day, t.hour), steps);
  }

  // Only publish aggregate counts; private hourly history stays private.
  static Future<void> publish(String uid) async {
    final start = weekStart();
    final end = start.add(const Duration(days: 7));
    final hours = await _db.collection('users').doc(uid)
        .collection('walking_insights')
        .where('hourStart', isGreaterThanOrEqualTo: Timestamp.fromDate(start))
        .where('hourStart', isLessThan: Timestamp.fromDate(end))
        .get(const GetOptions(source: Source.server));
    final sessions = await _db.collection('walking_sessions')
        .where('userId', isEqualTo: uid).get(const GetOptions(source: Source.server));
    int steps = 0;
    for (final doc in hours.docs) {
      final n = doc.data()['steps'];
      if (n is int && n >= 0) steps += n;
    }
    for (final doc in sessions.docs) {
      final hunt = completedHunt(doc.data(), start, end);
      if (hunt != null) steps += hunt.value;
    }
    if (FirebaseAuth.instance.currentUser?.uid != uid || weekStart() != start) return;
    final ref = scores(start).doc(uid);
    await _db.runTransaction((tx) async {
      final old = await tx.get(ref);
      if (old.data()?['steps'] == steps) return;
      if (FirebaseAuth.instance.currentUser?.uid != uid) return;
      tx.set(ref, {'steps': steps, 'weekStart': Timestamp.fromDate(start),
        'updatedAt': FieldValue.serverTimestamp()});
    });
  }

  // Refresh while the leaderboard is visible, including week rollover.
  static Stream<FreejWeek> watch() async* {
    while (true) {
      try { yield await load(); } catch (_) { yield FreejWeek.unavailable(); }
      await Future<void>.delayed(const Duration(seconds: 60));
    }
  }
  static Future<FreejWeek> load() async {
    final start = weekStart();
    final results = await Future.wait([
      scores(start).get(const GetOptions(source: Source.server)),
      _db.collection('freej').get(const GetOptions(source: Source.server)),
      _db.collection('leaderboard').get(const GetOptions(source: Source.server)),
    ]);
    final counts = <String,int>{for (final d in results[0].docs)
      if (d.data()['steps'] is int) d.id: d.data()['steps'] as int};
    final names = <String,String>{for (final d in results[2].docs)
      if (d.data()['name'] is String) d.id: d.data()['name'] as String};
    final teams = await Future.wait(results[1].docs.map((d) async {
      final members = await d.reference.collection('members').get(const GetOptions(source: Source.server));
      final ids = members.docs.map((m) => m.id).toList();
      return FreejWeekTeam(d.id, (d.data()['name'] ?? 'Freej').toString(), ids, counts);
    }));
    teams.sort((a,b) { final c = b.average.compareTo(a.average); return c != 0 ? c : a.id.compareTo(b.id); });
    return FreejWeek(counts, names, teams, false);
  }
}
class FreejWeekTeam {
  final String id, name;
  final List<String> members;
  final Map<String,int> scores;
  FreejWeekTeam(this.id,this.name,this.members,this.scores);
  int get synced => members.where(scores.containsKey).length;
  double get average => members.isEmpty ? 0 : members.fold<int>(0,(s,id)=>s+(scores[id]??0))/members.length;
  bool get eligible => members.length >= 2 && synced == members.length;
}
class FreejWeek {
  final Map<String,int> scores;
  final Map<String,String> names;
  final List<FreejWeekTeam> teams;
  final bool error;
  FreejWeek(this.scores,this.names,this.teams,this.error);
  factory FreejWeek.unavailable() => FreejWeek({}, {}, [], true);
  List<FreejWeekTeam> get ranked => teams.where((t)=>t.eligible).toList();
  FreejWeekTeam? team(String id) { for(final t in teams) { if(t.id==id) return t; } return null; }
  int rank(FreejWeekTeam t) => 1+ranked.where((a)=>a.average>t.average).length;
}
