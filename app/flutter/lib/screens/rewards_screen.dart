
import 'dart:math';

import 'package:flutter/material.dart';
import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:firebase_auth/firebase_auth.dart';
import 'package:google_fonts/google_fonts.dart';

import 'main_shell.dart';
import '../theme/app_theme.dart';

// ============================================
// HELPERS
// ============================================

int _crystalCost(dynamic value) {
  if (value is num) return value.toInt();
  return int.tryParse(value?.toString() ?? '') ?? 0;
}

String _field(dynamic value, [String fallback = '']) {
  final text = value?.toString().trim() ?? '';
  return text.isEmpty ? fallback : text;
}

DateTime? _expiration(dynamic value) {
  if (value is Timestamp) return value.toDate();
  if (value is String && value.isNotEmpty) {
    return DateTime.tryParse(value);
  }
  return null;
}

bool _isExpired(dynamic value) {
  final date = _expiration(value);
  if (date == null) return false;

  final lastDay = DateTime(
    date.year,
    date.month,
    date.day,
    23,
    59,
    59,
  );

  return DateTime.now().isAfter(lastDay);
}

class RewardRedemptionException implements Exception {
  final String message;

  RewardRedemptionException(this.message);

  @override
  String toString() => message;
}

// ============================================
// REWARDS SHOP
// ============================================

class RewardsScreen extends StatefulWidget {
  const RewardsScreen({super.key});

  @override
  State<RewardsScreen> createState() => _RewardsScreenState();
}

class _RewardsScreenState extends State<RewardsScreen> {
  bool showProducts = true;

  Widget _companiesView() {
    return StreamBuilder<QuerySnapshot<Map<String, dynamic>>>(
      stream: FirebaseFirestore.instance
          .collection('partners')
          .snapshots(),
      builder: (context, snapshot) {
        if (snapshot.hasError) {
          return Center(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Text(
                'Firebase error:\n${snapshot.error}',
                textAlign: TextAlign.center,
              ),
            ),
          );
        }

        if (!snapshot.hasData) {
          return const Center(
            child: CircularProgressIndicator(),
          );
        }

        final companies = snapshot.data!.docs
            .where((doc) => doc.id != '_schema_placeholder')
            .toList();

        if (companies.isEmpty) {
          return const Center(
            child: Text('No companies available yet.'),
          );
        }

        return ListView.separated(
          padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
          itemCount: companies.length,
          separatorBuilder: (_, __) =>
              const SizedBox(height: 12),
          itemBuilder: (context, index) {
            final company = companies[index];
            final data = company.data();

            final name = _field(
              data['businessName'],
              'Company',
            );
            final industry = _field(data['industry']);
            final description = _field(data['description']);
            final logo = _field(data['logoUrl']);

            return Material(
              color: const Color(0xFFFFFCF7),
              borderRadius: BorderRadius.circular(18),
              clipBehavior: Clip.antiAlias,
              child: InkWell(
                onTap: () {
                  Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (_) => CompanyRewardsScreen(
                        companyId: company.id,
                        companyName: name,
                        industry: industry,
                        description: description,
                        logoUrl: logo,
                      ),
                    ),
                  );
                },
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Row(
                    children: [
                      Container(
                        width: 64,
                        height: 64,
                        decoration: BoxDecoration(
                          color: const Color(0xFFF3EEE7),
                          borderRadius: BorderRadius.circular(14),
                        ),
                        clipBehavior: Clip.antiAlias,
                        child: logo.isNotEmpty
                            ? Image.network(
                                logo,
                                fit: BoxFit.contain,
                                errorBuilder: (_, __, ___) =>
                                    const Icon(
                                  Icons.storefront,
                                  color: Color(0xFF722F42),
                                ),
                              )
                            : Center(
                                child: Text(
                                  name.isNotEmpty
                                      ? name[0].toUpperCase()
                                      : '?',
                                  style: const TextStyle(
                                    fontSize: 25,
                                    fontWeight: FontWeight.bold,
                                    color: Color(0xFF722F42),
                                  ),
                                ),
                              ),
                      ),
                      const SizedBox(width: 14),
                      Expanded(
                        child: Column(
                          crossAxisAlignment:
                              CrossAxisAlignment.start,
                          children: [
                            Text(
                              name,
                              style: const TextStyle(
                                fontSize: 16,
                                fontWeight: FontWeight.w700,
                                color: Color(0xFF292321),
                              ),
                            ),
                            Text(
                              industry,
                              style: const TextStyle(
                                fontSize: 12,
                                color: Color(0xFF786F6B),
                              ),
                            ),
                            if (description.isNotEmpty)
                              Text(
                                description,
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(
                                  fontSize: 12,
                                  color: Color(0xFF786F6B),
                                ),
                              ),
                          ],
                        ),
                      ),
                      const Icon(
                        Icons.chevron_right_rounded,
                        color: Color(0xFF722F42),
                      ),
                    ],
                  ),
                ),
              ),
            );
          },
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final cs = Theme.of(context).colorScheme;

    return Scaffold(
      appBar: const MasarAppBar(),
      body: StreamBuilder<QuerySnapshot<Map<String, dynamic>>>(
        stream: FirebaseFirestore.instance
            .collection('rewards')
            .snapshots(),
        builder: (context, snapshot) {
          if (snapshot.hasError) {
            return Center(
              child: Text(
                'Unable to load rewards.\n${snapshot.error}',
                textAlign: TextAlign.center,
                style: TextStyle(color: cs.error),
              ),
            );
          }

          if (!snapshot.hasData) {
            return const Center(
              child: CircularProgressIndicator(),
            );
          }

          final rewards = snapshot.data!.docs
              .where((doc) => doc.id != '_schema_placeholder')
              .toList();

          return Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(
                  16,
                  20,
                  16,
                  16,
                ),
                child: Column(
                  crossAxisAlignment:
                      CrossAxisAlignment.start,
                  children: [
                    Padding(
                      padding: const EdgeInsets.fromLTRB(
                        16,
                        16,
                        16,
                        0,
                      ),
                      child: Container(
                        padding: const EdgeInsets.all(4),
                        decoration: BoxDecoration(
                          color: const Color(0xFFEAE2D9),
                          borderRadius:
                              BorderRadius.circular(16),
                        ),
                        child: Row(
                          children: [
                            for (final isProducts in [true, false])
                              Expanded(
                                child: GestureDetector(
                                  onTap: () => setState(
                                    () =>
                                        showProducts = isProducts,
                                  ),
                                  child: Container(
                                    padding:
                                        const EdgeInsets.symmetric(
                                      vertical: 13,
                                    ),
                                    decoration: BoxDecoration(
                                      color: showProducts ==
                                              isProducts
                                          ? const Color(0xFFFFFCF7)
                                          : Colors.transparent,
                                      borderRadius:
                                          BorderRadius.circular(
                                        12,
                                      ),
                                    ),
                                    child: Text(
                                      isProducts
                                          ? 'Products'
                                          : 'Companies',
                                      textAlign: TextAlign.center,
                                      style: GoogleFonts.manrope(
                                        fontWeight:
                                            FontWeight.w700,
                                        color: showProducts ==
                                                isProducts
                                            ? const Color(
                                                0xFF722F42,
                                              )
                                            : const Color(
                                                0xFF786F6B,
                                              ),
                                      ),
                                    ),
                                  ),
                                ),
                              ),
                          ],
                        ),
                      ),
                    ),
                    Text(
                      'Rewards Shop',
                      style: GoogleFonts.spaceGrotesk(
                        fontSize: 26,
                        fontWeight: FontWeight.w800,
                        color: cs.onSurface,
                      ),
                    ),
                    const SizedBox(height: 5),
                    Text(
                      'Exchange your crystals for exciting rewards!',
                      style: GoogleFonts.manrope(
                        fontSize: 12,
                        color: cs.onSurfaceVariant,
                      ),
                    ),
                  ],
                ),
              ),
              Expanded(
                child: !showProducts
                    ? _companiesView()
                    : rewards.isEmpty
                        ? const Center(
                            child: Text(
                              'No rewards available yet.',
                            ),
                          )
                        : GridView.builder(
                            padding:
                                const EdgeInsets.fromLTRB(
                              16,
                              0,
                              16,
                              24,
                            ),
                            gridDelegate:
                                const SliverGridDelegateWithFixedCrossAxisCount(
                              crossAxisCount: 2,
                              crossAxisSpacing: 12,
                              mainAxisSpacing: 12,
                              childAspectRatio: 0.78,
                            ),
                            itemCount: rewards.length,
                            itemBuilder: (context, index) {
                              final doc = rewards[index];
                              final data = doc.data();

                              return _RewardCard(
                                title: _field(
                                  data['title'],
                                  'Reward',
                                ),
                                imageUrl:
                                    _field(data['imageUrl']),
                                crystalCost: _crystalCost(
                                  data['crystalCost'],
                                ),
                                onOpen: () {
                                  Navigator.of(context).push(
                                    MaterialPageRoute(
                                      builder: (_) =>
                                          RewardDetailsScreen(
                                        rewardId: doc.id,
                                      ),
                                    ),
                                  );
                                },
                              );
                            },
                          ),
              ),
            ],
          );
        },
      ),
    );
  }
}

// ============================================
// COMPANY REWARDS
// ============================================

class CompanyRewardsScreen extends StatelessWidget {
  final String companyId;
  final String companyName;
  final String industry;
  final String description;
  final String logoUrl;

  const CompanyRewardsScreen({
    super.key,
    required this.companyId,
    required this.companyName,
    required this.industry,
    required this.description,
    required this.logoUrl,
  });

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF8F5EF),
      appBar: AppBar(
        title: Text(companyName),
        backgroundColor: const Color(0xFFF8F5EF),
        foregroundColor: const Color(0xFF722F42),
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.all(20),
            child: Column(
              children: [
                Container(
                  width: 90,
                  height: 90,
                  decoration: BoxDecoration(
                    color: const Color(0xFFFFFCF7),
                    borderRadius: BorderRadius.circular(22),
                  ),
                  clipBehavior: Clip.antiAlias,
                  child: logoUrl.isNotEmpty
                      ? Image.network(
                          logoUrl,
                          fit: BoxFit.contain,
                          errorBuilder: (_, __, ___) =>
                              const Icon(
                            Icons.storefront_rounded,
                            size: 42,
                            color: Color(0xFF722F42),
                          ),
                        )
                      : Center(
                          child: Text(
                            companyName.isNotEmpty
                                ? companyName[0].toUpperCase()
                                : '?',
                            style: const TextStyle(
                              fontSize: 38,
                              fontWeight: FontWeight.bold,
                              color: Color(0xFF722F42),
                            ),
                          ),
                        ),
                ),
                const SizedBox(height: 14),
                Text(
                  companyName,
                  style: GoogleFonts.spaceGrotesk(
                    fontSize: 24,
                    fontWeight: FontWeight.w800,
                    color: const Color(0xFF292321),
                  ),
                ),
                const SizedBox(height: 5),
                Text(
                  industry,
                  style: GoogleFonts.manrope(
                    fontSize: 13,
                    color: const Color(0xFF722F42),
                  ),
                ),
                if (description.isNotEmpty) ...[
                  const SizedBox(height: 12),
                  Text(
                    description,
                    textAlign: TextAlign.center,
                    style: GoogleFonts.manrope(
                      fontSize: 13,
                      color: const Color(0xFF786F6B),
                    ),
                  ),
                ],
              ],
            ),
          ),
          const Divider(height: 1),
          Padding(
            padding: const EdgeInsets.fromLTRB(
              16,
              20,
              16,
              12,
            ),
            child: Align(
              alignment: Alignment.centerLeft,
              child: Text(
                'Available Rewards',
                style: GoogleFonts.spaceGrotesk(
                  fontSize: 20,
                  fontWeight: FontWeight.w700,
                  color: const Color(0xFF292321),
                ),
              ),
            ),
          ),
          Expanded(
            child: StreamBuilder<
                QuerySnapshot<Map<String, dynamic>>>(
              stream: FirebaseFirestore.instance
                  .collection('rewards')
                  .where(
                    'partnerId',
                    isEqualTo: companyId,
                  )
                  .snapshots(),
              builder: (context, snapshot) {
                if (snapshot.hasError) {
                  return const Center(
                    child: Text(
                      'Unable to load company rewards.',
                    ),
                  );
                }

                if (!snapshot.hasData) {
                  return const Center(
                    child: CircularProgressIndicator(),
                  );
                }

                final rewards = snapshot.data!.docs
                    .where(
                      (doc) =>
                          doc.id != '_schema_placeholder',
                    )
                    .toList();

                if (rewards.isEmpty) {
                  return const Center(
                    child: Text(
                      'No rewards available yet.',
                    ),
                  );
                }

                return GridView.builder(
                  padding: const EdgeInsets.fromLTRB(
                    16,
                    0,
                    16,
                    24,
                  ),
                  gridDelegate:
                      const SliverGridDelegateWithFixedCrossAxisCount(
                    crossAxisCount: 2,
                    crossAxisSpacing: 12,
                    mainAxisSpacing: 12,
                    childAspectRatio: 0.78,
                  ),
                  itemCount: rewards.length,
                  itemBuilder: (context, index) {
                    final doc = rewards[index];
                    final data = doc.data();

                    return _RewardCard(
                      title: _field(
                        data['title'],
                        'Reward',
                      ),
                      imageUrl: _field(
                        data['imageUrl'],
                      ),
                      crystalCost: _crystalCost(
                        data['crystalCost'],
                      ),
                      onOpen: () {
                        Navigator.of(context).push(
                          MaterialPageRoute(
                            builder: (_) =>
                                RewardDetailsScreen(
                              rewardId: doc.id,
                            ),
                          ),
                        );
                      },
                    );
                  },
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

// ============================================
// REWARD GRID CARD
// ============================================

class _RewardCard extends StatelessWidget {
  final String title;
  final String imageUrl;
  final int crystalCost;
  final VoidCallback onOpen;

  const _RewardCard({
    required this.title,
    required this.imageUrl,
    required this.crystalCost,
    required this.onOpen,
  });

  @override
  Widget build(BuildContext context) {
    final cs = Theme.of(context).colorScheme;
    final mc =
        Theme.of(context).extension<MasarColors>()!;

    return Material(
      color: mc.surfaceHigh,
      borderRadius: BorderRadius.circular(16),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onOpen,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Expanded(
              child: _RewardImage(
                imageUrl: imageUrl,
                fit: BoxFit.cover,
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(12),
              child: Column(
                crossAxisAlignment:
                    CrossAxisAlignment.start,
                children: [
                  Text(
                    title,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: GoogleFonts.spaceGrotesk(
                      fontSize: 14,
                      fontWeight: FontWeight.w700,
                      color: cs.onSurface,
                    ),
                  ),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      Icon(
                        Icons.diamond_rounded,
                        size: 18,
                        color: cs.primary,
                      ),
                      const SizedBox(width: 5),
                      Expanded(
                        child: Text(
                          '$crystalCost Crystals',
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: GoogleFonts.manrope(
                            fontSize: 12,
                            fontWeight: FontWeight.w700,
                            color: cs.primary,
                          ),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ============================================
// REWARD DETAILS SCREEN
// ============================================

class RewardDetailsScreen extends StatefulWidget {
  final String rewardId;

  const RewardDetailsScreen({
    super.key,
    required this.rewardId,
  });

  @override
  State<RewardDetailsScreen> createState() =>
      _RewardDetailsScreenState();
}

class _RewardDetailsScreenState
    extends State<RewardDetailsScreen> {
  bool _redeeming = false;

  Future<void> _redeemReward() async {
    if (_redeeming) return;

    final user = FirebaseAuth.instance.currentUser;

    if (user == null) {
      _message('Please log in to redeem rewards.');
      return;
    }

    setState(() => _redeeming = true);

    try {
      final db = FirebaseFirestore.instance;

      final userRef = db.collection('users').doc(user.uid);

      final rewardRef = db
          .collection('rewards')
          .doc(widget.rewardId);

      // Create one claim document ID for this redemption.
      // Keep the ID stable if Firestore retries the transaction.
      final redemptionRef =
          db.collection('user_rewards').doc();

      final claimId = redemptionRef.id;

      // A cryptographically secure, uppercase, 24-character
      // hexadecimal code, as required by the Firestore rules.
      final random = Random.secure();

      final claimCode = List.generate(
        12,
        (_) => random
            .nextInt(256)
            .toRadixString(16)
            .padLeft(2, '0'),
      ).join().toUpperCase();

      await db.runTransaction<void>((transaction) async {
        // Firestore requires transaction reads before writes.
        final rewardSnapshot =
            await transaction.get(rewardRef);

        final userSnapshot =
            await transaction.get(userRef);

        if (!rewardSnapshot.exists) {
          throw RewardRedemptionException(
            'This reward is no longer available.',
          );
        }

        if (!userSnapshot.exists) {
          throw RewardRedemptionException(
            'Your user profile could not be found.',
          );
        }

        final reward = rewardSnapshot.data()!;
        final userData = userSnapshot.data()!;

        // Your Firestore rules require integer values.
        final rawCost = reward['crystalCost'];
        final rawBalance = userData['crystals'];

        if (rawCost is! int || rawCost <= 0) {
          throw RewardRedemptionException(
            'This reward has an invalid crystal cost.',
          );
        }

        if (rawBalance is! int) {
          throw RewardRedemptionException(
            'Your crystal balance is not configured correctly.',
          );
        }

        final int cost = rawCost;
        final int currentCrystals = rawBalance;

        // The rules require the exact string "Active".
        if (reward['availStatus'] != 'Active') {
          throw RewardRedemptionException(
            'This reward is currently unavailable.',
          );
        }

        if (_isExpired(reward['expDate'])) {
          throw RewardRedemptionException(
            'This reward has expired.',
          );
        }

        // Preserve the existing "Reach level N" condition.
        final conditions = _field(reward['conditions']);

        final levelMatch = RegExp(
          r'reach\s+level\s+(\d+)',
          caseSensitive: false,
        ).firstMatch(conditions);

        if (levelMatch != null) {
          final requiredLevel =
              int.parse(levelMatch.group(1)!);

          final currentLevel =
              _crystalCost(userData['level']);

          if (currentLevel < requiredLevel) {
            throw RewardRedemptionException(
              'You must reach level $requiredLevel '
              'to redeem this reward.',
            );
          }
        }

        if (currentCrystals < cost) {
          final missing = cost - currentCrystals;

          throw RewardRedemptionException(
            'Not enough crystals! '
            'You need $missing more '
            '${missing == 1 ? 'crystal' : 'crystals'}.',
          );
        }

        // Your rules require an integer inventory.
        final quantityTotal = reward['quantityTotal'];
        final redeemedCount = reward['redeemedCount'] ?? 0;

        if (quantityTotal is! int ||
            quantityTotal <= 0 ||
            redeemedCount is! int ||
            redeemedCount < 0) {
          throw RewardRedemptionException(
            'Reward inventory is not configured correctly.',
          );
        }

        if (redeemedCount >= quantityTotal) {
          throw RewardRedemptionException(
            'This reward is out of stock.',
          );
        }

        // ========================================
        // ATOMIC WRITE 1: USER BALANCE
        // ========================================

        transaction.update(userRef, {
          'crystals': currentCrystals - cost,
          'lastClaimId': claimId,
        });

        // ========================================
        // ATOMIC WRITE 2: REWARD INVENTORY
        // ========================================

        transaction.update(rewardRef, {
          'redeemedCount': redeemedCount + 1,
          'lastClaimId': claimId,
        });

        // ========================================
        // ATOMIC WRITE 3: REDEMPTION CLAIM
        // Exactly six fields, as required by rules.
        // ========================================

        transaction.set(redemptionRef, {
          'userId': user.uid,
          'rewardId': widget.rewardId,
          'crystalCost': cost,
          'claimCode': claimCode,
          'status': 'issued',
          'createdAt': FieldValue.serverTimestamp(),
        });

      }); // IMPORTANT: closes runTransaction

      // Everything below runs only after the
      // entire Firestore transaction succeeds.

      if (!mounted) return;

      await showDialog<void>(
        context: context,
        builder: (dialogContext) => AlertDialog(
          title: const Text('Reward Redeemed!'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(
                Icons.card_giftcard_rounded,
                size: 48,
              ),
              const SizedBox(height: 16),
              const Text(
                'Show this code at the sponsor location.',
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 16),
              SelectableText(
                claimCode,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  fontSize: 19,
                  fontWeight: FontWeight.bold,
                  letterSpacing: 1.5,
                ),
              ),
              const SizedBox(height: 12),
              const Text(
                'Your reward code is saved to your account.',
                textAlign: TextAlign.center,
              ),
            ],
          ),
          actions: [
            FilledButton(
              onPressed: () {
                Navigator.of(dialogContext).pop();
              },
              child: const Text('Done'),
            ),
          ],
        ),
      );

      if (mounted) {
        Navigator.of(context).pop();
      }
    } on RewardRedemptionException catch (e) {
      _message(e.message);
    } on FirebaseException catch (e) {
      if (e.code == 'permission-denied') {
        _message(
          'Firestore rejected the redemption. '
          'Please check the reward inventory, '
          'balance, and security rules.',
        );
      } else {
        _message(
          'Redemption failed: ${e.message ?? e.code}',
        );
      }
    } catch (e) {
      _message(
        'Could not redeem reward: $e',
      );
    } finally {
      if (mounted) {
        setState(() => _redeeming = false);
      }
    }
  }

  void _message(String text) {
    if (!mounted) return;

    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(text),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final cs = Theme.of(context).colorScheme;
    final user = FirebaseAuth.instance.currentUser;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Reward Details'),
      ),
      body: StreamBuilder<
          DocumentSnapshot<Map<String, dynamic>>>(
        stream: FirebaseFirestore.instance
            .collection('rewards')
            .doc(widget.rewardId)
            .snapshots(),
        builder: (context, snapshot) {
          if (snapshot.hasError) {
            return const Center(
              child: Text(
                'Unable to load reward details.',
              ),
            );
          }

          if (!snapshot.hasData) {
            return const Center(
              child: CircularProgressIndicator(),
            );
          }

          if (!snapshot.data!.exists) {
            return const Center(
              child: Text('Reward not found.'),
            );
          }

          final reward = snapshot.data!.data()!;

          final title = _field(
            reward['title'],
            'Reward',
          );

          final imageUrl = _field(
            reward['imageUrl'],
          );

          final description = _field(
            reward['description'],
            'No description available.',
          );

          final conditions = _field(
            reward['conditions'],
            'No special conditions',
          );

          final cost = _crystalCost(
            reward['crystalCost'],
          );

          final expiry = _field(
            reward['expDate'],
            'Not specified',
          );

          final status = _field(
            reward['availStatus'],
            'Active',
          );

          final available =
              status == 'Active' &&
              !_isExpired(reward['expDate']);

          return SingleChildScrollView(
            padding: const EdgeInsets.all(20),
            child: Column(
              crossAxisAlignment:
                  CrossAxisAlignment.start,
              children: [
                GestureDetector(
                  onTap: () => _openRewardImage(
                    context,
                    imageUrl,
                    title,
                  ),
                  child: ClipRRect(
                    borderRadius:
                        BorderRadius.circular(18),
                    child: AspectRatio(
                      aspectRatio: 1.2,
                      child: _RewardImage(
                        imageUrl: imageUrl,
                        fit: BoxFit.contain,
                      ),
                    ),
                  ),
                ),
                const SizedBox(height: 20),
                Text(
                  title,
                  style: GoogleFonts.spaceGrotesk(
                    fontSize: 26,
                    fontWeight: FontWeight.w800,
                    color: cs.onSurface,
                  ),
                ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    Icon(
                      Icons.diamond_rounded,
                      color: cs.primary,
                    ),
                    const SizedBox(width: 8),
                    Text(
                      '$cost Crystals',
                      style: GoogleFonts.spaceGrotesk(
                        fontSize: 20,
                        fontWeight: FontWeight.w700,
                        color: cs.primary,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 14),

                if (user != null)
                  StreamBuilder<
                      DocumentSnapshot<Map<String, dynamic>>>(
                    stream: FirebaseFirestore.instance
                        .collection('users')
                        .doc(user.uid)
                        .snapshots(),
                    builder: (context, userSnapshot) {
                      if (userSnapshot.hasError) {
                        return const Text(
                          'Unable to load your crystal balance.',
                        );
                      }

                      if (!userSnapshot.hasData ||
                          !userSnapshot.data!.exists) {
                        return const Text(
                          'Loading your crystal balance...',
                        );
                      }

                      final balance = _crystalCost(
                        userSnapshot.data!
                            .data()?['crystals'],
                      );

                      return Text(
                        'Your balance: $balance Crystals',
                        style: GoogleFonts.manrope(
                          fontSize: 13,
                          fontWeight: FontWeight.w600,
                          color: cs.onSurfaceVariant,
                        ),
                      );
                    },
                  ),

                const SizedBox(height: 24),
                Text(
                  'Description',
                  style: GoogleFonts.spaceGrotesk(
                    fontSize: 18,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  description,
                  style: GoogleFonts.manrope(
                    fontSize: 14,
                    color: cs.onSurfaceVariant,
                  ),
                ),
                const SizedBox(height: 20),
                _DetailRow(
                  icon: Icons.rule_rounded,
                  label: 'Conditions',
                  value: conditions,
                ),
                const SizedBox(height: 12),
                _DetailRow(
                  icon: Icons.event_rounded,
                  label: 'Expires',
                  value: expiry,
                ),
                const SizedBox(height: 12),
                _DetailRow(
                  icon: Icons.info_outline_rounded,
                  label: 'Availability',
                  value: available
                      ? 'Active'
                      : 'Unavailable',
                ),
                const SizedBox(height: 28),
                SizedBox(
                  width: double.infinity,
                  height: 52,
                  child: ElevatedButton.icon(
                    onPressed:
                        (!available || _redeeming)
                            ? null
                            : _redeemReward,
                    icon: _redeeming
                        ? const SizedBox(
                            height: 18,
                            width: 18,
                            child:
                                CircularProgressIndicator(
                              strokeWidth: 2,
                            ),
                          )
                        : const Icon(
                            Icons.card_giftcard_rounded,
                          ),
                    label: Text(
                      _redeeming
                          ? 'Redeeming...'
                          : 'Redeem for $cost Crystals',
                    ),
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}

// ============================================
// DETAILS ROW
// ============================================

class _DetailRow extends StatelessWidget {
  final IconData icon;
  final String label;
  final String value;

  const _DetailRow({
    required this.icon,
    required this.label,
    required this.value,
  });

  @override
  Widget build(BuildContext context) {
    final cs = Theme.of(context).colorScheme;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(
          icon,
          size: 19,
          color: cs.primary,
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Text(
            '$label: $value',
            style: GoogleFonts.manrope(
              fontSize: 13,
              color: cs.onSurfaceVariant,
            ),
          ),
        ),
      ],
    );
  }
}

// ============================================
// REWARD IMAGE
// ============================================

class _RewardImage extends StatelessWidget {
  final String imageUrl;
  final BoxFit fit;

  const _RewardImage({
    required this.imageUrl,
    required this.fit,
  });

  @override
  Widget build(BuildContext context) {
    final mc =
        Theme.of(context).extension<MasarColors>()!;

    final cs = Theme.of(context).colorScheme;

    if (imageUrl.isEmpty) {
      return Container(
        color: mc.surfaceHighest,
        child: Icon(
          Icons.card_giftcard_rounded,
          size: 48,
          color: cs.outline,
        ),
      );
    }

    return Container(
      color: mc.surfaceHighest,
      child: Image.network(
        imageUrl,
        fit: fit,
        loadingBuilder: (context, child, progress) {
          if (progress == null) return child;
          return const Center(
            child: CircularProgressIndicator(),
          );
        },
        errorBuilder: (
          context,
          error,
          stackTrace,
        ) {
          return Center(
            child: Icon(
              Icons.broken_image_outlined,
              size: 48,
              color: cs.outline,
            ),
          );
        },
      ),
    );
  }
}

// ============================================
// FULL-SCREEN IMAGE VIEWER
// ============================================

void _openRewardImage(
  BuildContext context,
  String imageUrl,
  String title,
) {
  if (imageUrl.isEmpty) return;

  Navigator.of(context).push(
    MaterialPageRoute(
      builder: (_) => Scaffold(
        backgroundColor: Colors.black,
        appBar: AppBar(
          backgroundColor: Colors.black,
          foregroundColor: Colors.white,
          title: Text(title),
        ),
        body: Center(
          child: InteractiveViewer(
            minScale: 0.8,
            maxScale: 5,
            child: Image.network(
              imageUrl,
              fit: BoxFit.contain,
              errorBuilder: (
                context,
                error,
                stackTrace,
              ) {
                return const Icon(
                  Icons.broken_image_outlined,
                  color: Colors.white,
                  size: 60,
                );
              },
            ),
          ),
        ),
      ),
    ),
  );
}
