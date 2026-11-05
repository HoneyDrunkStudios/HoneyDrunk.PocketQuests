import { useCallback, useEffect, useRef, useState } from "react";
import * as Crypto from "expo-crypto";
import type { SavedPlace, SavePlaceRequest } from "../../api/generated";
import type { PointOfInterest } from "../../api/places-contract";
import { useSessionActions } from "../../session/session";

export function useDiscoveries() {
  const { readDiscoveries, saveDiscovery, removeDiscovery } =
    useSessionActions();
  const [places, setPlaces] = useState<SavedPlace[]>([]);
  const [busy, setBusy] = useState(false);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");
  const lifetime = useRef<object | null>(null);
  const pending = useRef(false);
  const run = useCallback(async (work: () => Promise<void>) => {
    if (pending.current) return false;
    const owner = lifetime.current;
    pending.current = true;
    setBusy(true);
    setError("");
    try {
      await work();
      return lifetime.current === owner;
    } catch (error_) {
      if (lifetime.current === owner) {
        setError(
          (error_ instanceof Error
            ? error_.message
            : "The journal could not save.") +
            " Reload saved places before making another change.",
        );
        setReady(false);
      }
      return false;
    } finally {
      if (lifetime.current === owner) {
        pending.current = false;
        setBusy(false);
      }
    }
  }, []);
  const reload = useCallback(
    () =>
      run(async () => {
        const owner = lifetime.current;
        const rows = await readDiscoveries();
        if (lifetime.current === owner) {
          setPlaces(rows);
          setReady(true);
        }
      }),
    [readDiscoveries, run],
  );
  useEffect(() => {
    const owner = {};
    lifetime.current = owner;
    pending.current = false;
    void reload();
    return () => {
      if (lifetime.current === owner) lifetime.current = null;
    };
  }, [reload]);
  const save = (request: SavePlaceRequest) =>
    run(async () => {
      if (!ready) throw new Error("Reload saved places first.");
      const owner = lifetime.current;
      const row = await saveDiscovery(request);
      if (lifetime.current === owner)
        setPlaces((current) => [
          row,
          ...current.filter((item) => item.id !== row.id),
        ]);
    });
  const add = (point: PointOfInterest) =>
    save({
      id: Crypto.randomUUID(),
      placeKey: point.id,
      name: point.name,
      kind: point.kind,
      categoryId: point.categoryId,
      latitude: point.latitude,
      longitude: point.longitude,
      notes: "",
      visited: false,
      questOccurrenceId: null,
      expectedRevision: 0,
    });
  const remove = (place: SavedPlace) =>
    run(async () => {
      if (!ready) throw new Error("Reload saved places first.");
      const owner = lifetime.current;
      await removeDiscovery({ id: place.id, expectedRevision: place.revision });
      if (lifetime.current === owner)
        setPlaces((current) => current.filter((item) => item.id !== place.id));
    });
  return { places, busy, ready, error, reload, save, add, remove };
}
export type DiscoveryJournalState = ReturnType<typeof useDiscoveries>;
