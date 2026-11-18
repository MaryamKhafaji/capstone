import { initializeApp, cert, getApps } from 'firebase-admin/app';
import { getAuth } from 'firebase-admin/auth';
import { getFirestore } from 'firebase-admin/firestore';

function initialize() {
  if (getApps().length) return;
  // Add the entire Firebase service-account JSON as an encrypted Netlify environment variable.
  // This is a SERVER-ONLY secret; never include it in public/ or in source control.
  const value = process.env.FIREBASE_SERVICE_ACCOUNT_JSON;
  if (!value) throw new Error('Backend is missing FIREBASE_SERVICE_ACCOUNT_JSON.');
  let creds;
  try { creds = JSON.parse(value); } catch { throw new Error('FIREBASE_SERVICE_ACCOUNT_JSON must contain valid JSON.'); }
  initializeApp({ credential: cert(creds) });
}
export function firestore() { initialize(); return getFirestore(); }

export async function authenticated(request) {
  initialize();
  const match = /^Bearer (.+)$/i.exec(request.headers.get('authorization') || '');
  if (!match) throw httpError(401, 'Please sign in to MasAR+ first.');
  let decoded;
  try { decoded = await getAuth().verifyIdToken(match[1], true); }
  catch { throw httpError(401, 'Your sign-in has expired. Please sign in again.'); }
  return decoded.uid;
}
export async function userRole(uid) {
  const db = firestore();
  const [admin, partner] = await Promise.all([
    db.collection('admins').doc(uid).get(),
    db.collection('partners').doc(uid).get(),
  ]);
  if (admin.exists) return { role: 'admin', uid };
  if (partner.exists && partner.data().ownerUid === uid && (
    partner.data().status === 'active' ||
    (partner.data().status === 'approved' && partner.data().verified === true)
  )) return { role: 'sponsor', uid };
  throw httpError(403, 'Your sponsor account is inactive.');
}
export function httpError(status, message) { return Object.assign(new Error(message), { status }); }
export function json(body, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' } });
}
export function fail(error) {
  if (error?.status && error.status < 500) return json({ error: error.message }, error.status);
  console.error('MasAR+ backend error:', error);
  return json({ error: 'The MasAR+ server could not complete this request. Please try again.' }, 500);
}
