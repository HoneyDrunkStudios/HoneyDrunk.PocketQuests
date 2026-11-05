import { Text } from "react-native";
import { Link } from "expo-router";
import {
  distanceMeters,
  type Coordinate,
  type PointOfInterest,
} from "../../api/places-contract";
import { Button, Card, Label, colors, styles } from "../../shared/ui";
import type { DiscoveryJournalState } from "./use-discoveries";

export function ExplorationDetails({
  point,
  center,
  saved,
  visited,
  category,
  journal,
  onSaved,
}: Readonly<{
  point: PointOfInterest;
  center: Coordinate | null;
  saved: boolean;
  visited: boolean;
  category?: { id: string; name: string };
  journal: DiscoveryJournalState;
  onSaved: () => void;
}>) {
  let discoveryLabel = "Undiscovered";
  if (saved)
    discoveryLabel = visited ? "Saved · Visited" : "Saved · Want to visit";
  return (
    <Card>
      <Label>
        {discoveryLabel} · {point.kind}
      </Label>
      <Text accessibilityRole="header" style={styles.subtitle}>
        {point.name}
      </Text>
      <Label>
        About{" "}
        {(
          (center ? distanceMeters(point, center) : point.distanceMeters) / 1000
        ).toFixed(1)}{" "}
        km from the map center, straight-line distance.
      </Label>
      {!!point.address && <Label>{point.address}</Label>}
      <Button
        title={saved ? "Saved in your journal" : "Save place to journal"}
        secondary={saved}
        disabled={
          saved ||
          !journal.ready ||
          journal.busy ||
          journal.places.length >= 100
        }
        onPress={() => {
          void journal.add(point).then((saved) => {
            if (saved) onSaved();
          });
        }}
      />
      {!journal.ready && (
        <Label>
          Open Saved places to load or reconnect your journal before saving.
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
        Save this place to keep notes, mark a visit and link a quest in your
        journal below. It does not verify a visit or award XP. Check local
        access and opening hours before visiting.
      </Label>
    </Card>
  );
}
