// Example to integrate into the existing MasAR+ Flutter reward-redemption screen.
// IMPORTANT: This is a standalone integration example, NOT an edit to your app.
// Dependencies: firebase_auth (already used by MasAR+) and http (add to pubspec.yaml).
// Set your own deployed Netlify site address (HTTPS) before using it.

import 'dart:convert';
import 'dart:math';
import 'package:firebase_auth/firebase_auth.dart';
import 'package:http/http.dart' as http;

class MasarRewardClaim {
  final String code;
  final String title;
  final String type;
  final int? crystalsRemaining;
  MasarRewardClaim(this.code, this.title, this.type, this.crystalsRemaining);

  factory MasarRewardClaim.fromJson(Map<String, dynamic> json) => MasarRewardClaim(
    json['claimCode'] as String,
    (json['title'] ?? '') as String,
    (json['rewardType'] ?? '') as String,
    json['crystalsRemaining'] as int?,
  );
}

class MasarRewardService {
  // Replace with your own Netlify URL, e.g. https://your-site.netlify.app
  static const netlifyBaseUrl = 'https://YOUR_NETLIFY_SITE.netlify.app';

  // Generate ONE requestId per tap. Preserve it if a timeout requires a retry.
  static String createRequestId() {
    final random = Random.secure();
    return List.generate(16, (_) => random.nextInt(256).toRadixString(16).padLeft(2, '0')).join();
  }

  static Future<MasarRewardClaim> redeemSponsorReward({
    required String rewardId,
    required String requestId,
  }) async {
    final user = FirebaseAuth.instance.currentUser;
    if (user == null) throw StateError('Please sign in first.');
    final token = await user.getIdToken();
    final response = await http.post(
      Uri.parse('$netlifyBaseUrl/api/redeem-reward'),
      headers: {
        'Authorization': 'Bearer $token',
        'Content-Type': 'application/json',
      },
      body: jsonEncode({'rewardId': rewardId, 'requestId': requestId}),
    ).timeout(const Duration(seconds: 30));
    final json = jsonDecode(response.body) as Map<String, dynamic>;
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw StateError((json['error'] ?? 'Could not redeem the reward.').toString());
    }
    return MasarRewardClaim.fromJson(json);
  }
}

// Usage in the existing reward screen:
// 1. Create requestId ONCE when the player taps Redeem, e.g.:
//    final requestId = MasarRewardService.createRequestId();
// 2. Call redeemSponsorReward(rewardId: reward.id, requestId: requestId).
// 3. Show claim.code prominently, with title, partner and pickup location.
// 4. If the network times out, reuse the SAME requestId on retry.
// 5. Listen to users/{uid} and refresh the displayed crystal balance.
// 6. For order history, query user_rewards where userId == FirebaseAuth.instance.currentUser!.uid.
// IMPORTANT: Do NOT also decrement crystals or create a user_rewards record in Flutter.
// The Netlify function does both atomically and validates the current balance.
