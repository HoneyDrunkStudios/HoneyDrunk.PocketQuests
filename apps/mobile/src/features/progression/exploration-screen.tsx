import { useEffect, useMemo, useRef, useState } from "react";
import { Text, View } from "react-native";
import { createPlacesClient } from "../../api/places-client";
import type { SavedPlace } from "../../api/generated";
import {
  type Coordinate,
  type PointOfInterest,
  type SearchArea,
} from "../../api/places-contract";
import { placesConfig } from "../../config/places";
import { useAccountSnapshot } from "../../session/session";
import { Button, Card, Input, Label, styles } from "../../shared/ui";
import { ExplorationMap } from "./exploration-map";
import { ExplorationDetails } from "./exploration-details";
import { ExplorationResults } from "./exploration-results";
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
    } catch (error_) {
      if (!controller.signal.aborted)
        setError(
          error_ instanceof Error
            ? error_.message
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

  function renderContent() {
    if (showJournal)
      return (
        <DiscoveryJournal
          journal={discoveries}
          onShowMap={configured ? showSavedPlace : undefined}
        />
      );
    if (!configured)
      return (
        <Card>
          <Label>
            Map search isn’t available here yet. You can still open Saved
            places.
          </Label>
        </Card>
      );
    return (
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
              title={searchButtonTitle(loaded, moved)}
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
                The map moved. Search this area to replace the previous results.
              </Label>
            )}
            {loaded ? (
              <Label>
                {points.length} places found around the search center · about
                1.5 km radius. Some places may be missing from the map data.
              </Label>
            ) : (
              <Label>
                Showing your saved location. Find nearby places when you want to
                explore around it.
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
              Saved pins within about 1.5 km of the map center appear even when
              nearby search does not return them. Filled pins are saved; a check
              means you marked a visit.
            </Label>
          </>
        )}
        {selected && (
          <ExplorationDetails
            point={selected}
            center={center}
            saved={found.has(selected.id)}
            visited={visited.has(selected.id)}
            category={category}
            journal={discoveries}
            onSaved={() => setShowJournal(true)}
          />
        )}
        {(loaded || mapPoints.length > 0) && (
          <ExplorationResults
            visible={visible}
            limit={visibleLimit}
            filter={filter}
            selected={selected?.id}
            found={found}
            onFilter={(kind) => {
              setFilter(kind);
              setVisibleLimit(6);
            }}
            onSelect={(point) => {
              setSelected(point);
              setCenter(point);
              setMoved(true);
            }}
            onMore={() => setVisibleLimit((current) => current + 6)}
          />
        )}
      </>
    );
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
      {renderContent()}
    </View>
  );
}

function searchButtonTitle(loaded: boolean, moved: boolean) {
  if (!loaded) return "Find nearby places";
  return moved ? "Search this map area" : "Refresh places";
}
