
import {
  authenticated,
  firestore,
  json,
  fail,
  httpError,
} from "./_auth.mjs";

import { FieldValue } from "firebase-admin/firestore";

// Connect this Netlify function to the URL
// used by the website's Verify & Mark Collected button.
export const config = {
  path: "/api/fulfill-reward",
};

export default async (request) => {
  try {
    if (request.method !== "POST") {
      return json({ error: "Method not allowed." }, 405);
    }

    // Verify the Firebase ID token sent by app.js.
    const uid = await authenticated(request);
    const db = firestore();

    const body = await request.json().catch(() => null);

    const code =
      typeof body?.code === "string"
        ? body.code.trim().replace(/\s+/g, "").toUpperCase()
        : "";

    if (!/^[A-F0-9]{24}$/.test(code)) {
      throw httpError(400, "Invalid claim code format.");
    }

    // Only administrators and active sponsors may verify.
    const adminSnap = await db.collection("admins").doc(uid).get();
    const isAdmin = adminSnap.exists;

    if (!isAdmin) {
      const partnerSnap = await db
        .collection("partners")
        .doc(uid)
        .get();

      const partner = partnerSnap.data();

      const isActiveSponsor =
        partner?.ownerUid === uid &&
        (
          partner.status === "active" ||
          (partner.status === "approved" &&
            partner.verified === true)
        );

      if (!isActiveSponsor) {
        throw httpError(403, "Not authorized to verify rewards.");
      }
    }

    // Find the one-time claim using the code.
    const matches = await db
      .collection("user_rewards")
      .where("claimCode", "==", code)
      .limit(2)
      .get();

    if (matches.size !== 1) {
      throw httpError(404, "Claim code not found.");
    }

    const claimRef = matches.docs[0].ref;

    // Atomic transaction prevents two simultaneous
    // attempts from collecting the same reward.
    const result = await db.runTransaction(async (tx) => {
      const claimSnap = await tx.get(claimRef);

      if (!claimSnap.exists) {
        throw httpError(404, "Claim not found.");
      }

      const claim = claimSnap.data();

      if (claim.claimCode !== code) {
        throw httpError(404, "Invalid claim code.");
      }

      if (claim.status === "fulfilled") {
        throw httpError(
          409,
          "This reward has already been collected."
        );
      }

      if (claim.status !== "issued") {
        throw httpError(
          409,
          "This claim is not available for collection."
        );
      }

      if (
        typeof claim.rewardId !== "string" ||
        !claim.rewardId
      ) {
        throw httpError(409, "Invalid claim record.");
      }

      const rewardRef = db
        .collection("rewards")
        .doc(claim.rewardId);

      const rewardSnap = await tx.get(rewardRef);

      if (!rewardSnap.exists) {
        throw httpError(404, "Reward no longer exists.");
      }

      const reward = rewardSnap.data();

      // A sponsor can verify ONLY their own rewards.
      // Admins may verify any reward.
      if (!isAdmin && reward.partnerId !== uid) {
        throw httpError(
          403,
          "This reward belongs to another sponsor."
        );
      }

      if (claim.crystalCost !== reward.crystalCost) {
        throw httpError(
          409,
          "Claim and reward price do not match."
        );
      }

      // Mark the code used permanently.
      tx.update(claimRef, {
        status: "fulfilled",
        fulfilledAt: FieldValue.serverTimestamp(),
        fulfilledBy: uid,
      });

      // Track physical collection.
      // Do NOT increment redeemedCount here:
      // Flutter already incremented it when issuing the claim.
      tx.update(rewardRef, {
        fulfilledCount: FieldValue.increment(1),
      });

      return {
        title: reward.title || "Reward",
        rewardType: reward.rewardType || "reward",
      };
    });

    return json({
      success: true,
      title: result.title,
      rewardType: result.rewardType,
    });
  } catch (error) {
    return fail(error);
  }
};
