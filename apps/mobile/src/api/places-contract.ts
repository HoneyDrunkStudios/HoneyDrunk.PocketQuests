export type Coordinate = { latitude: number; longitude: number };
export type SearchArea = Coordinate & { id: string; name: string };
export type PointOfInterest = SearchArea & {
  kind: string;
  categoryId: string;
  distanceMeters: number;
  address: string;
};

export const placeKinds = [
  { name: "Parks", tag: "leisure", value: "park", categoryId: "c07" },
  { name: "Gardens", tag: "leisure", value: "garden", categoryId: "c07" },
  { name: "Libraries", tag: "amenity", value: "library", categoryId: "c02" },
  { name: "Museums", tag: "tourism", value: "museum", categoryId: "c02" },
  { name: "Art", tag: "tourism", value: "gallery", categoryId: "c03" },
  {
    name: "Community",
    tag: "amenity",
    value: "community_centre",
    categoryId: "c05",
  },
  { name: "Markets", tag: "amenity", value: "marketplace", categoryId: "c06" },
  { name: "Viewpoints", tag: "tourism", value: "viewpoint", categoryId: "c10" },
] as const;

export function validCoordinate(value: Coordinate) {
  return (
    Number.isFinite(value.latitude) &&
    Math.abs(value.latitude) <= 85 &&
    Number.isFinite(value.longitude) &&
    Math.abs(value.longitude) <= 180
  );
}

export function distanceMeters(a: Coordinate, b: Coordinate) {
  const radians = Math.PI / 180;
  const lat = Math.sin(((b.latitude - a.latitude) * radians) / 2);
  const lon = Math.sin(((b.longitude - a.longitude) * radians) / 2);
  const value =
    lat * lat +
    Math.cos(a.latitude * radians) * Math.cos(b.latitude * radians) * lon * lon;
  return Math.round(6371000 * 2 * Math.asin(Math.sqrt(Math.min(1, value))));
}

function record(value: unknown): Record<string, unknown> {
  return value !== null && typeof value === "object" && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}
function label(value: unknown, limit = 180) {
  return typeof value === "string" ? value.trim().slice(0, limit) : "";
}

export function decodeAreas(value: unknown): SearchArea[] {
  const features = record(value).features;
  if (!Array.isArray(features))
    throw new Error("Area search returned an unreadable response.");
  return features.slice(0, 5).flatMap((item, index) => {
    const feature = record(item),
      geometry = record(feature.geometry),
      props = record(feature.properties);
    const coordinates = geometry.coordinates;
    if (!Array.isArray(coordinates) || geometry.type !== "Point") return [];
    const point = { longitude: coordinates[0], latitude: coordinates[1] };
    if (
      typeof point.latitude !== "number" ||
      typeof point.longitude !== "number" ||
      !validCoordinate(point as Coordinate)
    )
      return [];
    const name = [
      ...new Set(
        [props.name, props.city, props.state, props.country]
          .map((value) => label(value))
          .filter(Boolean),
      ),
    ].join(", ");
    return name
      ? [{ ...(point as Coordinate), id: `area-${index}`, name }]
      : [];
  });
}

export function decodePlaces(
  value: unknown,
  center: Coordinate,
): PointOfInterest[] {
  const response = record(value);
  if (!Array.isArray(response.features))
    throw new Error("Places could not be read. Try again later.");
  const seen = new Set<string>();
  return response.features
    .slice(0, 100)
    .flatMap((raw) => {
      const item = record(raw),
        tags = record(item.properties),
        geometry = record(item.geometry);
      const coordinates = geometry.coordinates;
      if (geometry.type !== "Point" || !Array.isArray(coordinates)) return [];
      const coordinate = {
        latitude: coordinates[1],
        longitude: coordinates[0],
      };
      const kind = placeKinds.find(
        (kind) => tags.osm_key === kind.tag && tags.osm_value === kind.value,
      );
      const type = (
        { N: "node", W: "way", R: "relation" } as Record<string, string>
      )[String(tags.osm_type)];
      if (
        !kind ||
        ["private", "no"].includes(
          String(record(tags.extra).access ?? tags.access),
        ) ||
        !type ||
        typeof tags.osm_id !== "number" ||
        !Number.isSafeInteger(tags.osm_id) ||
        tags.osm_id <= 0 ||
        typeof coordinate.latitude !== "number" ||
        typeof coordinate.longitude !== "number" ||
        !validCoordinate(coordinate as Coordinate)
      )
        return [];
      const name = label(tags.name),
        id = `${type}/${tags.osm_id}`;
      const distance = distanceMeters(center, coordinate as Coordinate);
      if (!name || seen.has(id) || distance > 1800) return [];
      seen.add(id);
      const address = [tags.housenumber, tags.street, tags.city]
        .map((value) => label(value, 100))
        .filter(Boolean)
        .join(" ");
      return [
        {
          ...(coordinate as Coordinate),
          id,
          name,
          kind: kind.name,
          categoryId: kind.categoryId,
          distanceMeters: distance,
          address,
        },
      ];
    })
    .sort((a, b) => a.distanceMeters - b.distanceMeters)
    .slice(0, 40);
}

export function placesParameters(center: Coordinate) {
  if (!validCoordinate(center)) throw new Error("Choose a valid map area.");
  const query = new URLSearchParams({
    lat: center.latitude.toFixed(4),
    lon: center.longitude.toFixed(4),
    radius: "1.5",
    limit: "40",
  });
  for (const kind of placeKinds)
    query.append("osm_tag", `${kind.tag}:${kind.value}`);
  return query;
}
