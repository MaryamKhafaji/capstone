
import { authenticated, userRole, firestore, json, fail, httpError } from './_auth.mjs';
import { FieldValue } from 'firebase-admin/firestore';

export default async (request) => {
  try {
    if (request.method !== 'POST') {
      return json({ error: 'Method not allowed.' }, 405);
    }

    const uid = await authenticated(request);
    await userRole(uid);

    if (!process.env.GEMINI_API_KEY) {
      throw httpError(503, 'AI suggestions are not configured yet. You can still type your own description.');
    }

    // Read the uploaded image and product details.
    const raw = await request.text();

    if (raw.length > 4_300_000) {
      throw httpError(413, 'Image is too large for AI description.');
    }

    const { title, rewardType, imageData } = JSON.parse(raw);

    if (
      typeof title !== 'string' ||
      !title.trim() ||
      title.length > 140 ||
      !['gift', 'voucher'].includes(rewardType)
    ) {
      throw httpError(400, 'Enter a product name and reward type.');
    }

    if (
      typeof imageData !== 'string' ||
      !/^data:image\/(?:webp|jpeg|png);base64,[A-Za-z0-9+/=]+$/.test(imageData)
    ) {
      throw httpError(400, 'Please choose a product photo.');
    }

    // Limit each account to 5 generations per day.
    const date = new Date().toISOString().slice(0, 10);

    const quotaRef = firestore()
      .collection('ai_description_usage')
      .doc(`${uid}_${date}`);

    await firestore().runTransaction(async (tx) => {
      const snap = await tx.get(quotaRef);
      const count = snap.exists ? (snap.data().count || 0) : 0;

      if (count >= 5) {
        throw httpError(
          429,
          'You have used all 5 AI suggestions today. You can still type your description.'
        );
      }

      tx.set(quotaRef, {
        count: count + 1,
        userId: uid,
        date,
        updatedAt: FieldValue.serverTimestamp()
      });
    });

    // Describe the image and connect it to MasAR+.
    const prompt = `
Write a natural, short description (2 sentences, under 55 words)
for a MasAR+ sponsor reward.

MasAR+ motivates walking through augmented-reality exploration,
challenges, and crystals redeemed for rewards.

Product name: ${title.trim()}
Reward type: ${rewardType}

Describe only visible or explicitly supplied facts.
Connect the reward subtly to MasAR+'s active-lifestyle gameplay.

Do not invent product specifications, discounts,
eligibility requirements, or locations.

Return only the description.
`;

    const match =
      /^data:(image\/(?:webp|jpeg|png));base64,(.+)$/.exec(imageData);

    // Request Gemini 2.5 Flash.
    const result = await fetch(
      'https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent',
      {
        method: 'POST',
        signal: AbortSignal.timeout(25000),
        headers: {
          'x-goog-api-key': process.env.GEMINI_API_KEY,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          contents: [
            {
              role: 'user',
              parts: [
                { text: prompt },
                {
                  inline_data: {
                    mime_type: match[1],
                    data: match[2]
                  }
                }
              ]
            }
          ],
          generationConfig: {
            maxOutputTokens: 250,
            temperature: 0.5,
            thinkingConfig: {
              thinkingBudget: 0
            }
          }
        })
      }
    );

    if (!result.ok) {
      if (result.status === 429) {
        throw httpError(
          429,
          'Gemini free quota reached. Please write your description manually.'
        );
      }

      throw httpError(
        502,
        'Gemini is temporarily unavailable. Please write your description manually.'
      );
    }

    const payload = await result.json();

    const description = (
      payload.candidates?.[0]?.content?.parts || []
    )
      .map(part => part.text || '')
      .join(' ')
      .trim();

    if (!description) {
      throw httpError(
        502,
        'Gemini returned no description. Please try again or type it yourself.'
      );
    }

    return json({
      description: description.slice(0, 1000)
    });

  } catch (error) {
    if (
      error.name === 'AbortError' ||
      error.name === 'TimeoutError'
    ) {
      return json({
        error: 'The AI request timed out. Please try again.'
      }, 504);
    }

    return fail(error);
  }
};
