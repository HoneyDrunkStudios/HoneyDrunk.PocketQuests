# Saved places and quest links

Explore now has **Map** and **Saved places** views. Saving a map result adds an account-owned journal entry. Players can keep private notes, switch between Want to visit and Visited, search/filter their journal, and link one accepted quest. A successful save survives reopening the app and API restarts. The journal is limited to 100 places per account.

Visit status is a player record, not verified physical presence. Saving, visiting and linking do not complete quests, grant XP or unlock rewards. The linked quest opens its existing detail/completion flow. Removing a journal entry deletes its notes and visit record but leaves the quest intact.

## Storage and ownership

The authoritative schema contract defines `pocketquests.SavedPlace`; generated DDL, the DACPAC and EF mapping agree on its shape. SQL is the source of truth. There is no replacement local-storage database.

| Concern | Contract |
| --- | --- |
| Row meaning | One current saved map place owned by one account |
| Primary key | Application-generated UUID `Id` |
| Alternate keys | `(AccountId, Id)` and `(AccountId, PlaceKey)` |
| Relationships | Account, category, and optional same-account quest occurrence |
| Place identity | Validated OSM `node/`, `way/` or `relation/` identifier |
| Mutable content | Name, category/kind, coordinates, notes, visit status and quest link |
| Bounds | 180-character name, 1,000-character notes, validated coordinates and kind/category pair |
| Concurrency | Expected integer revision under the account command lock; SQL rowversion also mapped |
| History | Current value only; no historical copies of private notes |
| Audit | Save/remove action and account target; no place name, coordinates or notes in action audit |
| Retention | Until explicitly removed or account erasure; recovery follows the account lifecycle |
| Export | `savedPlaces` in JSON and `discoveries.csv` in CSV ZIP; CSV formula neutralization retained |

Queries constrain AccountId in SQL. Composite foreign keys independently reject linking another account's occurrence. The service additionally requires the occurrence to have been accepted. The 100-place limit and uniqueness check execute under the same account lock as writes; exports take that lock for a consistent snapshot. Account erasure removes saved places before occurrences and the account.

## API and recovery

Authenticated, rate-limited endpoints:

- `GET /api/discoveries`: read the current account's bounded journal.
- `POST /api/discoveries/save`: create with ExpectedRevision 0, or update using the last returned revision.
- `POST /api/discoveries/remove`: conditionally remove using the last returned revision. An already absent entry succeeds.

Stale edits and duplicate creates return 409. These operations are separate from the XP command/replay stream. There is no automatic mutation retry: after any uncertain or failed write, the client retains the displayed data and requires **Reload saved places** before another mutation. Reload resolves whether the server committed the earlier request. A response from an old authentication session cannot update the current session.

Journal access requires an online authenticated API. This increment does not add an offline queue or a durable offline journal cache. A failed save leaves notes in the open editor. Unsaved edits are not durable: closing the editor, switching views or loading a newer revision can discard them. Only a successful **Save place details** persists changes.

Public map requests remain in the unauthenticated places adapter. Journal requests use the authenticated Pocket Quests client. Notes, quest links and account credentials are never sent to map providers. No background location tracking is introduced.

## Returning to saved places

Expand an entry in Saved places and choose **Show on map** to center the in-app map on its retained coordinates. This action does not request GPS, perform a geocoder/nearby search, record a visit or change quest progress. Public tiles load for the chosen area. Nearby search only runs after **Find nearby places** is pressed.

Saved places within approximately 1.5 km of the current map center are combined with provider results by place key, so omitted provider results do not hide retained discoveries. Filled markers are saved; a check denotes a player-reported visit. **Show saved places only** filters the map/list. Straight-line distance is measured from the current map center, not the player's GPS position. Private notes and quest links are excluded from the map-point projection and third-party requests.

Opening a saved location cancels any active search and ignores its late response. If the open editor has changed notes, visit status or quest link, **Show on map** is disabled until those edits are saved or explicitly reset. These changes reuse the existing SavedPlace API/schema and require no migration.

## Rollout and acceptance

The matching API and additive SavedPlace schema must be updated together. For an existing local preview, back up and verify the database, restore a disposable rehearsal, apply only the new table/index/metadata statements and compare every pre-existing product table before committing. The November 2 feature-batch report records the current checkout's rollout evidence. Historical validation from another checkout is not acceptance for this tree.

Regression coverage includes concurrent revisions, cross-account access and links, SQL coordinate/FK constraints, capacity, JSON/CSV export, account erasure, session ownership and failed-save recovery. Browser map responses can be deterministic fixtures while private journal requests use real API/SQL persistence. Live public-provider checks and installed-device acceptance are separate lanes.
