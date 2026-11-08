import { useAppearanceAutosave } from "./appearance-autosave";
import Ionicons from "@expo/vector-icons/Ionicons";
import { useEffect, useState } from "react";
import { Pressable, Text, View, useWindowDimensions } from "react-native";
import { useAccountSnapshot, useSessionStatus } from "../../session/session";
import type { State } from "../../shared/contracts";
import { Button, Label, useAppTheme } from "../../shared/ui";
import { TravelerBackdrop } from "./avatar-backdrop";
import { AvatarFigure, OutfitPreview } from "./avatar-figure";
import {
  accessories,
  poses,
  roomSlots,
  backdrops,
  extraAccessories,
  scarfNames,
  scarfColors,
  availableCosmetic,
  canWear,
  decodeAvatar,
  encodeAvatar,
  hairNames,
  hairColors,
  hairStyles,
  faceNames,
  buildNames,
  bodyTypeNames,
  petiteBuild,
  starterLooks,
  outfitPreview,
  outfits,
  skinTones,
  starterAvatar,
  type AvatarLook,
} from "./avatar-look";
import {
  randomizeAvatarSection,
  restoreAvatarSection,
  type AvatarSection,
} from "./avatar-customization";

function swatchPalette(slot: keyof AvatarLook) {
  if (slot === "skin") return skinTones;
  return slot === "scarfColor" ? scarfColors : hairColors;
}

function previewOpacity(pressed: boolean) {
  return pressed ? 0.8 : 1;
}

function previewLabel(
  showingSaved: boolean,
  dirty: boolean,
  needsInitialSave: boolean,
) {
  if (showingSaved) return "Showing saved look — your preview is kept";
  if (dirty) return "Preview — not saved";
  return needsInitialSave ? "Starter look" : "Your saved look";
}

function Wardrobe({
  state,
  onContinue,
  equipmentOnly = false,
}: Readonly<{
  state: State;
  onContinue?: () => void;
  equipmentOnly?: boolean;
}>) {
  const { styles, colors } = useAppTheme();
  const { width } = useWindowDimensions();
  const appearance = useAppearanceAutosave();
  const { busy, pending, offline, queuedCount } = useSessionStatus();
  const [advanceCode, setAdvanceCode] = useState<string | null>(null);
  const error = appearance.notice;
  const [section, setSection] = useState<AvatarSection | "Looks">("Clothing");
  const [history, setHistory] = useState<string[]>([]);
  const [future, setFuture] = useState<string[]>([]);
  const [comparing, setComparing] = useState(false);
  const [zoomed, setZoomed] = useState(false);
  const [advanced, setAdvanced] = useState(false);
  const [pickedBuild, setPickedBuild] = useState(false);
  const [kept, setKept] = useState<Partial<Record<keyof AvatarLook, boolean>>>(
    {},
  );
  const [equipmentSlot, setEquipmentSlot] = useState<
    "outfit" | "accessory" | "extra" | "pose" | "backdrop" | null
  >(null);
  const saved = state.profile.avatarCode ?? starterAvatar;
  const choice = appearance.avatarCode;
  const look = decodeAvatar(choice);
  const dirty = choice !== saved;
  const needsInitialSave = state.profile.avatarCode == null;
  const showingSaved = comparing && dirty;
  const wearable = canWear(look, state.entitlements);
  useEffect(() => {
    if (
      advanceCode === choice &&
      !dirty &&
      !needsInitialSave &&
      !busy &&
      !pending &&
      queuedCount === 0 &&
      !error
    )
      onContinue?.();
  }, [
    advanceCode,
    choice,
    dirty,
    needsInitialSave,
    busy,
    pending,
    queuedCount,
    error,
    onContinue,
  ]);
  const changeAvatar = (code: string) => {
    const next = decodeAvatar(code);
    for (const slot of ["room", ...roomSlots] as const) next[slot] = look[slot];
    appearance.changeAvatar(encodeAvatar(next));
  };
  const preview = (code: string) => {
    if (code !== choice) {
      setHistory((previous) => [...previous.slice(-19), choice]);
      setFuture([]);
      changeAvatar(code);
    }
    setComparing(false);
    setAdvanceCode(null);
  };
  const choose = (slot: keyof AvatarLook, value: number) => {
    if (slot === "build") setPickedBuild(true);
    if (slot === "bodyType" && value === look.bodyType) return;
    const next = { ...look, [slot]: value };
    if (slot === "bodyType" && onContinue && !pickedBuild) {
      next.build = value === 1 ? petiteBuild : 1;
    }
    preview(encodeAvatar(next));
  };
  const keep = (title: string, slot: keyof AvatarLook) => (
    <Button
      title={`${kept[slot] ? "Keeping" : "Keep"} ${title.toLowerCase()} when randomizing`}
      secondary={!kept[slot]}
      onPress={() =>
        setKept((current) => ({ ...current, [slot]: !current[slot] }))
      }
    />
  );
  const options = (title: string, slot: keyof AvatarLook, names: string[]) => (
    <View style={{ gap: 8 }}>
      <Text accessibilityRole="header" style={styles.subtitle}>
        {title}
      </Text>
      {advanced && keep(title, slot)}
      <View
        accessibilityRole="radiogroup"
        accessibilityLabel={title}
        style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}
      >
        {names.map((name, index) => (
          <Pressable
            key={name}
            accessibilityRole="radio"
            aria-checked={look[slot] === index}
            accessibilityState={{
              checked: look[slot] === index,
            }}
            accessibilityLabel={`${look[slot] === index ? "Selected: " : ""}${title}: ${name}`}
            onPress={() => choose(slot, index)}
            style={({ pressed }) => ({
              minHeight: 48,
              minWidth: 48,
              paddingVertical: 10,
              paddingHorizontal:
                slot === "skin" || slot === "hairColor" || slot === "scarfColor"
                  ? 10
                  : 14,
              flexDirection: "row",
              alignItems: "center",
              gap: 8,
              borderRadius: 12,
              borderWidth: 1,
              borderColor: colors.gold,
              backgroundColor:
                look[slot] === index ? colors.gold : colors.paper,
              opacity: previewOpacity(pressed),
            })}
          >
            {(slot === "skin" ||
              slot === "hairColor" ||
              slot === "scarfColor") && (
              <View
                accessible={false}
                style={{
                  width: 26,
                  height: 26,
                  borderRadius: 13,
                  borderWidth: 1,
                  borderColor: colors.line,
                  backgroundColor: swatchPalette(slot)[index],
                }}
              />
            )}
            {slot !== "skin" &&
              slot !== "hairColor" &&
              slot !== "scarfColor" && (
                <Text
                  style={[
                    styles.text,
                    {
                      color:
                        look[slot] === index ? colors.onGold : colors.green,
                    },
                  ]}
                >
                  {name}
                </Text>
              )}
          </Pressable>
        ))}
      </View>
    </View>
  );
  const wardrobe = (
    title: string,
    slot: "outfit" | "accessory",
    items: typeof outfits,
  ) => (
    <View style={{ gap: 8 }}>
      <Text accessibilityRole="header" style={styles.subtitle}>
        {title}
      </Text>
      {advanced && keep(title, slot)}
      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 10 }}>
        {items.map((item, index) => {
          const unlocked = availableCosmetic(item.rewardId, state.entitlements);
          if (!unlocked) return null;
          const reward = state.entitlements.find(
            (entry) => entry.id === item.rewardId,
          );
          return (
            <View
              key={item.name}
              style={{
                gap: 4,
                flexBasis: slot === "outfit" ? "30%" : "100%",
                flexGrow: 1,
              }}
            >
              <Pressable
                accessibilityRole="button"
                accessibilityLabel={`${look[slot] === index ? "Selected preview: " : ""}${item.name}`}
                onPress={() => choose(slot, index)}
                style={({ pressed }) => ({
                  minHeight: slot === "outfit" ? 120 : 48,
                  borderWidth: look[slot] === index ? 2 : 1,
                  borderColor:
                    look[slot] === index ? colors.ochre : colors.line,
                  borderRadius: 12,
                  padding: 8,
                  alignItems: "center",
                  justifyContent: "center",
                  gap: 6,
                  backgroundColor:
                    look[slot] === index ? colors.card : colors.paper,
                  opacity: pressed ? 0.8 : 1,
                })}
              >
                {slot === "outfit" && (
                  <OutfitPreview
                    code={encodeAvatar({ ...look, outfit: index })}
                    size={76}
                  />
                )}
                <Text
                  style={[styles.text, { fontSize: 13, textAlign: "center" }]}
                >
                  {item.name}
                </Text>
              </Pressable>
              {item.rewardId && (
                <Label style={styles.muted}>
                  {unlocked ? "Unlocked by" : "Earn"}{" "}
                  {reward?.name ?? "the matching reward"}
                  {unlocked ? "." : " to wear this."}
                </Label>
              )}
            </View>
          );
        })}
      </View>
    </View>
  );
  const sceneOptions = (slot: "pose" | "backdrop", items: typeof poses) => (
    <View
      accessibilityRole="radiogroup"
      accessibilityLabel={slot === "pose" ? "Pose" : "Background"}
      style={{ gap: 10 }}
    >
      <Text accessibilityRole="header" style={styles.subtitle}>
        {slot === "pose" ? "Choose a pose" : "Choose a background"}
      </Text>
      {items.map((item, index) =>
        availableCosmetic(item.rewardId, state.entitlements) ? (
          <Pressable
            key={item.name}
            accessibilityRole="radio"
            aria-checked={look[slot] === index}
            accessibilityLabel={item.name}
            accessibilityState={{ checked: look[slot] === index }}
            onPress={() => choose(slot, index)}
            style={({ pressed }) => ({
              minHeight: 48,
              padding: 12,
              borderRadius: 12,
              borderWidth: 1,
              borderColor: colors.gold,
              alignItems: "center",
              justifyContent: "center",
              backgroundColor:
                look[slot] === index ? colors.gold : colors.paper,
              opacity: pressed ? 0.8 : 1,
            })}
          >
            <Text
              style={[
                styles.text,
                {
                  fontFamily: "QuestSansBold",
                  color: look[slot] === index ? colors.onGold : colors.ink,
                },
              ]}
            >
              {item.name}
            </Text>
          </Pressable>
        ) : null,
      )}
      <Label style={styles.muted}>
        More {slot === "pose" ? "poses" : "backgrounds"} unlock through quest
        rewards. See Progress for your next unlock.
      </Label>
    </View>
  );
  if (equipmentOnly)
    return (
      <View style={{ gap: 18 }}>
        <Label>
          Your traveler, ready for the day. Tap a slot to change what you wear.
        </Label>
        <View style={{ flexDirection: "row", alignItems: "center", gap: 12 }}>
          <View style={{ flex: 1, alignItems: "center", overflow: "hidden" }}>
            <TravelerBackdrop
              avatarCode={choice}
              width={Math.max(144, Math.min(width, 780) / 2 - 22)}
              height={330}
              portrait
            />
            <AvatarFigure code={choice} size={330} pose="relaxed" />
          </View>
          <View style={{ flex: 1, gap: 12 }}>
            {(
              [
                {
                  slot: "outfit",
                  title: "Clothing",
                  icon: "shirt-outline",
                  name: outfits[look.outfit].name,
                },
                {
                  slot: "accessory",
                  title: "Gear",
                  icon: "bag-handle-outline",
                  name: accessories[look.accessory].name,
                },
                {
                  slot: "extra",
                  title: "Extra accessory",
                  icon: "glasses-outline",
                  name: extraAccessories[look.extra].replace(/^Add /, ""),
                },
              ] as const
            ).map((item) => (
              <Pressable
                key={item.slot}
                accessibilityRole="button"
                accessibilityLabel={`Change ${item.title.toLowerCase()}, equipped: ${item.name}`}
                accessibilityState={{ expanded: equipmentSlot === item.slot }}
                aria-expanded={equipmentSlot === item.slot}
                onPress={() =>
                  setEquipmentSlot(
                    equipmentSlot === item.slot ? null : item.slot,
                  )
                }
                style={{
                  minHeight: 92,
                  padding: 12,
                  borderRadius: 12,
                  borderWidth: equipmentSlot === item.slot ? 2 : 1,
                  borderColor:
                    equipmentSlot === item.slot ? colors.ochre : colors.line,
                  backgroundColor: colors.card,
                  gap: 5,
                }}
              >
                <Ionicons
                  name={item.icon}
                  size={22}
                  color={colors.green}
                  accessible={false}
                />
                <Text style={[styles.muted, { fontSize: 12 }]}>
                  {item.title}
                </Text>
                <Text
                  style={[styles.text, { fontSize: 14, fontWeight: "600" }]}
                >
                  {item.name}
                </Text>
              </Pressable>
            ))}
          </View>
        </View>
        <View style={{ flexDirection: "row", gap: 10 }}>
          {(
            [
              {
                slot: "pose",
                title: "Pose",
                name: poses[look.pose].name,
                icon: "body-outline",
              },
              {
                slot: "backdrop",
                title: "Background",
                name: backdrops[look.backdrop].name,
                icon: "image-outline",
              },
            ] as const
          ).map((item) => (
            <Pressable
              key={item.slot}
              accessibilityRole="button"
              accessibilityLabel={`Change ${item.title.toLowerCase()}, equipped: ${item.name}`}
              accessibilityState={{ expanded: equipmentSlot === item.slot }}
              aria-expanded={equipmentSlot === item.slot}
              onPress={() =>
                setEquipmentSlot(equipmentSlot === item.slot ? null : item.slot)
              }
              style={{
                flex: 1,
                minHeight: 82,
                padding: 12,
                gap: 5,
                borderRadius: 12,
                borderWidth: equipmentSlot === item.slot ? 2 : 1,
                borderColor:
                  equipmentSlot === item.slot ? colors.ochre : colors.line,
                backgroundColor: colors.card,
              }}
            >
              <Ionicons
                name={item.icon}
                size={22}
                color={colors.green}
                accessible={false}
              />
              <Text style={[styles.muted, { fontSize: 12 }]}>{item.title}</Text>
              <Text
                style={[
                  styles.text,
                  { fontSize: 14, fontFamily: "QuestSansBold" },
                ]}
              >
                {item.name}
              </Text>
            </Pressable>
          ))}
        </View>
        {equipmentSlot === "pose" && sceneOptions("pose", poses)}
        {equipmentSlot === "backdrop" && sceneOptions("backdrop", backdrops)}
        <Label style={styles.muted}>{"Your equipped look"}</Label>
        {equipmentSlot === "outfit" && (
          <>
            <Label style={styles.muted}>
              Each outfit includes its top, trousers and shoes.
            </Label>
            {wardrobe("Choose clothing", "outfit", outfits)}
          </>
        )}
        {equipmentSlot === "accessory" &&
          wardrobe("Choose gear", "accessory", accessories)}
        {equipmentSlot === "extra" &&
          options("Choose an extra accessory", "extra", extraAccessories)}
        {(equipmentSlot === "extra" || equipmentSlot === "accessory") &&
          (look.extra === 2 || look.accessory === 5) &&
          options("Scarf color", "scarfColor", scarfNames)}
        {equipmentSlot && (
          <Button
            title="Done choosing"
            secondary
            onPress={() => setEquipmentSlot(null)}
          />
        )}
        <Label style={styles.muted}>Changes save automatically.</Label>
        {offline && (
          <Label>
            Your choices stay here while offline. Reconnect to save them.
          </Label>
        )}
        {!wearable && (
          <Label>
            An item is no longer available. Choose an item you own before
            saving.
          </Label>
        )}
      </View>
    );
  return (
    <View style={{ gap: 12 }}>
      <Label>
        Make this character your own. Everything shown here is ready to wear.
      </Label>
      {options("Body type", "bodyType", bodyTypeNames)}
      {options("Build", "build", buildNames)}
      <View style={{ alignItems: "center" }}>
        <AvatarFigure
          code={showingSaved ? saved : choice}
          portrait={zoomed}
          size={zoomed ? 180 : 292}
        />
      </View>
      {advanced && (
        <Button
          title={zoomed ? "Show full traveler" : "Zoom in on face"}
          secondary
          onPress={() => setZoomed((current) => !current)}
        />
      )}
      {(dirty || advanced) && (
        <Label style={{ fontSize: 12 }}>
          {showingSaved
            ? previewLabel(true, dirty, needsInitialSave)
            : "Changes save automatically."}
        </Label>
      )}
      {advanced && dirty && (
        <Button
          title={showingSaved ? "Return to preview" : "Compare saved look"}
          secondary
          onPress={() => setComparing((current) => !current)}
        />
      )}
      {advanced && history.length > 0 && (
        <Button
          title="Undo preview edit"
          secondary
          onPress={() => {
            setFuture((previous) => [...previous.slice(-19), choice]);
            changeAvatar(history.at(-1)!);
            setHistory((previous) => previous.slice(0, -1));
            setComparing(false);
          }}
        />
      )}
      {advanced && future.length > 0 && (
        <Button
          title="Redo preview edit"
          secondary
          onPress={() => {
            setHistory((previous) => [...previous.slice(-19), choice]);
            changeAvatar(future.at(-1)!);
            setFuture((previous) => previous.slice(0, -1));
            setComparing(false);
          }}
        />
      )}
      {advanced && (
        <View style={{ gap: 8 }}>
          <Button
            title="Surprise me"
            secondary
            onPress={() =>
              preview(
                randomizeAvatarSection(choice, "All", state.entitlements, kept),
              )
            }
          />
        </View>
      )}
      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
        {(
          (advanced
            ? ["Identity", "Hair", "Clothing", "Accessories", "Looks"]
            : ["Identity", "Hair", "Clothing"]) as readonly (
            AvatarSection | "Looks"
          )[]
        ).map((name) => (
          <Pressable
            key={name}
            accessibilityRole="tab"
            accessibilityState={{ selected: section === name }}
            onPress={() => setSection(name)}
            style={{
              minHeight: 48,
              paddingHorizontal: 14,
              paddingVertical: 12,
              flexGrow: 1,
              alignItems: "center",
              borderBottomWidth: section === name ? 3 : 1,
              borderBottomColor: section === name ? colors.ochre : colors.line,
              backgroundColor: colors.paper,
            }}
          >
            <Text
              style={[
                styles.text,
                { fontWeight: section === name ? "700" : "400" },
              ]}
            >
              {name === "Identity"
                ? "Body"
                : name === "Clothing"
                  ? "Clothes"
                  : name}
            </Text>
          </Pressable>
        ))}
      </View>
      {advanced && section !== "Looks" && (
        <View style={{ gap: 8 }}>
          <Label>
            Try just this part of your look. Your other choices stay as you made
            them.
          </Label>
          <Button
            title={`Randomize ${section.toLowerCase()} only`}
            secondary
            onPress={() =>
              preview(
                randomizeAvatarSection(
                  choice,
                  section,
                  state.entitlements,
                  kept,
                ),
              )
            }
          />
          <Button
            title={`Restore saved ${section.toLowerCase()}`}
            secondary
            disabled={restoreAvatarSection(choice, saved, section) === choice}
            onPress={() =>
              preview(restoreAvatarSection(choice, saved, section))
            }
          />
        </View>
      )}
      {section === "Looks" && (
        <View style={{ gap: 8 }}>
          <Label>
            Choose an outfit. Your face, skin tone and hair stay the same.
          </Label>
          <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 12 }}>
            {starterLooks.map((preset) => (
              <View
                key={preset.name}
                style={{
                  flexBasis: 120,
                  flexGrow: 1,
                  alignItems: "center",
                  gap: 8,
                }}
              >
                <AvatarFigure
                  code={outfitPreview(choice, preset.code)}
                  size={140}
                />
                <Button
                  title={preset.name}
                  secondary
                  onPress={() => {
                    preview(outfitPreview(choice, preset.code));
                  }}
                />
              </View>
            ))}
          </View>
        </View>
      )}
      {!wearable && (
        <Label>
          This item is no longer available. Choose something you own.
        </Label>
      )}
      {section === "Identity" && (
        <>
          {advanced && options("Face", "face", faceNames)}
          {options(
            "Skin tone",
            "skin",
            skinTones.map((_, index) => `Tone ${index + 1}`),
          )}
        </>
      )}
      {section === "Hair" && (
        <>
          {options("Hair color", "hairColor", hairNames)}
          {options("Hair style", "hairStyle", hairStyles)}
        </>
      )}
      {section === "Clothing" && wardrobe("Clothing", "outfit", outfits)}
      {section === "Accessories" && (
        <>
          {wardrobe("Accessories", "accessory", accessories)}
          <Label>
            Layer free glasses or a scarf with your chosen gear. Duplicate items
            appear once.
          </Label>
          {options("Extra accessory", "extra", extraAccessories)}
          {(look.accessory === 5 || look.extra === 2) &&
            options("Scarf color", "scarfColor", scarfNames)}
        </>
      )}
      {advanced && (
        <Label>
          Undo can remove an unlock. If that happens, your look returns to the
          starter item. Frames and their matching clothing also depend on your
          current rank.
        </Label>
      )}
      <Button
        title={advanced ? "Fewer options" : "More editing options"}
        secondary
        onPress={() => setAdvanced(!advanced)}
      />
      {onContinue && (
        <Button
          title="Continue"
          disabled={!wearable || offline || pending || queuedCount > 0}
          onPress={() => {
            if (!dirty && !needsInitialSave && !busy && !error) onContinue();
            else setAdvanceCode(choice);
          }}
        />
      )}
      {!onContinue && (
        <Label style={styles.muted}>Changes save automatically.</Label>
      )}
      {offline && (
        <Label>You can preview offline. Reconnect to save your avatar.</Label>
      )}
      {advanced && dirty && (
        <Button
          title="Reset avatar preview"
          secondary
          disabled={queuedCount > 0}
          onPress={() => {
            changeAvatar(saved);
            setHistory([]);
            setFuture([]);
            setComparing(false);
          }}
        />
      )}
    </View>
  );
}
export function AvatarEditor({
  onContinue,
  equipmentOnly = false,
}: Readonly<{ onContinue?: () => void; equipmentOnly?: boolean }> = {}) {
  const { state } = useAccountSnapshot();
  return state ? (
    <Wardrobe
      state={state}
      onContinue={onContinue}
      equipmentOnly={equipmentOnly}
    />
  ) : null;
}
