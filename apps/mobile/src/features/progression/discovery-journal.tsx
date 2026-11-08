import { useState } from "react";
import { Text, View } from "react-native";
import { Link } from "expo-router";
import type { SavedPlace } from "../../api/generated";
import { useAccountSnapshot, useSessionStatus } from "../../session/session";
import { Button, Card, Input, Label, useAppTheme } from "../../shared/ui";
import type { DiscoveryJournalState } from "./use-discoveries";

export function DiscoveryJournal({
  journal,
  onShowMap,
}: Readonly<{
  journal: DiscoveryJournalState;
  onShowMap?: (place: SavedPlace) => void;
}>) {
  const { styles } = useAppTheme();
  const [expanded, setExpanded] = useState<string | null>(null);
  const [filter, setFilter] = useState("All");
  const [search, setSearch] = useState("");
  const [limit, setLimit] = useState(6);
  const matches = journal.places.filter(
    (place) =>
      (filter === "All" || (filter === "Visited") === !!place.visitedAt) &&
      place.name
        .toLocaleLowerCase()
        .includes(search.trim().toLocaleLowerCase()),
  );
  return (
    <Card>
      <Text accessibilityRole="header" style={styles.subtitle}>
        Discovery journal
      </Text>
      <Label>{journal.places.length} saved places · up to 100</Label>
      <Label>
        Keep places you want to visit, write private notes, and link a quest.
        Saved changes belong to your account.
      </Label>
      <Button
        title="Reload saved places"
        secondary
        disabled={journal.busy}
        onPress={() => void journal.reload()}
      />
      {journal.busy && (
        <Label accessibilityLiveRegion="polite">Updating your journal…</Label>
      )}
      {!!journal.error && (
        <Text accessibilityRole="alert" style={styles.text}>
          {journal.error}
        </Text>
      )}
      <Input
        accessibilityLabel="Search saved places"
        placeholder="Find a saved place"
        value={search}
        onChangeText={(value) => {
          setSearch(value);
          setLimit(6);
        }}
      />
      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
        {["All", "Want to visit", "Visited"].map((value) => (
          <View key={value} style={{ maxWidth: "100%" }}>
            <Button
              title={value}
              accessibilityLabel={`${filter === value ? "Selected: " : ""}${value} saved places`}
              secondary={filter !== value}
              onPress={() => {
                setFilter(value);
                setLimit(6);
              }}
            />
          </View>
        ))}
      </View>
      {matches.slice(0, limit).map((place) => (
        <View key={place.id} style={{ gap: 8 }}>
          <Button
            title={`${place.name} · ${place.visitedAt ? "Visited" : "Want to visit"}`}
            secondary
            onPress={() => setExpanded(expanded === place.id ? null : place.id)}
          />
          {expanded === place.id && (
            <SavedPlaceEditor
              key={`${place.id}/${place.revision}`}
              place={place}
              journal={journal}
              onShowMap={onShowMap}
            />
          )}
        </View>
      ))}
      {matches.length > limit && (
        <Button
          title="Show more saved places"
          secondary
          onPress={() => setLimit((value) => value + 6)}
        />
      )}
      {journal.ready && matches.length === 0 && (
        <Label>
          {journal.places.length
            ? "No saved places match this filter."
            : "Save a place from the map to begin your journal."}
        </Label>
      )}
      <Label>
        Visit status is your own record. It does not verify GPS presence or
        award XP. Complete linked quests through their usual completion screen.
      </Label>
    </Card>
  );
}
function SavedPlaceEditor({
  place,
  journal,
  onShowMap,
}: Readonly<{
  place: SavedPlace;
  journal: DiscoveryJournalState;
  onShowMap?: (place: SavedPlace) => void;
}>) {
  const { styles, colors } = useAppTheme();
  const { state } = useAccountSnapshot();
  const { offline, busy } = useSessionStatus();
  const [notes, setNotes] = useState(place.notes);
  const [visited, setVisited] = useState(!!place.visitedAt);
  const [questId, setQuestId] = useState<string | null>(
    place.questOccurrenceId,
  );
  const [choosing, setChoosing] = useState(false);
  const [questSearch, setQuestSearch] = useState("");
  const [confirmRemove, setConfirmRemove] = useState(false);
  const choices = (state?.occurrences ?? []).filter(
    (item) =>
      !item.pendingCompletion &&
      item.status !== "Offered" &&
      item.occurrence.quest.title
        .toLocaleLowerCase()
        .includes(questSearch.toLocaleLowerCase()),
  );
  const linked = state?.occurrences.find(
    (item) => item.occurrence.id === place.questOccurrenceId,
  );
  const disabled = !journal.ready || journal.busy || busy || offline;
  const dirty =
    notes !== place.notes ||
    visited !== !!place.visitedAt ||
    questId !== place.questOccurrenceId;
  return (
    <View style={{ gap: 10, padding: 8 }}>
      <Label>
        {place.kind} · saved {new Date(place.createdAt).toLocaleDateString()}
      </Label>
      {place.visitedAt && (
        <Label>
          Marked visited {new Date(place.visitedAt).toLocaleDateString()}
        </Label>
      )}
      {onShowMap && (
        <>
          <Button
            title="Show on map"
            secondary
            disabled={dirty || journal.busy}
            onPress={() => onShowMap(place)}
          />
          {dirty ? (
            <>
              <Label>Save or reset these edits before opening the map.</Label>
              <Button
                title="Reset place edits"
                secondary
                disabled={journal.busy}
                onPress={() => {
                  setNotes(place.notes);
                  setVisited(!!place.visitedAt);
                  setQuestId(place.questOccurrenceId);
                  setConfirmRemove(false);
                }}
              />
            </>
          ) : (
            <Label>
              Opens this saved location inside Explore. Map tiles load for that
              area; no location permission is needed.
            </Label>
          )}
        </>
      )}
      <Button
        title={
          visited
            ? "Visited — switch to want to visit"
            : "Want to visit — mark visited"
        }
        secondary
        disabled={disabled}
        onPress={() => setVisited((value) => !value)}
      />
      <Input
        accessibilityLabel={`Notes for ${place.name}`}
        placeholder="What would you like to do here?"
        multiline
        maxLength={1000}
        value={notes}
        editable={!journal.busy}
        onChangeText={setNotes}
      />
      <Label>{notes.length}/1,000 characters</Label>
      <Button
        title={choosing ? "Hide quest choices" : "Link an accepted quest"}
        secondary
        disabled={disabled}
        onPress={() => setChoosing((value) => !value)}
      />
      {choosing && (
        <>
          <Input
            accessibilityLabel="Find a quest to link"
            placeholder="Search your accepted quests"
            value={questSearch}
            onChangeText={setQuestSearch}
          />
          <Button
            title="No linked quest"
            secondary={questId !== null}
            disabled={disabled}
            onPress={() => setQuestId(null)}
          />
          {choices.slice(0, 12).map((item) => (
            <Button
              key={item.occurrence.id}
              title={`${questId === item.occurrence.id ? "Selected: " : ""}${item.occurrence.quest.title}`}
              secondary={questId !== item.occurrence.id}
              disabled={disabled}
              onPress={() => setQuestId(item.occurrence.id)}
            />
          ))}
          {choices.length > 12 && (
            <Label>Search to narrow these quest choices.</Label>
          )}
          {!choices.length && (
            <Label>
              Accept a quest from the quest board, then return to link it here.
            </Label>
          )}
        </>
      )}
      <Button
        title="Save place details"
        disabled={disabled}
        onPress={() =>
          void journal.save({
            id: place.id,
            placeKey: place.placeKey,
            name: place.name,
            kind: place.kind,
            categoryId: place.categoryId,
            latitude: place.latitude,
            longitude: place.longitude,
            notes,
            visited,
            questOccurrenceId: questId,
            expectedRevision: place.revision,
          })
        }
      />
      {place.questOccurrenceId && (
        <Link
          href={{
            pathname: "/quest/[id]",
            params: { id: place.questOccurrenceId },
          }}
          style={[
            styles.text,
            { color: colors.gold, paddingVertical: 14, minHeight: 48 },
          ]}
        >
          Open {linked?.occurrence.quest.title ?? "linked quest"}
          {linked ? ` · ${linked.status}` : ""}
        </Link>
      )}
      {offline && (
        <Label>
          Connect to save changes. Your current notes stay in this open editor.
        </Label>
      )}
      <Button
        title={
          confirmRemove ? "Confirm remove saved place" : "Remove saved place"
        }
        secondary
        disabled={disabled}
        onPress={() => {
          if (confirmRemove) void journal.remove(place);
          else setConfirmRemove(true);
        }}
      />
      {confirmRemove && (
        <>
          <Label>
            Removing this place deletes its notes and visit record. The linked
            quest stays intact.
          </Label>
          <Button
            title="Keep saved place"
            secondary
            disabled={disabled}
            onPress={() => setConfirmRemove(false)}
          />
        </>
      )}
    </View>
  );
}
