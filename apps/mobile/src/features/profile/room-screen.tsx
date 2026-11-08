import { useState, type ReactNode } from "react";
import {
  Image,
  Pressable,
  ScrollView,
  Text,
  View,
  useWindowDimensions,
} from "react-native";
import Ionicons from "@expo/vector-icons/Ionicons";
import { useAccountSnapshot } from "../../session/session";
import { Label, useAppTheme } from "../../shared/ui";
import { AvatarFigure } from "./avatar-figure";
import {
  AppearanceSaveNotice,
  useAppearanceAutosave,
} from "./appearance-autosave";
import {
  decodeAvatar,
  encodeAvatar,
  roomDecorations,
  roomSlots,
  type RoomSlot,
} from "./avatar-look";
import { roomArt, roomSceneLayout, portraitRoomArt } from "./room-art";
import {
  RoomDecoration,
  RoomOptions,
  roomSlotNames,
  type RoomPanel,
} from "./room-options";

/** An edge-to-edge room; ordinary browsing has no heading or explanatory footer. */
export function TravelerRoom({
  topInset = 0,
  bottomInset = 0,
  recovery,
  onFindQuests,
}: Readonly<{
  topInset?: number;
  bottomInset?: number;
  recovery?: ReactNode;
  onFindQuests?: () => void;
}>) {
  const { state } = useAccountSnapshot();
  const appearance = useAppearanceAutosave();
  const { styles, colors } = useAppTheme();
  const window = useWindowDimensions();
  const [viewport, setViewport] = useState({
    width: window.width,
    height: Math.max(1, window.height - 72),
  });
  const [editing, setEditing] = useState(false);
  const [selected, setSelected] = useState<RoomSlot | null>(null);
  const [panel, setPanel] = useState<RoomPanel>(null);
  if (!state) return <Label>Opening your room...</Label>;
  const look = decodeAvatar(appearance.avatarCode);
  const scene = roomSceneLayout(viewport.width, viewport.height, look.room);
  const source = scene.portrait
    ? portraitRoomArt[look.room]
    : roomArt[look.room];
  const avatarSize = scene.height * (scene.portrait ? 0.58 : 0.76);
  const itemSize = Math.max(44, Math.min(104, scene.width * 0.13));
  const save = (next: typeof look) =>
    appearance.changeAvatar(encodeAvatar(next));
  const close = () => {
    setPanel(null);
    setSelected(null);
  };
  const place = (item: number) => {
    if (!selected) return;
    const next = { ...look };
    for (const slot of roomSlots)
      if (next[slot] === item && item !== 0) next[slot] = 0;
    next[selected] = item;
    save(next);
    close();
  };
  const control = {
    minHeight: 48,
    borderRadius: 24,
    paddingHorizontal: 16,
    flexDirection: "row" as const,
    alignItems: "center" as const,
    gap: 8,
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.line,
  };
  return (
    <View
      testID="traveler-room-scene"
      onLayout={(event) => {
        const { width, height } = event.nativeEvent.layout;
        if (width > 0 && height > 0)
          setViewport((current) =>
            current.width === width && current.height === height
              ? current
              : { width, height },
          );
      }}
      style={{ flex: 1, overflow: "hidden", backgroundColor: colors.paper }}
    >
      {!scene.portrait && (
        <Image
          source={source}
          accessible={false}
          resizeMode="cover"
          blurRadius={24}
          style={{
            position: "absolute",
            inset: 0,
            width: "100%",
            height: "100%",
          }}
        />
      )}
      <Image
        source={source}
        accessible={false}
        resizeMode="stretch"
        style={{
          position: "absolute",
          left: scene.x,
          top: scene.y,
          width: scene.width,
          height: scene.height,
        }}
      />
      <View
        pointerEvents="none"
        style={{
          position: "absolute",
          left: scene.floor[0] - (avatarSize * 0.55) / 2,
          top: scene.floor[1] - (avatarSize * 799) / 840,
        }}
      >
        <AvatarFigure code={appearance.avatarCode} size={avatarSize} />
      </View>
      {roomSlots.map((slot, index) => {
        const item = look[slot],
          anchor = scene.anchors[index];
        if (!editing && item === 0) return null;
        return (
          <Pressable
            key={slot}
            accessibilityRole="button"
            accessibilityLabel={`${roomSlotNames[index]}: ${roomDecorations[item].name}`}
            accessibilityState={{ selected: selected === slot }}
            onPress={() => {
              setSelected(slot);
              setPanel("decoration");
            }}
            style={{
              position: "absolute",
              left: anchor[0] - itemSize / 2,
              top: anchor[1] - itemSize,
              width: itemSize,
              height: itemSize,
              alignItems: "center",
              justifyContent: "flex-end",
              borderWidth: selected === slot ? 2 : 0,
              borderColor: colors.gold,
              borderRadius: 12,
            }}
          >
            {item > 0 ? (
              <RoomDecoration item={item} size={itemSize} />
            ) : (
              <View
                style={{
                  width: 44,
                  height: 44,
                  borderRadius: 22,
                  alignItems: "center",
                  justifyContent: "center",
                  backgroundColor: colors.card,
                }}
              >
                <Ionicons
                  name="add"
                  size={24}
                  color={colors.green}
                  accessible={false}
                />
              </View>
            )}
          </Pressable>
        );
      })}
      <View
        pointerEvents="box-none"
        style={{
          position: "absolute",
          top: topInset + 12,
          left: 12,
          right: 12,
          flexDirection: "row",
          justifyContent: "space-between",
          gap: 12,
        }}
      >
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Change room"
          onPress={() => setPanel("rooms")}
          style={control}
        >
          <Ionicons
            name="albums-outline"
            size={21}
            color={colors.green}
            accessible={false}
          />
          <Text style={styles.text}>Rooms</Text>
        </Pressable>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={editing ? "Finish decorating" : "Decorate room"}
          accessibilityState={{ expanded: editing }}
          onPress={() => {
            setEditing(!editing);
            close();
          }}
          style={control}
        >
          <Ionicons
            name={editing ? "checkmark" : "color-wand-outline"}
            size={21}
            color={colors.green}
            accessible={false}
          />
          <Text style={styles.text}>{editing ? "Done" : "Decorate"}</Text>
        </Pressable>
      </View>
      {recovery && (
        <View
          pointerEvents="box-none"
          style={{
            position: "absolute",
            top: topInset + 72,
            left: 12,
            right: 12,
            maxHeight: "50%",
          }}
        >
          <ScrollView keyboardShouldPersistTaps="handled">
            {recovery}
          </ScrollView>
        </View>
      )}
      <AppearanceSaveNotice />
      <RoomOptions
        panel={panel}
        selected={selected}
        look={look}
        state={state}
        bottomInset={bottomInset}
        onClose={close}
        onPanel={setPanel}
        onRoom={(room) => {
          save({ ...look, room });
          close();
        }}
        onPlace={place}
        onFindQuests={onFindQuests}
      />
    </View>
  );
}
