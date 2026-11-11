import { Modal, Pressable, ScrollView, Text, View } from "react-native";
import Ionicons from "@expo/vector-icons/Ionicons";
import Svg, { Image as SvgImage } from "react-native-svg";
import { Button, Label, useAppTheme } from "../../shared/ui";
import type { State } from "../../shared/contracts";
import {
  availableCosmetic,
  roomStyles,
  roomDecorations,
  roomSlots,
  type AvatarLook,
  type RoomSlot,
} from "./avatar-look";
import { decorationArt, decorationBounds } from "./room-art";

export function RoomDecoration({
  item,
  size,
}: Readonly<{ item: number; size: number }>) {
  return (
    <Svg
      width={size}
      height={size}
      viewBox={decorationBounds[item - 1].join(" ")}
      preserveAspectRatio="xMidYMax meet"
      accessible={false}
    >
      <SvgImage href={decorationArt} x={0} y={0} width={1536} height={1024} />
    </Svg>
  );
}
export const roomSlotNames = [
  "Upper left shelf",
  "Upper right shelf",
  "Left display table",
  "Right display table",
];
export type RoomPanel = "rooms" | "unlocks" | "decoration" | null;

/** Room controls stay in a dismissible sheet, leaving the normal scene clear. */
export function RoomOptions({
  panel,
  selected,
  look,
  state,
  bottomInset,
  onClose,
  onPanel,
  onRoom,
  onPlace,
  onFindQuests,
}: Readonly<{
  panel: RoomPanel;
  selected: RoomSlot | null;
  look: AvatarLook;
  state: State;
  bottomInset: number;
  onClose: () => void;
  onPanel: (panel: RoomPanel) => void;
  onRoom: (room: number) => void;
  onPlace: (item: number) => void;
  onFindQuests?: () => void;
}>) {
  const { styles, colors } = useAppTheme();
  if (!panel) return null;
  const owned = roomDecorations
    .map((item, index) => ({ item, index }))
    .filter(
      ({ item, index }) =>
        index > 0 && availableCosmetic(item.rewardId, state.entitlements),
    );
  let title = roomSlotNames[roomSlots.indexOf(selected!)];
  if (panel === "rooms") title = "Your rooms";
  else if (panel === "unlocks") title = "Room unlocks";
  return (
    <Modal transparent animationType="none" visible onRequestClose={onClose}>
      <View
        style={{
          flex: 1,
          justifyContent: "flex-end",
          backgroundColor: "rgba(10,8,24,0.38)",
        }}
      >
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Close room options"
          onPress={onClose}
          style={{ position: "absolute", inset: 0 }}
        />
        <View
          testID="room-options"
          accessibilityViewIsModal
          style={{
            backgroundColor: colors.paper,
            borderTopLeftRadius: 26,
            borderTopRightRadius: 26,
            padding: 20,
            paddingBottom: Math.max(bottomInset, 16),
            gap: 16,
            maxHeight: "70%",
            width: "100%",
            maxWidth: 620,
            alignSelf: "center",
          }}
        >
          <View style={{ flexDirection: "row", alignItems: "center", gap: 12 }}>
            <Text
              accessibilityRole="header"
              style={[styles.subtitle, { flex: 1 }]}
            >
              {title}
            </Text>
            <Pressable
              accessibilityRole="button"
              accessibilityLabel="Done with room options"
              onPress={onClose}
              style={{
                minWidth: 48,
                minHeight: 48,
                alignItems: "center",
                justifyContent: "center",
                borderRadius: 24,
                backgroundColor: colors.card,
              }}
            >
              <Ionicons
                name="close"
                size={24}
                color={colors.ink}
                accessible={false}
              />
            </Pressable>
          </View>
          <ScrollView
            keyboardShouldPersistTaps="handled"
            contentContainerStyle={{ gap: 14, paddingBottom: 8 }}
          >
            {panel === "rooms" && (
              <>
                <View
                  accessibilityRole="radiogroup"
                  accessibilityLabel="Owned rooms"
                  style={{ gap: 8 }}
                >
                  {roomStyles.map((room, index) =>
                    availableCosmetic(room.rewardId, state.entitlements) ? (
                      <Pressable
                        key={room.name}
                        accessibilityRole="radio"
                        accessibilityLabel={room.name}
                        aria-checked={look.room === index}
                        accessibilityState={{ checked: look.room === index }}
                        onPress={() => onRoom(index)}
                        style={{
                          minHeight: 56,
                          padding: 16,
                          borderWidth: look.room === index ? 2 : 1,
                          borderColor:
                            look.room === index ? colors.gold : colors.line,
                          borderRadius: 14,
                          backgroundColor: colors.card,
                        }}
                      >
                        <Text style={styles.text}>{room.name}</Text>
                      </Pressable>
                    ) : null,
                  )}
                </View>
                <Button
                  title="Unlock more rooms"
                  secondary
                  onPress={() => onPanel("unlocks")}
                />
              </>
            )}
            {panel === "unlocks" && (
              <>
                {roomStyles
                  .filter((room) => room.rewardId)
                  .map((room) => {
                    const reward = state.entitlements.find(
                      (item) => item.id === room.rewardId,
                    );
                    const lockedLabel = reward
                      ? `Reach rank ${reward.requiredRank} to unlock this room.`
                      : "Rank up to unlock this room.";
                    return (
                      <View
                        key={room.name}
                        style={{
                          padding: 16,
                          gap: 8,
                          borderRadius: 16,
                          backgroundColor: colors.card,
                        }}
                      >
                        <Text style={styles.subtitle}>{room.name}</Text>
                        <Label>
                          {reward?.earned ? "Unlocked" : lockedLabel}
                        </Label>
                        {reward && !reward.earned && (
                          <Label>Your rank is {state.rank.current}.</Label>
                        )}
                      </View>
                    );
                  })}
                {onFindQuests && (
                  <Button
                    title="Find a quest"
                    onPress={() => {
                      onClose();
                      onFindQuests();
                    }}
                  />
                )}
                <Button
                  title="Back to your rooms"
                  secondary
                  onPress={() => onPanel("rooms")}
                />
              </>
            )}
            {panel === "decoration" && selected && (
              <>
                {look[selected] > 0 && (
                  <Button
                    title="Clear display"
                    accessibilityLabel="Clear this display"
                    secondary
                    onPress={() => onPlace(0)}
                  />
                )}
                <View
                  style={{ flexDirection: "row", flexWrap: "wrap", gap: 10 }}
                >
                  {owned.map(({ item, index }) => (
                    <Pressable
                      key={item.name}
                      accessibilityRole="button"
                      accessibilityLabel={`Place ${item.name}`}
                      onPress={() => onPlace(index)}
                      style={{
                        minHeight: 100,
                        minWidth: 100,
                        alignItems: "center",
                        gap: 6,
                        padding: 10,
                        borderWidth: look[selected] === index ? 2 : 1,
                        borderColor:
                          look[selected] === index ? colors.gold : colors.line,
                        borderRadius: 14,
                        backgroundColor: colors.card,
                      }}
                    >
                      <RoomDecoration item={index} size={56} />
                      <Text
                        style={[
                          styles.text,
                          { fontSize: 13, maxWidth: 120, textAlign: "center" },
                        ]}
                      >
                        {item.name}
                      </Text>
                    </Pressable>
                  ))}
                </View>
                {owned.length === 0 && (
                  <>
                    <Label>
                      Reach level 2 to earn your first room decoration.
                    </Label>
                    {onFindQuests && (
                      <Button
                        title="Find a quest"
                        onPress={() => {
                          onClose();
                          onFindQuests();
                        }}
                      />
                    )}
                  </>
                )}
              </>
            )}
          </ScrollView>
        </View>
      </View>
    </Modal>
  );
}
