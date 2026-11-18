import 'dart:async';

import 'package:flutter/material.dart';
import 'package:firebase_auth/firebase_auth.dart';
import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:provider/provider.dart';

import '../theme/app_theme.dart';

import '../services/footwear_ble_service.dart';
import '../services/step_counter_service.dart';
import '../services/unity_hunt_bridge.dart';
import '../services/hunt_achievements.dart';

import 'home_screen.dart';
import 'history_screen.dart';
import 'rewards_screen.dart';
import 'leaderboard_screen.dart';
import 'profile_screen.dart';

class MainShell extends StatefulWidget {
  const MainShell({super.key});

  @override
  State<MainShell> createState() =>
      _MainShellState();
}

class _MainShellState extends State<MainShell>
    with WidgetsBindingObserver {
  int _selectedIndex = 0;

  late final FootwearBleService _footwearBle;
  late final StepCounterService _stepCounter;
  late final UnityHuntBridge _unityHuntBridge;
  StreamSubscription? _achievementDefinitions;
  StreamSubscription? _achievementProfile;
  Timer? _achievementTimer;
  bool _checkingAchievements = false;
  bool _achievementCheckPending = false;

  @override
  void initState() {
    super.initState();

    WidgetsBinding.instance.addObserver(this);

    _footwearBle =
        FootwearBleService();

    _stepCounter =
        StepCounterService();

    _unityHuntBridge =
        UnityHuntBridge();

    final uid = FirebaseAuth.instance.currentUser?.uid;
    if (uid != null) {
      final db = FirebaseFirestore.instance;
      _achievementProfile = db.collection('users').doc(uid).snapshots().listen(
        (_) => _scheduleAchievementCheck(),
        onError: (Object error) => debugPrint('Achievement profile listener: $error'),
      );
      _achievementDefinitions = db.collection('achievements')
          .where('active', isEqualTo: true).snapshots().listen(
        (_) => _scheduleAchievementCheck(),
        onError: (Object error) => debugPrint('Achievement listener: $error'),
      );
    }

    // Normal Flutter footwear steps.
    _footwearBle.onStep = () {
      _stepCounter.incrementStep();
    };

    _stepCounter.initialize();

    // If Flutter was previously closed/backgrounded
    // and there is a completed hunt waiting,
    // consume it when MainShell starts.
    WidgetsBinding.instance
        .addPostFrameCallback((_) {
      _consumePendingHunt();
    });

    _reconnectFootwear();
  }

  @override
  void didChangeAppLifecycleState(
    AppLifecycleState state,
  ) {
    if (state ==
        AppLifecycleState.resumed) {
      _handleAppResumed();
    }
  }

  Future<void> _handleAppResumed() async {
    // Process any result that Android stored
    // while Unity was running.
    await _reconnectFootwear();
    await _consumePendingHunt();
    _scheduleAchievementCheck();
  }

  void _scheduleAchievementCheck({Duration delay = const Duration(milliseconds: 500)}) {
    if (!mounted) return;
    _achievementTimer?.cancel();
    _achievementTimer = Timer(delay, _checkAchievements);
  }

  Future<void> _checkAchievements() async {
    if (_checkingAchievements) {
      _achievementCheckPending = true;
      return;
    }
    final uid = FirebaseAuth.instance.currentUser?.uid;
    if (uid == null) return;
    _checkingAchievements = true;
    try {
      await HuntAchievements().checkHistory(uid);
    } catch (error) {
      debugPrint('Could not check achievements, will retry: $error');
      if (mounted) _scheduleAchievementCheck(delay: const Duration(seconds: 30));
    } finally {
      _checkingAchievements = false;
      if (_achievementCheckPending) {
        _achievementCheckPending = false;
        _scheduleAchievementCheck();
      }
    }
  }

  Future<void> _reconnectFootwear() async {
    if (!await UnityHuntBridge.isGameRunning() && mounted) {
      await _footwearBle.reconnectSavedShoe();
    }
  }

  Future<void> _consumePendingHunt() async {
    try {
      await _unityHuntBridge
          .consumePendingResult();
    } catch (e) {
      debugPrint(
        'Could not consume pending Unity hunt: $e',
      );
    }
  }

  @override
  void dispose() {
    _achievementTimer?.cancel();
    _achievementProfile?.cancel();
    _achievementDefinitions?.cancel();
    WidgetsBinding.instance
        .removeObserver(this);

    _footwearBle.dispose();
    _stepCounter.dispose();

    super.dispose();
  }

  List<Widget> get _screens => [
        HomeScreen(
          active:
              _selectedIndex == 0,
        ),
        const HistoryScreen(),
        const RewardsScreen(),
        const LeaderboardScreen(),
        const ProfileScreen(),
      ];

  final _navItems = const [
    (
      Icons.grid_view_rounded,
      Icons.grid_view_rounded,
      'Home'
    ),
    (
      Icons.history_rounded,
      Icons.history_rounded,
      'History'
    ),
    (
      Icons.workspace_premium_rounded,
      Icons.workspace_premium_rounded,
      'Rewards'
    ),
    (
      Icons.leaderboard_rounded,
      Icons.leaderboard_rounded,
      'Freej'
    ),
    (
      Icons.person_rounded,
      Icons.person_rounded,
      'Profile'
    ),
  ];

  @override
  Widget build(
    BuildContext context,
  ) {
    final cs =
        Theme.of(context)
            .colorScheme;


    return MultiProvider(
      providers: [
        ChangeNotifierProvider<
            FootwearBleService>.value(
          value: _footwearBle,
        ),
        ChangeNotifierProvider<
            StepCounterService>.value(
          value: _stepCounter,
        ),
      ],
      child: Scaffold(
        body: IndexedStack(
          index: _selectedIndex,
          children: _screens,
        ),
        bottomNavigationBar:
            Container(
          decoration: BoxDecoration(
              color: const Color(0xFFFFFCF7),

            borderRadius:
                const BorderRadius
                    .vertical(
              top:
                  Radius.circular(
                20,
              ),
            ),
            border: Border(
              top: BorderSide(
                color: cs
                    .outlineVariant
                    .withValues(
                  alpha: 0.2,
                ),
              ),
            ),
          ),
          child: SafeArea(
            top: false,
            child: SizedBox(
              height: 62,
              child: Row(
                children:
                    List.generate(
                  _navItems.length,
                  (i) {
                    final item =
                        _navItems[i];

                    final selected =
                        i ==
                            _selectedIndex;

                    return Expanded(
                      child:
                          GestureDetector(
                        onTap: () {
                          setState(() {
                            _selectedIndex =
                                i;
                          });
                        },
                        behavior:
                            HitTestBehavior
                                .opaque,
                        child:
                            AnimatedContainer(
                          duration:
                              const Duration(
                            milliseconds:
                                200,
                          ),
                          margin:
                              const EdgeInsets
                                  .symmetric(
                            horizontal: 4,
                            vertical: 6,
                          ),
                          padding:
                              const EdgeInsets
                                  .symmetric(
                            horizontal: 6,
                            vertical: 6,
                          ),
                          decoration:
                              selected
                                  ? BoxDecoration(
                                      color: const Color(0xFF722F42),
                                      borderRadius:
                                          BorderRadius.circular(
                                        14,
                                      ),
                                    )
                                  : null,
                          child: Column(
                            mainAxisAlignment:
                                MainAxisAlignment
                                    .center,
                            children: [
                              Icon(
                                selected
                                    ? item.$2
                                    : item.$1,
                                size: 20,
                                color: selected
                                    ? const Color(0xFFFFFCF7)
                                    : const Color(0xFF786F6B),
                              ),
                              const SizedBox(
                                height: 2,
                              ),
                              Text(
                                item.$3
                                    .toUpperCase(),
                                style:
                                    GoogleFonts
                                        .manrope(
                                  fontSize:
                                      10,
                                  fontWeight:
                                      FontWeight
                                          .w700,
                                  letterSpacing:
                                      0.2,
                                  color: selected
                                      ? const Color(0xFFFFFCF7)
                                      : const Color(0xFF786F6B),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    );
                  },
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

// ========================================================
// SHARED APP BAR
// ========================================================

class MasarAppBar extends StatelessWidget
    implements PreferredSizeWidget {
  final List<Widget>? actions;

  const MasarAppBar({
    super.key,
    this.actions,
  });

  @override
  Size get preferredSize =>
      const Size.fromHeight(
        kToolbarHeight,
      );

  @override
  Widget build(
    BuildContext context,
  ) {
    final cs =
        Theme.of(context)
            .colorScheme;

    return AppBar(
      title: Text(
        'MASAR+',
        style:
            GoogleFonts.spaceGrotesk(
          fontSize: 20,
          fontWeight:
              FontWeight.w900,
          color:
             const Color(0xFF722F42),
          letterSpacing: 3,
        ),
      ),
      leading: IconButton(
        icon: Icon(
          Icons.menu_rounded,
          color: cs.primary,
        ),
        onPressed: () {},
      ),
      actions: actions ??
          [
            Container(
              width: 34,
              height: 34,
              margin:
                  const EdgeInsets.only(
                right: 16,
              ),
              decoration:
                  BoxDecoration(
                shape:
                    BoxShape.circle,
                color:
                    Theme.of(context)
                        .extension<
                            MasarColors>()!
                        .surfaceHigh,
                border:
                    Border.all(
                  color: cs
                      .primaryContainer
                      .withValues(
                    alpha: 0.5,
                  ),
                ),
              ),
              child: Icon(
                Icons.account_circle,
                color: cs.primary,
                size: 20,
              ),
            ),
          ],
    );
  }
}
