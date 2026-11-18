import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:firebase_auth/firebase_auth.dart';
import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import '../theme/app_theme.dart';
import '../widgets/shared_widgets.dart';
import 'main_shell.dart';
import 'freej_screen.dart';

class LeaderboardScreen extends StatefulWidget {
  const LeaderboardScreen({super.key});

  @override
  State<LeaderboardScreen> createState() =>
      _LeaderboardScreenState();
}

class _LeaderboardScreenState extends State<LeaderboardScreen> {
  int _tab = 0;
  @override
  Widget build(BuildContext context) {
    final cs = Theme.of(context).colorScheme;
    final mc = Theme.of(context).extension<MasarColors>()!;
    return Scaffold(
      appBar: const MasarAppBar(),
      body: SafeArea(child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          Text('Leaderboard', style: GoogleFonts.spaceGrotesk(fontSize: 26,
            fontWeight: FontWeight.w800, color: cs.onSurface)),
          const SizedBox(height: 16),
          Container(padding: const EdgeInsets.all(20),
            decoration: BoxDecoration(color: mc.surfaceContainer,
              borderRadius: BorderRadius.circular(18)),
            child: Column(children: [
              Text('YOUR WEEKLY STEPS', style: GoogleFonts.manrope(
                fontSize: 11, fontWeight: FontWeight.w700, color: cs.secondary)),
              const SizedBox(height: 8),
              Text('Not tracked yet', style: GoogleFonts.spaceGrotesk(
                fontSize: 26, fontWeight: FontWeight.w800, color: cs.primary)),
              const SizedBox(height: 4),
              Text('Sunday – Saturday · Kuwait time', style: GoogleFonts.manrope(
                fontSize: 11, color: cs.secondary)),
            ])),
          const SizedBox(height: 16),
          Container(padding: const EdgeInsets.all(4), decoration: BoxDecoration(
            color: mc.surfaceContainer, borderRadius: BorderRadius.circular(14)),
            child: Row(children: [
              _TabButton(label: 'Global', active: _tab == 0,
                onTap: () => setState(() => _tab = 0)),
              _TabButton(label: 'Freej', active: _tab == 1,
                onTap: () => setState(() => _tab = 1)),
            ])),
          const SizedBox(height: 22),
          if (_tab == 0) ...[
            Text('Global Rankings', style: GoogleFonts.manrope(
              fontSize: 18, fontWeight: FontWeight.w800, color: cs.onSurface)),
            const SizedBox(height: 5),
            Text('All-time steps · existing leaderboard', style:
              GoogleFonts.manrope(fontSize: 12, color: cs.secondary)),
            const SizedBox(height: 14),
            const _RealStepsLeaderboard(),
          ] else const _FreejLeaderboardPanel(),
        ]),
      )),
    );
  }
}

class _FreejLeaderboardPanel extends StatelessWidget {
  const _FreejLeaderboardPanel();
  @override
  Widget build(BuildContext context) {
    final uid = FirebaseAuth.instance.currentUser?.uid;
    if (uid == null) return const Text('Sign in to see Freej.');
    final cs = Theme.of(context).colorScheme;
    final mc = Theme.of(context).extension<MasarColors>()!;
    return StreamBuilder<DocumentSnapshot<Map<String, dynamic>>>(
      stream: FirebaseFirestore.instance.collection('freej_user_memberships').doc(uid).snapshots(),
      builder: (context, membership) {
        if (membership.hasError) return Text('Unable to load Freej: ${membership.error}');
        if (!membership.hasData) return const Center(child: CircularProgressIndicator());
        final id = membership.data!.data()?['freejId'];
        if (id is! String || id.isEmpty) {
          return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            Text('Walk together. Compete as a Freej.', style: GoogleFonts.manrope(
              fontSize: 19, fontWeight: FontWeight.w800)),
            const SizedBox(height: 14),
            Text('Create your own team or join your friends using an invite code.',
              style: TextStyle(color: cs.secondary)),
            const SizedBox(height: 16),
            FilledButton.icon(onPressed: () => Navigator.push(context,
              MaterialPageRoute(builder: (_) => const FreejScreen())),
              icon: const Icon(Icons.groups_outlined),
              label: const Text('Create or Join Freej')),
          ]);
        }
        return StreamBuilder<DocumentSnapshot<Map<String, dynamic>>>(
          stream: FirebaseFirestore.instance.collection('freej').doc(id).snapshots(),
          builder: (context, team) {
            if (team.hasError) return Text('Unable to load team: ${team.error}');
            if (!team.hasData) return const Center(child: CircularProgressIndicator());
            final data = team.data!.data();
            if (data == null) return const Text('Freej not found.');
            final count = (data['memberCount'] as num?)?.toInt() ?? 0;
            return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              Row(children: [
                Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text('Your Freej', style: GoogleFonts.manrope(
                    fontSize: 18, fontWeight: FontWeight.w800)),
                  Text('${data['name'] ?? 'Freej'} · $count members',
                    style: TextStyle(color: cs.secondary)),
                ])),
                TextButton.icon(onPressed: () => Navigator.push(context,
                  MaterialPageRoute(builder: (_) => const FreejScreen())),
                  icon: const Icon(Icons.settings_outlined, size: 18),
                  label: const Text('Manage')),
              ]),
              const SizedBox(height: 16),
              Row(children: [
                Expanded(child: _FreejStat(title: 'TEAM RANK', value: '—',
                  detail: count < 2 ? 'Need 2+ members' : 'Weekly ranking pending')),
                const SizedBox(width: 10),
                const Expanded(child: _FreejStat(title: 'TEAM AVERAGE',
                  value: '—', detail: 'Weekly steps / member')),
              ]),
              const SizedBox(height: 22),
              Text('Your Team Rankings', style: GoogleFonts.manrope(
                fontSize: 18, fontWeight: FontWeight.w800)),
              const SizedBox(height: 5),
              Text('All-time steps preview · weekly scores not connected',
                style: TextStyle(color: cs.secondary, fontSize: 12)),
              const SizedBox(height: 12),
              _FreejMemberRankings(freejId: id),
              const SizedBox(height: 22),
              Text('Freej League', style: GoogleFonts.manrope(
                fontSize: 18, fontWeight: FontWeight.w800)),
              const SizedBox(height: 10),
              Container(padding: const EdgeInsets.all(18), decoration: BoxDecoration(
                color: mc.surfaceContainer, borderRadius: BorderRadius.circular(16)),
                child: Text('Team league rankings will appear once verified weekly steps are connected. Teams need at least 2 members to qualify.',
                  style: TextStyle(color: cs.secondary))),
            ]);
          },
        );
      },
    );
  }
}

class _FreejStat extends StatelessWidget {
  final String title, value, detail;
  const _FreejStat({required this.title, required this.value, required this.detail});
  @override
  Widget build(BuildContext context) {
    final cs = Theme.of(context).colorScheme;
    final mc = Theme.of(context).extension<MasarColors>()!;
    return Container(padding: const EdgeInsets.all(14), decoration: BoxDecoration(
      color: mc.surfaceContainer, borderRadius: BorderRadius.circular(14)),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(title, style: TextStyle(color: cs.secondary, fontSize: 10,
          fontWeight: FontWeight.w700)),
        const SizedBox(height: 6),
        Text(value, style: GoogleFonts.spaceGrotesk(fontSize: 26,
          fontWeight: FontWeight.w800, color: cs.primary)),
        Text(detail, style: TextStyle(color: cs.secondary, fontSize: 10)),
      ]));
  }
}

class _FreejMemberRankings extends StatelessWidget {
  final String freejId;
  const _FreejMemberRankings({required this.freejId});
  @override
  Widget build(BuildContext context) {
    return StreamBuilder<QuerySnapshot<Map<String, dynamic>>>(
      stream: FirebaseFirestore.instance.collection('freej').doc(freejId)
        .collection('members').snapshots(),
      builder: (context, members) {
        if (members.hasError) return Text('Unable to load members: ${members.error}');
        if (!members.hasData) return const Center(child: CircularProgressIndicator());
        final ids = members.data!.docs.map((d) => d.id).toSet();
        if (ids.isEmpty) return const Text('No members yet.');
        return StreamBuilder<QuerySnapshot<Map<String, dynamic>>>(
          stream: FirebaseFirestore.instance.collection('leaderboard').snapshots(),
          builder: (context, board) {
            if (board.hasError) return Text('Unable to load scores: ${board.error}');
            if (!board.hasData) return const Center(child: CircularProgressIndicator());
            final entries = board.data!.docs.where((d) => ids.contains(d.id)).toList()
              ..sort((a,b) => _toInt(b.data()['totalSteps']).compareTo(_toInt(a.data()['totalSteps'])));
            final users = <_LeaderboardUser>[];
            for (var i=0; i<entries.length; i++) {
              final d=entries[i];
              users.add(_LeaderboardUser(userId:d.id,
                name:(d.data()['name'] ?? 'Walker').toString(),
                totalSteps:_toInt(d.data()['totalSteps']),
                streak:_toInt(d.data()['streak']), rank:i+1));
            }
            if (users.isEmpty) return const Text('No member step records yet.');
            return Column(children: [
              _StepsPodiumWidget(podium: users.take(3).toList()),
              const SizedBox(height: 18),
              ...users.map((u) => _StepsLeaderboardRow(user:u)),
            ]);
          },
        );
      },
    );
  }
}

// ========================================================
// DATA MODEL
// ========================================================

class _LeaderboardUser {
  final String userId;
  final String name;
  final int totalSteps;
  final int streak;
  final int rank;

  const _LeaderboardUser({
    required this.userId,
    required this.name,
    required this.totalSteps,
    required this.streak,
    required this.rank,
  });
}

// ========================================================
// WEEKLY DISTANCE TAB
// Ranked by totalSteps
// ========================================================

class _RealStepsLeaderboard
    extends StatelessWidget {
  const _RealStepsLeaderboard();

  @override
  Widget build(BuildContext context) {
    return StreamBuilder<
        QuerySnapshot<Map<String, dynamic>>>(
      stream: FirebaseFirestore.instance
          .collection('leaderboard')
          .orderBy(
            'totalSteps',
            descending: true,
          )
          .limit(10)
          .snapshots(),
      builder: (context, snapshot) {
        if (snapshot.hasError) {
          return Padding(
            padding:
                const EdgeInsets.symmetric(
              vertical: 30,
            ),
            child: Text(
              'Unable to load leaderboard.\n'
              '${snapshot.error}',
              textAlign: TextAlign.center,
            ),
          );
        }

        if (snapshot.connectionState ==
            ConnectionState.waiting) {
          return const Padding(
            padding:
                EdgeInsets.symmetric(
              vertical: 40,
            ),
            child:
                CircularProgressIndicator(),
          );
        }

        final docs =
            snapshot.data?.docs ?? [];

        if (docs.isEmpty) {
          return const Padding(
            padding:
                EdgeInsets.symmetric(
              vertical: 40,
            ),
            child: Text(
              'No leaderboard data yet.',
            ),
          );
        }

        final users =
            <_LeaderboardUser>[];

        for (int i = 0;
            i < docs.length;
            i++) {
          final doc = docs[i];
          final data = doc.data();

          users.add(
            _LeaderboardUser(
              userId:
                  (data['userId'] ??
                          doc.id)
                      .toString(),
              name:
                  (data['name'] ??
                          'Unknown User')
                      .toString(),
              totalSteps:
                  _toInt(
                data['totalSteps'],
              ),
              streak:
                  _toInt(
                data['streak'],
              ),
              rank: i + 1,
            ),
          );
        }

        final podium =
            users.take(3).toList();

        final rankedList = users;

        return Column(
          children: [
            if (podium.isNotEmpty)
              _StepsPodiumWidget(
                podium: podium,
              ),

            if (podium.isNotEmpty)
              const SizedBox(
                height: 20,
              ),

            SectionHeader(
              title: 'Top Rankings',
            ),

            const SizedBox(height: 8),

            ...rankedList.map(
              (user) =>
                  _StepsLeaderboardRow(
                user: user,
              ),
            ),
          ],
        );
      },
    );
  }
}

// ========================================================
// STREAK TAB
// Ranked by real streak
// ========================================================

class _RealStreakLeaderboard
    extends StatelessWidget {
  const _RealStreakLeaderboard();

  @override
  Widget build(BuildContext context) {
    return StreamBuilder<
        QuerySnapshot<Map<String, dynamic>>>(
      stream: FirebaseFirestore.instance
          .collection('leaderboard')
          .orderBy(
            'streak',
            descending: true,
          )
          .limit(10)
          .snapshots(),
      builder: (context, snapshot) {
        if (snapshot.hasError) {
          return Padding(
            padding:
                const EdgeInsets.symmetric(
              vertical: 30,
            ),
            child: Text(
              'Unable to load streak leaderboard.\n'
              '${snapshot.error}',
              textAlign: TextAlign.center,
            ),
          );
        }

        if (snapshot.connectionState ==
            ConnectionState.waiting) {
          return const Padding(
            padding:
                EdgeInsets.symmetric(
              vertical: 40,
            ),
            child:
                CircularProgressIndicator(),
          );
        }

        final docs =
            snapshot.data?.docs ?? [];

        if (docs.isEmpty) {
          return const Padding(
            padding:
                EdgeInsets.symmetric(
              vertical: 40,
            ),
            child: Text(
              'No streak data yet.',
            ),
          );
        }

        final users =
            <_LeaderboardUser>[];

        for (int i = 0;
            i < docs.length;
            i++) {
          final doc = docs[i];
          final data = doc.data();

          users.add(
            _LeaderboardUser(
              userId:
                  (data['userId'] ??
                          doc.id)
                      .toString(),
              name:
                  (data['name'] ??
                          'Unknown User')
                      .toString(),
              totalSteps:
                  _toInt(
                data['totalSteps'],
              ),
              streak:
                  _toInt(
                data['streak'],
              ),
              rank: i + 1,
            ),
          );
        }

        final podium =
            users.take(3).toList();

        final rankedList = users;

        return Column(
          children: [
            if (podium.isNotEmpty)
              _StreakPodiumWidget(
                podium: podium,
              ),

            if (podium.isNotEmpty)
              const SizedBox(
                height: 20,
              ),

            SectionHeader(
              title: 'Top Rankings',
            ),

            const SizedBox(height: 8),

            ...rankedList.map(
              (user) =>
                  _StreakLeaderboardRow(
                user: user,
              ),
            ),
          ],
        );
      },
    );
  }
}

// ========================================================
// WEEKLY DISTANCE PODIUM
// ========================================================

class _StepsPodiumWidget
    extends StatelessWidget {
  final List<_LeaderboardUser> podium;

  const _StepsPodiumWidget({
    required this.podium,
  });

  @override
  Widget build(BuildContext context) {
    final cs =
        Theme.of(context).colorScheme;

    final mc =
        Theme.of(context)
            .extension<MasarColors>()!;

    final screenWidth =
        MediaQuery.sizeOf(context).width;

    final scale =
        (screenWidth / 390.0)
            .clamp(0.78, 1.05);

    final ordered =
        <_LeaderboardUser?>[
      podium.length >= 2
          ? podium[1]
          : null,
      podium.isNotEmpty
          ? podium[0]
          : null,
      podium.length >= 3
          ? podium[2]
          : null,
    ];

    final barHeights = [
      58.0 * scale,
      82.0 * scale,
      44.0 * scale,
    ];

    final avatarSizes = [
      48.0 * scale,
      60.0 * scale,
      44.0 * scale,
    ];

    return Row(
      crossAxisAlignment:
          CrossAxisAlignment.end,
      children:
          List.generate(3, (i) {
        final user = ordered[i];

        if (user == null) {
          return const Expanded(
            child: SizedBox(),
          );
        }

        final isFirst =
            user.rank == 1;

        return Expanded(
          flex: isFirst ? 5 : 4,
          child: Padding(
            padding:
                const EdgeInsets.symmetric(
              horizontal: 3,
            ),
            child: Column(
              mainAxisSize:
                  MainAxisSize.min,
              mainAxisAlignment:
                  MainAxisAlignment.end,
              children: [
                if (isFirst)
                  Icon(
                    Icons
                        .workspace_premium_rounded,
                    color:
                        cs.primaryContainer,
                    size: 24 * scale,
                  ),

                if (isFirst)
                  SizedBox(
                    height: 2 * scale,
                  ),

                SizedBox(
                  width:
                      avatarSizes[i],
                  height:
                      avatarSizes[i],
                  child: Stack(
                    children: [
                      Container(
                        width:
                            avatarSizes[i],
                        height:
                            avatarSizes[i],
                        decoration:
                            BoxDecoration(
                          shape:
                              BoxShape.circle,
                          color: mc
                              .surfaceHighest,
                          border:
                              Border.all(
                            color: isFirst
                                ? cs
                                    .primaryContainer
                                : cs
                                    .outlineVariant,
                            width:
                                isFirst
                                    ? 3
                                    : 2,
                          ),
                          boxShadow:
                              isFirst
                                  ? [
                                      BoxShadow(
                                        color: cs
                                            .primaryContainer
                                            .withValues(
                                          alpha:
                                              0.30,
                                        ),
                                        blurRadius:
                                            10,
                                      ),
                                    ]
                                  : null,
                        ),
                        child: Icon(
                          Icons
                              .person_rounded,
                          color: isFirst
                              ? cs
                                  .primaryContainer
                              : cs.outline,
                          size:
                              avatarSizes[i] *
                                  0.55,
                        ),
                      ),

                      Positioned(
                        bottom: 0,
                        right: 0,
                        child: Container(
                          width:
                              19 * scale,
                          height:
                              19 * scale,
                          decoration:
                              BoxDecoration(
                            shape:
                                BoxShape.circle,
                            color: isFirst
                                ? cs
                                    .primaryContainer
                                : mc
                                    .surfaceHighest,
                            border:
                                Border.all(
                              color:
                                  cs.surface,
                              width: 1.5,
                            ),
                          ),
                          child: Center(
                            child:
                                FittedBox(
                              fit:
                                  BoxFit
                                      .scaleDown,
                              child: Text(
                                '${user.rank}',
                                style:
                                    GoogleFonts
                                        .spaceGrotesk(
                                  fontSize:
                                      9,
                                  fontWeight:
                                      FontWeight
                                          .w800,
                                  color: isFirst
                                      ? cs
                                          .surface
                                      : cs
                                          .secondary,
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),

                SizedBox(
                  height: 5 * scale,
                ),

                SizedBox(
                  width:
                      double.infinity,
                  child: FittedBox(
                    fit:
                        BoxFit.scaleDown,
                    child: Text(
                      user.name,
                      maxLines: 1,
                      style:
                          GoogleFonts.manrope(
                        fontSize:
                            11 * scale,
                        fontWeight:
                            FontWeight
                                .w700,
                        color: isFirst
                            ? cs
                                .primaryContainer
                            : cs.secondary,
                      ),
                    ),
                  ),
                ),

                SizedBox(
                  height: 2 * scale,
                ),

                SizedBox(
                  width:
                      double.infinity,
                  child: FittedBox(
                    fit:
                        BoxFit.scaleDown,
                    child: Text(
                      '${user.totalSteps} STEPS',
                      maxLines: 1,
                      style: GoogleFonts
                          .spaceGrotesk(
                        fontSize:
                            (isFirst
                                    ? 14
                                    : 12) *
                                scale,
                        fontWeight:
                            FontWeight
                                .w700,
                        color:
                            cs.primary,
                      ),
                    ),
                  ),
                ),

                SizedBox(
                  height: 5 * scale,
                ),

                Container(
                  width:
                      double.infinity,
                  height:
                      barHeights[i],
                  decoration:
                      BoxDecoration(
                    gradient: isFirst
                        ? mc
                            .primaryGradient
                        : null,
                    color: isFirst
                        ? null
                        : mc.surfaceHigh,
                    borderRadius:
                        const BorderRadius
                            .vertical(
                      top:
                          Radius.circular(
                        10,
                      ),
                    ),
                  ),
                  child: Center(
                    child: FittedBox(
                      child: Text(
                        '${user.rank}',
                        style: GoogleFonts
                            .spaceGrotesk(
                          fontSize:
                              (isFirst
                                      ? 24
                                      : 18) *
                                  scale,
                          fontWeight:
                              FontWeight
                                  .w800,
                          color: isFirst
                              ? cs.surface
                              : cs.outline,
                        ),
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        );
      }),
    );
  }
}

// ========================================================
// STREAK PODIUM
// Ranked by streak, displays streak + steps
// ========================================================

class _StreakPodiumWidget
    extends StatelessWidget {
  final List<_LeaderboardUser> podium;

  const _StreakPodiumWidget({
    required this.podium,
  });

  @override
  Widget build(BuildContext context) {
    final cs =
        Theme.of(context).colorScheme;

    final mc =
        Theme.of(context)
            .extension<MasarColors>()!;

    final screenWidth =
        MediaQuery.sizeOf(context).width;

    final scale =
        (screenWidth / 390.0)
            .clamp(0.78, 1.05);

    final ordered =
        <_LeaderboardUser?>[
      podium.length >= 2
          ? podium[1]
          : null,
      podium.isNotEmpty
          ? podium[0]
          : null,
      podium.length >= 3
          ? podium[2]
          : null,
    ];

    final barHeights = [
      58.0 * scale,
      82.0 * scale,
      44.0 * scale,
    ];

    final avatarSizes = [
      48.0 * scale,
      60.0 * scale,
      44.0 * scale,
    ];

    return Row(
      crossAxisAlignment:
          CrossAxisAlignment.end,
      children:
          List.generate(3, (i) {
        final user = ordered[i];

        if (user == null) {
          return const Expanded(
            child: SizedBox(),
          );
        }

        final isFirst =
            user.rank == 1;

        return Expanded(
          flex: isFirst ? 5 : 4,
          child: Padding(
            padding:
                const EdgeInsets.symmetric(
              horizontal: 3,
            ),
            child: Column(
              mainAxisSize:
                  MainAxisSize.min,
              mainAxisAlignment:
                  MainAxisAlignment.end,
              children: [
                if (isFirst)
                  Icon(
                    Icons
                        .workspace_premium_rounded,
                    color:
                        cs.primaryContainer,
                    size: 24 * scale,
                  ),

                if (isFirst)
                  SizedBox(
                    height: 2 * scale,
                  ),

                SizedBox(
                  width:
                      avatarSizes[i],
                  height:
                      avatarSizes[i],
                  child: Stack(
                    children: [
                      Container(
                        width:
                            avatarSizes[i],
                        height:
                            avatarSizes[i],
                        decoration:
                            BoxDecoration(
                          shape:
                              BoxShape.circle,
                          color: mc
                              .surfaceHighest,
                          border:
                              Border.all(
                            color: isFirst
                                ? cs
                                    .primaryContainer
                                : cs
                                    .outlineVariant,
                            width:
                                isFirst
                                    ? 3
                                    : 2,
                          ),
                          boxShadow:
                              isFirst
                                  ? [
                                      BoxShadow(
                                        color: cs
                                            .primaryContainer
                                            .withValues(
                                          alpha:
                                              0.30,
                                        ),
                                        blurRadius:
                                            10,
                                      ),
                                    ]
                                  : null,
                        ),
                        child: Icon(
                          Icons
                              .person_rounded,
                          color: isFirst
                              ? cs
                                  .primaryContainer
                              : cs.outline,
                          size:
                              avatarSizes[i] *
                                  0.55,
                        ),
                      ),

                      Positioned(
                        bottom: 0,
                        right: 0,
                        child: Container(
                          width:
                              19 * scale,
                          height:
                              19 * scale,
                          decoration:
                              BoxDecoration(
                            shape:
                                BoxShape.circle,
                            color: isFirst
                                ? cs
                                    .primaryContainer
                                : mc
                                    .surfaceHighest,
                            border:
                                Border.all(
                              color:
                                  cs.surface,
                              width: 1.5,
                            ),
                          ),
                          child: Center(
                            child:
                                FittedBox(
                              fit:
                                  BoxFit
                                      .scaleDown,
                              child: Text(
                                '${user.rank}',
                                style:
                                    GoogleFonts
                                        .spaceGrotesk(
                                  fontSize:
                                      9,
                                  fontWeight:
                                      FontWeight
                                          .w800,
                                  color: isFirst
                                      ? cs
                                          .surface
                                      : cs
                                          .secondary,
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),

                SizedBox(
                  height: 5 * scale,
                ),

                SizedBox(
                  width:
                      double.infinity,
                  child: FittedBox(
                    fit:
                        BoxFit.scaleDown,
                    child: Text(
                      user.name,
                      maxLines: 1,
                      style:
                          GoogleFonts.manrope(
                        fontSize:
                            11 * scale,
                        fontWeight:
                            FontWeight
                                .w700,
                        color: isFirst
                            ? cs
                                .primaryContainer
                            : cs.secondary,
                      ),
                    ),
                  ),
                ),

                SizedBox(
                  height: 1 * scale,
                ),

                SizedBox(
                  width:
                      double.infinity,
                  child: FittedBox(
                    fit:
                        BoxFit.scaleDown,
                    child: Text(
                      '${user.streak} '
                      '${user.streak == 1 ? 'DAY' : 'DAYS'} STREAK',
                      maxLines: 1,
                      style:
                          GoogleFonts.manrope(
                        fontSize:
                            9 * scale,
                        fontWeight:
                            FontWeight
                                .w600,
                        color:
                            cs.secondary,
                      ),
                    ),
                  ),
                ),

                SizedBox(
                  height: 2 * scale,
                ),

                SizedBox(
                  width:
                      double.infinity,
                  child: FittedBox(
                    fit:
                        BoxFit.scaleDown,
                    child: Text(
                      '${user.totalSteps} STEPS',
                      maxLines: 1,
                      style: GoogleFonts
                          .spaceGrotesk(
                        fontSize:
                            (isFirst
                                    ? 14
                                    : 12) *
                                scale,
                        fontWeight:
                            FontWeight
                                .w700,
                        color:
                            cs.primary,
                      ),
                    ),
                  ),
                ),

                SizedBox(
                  height: 5 * scale,
                ),

                Container(
                  width:
                      double.infinity,
                  height:
                      barHeights[i],
                  decoration:
                      BoxDecoration(
                    gradient: isFirst
                        ? mc
                            .primaryGradient
                        : null,
                    color: isFirst
                        ? null
                        : mc.surfaceHigh,
                    borderRadius:
                        const BorderRadius
                            .vertical(
                      top:
                          Radius.circular(
                        10,
                      ),
                    ),
                  ),
                  child: Center(
                    child: FittedBox(
                      child: Text(
                        '${user.rank}',
                        style: GoogleFonts
                            .spaceGrotesk(
                          fontSize:
                              (isFirst
                                      ? 24
                                      : 18) *
                                  scale,
                          fontWeight:
                              FontWeight
                                  .w800,
                          color: isFirst
                              ? cs.surface
                              : cs.outline,
                        ),
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        );
      }),
    );
  }
}

// ========================================================
// WEEKLY DISTANCE RANKING ROW
// ========================================================

class _StepsLeaderboardRow
    extends StatelessWidget {
  final _LeaderboardUser user;

  const _StepsLeaderboardRow({
    required this.user,
  });

  @override
  Widget build(BuildContext context) {
    final currentUid =
        FirebaseAuth
            .instance
            .currentUser
            ?.uid;

    final isMe =
        currentUid == user.userId;

    final cs =
        Theme.of(context).colorScheme;

    final mc =
        Theme.of(context)
            .extension<MasarColors>()!;

    final screenWidth =
        MediaQuery.sizeOf(context).width;

    final scale =
        (screenWidth / 390.0)
            .clamp(0.82, 1.05);

    return Container(
      margin:
          const EdgeInsets.only(
        bottom: 8,
      ),
      padding:
          EdgeInsets.all(
        14 * scale,
      ),
      decoration:
          BoxDecoration(
        color: isMe
            ? mc.surfaceHigh
            : mc.surfaceContainer,
        borderRadius:
            BorderRadius.circular(
          14,
        ),
        border: isMe
            ? Border.all(
                color: cs
                    .primaryContainer
                    .withValues(
                  alpha: 0.5,
                ),
                width: 1.5,
              )
            : null,
      ),
      child: Row(
        children: [
          if (isMe)
            Container(
              width: 3,
              height: 38 * scale,
              decoration:
                  BoxDecoration(
                gradient:
                    mc.primaryGradient,
                borderRadius:
                    BorderRadius.circular(
                  2,
                ),
              ),
            ),

          if (isMe)
            SizedBox(
              width: 9 * scale,
            ),

          SizedBox(
            width: 24 * scale,
            child: FittedBox(
              fit: BoxFit.scaleDown,
              child: Text(
                '${user.rank}',
                style: GoogleFonts
                    .spaceGrotesk(
                  fontSize:
                      13 * scale,
                  fontWeight:
                      FontWeight.w800,
                  color: isMe
                      ? cs
                          .primaryContainer
                      : cs.secondary,
                ),
              ),
            ),
          ),

          SizedBox(
            width: 8 * scale,
          ),

          Container(
            width: 36 * scale,
            height: 36 * scale,
            decoration:
                BoxDecoration(
              shape:
                  BoxShape.circle,
              color:
                  mc.surfaceHighest,
              border:
                  Border.all(
                color: isMe
                    ? cs
                        .primaryContainer
                        .withValues(
                        alpha:
                            0.5,
                      )
                    : cs
                        .outlineVariant
                        .withValues(
                        alpha:
                            0.3,
                      ),
              ),
            ),
            child: Icon(
              Icons.person_rounded,
              color: isMe
                  ? cs
                      .primaryContainer
                  : cs.outline,
              size: 18 * scale,
            ),
          ),

          SizedBox(
            width: 10 * scale,
          ),

          Expanded(
            child: Column(
              crossAxisAlignment:
                  CrossAxisAlignment.start,
              children: [
                Text(
                  isMe
                      ? '${user.name} (YOU)'
                      : user.name,
                  maxLines: 1,
                  overflow:
                      TextOverflow.ellipsis,
                  style:
                      GoogleFonts.manrope(
                    fontSize:
                        13 * scale,
                    fontWeight: isMe
                        ? FontWeight
                            .w800
                        : FontWeight
                            .w700,
                    color:
                        cs.onSurface,
                  ),
                ),
                Text(
                  isMe
                      ? 'Current Position'
                      : 'MasAR+ Walker',
                  maxLines: 1,
                  overflow:
                      TextOverflow.ellipsis,
                  style:
                      GoogleFonts.manrope(
                    fontSize:
                        10 * scale,
                    color: isMe
                        ? cs
                            .primaryContainer
                        : cs.secondary,
                  ),
                ),
              ],
            ),
          ),

          SizedBox(
            width: 8 * scale,
          ),

          SizedBox(
            width: 95 * scale,
            child: FittedBox(
              fit: BoxFit.scaleDown,
              alignment:
                  Alignment.centerRight,
              child: Text(
                '${user.totalSteps} STEPS',
                maxLines: 1,
                style: GoogleFonts
                    .spaceGrotesk(
                  fontSize:
                      14 * scale,
                  fontWeight:
                      FontWeight.w700,
                  color: isMe
                      ? cs
                          .primaryContainer
                      : cs.primary,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

// ========================================================
// STREAK RANKING ROW
// ========================================================

class _StreakLeaderboardRow
    extends StatelessWidget {
  final _LeaderboardUser user;

  const _StreakLeaderboardRow({
    required this.user,
  });

  @override
  Widget build(BuildContext context) {
    final currentUid =
        FirebaseAuth
            .instance
            .currentUser
            ?.uid;

    final isMe =
        currentUid == user.userId;

    final cs =
        Theme.of(context).colorScheme;

    final mc =
        Theme.of(context)
            .extension<MasarColors>()!;

    final screenWidth =
        MediaQuery.sizeOf(context).width;

    final scale =
        (screenWidth / 390.0)
            .clamp(0.82, 1.05);

    return Container(
      margin:
          const EdgeInsets.only(
        bottom: 8,
      ),
      padding:
          EdgeInsets.all(
        14 * scale,
      ),
      decoration:
          BoxDecoration(
        color: isMe
            ? mc.surfaceHigh
            : mc.surfaceContainer,
        borderRadius:
            BorderRadius.circular(
          14,
        ),
        border: isMe
            ? Border.all(
                color: cs
                    .primaryContainer
                    .withValues(
                  alpha: 0.5,
                ),
                width: 1.5,
              )
            : null,
      ),
      child: Row(
        children: [
          if (isMe)
            Container(
              width: 3,
              height: 38 * scale,
              decoration:
                  BoxDecoration(
                gradient:
                    mc.primaryGradient,
                borderRadius:
                    BorderRadius.circular(
                  2,
                ),
              ),
            ),

          if (isMe)
            SizedBox(
              width: 9 * scale,
            ),

          SizedBox(
            width: 24 * scale,
            child: FittedBox(
              fit: BoxFit.scaleDown,
              child: Text(
                '${user.rank}',
                style: GoogleFonts
                    .spaceGrotesk(
                  fontSize:
                      13 * scale,
                  fontWeight:
                      FontWeight.w800,
                  color: isMe
                      ? cs
                          .primaryContainer
                      : cs.secondary,
                ),
              ),
            ),
          ),

          SizedBox(
            width: 8 * scale,
          ),

          Container(
            width: 36 * scale,
            height: 36 * scale,
            decoration:
                BoxDecoration(
              shape:
                  BoxShape.circle,
              color:
                  mc.surfaceHighest,
              border:
                  Border.all(
                color: isMe
                    ? cs
                        .primaryContainer
                        .withValues(
                        alpha:
                            0.5,
                      )
                    : cs
                        .outlineVariant
                        .withValues(
                        alpha:
                            0.3,
                      ),
              ),
            ),
            child: Icon(
              Icons.person_rounded,
              color: isMe
                  ? cs
                      .primaryContainer
                  : cs.outline,
              size: 18 * scale,
            ),
          ),

          SizedBox(
            width: 10 * scale,
          ),

          Expanded(
            child: Column(
              crossAxisAlignment:
                  CrossAxisAlignment.start,
              children: [
                Text(
                  isMe
                      ? '${user.name} (YOU)'
                      : user.name,
                  maxLines: 1,
                  overflow:
                      TextOverflow.ellipsis,
                  style:
                      GoogleFonts.manrope(
                    fontSize:
                        13 * scale,
                    fontWeight: isMe
                        ? FontWeight
                            .w800
                        : FontWeight
                            .w700,
                    color:
                        cs.onSurface,
                  ),
                ),

                Text(
                  '${user.streak} '
                  '${user.streak == 1 ? 'Day' : 'Days'} Streak',
                  maxLines: 1,
                  overflow:
                      TextOverflow.ellipsis,
                  style:
                      GoogleFonts.manrope(
                    fontSize:
                        10 * scale,
                    color: isMe
                        ? cs
                            .primaryContainer
                        : cs.secondary,
                  ),
                ),
              ],
            ),
          ),

          SizedBox(
            width: 8 * scale,
          ),

          SizedBox(
            width: 95 * scale,
            child: FittedBox(
              fit: BoxFit.scaleDown,
              alignment:
                  Alignment.centerRight,
              child: Text(
                '${user.totalSteps} STEPS',
                maxLines: 1,
                style: GoogleFonts
                    .spaceGrotesk(
                  fontSize:
                      14 * scale,
                  fontWeight:
                      FontWeight.w700,
                  color: isMe
                      ? cs
                          .primaryContainer
                      : cs.primary,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

// ========================================================
// RESPONSIVE TAB BUTTON
// ========================================================

class _TabButton
    extends StatelessWidget {
  final String label;
  final bool active;
  final VoidCallback onTap;

  const _TabButton({
    required this.label,
    required this.active,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final cs =
        Theme.of(context).colorScheme;

    final mc =
        Theme.of(context)
            .extension<MasarColors>()!;

    return Expanded(
      child: GestureDetector(
        onTap: onTap,
        child: AnimatedContainer(
          duration:
              const Duration(
            milliseconds: 200,
          ),
          padding:
              const EdgeInsets.symmetric(
            vertical: 10,
            horizontal: 4,
          ),
          decoration:
              BoxDecoration(
            color: active
                ? mc.surfaceHigh
                : Colors.transparent,
            borderRadius:
                BorderRadius.circular(
              10,
            ),
          ),
          child: FittedBox(
            fit: BoxFit.scaleDown,
            child: Text(
              label,
              textAlign:
                  TextAlign.center,
              style: GoogleFonts
                  .spaceGrotesk(
                fontSize: 11,
                fontWeight:
                    FontWeight.w700,
                color: active
                    ? cs.primary
                    : cs.secondary,
                letterSpacing: 0.4,
              ),
            ),
          ),
        ),
      ),
    );
  }
}

// ========================================================
// HELPERS
// ========================================================

int _toInt(dynamic value) {
  if (value is int) {
    return value;
  }

  if (value is num) {
    return value.toInt();
  }

  return int.tryParse(
        value?.toString() ?? '',
      ) ??
      0;
}