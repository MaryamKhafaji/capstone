import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:firebase_auth/firebase_auth.dart';
import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import '../theme/app_theme.dart';
import '../widgets/shared_widgets.dart';
import 'main_shell.dart';

class HistoryScreen extends StatelessWidget {
  const HistoryScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final user = FirebaseAuth.instance.currentUser;

    final cs = Theme.of(context).colorScheme;
    final mc = Theme.of(context).extension<MasarColors>()!;

    if (user == null) {
      return Scaffold(
        appBar: const MasarAppBar(),
        body: const Center(
          child: Text('No user signed in.'),
        ),
      );
    }

    return Scaffold(
      appBar: const MasarAppBar(),
      body: StreamBuilder<
          DocumentSnapshot<Map<String, dynamic>>>(
        stream: FirebaseFirestore.instance
            .collection('users')
            .doc(user.uid)
            .snapshots(),
        builder: (context, userSnapshot) {
          final userData =
              userSnapshot.data?.data() ??
                  <String, dynamic>{};

          final totalSteps =
              _toInt(userData['totalSteps']);

          final totalCrystals =
              _toInt(userData['crystals']);

          return StreamBuilder<
              QuerySnapshot<Map<String, dynamic>>>(
            stream: FirebaseFirestore.instance
                .collection('walking_sessions')
                .where(
                  'userId',
                  isEqualTo: user.uid,
                )
                .snapshots(),
            builder: (context, sessionSnapshot) {
              if (sessionSnapshot.hasError) {
                return Center(
                  child: Padding(
                    padding:
                        const EdgeInsets.all(20),
                    child: Text(
                      'Unable to load activity history.\n'
                      '${sessionSnapshot.error}',
                      textAlign:
                          TextAlign.center,
                    ),
                  ),
                );
              }

              if (sessionSnapshot.connectionState ==
                      ConnectionState.waiting &&
                  !sessionSnapshot.hasData) {
                return const Center(
                  child:
                      CircularProgressIndicator(),
                );
              }

              final docs =
                  sessionSnapshot.data?.docs ??
                      [];

              // Ignore the schema placeholder.
              final sessionDocs = docs.where(
                (doc) =>
                    doc.id !=
                    '_schema_placeholder',
              ).toList();

              // Newest session first.
              sessionDocs.sort((a, b) {
                final aTime =
                    _timestampFrom(
                  a.data()['endTime'],
                );

                final bTime =
                    _timestampFrom(
                  b.data()['endTime'],
                );

                if (aTime == null &&
                    bTime == null) {
                  return 0;
                }

                if (aTime == null) {
                  return 1;
                }

                if (bTime == null) {
                  return -1;
                }

                return bTime.compareTo(aTime);
              });

              final now = DateTime.now();

              final sessionsThisMonth =
                  sessionDocs.where((doc) {
                final time = _timestampFrom(
                  doc.data()['endTime'],
                );

                if (time == null) {
                  return false;
                }

                return time.year == now.year &&
                    time.month == now.month;
              }).length;

              return SingleChildScrollView(
                padding:
                    const EdgeInsets.fromLTRB(
                  16,
                  16,
                  16,
                  24,
                ),
                child: Column(
                  crossAxisAlignment:
                      CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Activity History',
                      style:
                          GoogleFonts.spaceGrotesk(
                        fontSize: 26,
                        fontWeight:
                            FontWeight.w800,
                        color:
                            cs.onSurface,
                      ),
                    ),

                    const SizedBox(height: 2),

                    Text(
                      '$sessionsThisMonth '
                      '${sessionsThisMonth == 1 ? 'session' : 'sessions'} '
                      'this month',
                      style:
                          GoogleFonts.manrope(
                        fontSize: 12,
                        color:
                            cs.secondary,
                      ),
                    ),

                    const SizedBox(height: 16),

                    // ── Stats Row ──
                    Row(
                      children: [
                        Expanded(
                          child: StatCard(
                            icon: Icons
                                .directions_walk_rounded,
                            iconColor: cs
                                .primaryContainer,
                            value:
                                _formatNumber(
                              totalSteps,
                            ),
                            label:
                                'Total Steps',
                          ),
                        ),

                        const SizedBox(
                          width: 10,
                        ),

                        Expanded(
                          child: StatCard(
                            icon:
                                Icons.stars_rounded,
                            iconColor: mc
                                .tertiaryContainer,
                            value:
                                _formatNumber(
                              totalCrystals,
                            ),
                            label:
                                'Total Crystals',
                          ),
                        ),
                      ],
                    ),

                    const SizedBox(height: 20),

                    SectionLabel(
                      'All Sessions',
                    ),

                    const SizedBox(height: 10),

                    if (sessionDocs.isEmpty)
                      Padding(
                        padding:
                            const EdgeInsets
                                .symmetric(
                          vertical: 30,
                        ),
                        child: Center(
                          child: Text(
                            'No sessions yet.',
                            style:
                                GoogleFonts.manrope(
                              fontSize: 13,
                              color:
                                  cs.secondary,
                            ),
                          ),
                        ),
                      )
                    else
                      ...sessionDocs.map(
                        (doc) {
                          return _SessionCard(
                            data: doc.data(),
                          );
                        },
                      ),
                  ],
                ),
              );
            },
          );
        },
      ),
    );
  }

  static int _toInt(dynamic value) {
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

  static DateTime? _timestampFrom(
    dynamic value,
  ) {
    if (value is Timestamp) {
      return value.toDate();
    }

    return null;
  }

  static String _formatNumber(int value) {
    final text = value.toString();

    final buffer = StringBuffer();

    for (int i = 0;
        i < text.length;
        i++) {
      final positionFromEnd =
          text.length - i;

      buffer.write(text[i]);

      if (positionFromEnd > 1 &&
          positionFromEnd % 3 == 1) {
        buffer.write(',');
      }
    }

    return buffer.toString();
  }
}

// ========================================================
// REAL SESSION CARD
// ========================================================

class _SessionCard extends StatelessWidget {
  final Map<String, dynamic> data;

  const _SessionCard({
    required this.data,
  });

  @override
  Widget build(BuildContext context) {
    final cs =
        Theme.of(context).colorScheme;

    final mc =
        Theme.of(context)
            .extension<MasarColors>()!;

    final startTime =
        _timestampFrom(
      data['startTime'],
    );

    final endTime =
        _timestampFrom(
      data['endTime'],
    );

    final sessionTime =
        startTime ?? endTime;

    final sessionName =
        _sessionNameFromTime(
      sessionTime,
    );

    final date =
        _formatDate(
      sessionTime,
    );

    final steps =
        _toInt(
      data['numSteps'],
    );

    final energyEarned =
        _toInt(
      data['energyEarned'],
    );

    // New sessions use "legend".
    // Existing older test sessions may still have
    // sessionMode, so we safely fall back.
    final legend =
        (data['legend'] ??
                data['sessionMode'] ??
                'Unknown Legend')
            .toString();

    final duration =
        _formatDuration(
      startTime,
      endTime,
    );

    return Container(
      margin:
          const EdgeInsets.only(
        bottom: 10,
      ),
      padding:
          const EdgeInsets.all(
        16,
      ),
      decoration:
          BoxDecoration(
        color:
            mc.surfaceHigh,
        borderRadius:
            BorderRadius.circular(
          16,
        ),
      ),
      child: Column(
        crossAxisAlignment:
            CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment:
                CrossAxisAlignment.start,
            mainAxisAlignment:
                MainAxisAlignment
                    .spaceBetween,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment:
                      CrossAxisAlignment
                          .start,
                  children: [
                    Text(
                      sessionName,
                      style:
                          GoogleFonts.manrope(
                        fontSize: 10,
                        fontWeight:
                            FontWeight.w700,
                        color:
                            cs.secondary,
                        letterSpacing:
                            0.5,
                      ),
                    ),

                    Text(
                      date,
                      style: GoogleFonts
                          .spaceGrotesk(
                        fontSize: 16,
                        fontWeight:
                            FontWeight.w700,
                        color:
                            cs.onSurface,
                      ),
                    ),

                    const SizedBox(
                      height: 2,
                    ),

                    Text(
                      legend,
                      maxLines: 1,
                      overflow:
                          TextOverflow
                              .ellipsis,
                      style:
                          GoogleFonts.manrope(
                        fontSize: 10,
                        fontWeight:
                            FontWeight.w600,
                        color:
                            cs.outline,
                      ),
                    ),
                  ],
                ),
              ),

              const SizedBox(
                width: 10,
              ),

              Container(
                padding:
                    const EdgeInsets
                        .symmetric(
                  horizontal: 10,
                  vertical: 4,
                ),
                decoration:
                    BoxDecoration(
                  color: cs
                      .primaryContainer
                      .withValues(
                    alpha: 0.15,
                  ),
                  borderRadius:
                      BorderRadius.circular(
                    50,
                  ),
                ),
                child: Row(
                  mainAxisSize:
                      MainAxisSize.min,
                  children: [
                    Icon(
                      Icons.bolt_rounded,
                      color: cs
                          .primaryContainer,
                      size: 12,
                    ),

                    const SizedBox(
                      width: 2,
                    ),

                    Text(
                      '+$energyEarned ENERGY',
                      style:
                          GoogleFonts.manrope(
                        fontSize: 11,
                        fontWeight:
                            FontWeight.w700,
                        color: cs
                            .primaryContainer,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),

          const SizedBox(
            height: 12,
          ),

          Row(
            children: [
              Expanded(
                child: _MetricItem(
                  value:
                      _formatNumber(
                    steps,
                  ),
                  label:
                      'Steps',
                  color:
                      cs.primary,
                ),
              ),

              const SizedBox(
                width: 20,
              ),

              Expanded(
                child: _MetricItem(
                  value:
                      duration,
                  label:
                      'Duration',
                  color:
                      cs.onSurface,
                ),
              ),

              // Avg Heart removed.
            ],
          ),
        ],
      ),
    );
  }

  static DateTime? _timestampFrom(
    dynamic value,
  ) {
    if (value is Timestamp) {
      return value.toDate();
    }

    return null;
  }

  static int _toInt(dynamic value) {
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

  static String _sessionNameFromTime(
    DateTime? time,
  ) {
    if (time == null) {
      return 'Walking Session';
    }

    final hour = time.hour;

    if (hour >= 5 &&
        hour < 11) {
      return 'Morning Sprint';
    }

    if (hour >= 11 &&
        hour < 16) {
      return 'Midday Trek';
    }

    if (hour >= 16 &&
        hour < 20) {
      return 'Sunset Sprint';
    }

    return 'Night Walk';
  }

  static String _formatDate(
    DateTime? time,
  ) {
    if (time == null) {
      return 'Unknown Date';
    }

    const months = [
      'Jan',
      'Feb',
      'Mar',
      'Apr',
      'May',
      'Jun',
      'Jul',
      'Aug',
      'Sep',
      'Oct',
      'Nov',
      'Dec',
    ];

    return '${months[time.month - 1]} '
        '${time.day}, '
        '${time.year}';
  }

  static String _formatDuration(
    DateTime? start,
    DateTime? end,
  ) {
    if (start == null ||
        end == null) {
      return '--:--';
    }

    final difference =
        end.difference(start);

    if (difference.isNegative) {
      return '--:--';
    }

    final hours =
        difference.inHours;

    final minutes =
        difference.inMinutes
            .remainder(60);

    final seconds =
        difference.inSeconds
            .remainder(60);

    if (hours > 0) {
      return '$hours:'
          '${minutes.toString().padLeft(2, '0')}:'
          '${seconds.toString().padLeft(2, '0')}';
    }

    return '$minutes:'
        '${seconds.toString().padLeft(2, '0')}';
  }

  static String _formatNumber(int value) {
    final text =
        value.toString();

    final buffer =
        StringBuffer();

    for (int i = 0;
        i < text.length;
        i++) {
      final positionFromEnd =
          text.length - i;

      buffer.write(text[i]);

      if (positionFromEnd > 1 &&
          positionFromEnd % 3 == 1) {
        buffer.write(',');
      }
    }

    return buffer.toString();
  }
}

// ========================================================
// METRIC
// ========================================================

class _MetricItem
    extends StatelessWidget {
  final String value;
  final String label;
  final Color color;

  const _MetricItem({
    required this.value,
    required this.label,
    required this.color,
  });

  @override
  Widget build(BuildContext context) {
    final cs =
        Theme.of(context).colorScheme;

    return Column(
      crossAxisAlignment:
          CrossAxisAlignment.start,
      children: [
        FittedBox(
          fit:
              BoxFit.scaleDown,
          alignment:
              Alignment.centerLeft,
          child: Text(
            value,
            style: GoogleFonts
                .spaceGrotesk(
              fontSize: 17,
              fontWeight:
                  FontWeight.w700,
              color: color,
            ),
          ),
        ),

        Text(
          label.toUpperCase(),
          style:
              GoogleFonts.manrope(
            fontSize: 9,
            fontWeight:
                FontWeight.w700,
            color: cs.outline,
            letterSpacing: 0.5,
          ),
        ),
      ],
    );
  }
}