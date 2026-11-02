const development =
  process.env.EXPO_PUBLIC_APP_ENV === "development" ||
  (!process.env.EXPO_PUBLIC_APP_ENV && process.env.NODE_ENV !== "production");

// Public community services are limited to deliberate development searches.
// Releases need explicitly selected services with appropriate capacity/terms.
export const placesConfig = {
  geocoder:
    process.env.EXPO_PUBLIC_GEOCODER_URL ??
    (development ? "https://photon.komoot.io/api/" : ""),
  points:
    process.env.EXPO_PUBLIC_POI_URL ??
    (development ? "https://photon.komoot.io/reverse" : ""),
  tiles:
    process.env.EXPO_PUBLIC_MAP_TILES_URL ??
    (development ? "https://tile.openstreetmap.org/{z}/{x}/{y}.png" : ""),
};
export const mapUserAgent =
  "PocketQuests/1.0 (HoneyDrunkStudios; local development map)";
export function requireMapService(url: string) {
  if (!url || !url.startsWith("https://"))
    throw new Error("The map service is not configured for this build.");
  return url;
}
