import { useMemo, useState } from "react";
import {
  Image,
  PanResponder,
  Platform,
  Pressable,
  Text,
  View,
} from "react-native";
import Ionicons from "@expo/vector-icons/Ionicons";
import { Link } from "expo-router";
import type { Coordinate, PointOfInterest } from "../../api/places-contract";
import { placesConfig, mapUserAgent } from "../../config/places";
import { Label, useAppTheme } from "../../shared/ui";
import {
  mapHeight,
  mapPosition,
  moveCenter,
  visibleTiles,
} from "./map-geometry";

const moveByAction = new Map<string, readonly [number, number]>([
  ["West", [-160, 0]],
  ["East", [160, 0]],
  ["North", [0, -160]],
  ["South", [0, 160]],
]);

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
  fill = false,
  controlsTop,
}: Readonly<{
  fill?: boolean;
  controlsTop?: number;
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
  const { colors, styles, isDark } = useAppTheme();
  const [width, setWidth] = useState(320);
  const [height, setHeight] = useState(mapHeight);
  const [drag, setDrag] = useState({ x: 0, y: 0, scale: 1 });
  const [tileError, setTileError] = useState(false);
  const centerCovered = points.some((point) => {
    const pixel = mapPosition(point, center, zoom, width, height);
    return (
      Math.abs(pixel.x - width / 2) < 24 && Math.abs(pixel.y - height / 2) < 24
    );
  });
  const pan = useMemo(() => {
    const gesture = { distance: 0, x: 0, y: 0, scale: 1 };
    return PanResponder.create({
      onStartShouldSetPanResponder: (event) =>
        event.nativeEvent.touches.length > 1,
      onMoveShouldSetPanResponder: (_, movement) =>
        Math.abs(movement.dx) + Math.abs(movement.dy) > 8,
      onPanResponderGrant: (event) => {
        const touches = event.nativeEvent.touches;
        Object.assign(gesture, {
          distance:
            touches.length > 1
              ? Math.hypot(
                  touches[0].pageX - touches[1].pageX,
                  touches[0].pageY - touches[1].pageY,
                )
              : 0,
          x: 0,
          y: 0,
          scale: 1,
        });
      },
      onPanResponderMove: (event, movement) => {
        const touches = event.nativeEvent.touches;
        if (touches.length > 1) {
          const distance = Math.hypot(
            touches[0].pageX - touches[1].pageX,
            touches[0].pageY - touches[1].pageY,
          );
          if (!gesture.distance) gesture.distance = distance;
          gesture.scale = Math.max(
            0.5,
            Math.min(2, distance / Math.max(1, gesture.distance)),
          );
        } else if (!gesture.distance) {
          gesture.x = movement.dx;
          gesture.y = movement.dy;
        }
        setDrag({
          x: gesture.x,
          y: gesture.y,
          scale: gesture.scale,
        });
      },
      onPanResponderRelease: () => {
        setTileError(false);
        if (gesture.distance)
          onZoom(
            Math.max(
              12,
              Math.min(18, zoom + Math.round(Math.log2(gesture.scale))),
            ),
          );
        else onMove(moveCenter(center, zoom, -gesture.x, -gesture.y));
        setDrag({ x: 0, y: 0, scale: 1 });
      },
      onPanResponderTerminate: () => setDrag({ x: 0, y: 0, scale: 1 }),
    });
  }, [center, zoom, onMove, onZoom]);
  return (
    <View style={fill ? { position: "absolute", inset: 0 } : { gap: 8 }}>
      <View
        testID="exploration-map"
        onLayout={(event) => {
          setWidth(Math.max(1, event.nativeEvent.layout.width));
          setHeight(Math.max(1, event.nativeEvent.layout.height));
        }}
        accessibilityLabel="Exploration map. Drag to move, pinch to zoom."
        accessibilityActions={[
          { name: "increment", label: "Zoom in" },
          { name: "decrement", label: "Zoom out" },
          ...["North", "South", "West", "East"].map((direction) => ({
            name: direction,
            label: `Move ${direction.toLowerCase()}`,
          })),
        ]}
        onAccessibilityAction={(event) => {
          const action = event.nativeEvent.actionName;
          if (action === "increment") onZoom(Math.min(18, zoom + 1));
          else if (action === "decrement") onZoom(Math.max(12, zoom - 1));
          else {
            const [x, y] = moveByAction.get(action) ?? [0, 0];
            onMove(moveCenter(center, zoom, x, y));
          }
        }}
        style={{
          height: fill ? "100%" : mapHeight,
          width: "100%",
          overflow: "hidden",
          borderRadius: fill ? 0 : 18,
          borderWidth: fill ? 0 : 2,
          borderColor: colors.gold,
          backgroundColor: colors.paper,
        }}
      >
        <View {...pan.panHandlers} style={{ position: "absolute", inset: 0 }}>
          <View
            pointerEvents="none"
            style={{
              position: "absolute",
              inset: 0,
              transform: [
                { translateX: drag.x },
                { translateY: drag.y },
                { scale: drag.scale },
              ],
            }}
          >
            {visibleTiles(center, zoom, width, height).map((tile) => (
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
                  opacity: isDark ? 0.45 : 0.82,
                }}
              />
            ))}
          </View>
          {points.map((point, index) => {
            const pixel = mapPosition(point, center, zoom, width, height);
            if (
              pixel.x < 24 ||
              pixel.x > width - 24 ||
              pixel.y < 24 ||
              pixel.y > height - 24
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
                  left:
                    width / 2 +
                    (pixel.x - width / 2) * drag.scale +
                    drag.x -
                    24,
                  top:
                    height / 2 +
                    (pixel.y - height / 2) * drag.scale +
                    drag.y -
                    24,
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
      <View
        style={{
          position: "absolute",
          right: 16,
          top: controlsTop ?? (fill ? 235 : 16),
          gap: 8,
        }}
      >
        {(["in", "out"] as const).map((direction) => (
          <Pressable
            key={direction}
            accessibilityRole="button"
            accessibilityLabel={`Zoom ${direction}`}
            accessibilityState={{
              disabled: direction === "in" ? zoom >= 18 : zoom <= 12,
            }}
            disabled={direction === "in" ? zoom >= 18 : zoom <= 12}
            onPress={() => {
              setTileError(false);
              onZoom(
                direction === "in"
                  ? Math.min(18, zoom + 1)
                  : Math.max(12, zoom - 1),
              );
            }}
            style={{
              width: 48,
              height: 48,
              borderRadius: 16,
              backgroundColor: colors.paper,
              alignItems: "center",
              justifyContent: "center",
              borderWidth: 1,
              borderColor: colors.gold,
            }}
          >
            <Ionicons
              name={direction === "in" ? "add" : "remove"}
              size={24}
              color={colors.gold}
              accessible={false}
            />
          </Pressable>
        ))}
      </View>
      {tileError && (
        <Label
          style={{
            position: "absolute",
            top: 52,
            left: 16,
            right: 76,
            backgroundColor: colors.paper,
            padding: 8,
          }}
        >
          Some map tiles could not load. Place details and the list below are
          still available.
        </Label>
      )}
      <Link
        href="https://www.openstreetmap.org/copyright"
        target="_blank"
        style={[
          styles.text,
          {
            position: "absolute",
            top: 8,
            left: 12,
            color: "#203A25",
            backgroundColor: "#E8DCC4",
            fontSize: 11,
            minHeight: 32,
            padding: 6,
            borderRadius: 6,
          },
        ]}
      >
        Map data © OpenStreetMap contributors · ODbL
      </Link>
    </View>
  );
}
