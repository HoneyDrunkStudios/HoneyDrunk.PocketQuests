import { View } from "react-native";
import { placeKinds, type PointOfInterest } from "../../api/places-contract";
import { Button, Label } from "../../shared/ui";

export function ExplorationResults({
  visible,
  limit,
  filter,
  selected,
  found,
  onFilter,
  onSelect,
  onMore,
}: Readonly<{
  visible: readonly PointOfInterest[];
  limit: number;
  filter: string;
  selected?: string;
  found: ReadonlySet<string>;
  onFilter: (kind: string) => void;
  onSelect: (point: PointOfInterest) => void;
  onMore: () => void;
}>) {
  return (
    <>
      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
        {["All", ...placeKinds.map((kind) => kind.name)].map((kind) => (
          <View key={kind} style={{ maxWidth: "100%" }}>
            <Button
              title={kind}
              secondary={filter !== kind}
              accessibilityLabel={`${filter === kind ? "Selected: " : ""}${kind} places`}
              onPress={() => onFilter(kind)}
            />
          </View>
        ))}
      </View>
      <Label>
        {visible.length} places shown. Choose from the list if markers overlap.
      </Label>
      {visible.slice(0, limit).map((point, index) => (
        <Button
          key={point.id}
          title={`${index + 1}. ${point.name} · ${found.has(point.id) ? "Discovered" : point.kind}`}
          secondary={selected !== point.id}
          onPress={() => onSelect(point)}
        />
      ))}
      {visible.length > limit && (
        <Button title="Show more places" secondary onPress={onMore} />
      )}
      {visible.length === 0 && (
        <Label>
          No places match here. Try All, another area, or a different map
          center.
        </Label>
      )}
    </>
  );
}
