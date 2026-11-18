import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:flutter/material.dart';

import '../services/hunt_achievements.dart';

class ProfileAchievements extends StatefulWidget {
  const ProfileAchievements({super.key, required this.userId});

  final String userId;

  @override
  State<ProfileAchievements> createState() => _ProfileAchievementsState();
}

class _ProfileAchievementsState extends State<ProfileAchievements> {
  late Stream<QuerySnapshot<Map<String, dynamic>>> _badges;
  late Future<void> _historyCheck;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void didUpdateWidget(covariant ProfileAchievements oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.userId != widget.userId) _load();
  }

  void _load() {
    _badges = FirebaseFirestore.instance
        .collection('users')
        .doc(widget.userId)
        .collection('earned_badges')
        .orderBy('earnedAt', descending: true)
        .snapshots();
    _historyCheck = HuntAchievements().checkHistory(widget.userId);
  }

  @override
  Widget build(BuildContext context) {
    return StreamBuilder<QuerySnapshot<Map<String, dynamic>>>(
      stream: _badges,
      builder: (context, badges) {
        if (badges.hasError) {
          return const Text('Could not load achievements.');
        }
        return FutureBuilder<void>(
          future: _historyCheck,
          builder: (context, check) {
            final docs = badges.data?.docs ?? [];
            final pending = check.connectionState != ConnectionState.done;
            if (!badges.hasData || (pending && docs.isEmpty)) {
              return const Padding(
                padding: EdgeInsets.all(16),
                child: Center(child: CircularProgressIndicator()),
              );
            }
            return Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (check.hasError)
                  ListTile(
                    title: const Text('Could not check previous hunts.'),
                    trailing: TextButton(
                      onPressed: () => setState(() {
                        _historyCheck =
                            HuntAchievements().checkHistory(widget.userId);
                      }),
                      child: const Text('Retry'),
                    ),
                  ),
                if (docs.isEmpty && !check.hasError)
                  const ListTile(
                    leading: Icon(Icons.workspace_premium_outlined),
                    title: Text('No achievements earned yet'),
                    subtitle: Text('Complete hunts to unlock achievements.'),
                  ),
                for (final doc in docs)
                  Card(
                    child: ListTile(
                      leading: _BadgeImage(badge: doc),
                      title: Text(
                          (doc.data()['name'] ?? 'Achievement').toString()),
                      subtitle:
                          Text((doc.data()['description'] ?? '').toString()),
                    ),
                  ),
              ],
            );
          },
        );
      },
    );
  }
}

class _BadgeImage extends StatelessWidget {
  const _BadgeImage({required this.badge});

  final QueryDocumentSnapshot<Map<String, dynamic>> badge;

  Widget _image(String category) {
    final filename = switch (category) {
      'walking' => 'walking',
      'monster_hunting' => 'monster_hunting',
      'crystals_energy' => 'crystals_energy',
      'consistency' => 'consistency_streaks',
      'special' => 'special_challenges',
      _ => null,
    };
    if (filename == null) return const Icon(Icons.workspace_premium_rounded);
    return Image.asset('assets/badges/$filename.png',
        width: 48, height: 48, fit: BoxFit.contain);
  }

  @override
  Widget build(BuildContext context) {
    final category = badge.data()['category'];
    if (category is String && category.isNotEmpty) return _image(category);
    // Older earned records stored only the achievement ID and description.
    return FutureBuilder<DocumentSnapshot<Map<String, dynamic>>>(
      future: FirebaseFirestore.instance
          .collection('achievements')
          .doc(badge.id)
          .get(),
      builder: (context, snapshot) =>
          _image(categoryForAchievement(snapshot.data?.data() ?? {})),
    );
  }
}
