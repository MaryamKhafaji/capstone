import { mapsUrl, mapCoordinates } from "../../public/js/hunt-maps.js";

export default async function resolveHuntMap(request) {
  if (request.method !== "POST") return Response.json({ error: "Use POST." }, { status: 405 });
  try {
    const body = await request.text();
    if (body.length > 2500) throw new Error("Link is too long.");
    let target = mapsUrl(JSON.parse(body).url);
    for (let hop = 0; target.short && hop < 4; hop++) {
      const response = await fetch(target.url, { redirect: "manual", signal: AbortSignal.timeout(4000) });
      const next = response.headers.get("location");
      await response.body?.cancel();
      if (response.status < 300 || response.status >= 400 || !next) break;
      target = mapsUrl(new URL(next, target.url).href);
    }
    const pin = mapCoordinates(target.url);
    if (pin) return Response.json(pin);
  } catch {
    // Some shared place links do not expose coordinates in their redirect.
  }
  return Response.json({ error: "Couldn't find the pin. Enter its latitude and longitude from Google Maps." }, { status: 422 });
}
