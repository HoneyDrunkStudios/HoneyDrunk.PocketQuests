import type { Coordinate } from "../../api/places-contract";
export const mapHeight = 400;
export function worldPoint(point: Coordinate, zoom: number) {
  const size = 256 * 2 ** zoom;
  const sine = Math.sin(
    (Math.max(-85, Math.min(85, point.latitude)) * Math.PI) / 180,
  );
  return {
    x: ((point.longitude + 180) / 360) * size,
    y: (0.5 - Math.log((1 + sine) / (1 - sine)) / (4 * Math.PI)) * size,
  };
}
export function moveCenter(
  point: Coordinate,
  zoom: number,
  dx: number,
  dy: number,
): Coordinate {
  const world = worldPoint(point, zoom),
    size = 256 * 2 ** zoom;
  const x = (((world.x + dx) % size) + size) % size,
    y = Math.max(0, Math.min(size, world.y + dy));
  return {
    longitude: (x / size) * 360 - 180,
    latitude: Math.max(
      -85,
      Math.min(
        85,
        (Math.atan(Math.sinh(Math.PI * (1 - (2 * y) / size))) * 180) / Math.PI,
      ),
    ),
  };
}
export function mapPosition(
  point: Coordinate,
  center: Coordinate,
  zoom: number,
  width: number,
  height = mapHeight,
) {
  const a = worldPoint(point, zoom),
    b = worldPoint(center, zoom),
    size = 256 * 2 ** zoom;
  let dx = a.x - b.x;
  if (dx > size / 2) dx -= size;
  if (dx < -size / 2) dx += size;
  return { x: width / 2 + dx, y: height / 2 + a.y - b.y };
}
export function visibleTiles(
  center: Coordinate,
  zoom: number,
  width: number,
  height = mapHeight,
) {
  const point = worldPoint(center, zoom),
    n = 2 ** zoom;
  const left = point.x - width / 2,
    top = point.y - height / 2;
  const result = [];
  for (
    let y = Math.floor(top / 256);
    y <= Math.floor((top + height - 1) / 256);
    y++
  ) {
    if (y < 0 || y >= n) continue;
    for (
      let x = Math.floor(left / 256);
      x <= Math.floor((left + width - 1) / 256);
      x++
    )
      result.push({
        x: ((x % n) + n) % n,
        y,
        left: x * 256 - left,
        top: y * 256 - top,
      });
  }
  return result;
}
