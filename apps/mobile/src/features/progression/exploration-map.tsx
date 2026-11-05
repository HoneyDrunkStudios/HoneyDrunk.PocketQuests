import { useMemo, useState } from "react";
import {
  Image,
  PanResponder,
  Platform,
  Pressable,
  Text,
  View,
} from "react-native";
import { Link } from "expo-router";
import type { Coordinate, PointOfInterest } from "../../api/places-contract";
import { placesConfig, mapUserAgent } from "../../config/places";
import { Button, Label, colors, styles } from "../../shared/ui";
import {
  mapHeight,
  mapPosition,
  moveCenter,
  visibleTiles,
} from "./map-geometry";

export function ExplorationMap({
  center,
  zoom,
  points,
  discovered,
  visited,
  selected,
  onMove,
  onZoom,
  onSelect,
}: Readonly<{
  center: Coordinate;
  zoom: number;
  points: PointOfInterest[];
  discovered: Set<string>;
  visited?: Set<string>;
  selected?: string;
  onMove(point: Coordinate): void;
  onZoom(zoom: number): void;
  onSelect(point: PointOfInterest): void;
}>) {
  const [width, setWidth] = useState(320);
  const [tileError, setTileError] = useState(false);
  const centerCovered = points.some((point) => {
    const pixel = mapPosition(point, center, zoom, width);
    return (
      Math.abs(pixel.x - width / 2) < 24 &&
      Math.abs(pixel.y - mapHeight / 2) < 24
    );
  });
  const pan = useMemo(
    () =>
      PanResponder.create({
        onMoveShouldSetPanResponder: (_, gesture) =>
          Math.abs(gesture.dx) + Math.abs(gesture.dy) > 12,
        onPanResponderRelease: (_, gesture) => {
          setTileError(false);
          onMove(moveCenter(center, zoom, -gesture.dx, -gesture.dy));
        },
      }),
    [center, zoom, onMove],
  );
  return (
    <View style={{ gap: 8 }}>
      <View
        testID="exploration-map"
        onLayout={(event) =>
          setWidth(Math.max(240, Math.min(780, event.nativeEvent.layout.width)))
        }
        style={{
          height: mapHeight,
          width: "100%",
          overflow: "hidden",
          borderRadius: 18,
          borderWidth: 2,
          borderColor: colors.gold,
          backgroundColor: colors.card,
        }}
      >
        <View {...pan.panHandlers} style={{ position: "absolute", inset: 0 }}>
          {visibleTiles(center, zoom, width).map((tile) => (
            <Image
              key={`${zoom}/${tile.x}/${tile.y}`}
              accessible={false}
              pointerEvents="none"
              source={{
                uri: placesConfig.tiles
                  .replace("{z}", String(zoom))
                  .replace("{x}", String(tile.x))
                  .replace("{y}", String(tile.y)),
                ...(Platform.OS === "web"
                  ? {}
                  : { headers: { "User-Agent": mapUserAgent } }),
                cache: "default",
              }}
              onError={() => setTileError(true)}
              style={{
                position: "absolute",
                width: 256,
                height: 256,
                left: tile.left,
                top: tile.top,
              }}
            />
          ))}
          {points.map((point, index) => {
            const pixel = mapPosition(point, center, zoom, width);
            if (
              pixel.x < 24 ||
              pixel.x > width - 24 ||
              pixel.y < 24 ||
              pixel.y > mapHeight - 24
            )
              return null;
            const found = discovered.has(point.id);
            let discoveryLabel = "undiscovered";
            if (found)
              discoveryLabel = visited?.has(point.id)
                ? "saved, visited"
                : "saved, want to visit";
            return (
              <Pressable
                key={point.id}
                accessibilityRole="button"
                accessibilityLabel={`${index + 1}. ${point.name}, ${point.kind}, ${discoveryLabel}`}
                accessibilityState={{ selected: point.id === selected }}
                onPress={() => onSelect(point)}
                style={{
                  position: "absolute",
                  left: pixel.x - 24,
                  top: pixel.y - 24,
                  width: 48,
                  height: 48,
                  alignItems: "center",
                  justifyContent: "center",
                  zIndex: point.id === selected ? 2 : 1,
                }}
              >
                <View
                  style={{
                    width: 36,
                    height: 36,
                    borderRadius: 18,
                    borderWidth: point.id === selected ? 3 : 2,
                    borderColor:
                      point.id === selected ? "#FFFFFF" : colors.gold,
                    backgroundColor: found ? colors.gold : colors.paper,
                    alignItems: "center",
                    justifyContent: "center",
                  }}
                >
                  <Text
                    allowFontScaling={false}
                    style={{
                      fontWeight: "700",
                      fontSize: 16,
                      color: found ? colors.onGold : colors.ink,
                    }}
                  >
                    {visited?.has(point.id) ? "✓" : index + 1}
                  </Text>
                </View>
              </Pressable>
            );
          })}
        </View>
        {!centerCovered && (
          <View
            testID="map-center-indicator"
            pointerEvents="none"
            style={{
              position: "absolute",
              left: "50%",
              top: "50%",
              width: 10,
              height: 10,
              marginLeft: -5,
              marginTop: -5,
              borderWidth: 2,
              borderColor: colors.paper,
              backgroundColor: "#FFFFFF",
              borderRadius: 5,
            }}
          />
        )}
      </View>
      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
        <Button
          title="Zoom in"
          secondary
          disabled={zoom >= 18}
          onPress={() => {
            setTileError(false);
            onZoom(Math.min(18, zoom + 1));
          }}
        />
        <Button
          title="Zoom out"
          secondary
          disabled={zoom <= 12}
          onPress={() => {
            setTileError(false);
            onZoom(Math.max(12, zoom - 1));
          }}
        />
        <Button
          title="North"
          secondary
          onPress={() => onMove(moveCenter(center, zoom, 0, -160))}
        />
        <Button
          title="South"
          secondary
          onPress={() => onMove(moveCenter(center, zoom, 0, 160))}
        />
        <Button
          title="West"
          secondary
          onPress={() => onMove(moveCenter(center, zoom, -160, 0))}
        />
        <Button
          title="East"
          secondary
          onPress={() => onMove(moveCenter(center, zoom, 160, 0))}
        />
      </View>
      <Label>
        Drag to explore. Gold markers are discoveries. The white dot marks the
        map center when no place marker covers it. It is not your live position.
      </Label>
      {tileError && (
        <Label>
          Some map tiles could not load. Place details and the list below are
          still available.
        </Label>
      )}
      <Link
        href="https://www.openstreetmap.org/copyright"
        target="_blank"
        style={[
          styles.text,
          { color: colors.gold, minHeight: 48, paddingVertical: 12 },
        ]}
      >
        Map data © OpenStreetMap contributors · ODbL
      </Link>
    </View>
  );
}
