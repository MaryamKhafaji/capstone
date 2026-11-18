import { randomBytes } from 'node:crypto';
import { FieldValue } from 'firebase-admin/firestore';
import { authenticated, firestore, json, fail, httpError } from './_auth.mjs';

// Called from Flutter only; debits crystals + commits stock + creates a one-time claim atomically.
export default async (request) => {
  try {
    if (request.method !== 'POST') return json({ error: 'Method not allowed.' }, 405);
    const uid = await authenticated(request);
    const raw = await request.text();
    if (raw.length > 2000) throw httpError(413, 'Request too large.');
    const { rewardId, requestId } = JSON.parse(raw);
    if (typeof rewardId !== 'string' || !/^sponsor_[A-Za-z0-9_-]{1,128}$/.test(rewardId)) throw httpError(400, 'Invalid sponsor reward ID.');
    if (typeof requestId !== 'string' || !/^[A-Za-z0-9_-]{8,80}$/.test(requestId)) throw httpError(400, 'A unique request ID is required to prevent duplicate redemptions.');
    const db = firestore();
    const userRef = db.collection('users').doc(uid);
    const rewardRef = db.collection('rewards').doc(rewardId);
    const idemRef = db.collection('reward_redemption_requests').doc(`${uid}_${requestId}`);
    const code = randomBytes(12).toString('hex').toUpperCase(); // 96-bit one-time claim code
    const claimRef = db.collection('user_rewards').doc(code);
    const result = await db.runTransaction(async (tx) => {
      const existing = await tx.get(idemRef);
      if (existing.exists) {
        if (existing.data().rewardId !== rewardId) throw httpError(409, 'This request ID was already used for another reward.');
        return { claimCode: existing.data().claimCode, previous: true };
      }
      const [player, reward] = await Promise.all([tx.get(userRef), tx.get(rewardRef)]);
      if (!player.exists || !reward.exists) throw httpError(404, 'Player or reward not found.');
      const r = reward.data(), u = player.data();
      if (r.availStatus !== 'Active' || !r.sourceProductId || !['gift','voucher'].includes(r.rewardType)) throw httpError(409, 'This reward is not available.');
      const price = r.crystalCost;
      if (!Number.isInteger(price) || price < 1 || !Number.isInteger(r.quantityTotal) || !Number.isInteger(r.redeemedCount)) throw httpError(409, 'Reward pricing or inventory needs attention.');
      if (r.redeemedCount >= r.quantityTotal) throw httpError(409, 'This reward is out of stock.');
      if (!Number.isInteger(u.crystals) || u.crystals < price) throw httpError(409, 'Not enough crystals.');
      tx.update(userRef, { crystals: u.crystals - price });
      tx.update(rewardRef, { redeemedCount: r.redeemedCount + 1, updatedAt: FieldValue.serverTimestamp() });
      tx.create(claimRef, {
        userId: uid, rewardId, partnerId: r.partnerId, title: r.title,
        rewardType: r.rewardType, crystalCost: price, claimCode: code,
        pickupLocation: r.pickupLocation || "Contact the sponsor for collection details",
        status: 'issued', redeemDate: FieldValue.serverTimestamp(),
        createdAt: FieldValue.serverTimestamp(),
      });
      tx.create(idemRef, { userId: uid, rewardId, claimCode: code, createdAt: FieldValue.serverTimestamp() });
      return { claimCode: code, previous: false, crystalsRemaining: u.crystals - price, title: r.title, rewardType: r.rewardType };
    });
    // Return saved claim details on retry; it must NOT debit twice.
    if (result.previous) {
      const saved = await db.collection('user_rewards').doc(result.claimCode).get();
      if (!saved.exists || saved.data().userId !== uid) throw httpError(409, 'Unable to retrieve the previous claim.');
      return json({ claimCode: result.claimCode, title: saved.data().title, rewardType: saved.data().rewardType, alreadyProcessed: true });
    }
    return json({ ...result, alreadyProcessed: false }, 201);
  } catch (error) { return fail(error); }
};
