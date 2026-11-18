const number = "([+-]?(?:\\d+(?:\\.\\d+)?|\\.\\d+))";
const pair = new RegExp(`^\\s*${number}\\s*,\\s*${number}\\s*$`);

export function mapsUrl(value) {
  const url = new URL(value);
  const { hostname, pathname } = url;
  const short = (hostname === "maps.app.goo.gl" && /^\/[\w-]+$/.test(pathname)) ||
    (hostname === "goo.gl" && /^\/maps\/[\w-]+$/.test(pathname));
  const google = ["google.com", "www.google.com", "maps.google.com"].includes(hostname) &&
    (hostname === "maps.google.com" || /^\/maps(?:\/|$)/.test(pathname));
  if (url.protocol !== "https:" || url.username || url.password || url.port ||
    (!short && !google) || url.href.length > 2048) throw new Error("Enter a valid Google Maps link.");
  return { url, short };
}

export function mapCoordinates(url) {
  const path = decodeURIComponent(url.pathname);
  const pin = (path + decodeURIComponent(url.search)).match(new RegExp(`!3d${number}!4d${number}(?=!|/|&|$)`));
  const reversePin = (path + decodeURIComponent(url.search)).match(new RegExp(`!2d${number}!3d${number}(?=!|/|&|$)`));
  const search = ["query", "q"].map((key) => url.searchParams.get(key)?.match(pair)).find(Boolean);
  const pathPin = path.split("/").map((segment) => segment.match(pair)).find(Boolean);
  const match = search || pathPin || pin;
  // The @ coordinates in a place URL indicate the viewport, so use the actual pin instead.
  const coordinates = match ? [Number(match[1]), Number(match[2])] :
    reversePin ? [Number(reversePin[2]), Number(reversePin[1])] : null;
  if (!coordinates || Math.abs(coordinates[0]) > 90 || Math.abs(coordinates[1]) > 180) return null;
  return { latitude: coordinates[0], longitude: coordinates[1] };
}
