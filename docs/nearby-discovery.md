# Real-world discovery inside Pocket Quests

Explore opens an embedded real-world map with selectable points of interest, place details and category quest links. It uses the same account-owned quest catalog as the rest of the app.

## Player flow

Search a town or neighborhood and choose an area, or explicitly tap Use my location for one foreground location request. Mounting the view never searches or requests permission. Denied permission leaves manual search available. Drag-to-pan applies on release; directional controls and zoom levels 12–18 are also available. Moving the map loads visible tiles but only Search this map area runs another place search.

Eight filters cover parks, gardens, libraries, museums, galleries, community centers, markets and viewpoints. Results contain up to 40 named places around a requested 1.5 km radius, ordered by straight-line distance from the last search center. Decoding rejects points beyond 1.8 km to allow a small provider geometry tolerance. Results are incomplete map data, not guaranteed public access or current hours. Known private/no-access tags are rejected when supplied; absent tags are not permission to enter.

Numbered markers and a paginated list select the same place. Details show its name, available address, distance and matching authenticated quest-category link. **Save this place** turns its marker gold and opens **Saved places**. The account journal supports private notes, Want to visit/Visited status, search, filters and a link to an accepted quest. Confirmed changes persist in SQL across sessions. Removing a place leaves its linked quest intact. The journal holds at most 100 places. These records do not verify presence, award XP, complete quests or unlock cosmetics. See [saved discoveries](saved-discoveries.md) for ownership, online requirements and recovery behavior.

## Integration boundaries

- `api/places-client.ts` owns public HTTP without account tokens or cookies. `places-contract.ts` validates unknown Photon GeoJSON, coordinates, IDs, categories and bounded text before rendering. Requests time out after 20 seconds and abort on view cleanup. Late responses cannot repopulate a new view.
- The component-local client caches up to 12 validated responses for five minutes, throttles each service endpoint to one request per 1.5 seconds and performs no automatic retries. Invalid responses are not cached. Errors retain the existing map and results.
- `features/progression/map-location.ts` owns Expo Location. Permission is requested only from the explicit button. No background tracking or location history is added. Search areas/coordinates and viewed tiles go to the map-data providers; account and quest data do not.
- `exploration-map.tsx` renders visible raster tiles and 48-point marker targets. Marker numbers stay fixed-size; accompanying labels and lists scale. The white center dot is explicitly not a live position. The list remains available for overlapping markers or failed tiles. Attribution is visible below the map.
- `map-geometry.ts` owns bounded Web Mercator projection and longitude wrapping. Only visible tiles are requested, using normal HTTP caching, native application identification and browser referrers. No offline packs, bulk downloads or speculative prefetching are implemented.
- Architecture lint admits HTTP only in designated adapters and Expo Location only in its location adapter, with rejection and acceptance fixtures.

## Development and release configuration

Development builds default to Photon area search (`https://photon.komoot.io/api/`), Photon nearby search (`https://photon.komoot.io/reverse`) and OpenStreetMap standard tiles. A live probe through the actual client returned 30 POIs around Asheville, North Carolina: a public sample area, not the user's location. The November 2 check made two public requests through the current adapter and both returned HTTP 200.

Release builds have no public demo service defaults. Choose appropriate service capacity, terms and attribution before configuring these public build-time settings:

- `EXPO_PUBLIC_GEOCODER_URL`: HTTPS Photon-compatible area-search endpoint without query parameters.
- `EXPO_PUBLIC_POI_URL`: HTTPS Photon-compatible nearby/reverse endpoint without query parameters.
- `EXPO_PUBLIC_MAP_TILES_URL`: HTTPS raster template containing `{z}`, `{x}` and `{y}`.

These are public settings, not secret storage. Different provider protocols require decoder changes; attribution and application identification also need review for the selected provider. Community development endpoints have no app-specific SLA. No paid service, account or infrastructure was provisioned. Missing release configuration shows an explicit unavailable state.

## Verification and remaining acceptance

Tests cover explicit loading, selection and quest links, discovery/removal, filtering, map movement without implicit place searches, empty results, permission denial, provider failure retaining results, duplicate suppression, unmount cancellation, malformed/private/duplicate/distant points, rate limits, caching, coordinate bounds and dateline projection.

Native configuration introspection includes Android coarse/fine foreground permission and the iOS when-in-use explanation. Background location, Always permission and motion activity are disabled. Browser and SQL acceptance evidence is recorded in the November 2 feature-batch report; component fixtures and web checks do not establish installed-device acceptance.

Installed Android/iOS GPS prompts, touch gestures, screen readers, native tile caching and coverage elsewhere remain to be exercised. Rebuild a development client whose native binary predates Expo Location. Saved places require the matching API and additive database update. Visit status remains a personal journal record.

## Primary references

- [Photon API](https://github.com/komoot/photon/blob/master/docs/api-v1.md): area and nearby searches.
- [Photon project](https://github.com/komoot/photon): public demo service limits.
- [OpenStreetMap tile policy](https://operations.osmfoundation.org/policies/tiles/): attribution, identification, caching and download limits.
- [Expo SDK 58 Location](https://docs.expo.dev/versions/v58.0.0/sdk/location/): foreground permissions and native configuration.
