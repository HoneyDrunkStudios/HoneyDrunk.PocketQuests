import { validCoordinate, type Coordinate } from "../../api/places-contract";
export async function locateOnce(signal?: AbortSignal): Promise<Coordinate> {
  const requireActive = () => {
    if (signal?.aborted) throw new Error("Location search cancelled.");
  };
  requireActive();
  let Location: typeof import("expo-location");
  try {
    Location = await import("expo-location");
  } catch {
    throw new Error(
      "Location is unavailable in this app build. Search for a town or neighborhood instead.",
    );
  }
  requireActive();
  const permission = await Location.requestForegroundPermissionsAsync();
  requireActive();
  if (permission.status !== "granted")
    throw new Error(
      "Location access is off. Search for a town or neighborhood instead.",
    );
  let timeout: ReturnType<typeof setTimeout> | undefined;
  try {
    const result = await Promise.race([
      Location.getCurrentPositionAsync({
        accuracy: Location.Accuracy.Balanced,
      }),
      new Promise<never>((_, reject) => {
        timeout = setTimeout(
          () =>
            reject(
              new Error("Location took too long. Try again or search an area."),
            ),
          20000,
        );
      }),
    ]);
    requireActive();
    const point = {
      latitude: result.coords.latitude,
      longitude: result.coords.longitude,
    };
    if (!validCoordinate(point))
      throw new Error("This location is outside the supported map area.");
    return point;
  } finally {
    clearTimeout(timeout);
  }
}
