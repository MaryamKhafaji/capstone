# MasAR+ Admin + Sponsor Portal — streamlined November 2026 version

This project modifies the existing `MasarPlusAdmin-Partners-Unified.zip` implementation. The original Tailwind layout, colors, sidebar, cards, modals, Dashboard, Rewards, Partners, Flags and Settings are retained. Only the requested sponsor fields/flows and Analytics navigation are changed.

## Architecture (important)

- **Site and image backend:** Netlify Hosting + Netlify Functions + Netlify Blobs (images); no Cloudinary calls remain.
- **Accounts and database:** Existing Firebase Auth and Cloud Firestore are retained, so the original Flutter app is not silently migrated to DynamoDB/AWS. The previously discussed AWS migration is **not** included in this ZIP.
- **Optional AI description:** A Netlify Function calls an image-capable OpenAI API model with an environment secret. This requires an OpenAI API key with API billing enabled; ChatGPT subscription does not include API usage. Without a key, manual descriptions work normally.
- **All secrets are server-only** via Netlify environment variables. The Firebase web config in `public/js/firebase-config.js` is a public client config, not the Firebase service-account private key.

## Agreed product behavior

Sponsor signup is **immediate**: `partners/{firebaseAuthUID}` is created with `status: "active"` and `verified: false`. Here `verified` is business vetting, **not** permission to submit products. Admins may optionally verify or suspend the company in Partners; suspension blocks product management and image/AI backend requests. Existing `status: approved` plus `verified: true` partners still work.

Sponsors only enter: product name, optional photo, type (**Gift or Voucher**), **value in KD**, quantity, and a short editable description. "Generate description" sends the photo and product name to a vision-capable API when explicitly clicked; it connects the visible product to MasAR+'s walking/AR/crystal rewards theme without inventing facts. The sponsor may edit available (unselected) products immediately. No approvals, requests, cashback, commercial terms, or change-request page. Admin may edit or deny available offers, publish one full quantity, suspend companies or unpublish a reward. Selected products are locked to sponsors.

**Conversion**: 1 KD = 1,000 crystals. Since 1 KD = 1,000 fils, the Firestore `valueFils` integer equals the calculated `crystalCost` integer. For example, KD 2.500 = 2,500 crystals. Admin selection does not ask for a price or extra game conditions. On publishing, `rewards/sponsor_{productId}` receives `crystalCost` and other display fields, but **not `valueFils`**. Only rewards marked `availStatus: "Active"` should appear in Flutter.

**Inventory & in-person collection**: The entire submitted quantity is committed when published. Flutter calls the server endpoint to atomically deduct crystals, increment `redeemedCount`, and issue a one-time 24-character claim code in `user_rewards/{claimCode}`. The player visits the sponsor's branch and shows the claim code. Sponsor opens **Verify Rewards** in the same portal and enters the code; the server checks it belongs to that company, marks it `fulfilled` once, and increments `fulfilledCount`. Voucher = use the one-time voucher at the location; Gift = collect the sponsored physical item there. **No e-commerce, delivery, payment processor, or retail POS integration.** The sponsor provides the actual product. If a sponsor doesn't have a pickup location, MasAR+ must coordinate one before allowing redemptions.

## Database collections

Reused: `partners`, `rewards`, `users`, `user_rewards`, `admins`, `walking_sessions`, `leaderboard`, `hunts`, `flags`, `session_records`, and `user_hunts`.

New: **`sponsor_products`** for offered gift/voucher catalog, **`reward_redemption_requests`** for retry-safe debits, and **`ai_description_usage`** for a five-per-day usage cap. `sponsor_change_requests` is **no longer used**. Do not delete old collections/data without a backup. Firestore creates collections on the first write.

## Before deploying

1. **BACK UP** your existing Firestore rules, Firestore data and the original website ZIP. Never paste the proposed new rules without testing against the actual Flutter code. The project is not connected to your Firebase account from this environment.
2. **Netlify site:** connect this project repository to Netlify (recommended) or deploy using Netlify CLI. Project root contains `netlify.toml`, `package.json`, `netlify/functions/`, and `public/`. Publishing a static `public` folder by itself does NOT deploy the backend Functions.
3. **Install:** from the project root, run `npm install`. For local testing run `npx netlify dev` (or `npm run dev`). This serves the website AND `/api/*` Functions. VS Code Live Server serves HTML/JS only; image upload, AI generation, redemption and code verification won't work there.
4. **Netlify environment variables:** in Project configuration → Environment variables add `FIREBASE_SERVICE_ACCOUNT_JSON` with the **entire service-account JSON** from Firebase Console → Project settings → Service accounts → Generate new private key. Treat it like a password: never commit it, email it or paste it into frontend code. Add `OPENAI_API_KEY` (optional but required for AI descriptions), optionally `OPENAI_DESCRIPTION_MODEL` (default `gpt-4.1-mini`). Ensure these variables are available to Functions; redeploy after setting.
5. **Firebase Auth:** enable email/password. Add your published Netlify domain to Firebase Auth Authorized domains where needed. Ensure an admin exists in Firebase Auth and `admins/{uid}`. The public web config stays in `public/js/firebase-config.js`.
6. **Firestore rules:** review `firebase/firestore.rules` carefully. The rules *intentionally prevent clients from updating `users.crystals`*; only the server can debit them. **Do not deploy these rules until you change Flutter's crystal-earning implementation**, otherwise the current client-side earning path may fail. Existing Flutter clients that create `user_rewards` directly also need migration to the provided endpoint. Test via Firebase Emulator before production.
7. **Flutter:** adapt `flutter_integration/reward_claim_service.dart` into your existing reward screen. Update the API URL to your deployed Netlify URL and add the `http` dependency. The original Flutter source was not supplied, so the provided Dart service is an example; the app itself has **not** yet been edited or tested.
8. **Images:** no Cloudinary account is needed. New product/logo images are resized in-browser, uploaded securely to a Netlify Function and stored in Netlify Blobs. Image URLs go into Firestore. Older Cloudinary URLs remain in old records until individually replaced. Netlify Blobs and Functions are subject to Netlify plan limits.
9. **AI quota:** 5 generated descriptions per signed-in account per UTC day; free-form manual writing is always available. AI can make mistakes. Review suggestions before saving; do not use it to invent product specs or promises.
10. **No automated testing against live services was performed.** Netlify credentials, Firebase secret, Flutter source and OpenAI key were not available. Deploy/test on staging first.

## Browser test checklist

- Admin login still opens the existing dashboard and pages. Analytics has Week / Month / Year / Total, and CSV/JSON export at the end. Export nav and Change Requests nav are gone.
- Sponsor signs up and enters the sponsor dashboard without admin approval. The company doc is `partners/{uid}` with `status: active`, `verified: false`.
- Upload company logo and product photo, check public Netlify-hosted image URL works on mobile.
- Sponsor creates a KD 2.500 gift, and edits it without approval while unselected. Try AI Generate description, review and edit the text.
- Admin sees it under Rewards, can Edit / Deny / Publish. Publish displays **2,500 crystals** automatically, commits all units, and locks sponsor edits.
- Show only published rewards in the Flutter UI; test Netlify `/api/redeem-reward` with a signed-in player and stable `requestId`. A successful claim lowers crystals and increments stock used once. Retry with the same ID must return the same claim code without double charging.
- Sponsor verifies the player's 24-character code in person. Reusing the same code must fail.
- Repeat with a voucher; ensure the partner honors the voucher as a one-time use.
- Check permission-denied on modifying another sponsor's products, reading other users' claims, and modifying crystals directly.

## Known limitations / issues to resolve before production

- **Flutter earning path**: current player-write rules previously allowed direct crystal updates. They are incompatible with a secure wallet. You must migrate earning to a trusted backend validated against genuine activity, including BLE/heel-pad integrity as feasible. Only the redemption debit is supplied here. We have NOT claimed the entire game's crystal economy is secure yet.
- Netlify image uploads rely on valid Firebase credentials and a Netlify site with functions enabled. Uploaded images do not automatically get deleted on replacement, because published rewards and old claims may still reference the old asset.
- Public sponsor signup allows immediate access; consider moderation/abuse protection for real-world deployment. Admin can suspend company accounts and deny products.
- Our browser has not completed real, authenticated end-to-end tests on Netlify/Firebase/OpenAI. Treat this ZIP as a build for staging, not a production-certified deployment.
- Legacy cashback/old sponsor-product documents remain in Firestore. New sponsor submissions accept Gift or Voucher only; existing legacy records are not automatically deleted or migrated.
- No website CSS redesign has been introduced. Changes reuse the existing Tailwind theme and components.
- Legacy Flutter code may use an unfiltered `partners` collection query. The new rules restrict that list to admins to protect sponsor contact details; adjust Flutter to use explicit published partner references or a public projection before enabling these rules.
- The prior “Unknown API key” sign-in error, if still present, must be resolved in Firebase Authentication/web configuration. Changing hosting does not repair an invalid Firebase API key.
- For production, add server-side limits and monitoring for public sponsor signups and the image upload endpoint.

