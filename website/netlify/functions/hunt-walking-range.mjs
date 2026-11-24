// Straight-line proximity estimate at 4 km/h; this is not a walking-route ETA.
export default async function huntWalkingRange(request) {
  if (request.method !== 'POST') return Response.json({ error: 'Use POST.' }, { status: 405 });
  const body = await request.json().catch(() => null);
  const minutes = body?.walkingMinutes;
  if (![15, 30, 45, 60].includes(minutes)) {
    return Response.json({ error: 'Choose 15, 30, 45, or 60 minutes.' }, { status: 422 });
  }
  return Response.json({ walkingMinutes: minutes, maxSearchMeters: minutes * 4000 / 60 });
}
