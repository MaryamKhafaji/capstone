import { getStore } from '@netlify/blobs';
import { randomBytes } from 'node:crypto';
import { authenticated, userRole, json, fail, httpError } from './_auth.mjs';

const STORE = 'masar-reward-images';
const validKey = /^[a-zA-Z0-9_-]+\/(?:logo|product)\/[a-f0-9]{32}\.webp$/;
const MAX_BYTES = 3 * 1024 * 1024;

export default async (request) => {
  try {
    if (request.method === 'GET') {
      const key = new URL(request.url).searchParams.get('key') || '';
      if (!validKey.test(key)) return new Response('Not found', { status: 404 });
      // Published product photos are intentionally public so Flutter can display them.
      const entry = await getStore(STORE).get(key, { type: 'arrayBuffer' });
      if (!entry) return new Response('Not found', { status: 404 });
      return new Response(entry, { headers: {
        'Content-Type': 'image/webp', 'Cache-Control': 'public, max-age=86400',
        'X-Content-Type-Options': 'nosniff',
      }});
    }
    if (request.method !== 'POST') return json({ error: 'Method not allowed.' }, 405);
    const uid = await authenticated(request);
    await userRole(uid); // Admin or active sponsor. No anonymous uploads.
    const form = await request.formData();
    const file = form.get('image');
    const kind = form.get('kind');
    if (!['product', 'logo'].includes(kind)) throw httpError(400, 'Invalid image category.');
    if (!file || typeof file.arrayBuffer !== 'function' || file.type !== 'image/webp') {
      throw httpError(400, 'Choose a JPG, PNG or WebP photo.');
    }
    if (file.size < 20 || file.size > MAX_BYTES) throw httpError(413, 'The image is too large. Please try a smaller photo.');
    const data = await file.arrayBuffer();
    const bytes = new Uint8Array(data);
    const signature = (start, text) => text.split('').every((ch, i) => bytes[start + i] === ch.charCodeAt(0));
    if (!signature(0, 'RIFF') || !signature(8, 'WEBP')) throw httpError(400, 'The image could not be verified. Try another file.');
    const key = `${uid}/${kind}/${randomBytes(16).toString('hex')}.webp`;
    await getStore(STORE).set(key, data, { metadata: { type: 'image/webp', uploadedBy: uid, kind }, onlyIfNew: true });
    const origin = process.env.URL || new URL(request.url).origin;
    const url = `${origin.replace(/\/$/, '')}/api/reward-images?key=${encodeURIComponent(key)}`;
    return json({ url, key }, 201);
  } catch (error) { return fail(error); }
};
