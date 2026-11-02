import { useEffect, useMemo, useRef, useState } from "react";
import { Text, View } from "react-native";
import { Link } from "expo-router";
import { createPlacesClient } from "../../api/places-client";
import type { SavedPlace } from "../../api/generated";
import {
  placeKinds,
  distanceMeters,
  type Coordinate,
  type PointOfInterest,
  type SearchArea,
} from "../../api/places-contract";
import { placesConfig } from "../../config/places";
import { useAccountSnapshot } from "../../session/session";
import { Button, Card, Input, Label, colors, styles } from "../../shared/ui";
import { ExplorationMap } from "./exploration-map";
import { locateOnce } from "./map-location";
import { useDiscoveries } from "./use-discoveries";
import { DiscoveryJournal } from "./discovery-journal";
import { mergeSavedMapPoints, savedMapPoint } from "./saved-map-points";

export function ExplorationScreen() {
  const { catalog } = useAccountSnapshot();
  const client = useMemo(() => createPlacesClient(), []);
  const active = useRef<AbortController | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [query, setQuery] = useState("");
  const [areas, setAreas] = useState<SearchArea[] | null>(null);
  const [center, setCenter] = useState<Coordinate | null>(null);
  const [areaName, setAreaName] = useState("");
  const [zoom, setZoom] = useState(14);
  const [points, setPoints] = useState<PointOfInterest[]>([]);
  const [loaded, setLoaded] = useState(false);
  const [moved, setMoved] = useState(false);
  const [selected, setSelected] = useState<PointOfInterest | null>(null);
  const [filter, setFilter] = useState("All");
  const discoveries = useDiscoveries();
  const [showJournal, setShowJournal] = useState(false);
  const [savedOnly, setSavedOnly] = useState(false);
  const [choosingArea, setChoosingArea] = useState(true);
  const [visibleLimit, setVisibleLimit] = useState(6);
  useEffect(
    () => () => {
      active.current?.abort();
      active.current = null;
    },
    [],
  );
  const found = useMemo(
    () => new Set(discoveries.places.map((point) => point.placeKey)),
    [discoveries.places],
  );
  const visited = useMemo(
    () =>
      new Set(
        discoveries.places
          .filter((place) => place.visitedAt)
          .map((place) => place.placeKey),
      ),
    [discoveries.places],
  );
  const mapPoints = useMemo(
    () =>
      center ? mergeSavedMapPoints(points, discoveries.places, center) : points,
    [points, discoveries.places, center],
  );
  const visible = mapPoints.filter(
    (point) =>
      (filter === "All" || point.kind === filter) &&
      (!savedOnly || found.has(point.id)),
  );
  const category = catalog?.categories.find(
    (item) => item.id === selected?.categoryId,
  );
  const configured =
    !!placesConfig.tiles && !!placesConfig.geocoder && !!placesConfig.points;

  async function run(work: (signal: AbortSignal) => Promise<void>) {
    if (active.current) return;
    const controller = new AbortController();
    active.current = controller;
    setBusy(true);
    setError("");
    try {
      await work(controller.signal);
    } catch (failure) {
      if (!controller.signal.aborted)
        setError(
          failure instanceof Error
            ? failure.message
            : "The map could not load. Please try again.",
        );
    } finally {
      if (active.current === controller) {
        active.current = null;
        setBusy(false);
      }
    }
  }
  async function loadPoints(
    point: Coordinate,
    name: string,
    signal: AbortSignal,
  ) {
    const result = await client.points(point, signal);
    if (signal.aborted) return;
    setCenter(point);
    setAreaName(name);
    setPoints(result);
    setLoaded(true);
    setMoved(false);
    setSelected(null);
    setFilter("All");
    setAreas(null);
    setChoosingArea(false);
    setVisibleLimit(6);
  }
  function move(point: Coordinate) {
    setCenter(point);
    setMoved(true);
  }
  function showSavedPlace(place: SavedPlace) {
    active.current?.abort();
    active.current = null;
    setBusy(false);
    setError("");
    setCenter({ latitude: place.latitude, longitude: place.longitude });
    setAreaName(place.name);
    setZoom(16);
    setPoints([]);
    setLoaded(false);
    setMoved(false);
    setSelected(savedMapPoint(place, place));
    setFilter("All");
    setSavedOnly(false);
    setAreas(null);
    setChoosingArea(false);
    setVisibleLimit(6);
    setShowJournal(false);
  }

  return (
    <View testID="exploration" style={{ gap: 16 }}>
      <Text accessibilityRole="header" style={styles.title}>
        Explore nearby
      </Text>
      <Label>
        Explore real places on your map. Reveal points of interest, collect
        discoveries and bring a quest along.
      </Label>
      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
        <Button
          title="Map"
          secondary={showJournal}
          onPress={() => setShowJournal(false)}
        />
        <Button
          title="Saved places"
          secondary={!showJournal}
          onPress={() => setShowJournal(true)}
        />
      </View>
      {showJournal ? (
        <DiscoveryJournal
          journal={discoveries}
          onShowMap={configured ? showSavedPlace : undefined}
        />
      ) : !configured ? (
        <Card>
          <Label>
            Map search isn’t available here yet. You can still open Saved
            places.
          </Label>
        </Card>
      ) : (
        <>
          {choosingArea && (
            <Card>
              <Text accessibilityRole="header" style={styles.subtitle}>
                Choose your starting point
              </Text>
              <Input
                accessibilityLabel="Search area"
                placeholder="Town or neighborhood"
                value={query}
                maxLength={100}
                editable={!busy}
                onChangeText={(value) => {
                  setQuery(value);
                  setAreas(null);
                }}
              />
              <Button
                title="Find an area"
                disabled={busy || query.trim().length < 2}
                onPress={() =>
                  void run(async (signal) => {
                    const result = await client.areas(query, signal);
                    if (!signal.aborted) setAreas(result);
                  })
                }
              />
              <Button
                title="Use my location"
                secondary
                disabled={busy}
                onPress={() =>
                  void run(async (signal) => {
                    const point = await locateOnce(signal);
                    if (!signal.aborted)
                      await loadPoints(
                        point,
                        "Near your selected location",
                        signal,
                      );
                  })
                }
              />
              <Label>
                Searches send the area or chosen map center to our map-data
                providers. Location is requested once when you tap; there is no
                background tracking.
              </Label>
              {areas?.map((area) => (
                <Button
                  key={area.id}
                  title={area.name}
                  secondary
                  disabled={busy}
                  onPress={() =>
                    void run((signal) => loadPoints(area, area.name, signal))
                  }
                />
              ))}
              {areas?.length === 0 && (
                <Label>
                  No matching areas. Try including a region or country.
                </Label>
              )}
            </Card>
          )}
          {!choosingArea && (
            <Button
              title="Change area"
              secondary
              disabled={busy}
              onPress={() => setChoosingArea(true)}
            />
          )}
          {busy && (
            <Label accessibilityLiveRegion="polite">
              Charting your surroundings…
            </Label>
          )}
          {!!error && (
            <Text accessibilityRole="alert" style={styles.text}>
              {error}
            </Text>
          )}
          {center && (
            <>
              <Text accessibilityRole="header" style={styles.subtitle}>
                {areaName}
              </Text>
              <ExplorationMap
                center={center}
                zoom={zoom}
                points={visible}
                discovered={found}
                visited={visited}
                selected={selected?.id}
                onMove={move}
                onZoom={setZoom}
                onSelect={setSelected}
              />
              <Button
                title={
                  !loaded
                    ? "Find nearby places"
                    : moved
                      ? "Search this map area"
                      : "Refresh places"
                }
                disabled={busy}
                onPress={() =>
                  void run((signal) =>
                    loadPoints(
                      center,
                      moved ? "Selected map area" : areaName,
                      signal,
                    ),
                  )
                }
              />
              {moved && (
                <Label>
                  The map moved. Search this area to replace the previous
                  results.
                </Label>
              )}
              {loaded ? (
                <Label>
                  {points.length} places found around the search center · about
                  1.5 km radius. Some places may be missing from the map data.
                </Label>
              ) : (
                <Label>
                  Showing your saved location. Find nearby places when you want
                  to explore around it.
                </Label>
              )}
              <Button
                title={
                  savedOnly ? "Show all map places" : "Show saved places only"
                }
                secondary
                onPress={() => {
                  setSavedOnly((value) => !value);
                  setVisibleLimit(6);
                }}
              />
              <Label>
                Saved pins within about 1.5 km of the map center appear even
                when nearby search does not return them. Filled pins are saved;
                a check means you marked a visit.
              </Label>
            </>
          )}
          {selected && (
            <Card>
              <Label>
                {found.has(selected.id)
                  ? visited.has(selected.id)
                    ? "Saved · Visited"
                    : "Saved · Want to visit"
                  : "Undiscovered"}{" "}
                · {selected.kind}
              </Label>
              <Text accessibilityRole="header" style={styles.subtitle}>
                {selected.name}
              </Text>
              <Label>
                About{" "}
                {(
                  (center
                    ? distanceMeters(selected, center)
                    : selected.distanceMeters) / 1000
                ).toFixed(1)}{" "}
                km from the map center, straight-line distance.
              </Label>
              {!!selected.address && <Label>{selected.address}</Label>}
              <Button
                title={
                  found.has(selected.id)
                    ? "Saved in your journal"
                    : "Save place to journal"
                }
                secondary={found.has(selected.id)}
                disabled={
                  found.has(selected.id) ||
                  !discoveries.ready ||
                  discoveries.busy ||
                  discoveries.places.length >= 100
                }
                onPress={() => {
                  void discoveries.add(selected).then((saved) => {
                    if (saved) setShowJournal(true);
                  });
                }}
              />
              {!discoveries.ready && (
                <Label>
                  Open Saved places to load or reconnect your journal before
                  saving.
                </Label>
              )}
              {category && (
                <Link
                  href={{
                    pathname: "/quest/discover/[categoryId]",
                    params: { categoryId: category.id },
                  }}
                  style={[
                    styles.text,
                    { color: colors.gold, paddingVertical: 14, minHeight: 48 },
                  ]}
                >
                  Browse {category.name} quests
                </Link>
              )}
              <Label>
                Save this place to keep notes, mark a visit and link a quest in
                your journal below. It does not verify a visit or award XP.
                Check local access and opening hours before visiting.
              </Label>
            </Card>
          )}
          {(loaded || mapPoints.length > 0) && (
            <>
              <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
                {["All", ...placeKinds.map((kind) => kind.name)].map((kind) => (
                  <View key={kind} style={{ maxWidth: "100%" }}>
                    <Button
                      title={kind}
                      secondary={filter !== kind}
                      accessibilityLabel={`${filter === kind ? "Selected: " : ""}${kind} places`}
                      onPress={() => {
                        setFilter(kind);
                        setVisibleLimit(6);
                      }}
                    />
                  </View>
                ))}
              </View>
              <Label>
                {visible.length} places shown. Choose from the list if markers
                overlap.
              </Label>
              {visible.slice(0, visibleLimit).map((point, index) => (
                <Button
                  key={point.id}
                  title={`${index + 1}. ${point.name} · ${found.has(point.id) ? "Discovered" : point.kind}`}
                  secondary={selected?.id !== point.id}
                  onPress={() => {
                    setSelected(point);
                    setCenter(point);
                    setMoved(true);
                  }}
                />
              ))}
              {visible.length > visibleLimit && (
                <Button
                  title="Show more places"
                  secondary
                  onPress={() => setVisibleLimit((current) => current + 6)}
                />
              )}
              {visible.length === 0 && (
                <Label>
                  No places match here. Try All, another area, or a different
                  map center.
                </Label>
              )}
            </>
          )}
        </>
      )}
    </View>
  );
}
