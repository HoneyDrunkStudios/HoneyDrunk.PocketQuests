import type { SavedPlace } from "../../api/generated";
import {
  distanceMeters,
  type Coordinate,
  type PointOfInterest,
} from "../../api/places-contract";

/** Explicitly project public map fields; private notes and quest links never leave the journal. */
export function savedMapPoint(
  place: SavedPlace,
  center: Coordinate,
): PointOfInterest {
  return {
    id: place.placeKey,
    name: place.name,
    kind: place.kind,
    categoryId: place.categoryId,
    latitude: place.latitude,
    longitude: place.longitude,
    address: "",
    distanceMeters: distanceMeters(place, center),
  };
}

export function mergeSavedMapPoints(
  points: PointOfInterest[],
  saved: SavedPlace[],
  center: Coordinate,
) {
  const merged = new Map(
    points.map((point) => [
      point.id,
      { ...point, distanceMeters: distanceMeters(point, center) },
    ]),
  );
  for (const place of saved) {
    const point = savedMapPoint(place, center);
    if (point.distanceMeters > 1500) continue;
    const existing = merged.get(point.id);
    merged.set(point.id, { ...point, address: existing?.address ?? "" });
  }
  return [...merged.values()];
}
