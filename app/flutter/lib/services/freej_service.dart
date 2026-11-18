import 'dart:math';
import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:firebase_auth/firebase_auth.dart';

class FreejService {
  final _db = FirebaseFirestore.instance;
  final _auth = FirebaseAuth.instance;

  String get _uid {
    final uid = _auth.currentUser?.uid;
    if (uid == null) throw StateError('Sign in first');
    return uid;
  }

  DocumentReference<Map<String, dynamic>> _membership(String uid) =>
      _db.collection('freej_user_memberships').doc(uid);

  String _newCode() {
    const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
    final rng = Random.secure();
    return List.generate(10, (_) => alphabet[rng.nextInt(alphabet.length)]).join();
  }

  Future<Map<String, dynamic>> create(String rawName) async {
    final uid = _uid;
    final name = rawName.trim();
    if (name.length < 3 || name.length > 40) {
      throw ArgumentError('Freej name must contain 3–40 characters.');
    }
    final code = _newCode();
    final teamRef = _db.collection('freej').doc(code);
    final userRef = _membership(uid);
    final memberRef = teamRef.collection('members').doc(uid);
    await _db.runTransaction((tx) async {
      final existing = await tx.get(userRef);
      final team = await tx.get(teamRef);
      if (existing.exists) throw StateError('You already belong to a Freej.');
      if (team.exists) throw StateError('Invite code collision. Please retry.');
      tx.set(teamRef, {
        'name': name, 'inviteCode': code, 'captainUid': uid,
        'memberCount': 1, 'createdAt': FieldValue.serverTimestamp(),
      });
      tx.set(memberRef, {'uid': uid, 'joinedAt': FieldValue.serverTimestamp()});
      tx.set(userRef, {'freejId': code, 'joinedAt': FieldValue.serverTimestamp()});
    });
    return {'freejId': code, 'inviteCode': code};
  }

  Future<void> join(String rawCode) async {
    final uid = _uid;
    final code = rawCode.trim().toUpperCase();
    if (!RegExp(r'^[A-HJ-NP-Z2-9]{10}$').hasMatch(code)) {
      throw ArgumentError('Enter a valid 10-character invite code.');
    }
    final teamRef = _db.collection('freej').doc(code);
    final userRef = _membership(uid);
    final memberRef = teamRef.collection('members').doc(uid);
    await _db.runTransaction((tx) async {
      final current = await tx.get(userRef);
      final team = await tx.get(teamRef);
      if (current.exists) throw StateError('Leave your current Freej first.');
      if (!team.exists) throw StateError('Invite code not found.');
      tx.set(memberRef, {'uid': uid, 'joinedAt': FieldValue.serverTimestamp()});
      tx.set(userRef, {'freejId': code, 'joinedAt': FieldValue.serverTimestamp()});
      tx.update(teamRef, {'memberCount': FieldValue.increment(1)});
    });
  }

  Future<void> transferCaptain(String targetUid) async {
    final uid = _uid;
    if (targetUid == uid) throw ArgumentError('Choose another member.');
    final userRef = _membership(uid);
    await _db.runTransaction((tx) async {
      final membership = await tx.get(userRef);
      if (!membership.exists) throw StateError('You are not in a Freej.');
      final code = membership.data()!['freejId'] as String;
      final teamRef = _db.collection('freej').doc(code);
      final team = await tx.get(teamRef);
      final target = await tx.get(teamRef.collection('members').doc(targetUid));
      if (!team.exists || team.data()!['captainUid'] != uid) {
        throw StateError('Only the captain can transfer ownership.');
      }
      if (!target.exists) throw StateError('Target is not a member.');
      tx.update(teamRef, {'captainUid': targetUid});
    });
  }

  Future<void> leave() async {
    final uid = _uid;
    final userRef = _membership(uid);
    await _db.runTransaction((tx) async {
      final membership = await tx.get(userRef);
      if (!membership.exists) throw StateError('You are not in a Freej.');
      final code = membership.data()!['freejId'] as String;
      final teamRef = _db.collection('freej').doc(code);
      final team = await tx.get(teamRef);
      if (!team.exists) throw StateError('Freej not found.');
      final data = team.data()!;
      final count = (data['memberCount'] as num).toInt();
      if (data['captainUid'] == uid && count > 1) {
        throw StateError('Transfer captain before leaving.');
      }
      tx.delete(teamRef.collection('members').doc(uid));
      tx.delete(userRef);
      if (count == 1) {
        tx.delete(teamRef);
      } else {
        tx.update(teamRef, {'memberCount': FieldValue.increment(-1)});
      }
    });
  }

  Stream<DocumentSnapshot<Map<String, dynamic>>> myMembership() =>
      _membership(_uid).snapshots();

  Stream<DocumentSnapshot<Map<String, dynamic>>> freej(String freejId) =>
      _db.collection('freej').doc(freejId).snapshots();

  Stream<QuerySnapshot<Map<String, dynamic>>> members(String freejId) =>
      _db.collection('freej').doc(freejId).collection('members').snapshots();
}
