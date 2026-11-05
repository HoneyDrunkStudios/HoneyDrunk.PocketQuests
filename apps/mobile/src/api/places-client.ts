import { Platform } from "react-native";
import {
  placesConfig,
  mapUserAgent,
  requireMapService,
} from "../config/places";
import {
  decodeAreas,
  decodePlaces,
  placesParameters,
  type Coordinate,
} from "./places-contract";

/** Public map transport deliberately carries no account token or cookies. */
export function createPlacesClient() {
  const cache = new Map<string, { at: number; value: unknown }>();
  const lastRequests = new Map<string, number>();
  async function read<T>(
    url: string,
    signal: AbortSignal,
    decode: (value: unknown) => T,
  ): Promise<T> {
    if (signal.aborted) throw new Error("Search cancelled.");
    const key = url;
    const cached = cache.get(key);
    if (cached && Date.now() - cached.at < 300000) return cached.value as T;
    const endpoint = url.split("?")[0];
    if (Date.now() - (lastRequests.get(endpoint) ?? 0) < 1500)
      throw new Error("Please wait a moment before searching again.");
    lastRequests.set(endpoint, Date.now());
    const controller = new AbortController();
    const abort = () => controller.abort();
    signal.addEventListener("abort", abort, { once: true });
    const timeout = setTimeout(abort, 20000);
    try {
      if (signal.aborted) throw new Error("Search cancelled.");
      const response = await fetch(requireMapService(url), {
        method: "GET",
        credentials: "omit",
        headers: {
          Accept: "application/json",
          ...(Platform.OS === "web" ? {} : { "User-Agent": mapUserAgent }),
        },
        signal: controller.signal,
      });
      if (!response.ok)
        throw new Error(
          response.status === 429
            ? "The map service is busy. Try again in a moment."
            : "The map service is unavailable. Your current map is still here.",
        );
      const text = await response.text();
      if (text.length > 1000000)
        throw new Error("The map response was too large.");
      let json: unknown;
      try {
        json = JSON.parse(text);
      } catch {
        throw new Error("The map service returned unreadable data.");
      }
      const value = decode(json);
      if (signal.aborted) throw new Error("Search cancelled.");
      if (cache.size >= 12) cache.delete(cache.keys().next().value!);
      cache.set(key, { at: Date.now(), value });
      return value;
    } finally {
      clearTimeout(timeout);
      signal.removeEventListener("abort", abort);
    }
  }
  return {
    async areas(query: string, signal: AbortSignal) {
      const term = query.trim();
      if (term.length < 2 || term.length > 100)
        throw new Error("Enter an area between 2 and 100 characters.");
      const url =
        requireMapService(placesConfig.geocoder) +
        "?" +
        new URLSearchParams({ q: term, limit: "5" });
      return read(url, signal, decodeAreas);
    },
    async points(center: Coordinate, signal: AbortSignal) {
      return read(
        requireMapService(placesConfig.points) + "?" + placesParameters(center),
        signal,
        (value) => decodePlaces(value, center),
      );
    },
  };
}
