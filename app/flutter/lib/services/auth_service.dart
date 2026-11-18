import 'package:firebase_auth/firebase_auth.dart';
import 'package:cloud_firestore/cloud_firestore.dart';

class AuthService {
  final FirebaseAuth _auth = FirebaseAuth.instance;
  final FirebaseFirestore _db = FirebaseFirestore.instance;

  Future<UserCredential> register({
    required String name,
    required String email,
    required String password,
  }) async {
    final credential =
        await _auth.createUserWithEmailAndPassword(
      email: email.trim(),
      password: password,
    );

    final user = credential.user!;

    await user.updateDisplayName(name.trim());

    final batch = _db.batch();

    final userRef =
        _db.collection('users').doc(user.uid);

    final leaderboardRef =
        _db.collection('leaderboard').doc(user.uid);

    // Main user document.
    batch.set(userRef, {
      'name': name.trim(),
      'email': user.email,
      'level': 1,
      'xp': 0,
      'totalSteps': 0,
      'crystals': 0,
      'streak': 0,
      'createdAt': FieldValue.serverTimestamp(),
    });

    // Public leaderboard mirror.
    batch.set(leaderboardRef, {
      'userId': user.uid,
      'name': name.trim(),
      'totalSteps': 0,
      'streak': 0,
      'crystals': 0,
    });

    await batch.commit();

    return credential;
  }

  Future<UserCredential> login({
    required String email,
    required String password,
  }) {
    return _auth.signInWithEmailAndPassword(
      email: email.trim(),
      password: password,
    );
  }

  Future<void> logout() {
    return _auth.signOut();
  }

  Stream<User?> get authStateChanges =>
      _auth.authStateChanges();
}