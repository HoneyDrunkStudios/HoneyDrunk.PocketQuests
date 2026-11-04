import { useRef, useState } from "react";
import { Pressable, Text, View } from "react-native";
import {
  useAccountSnapshot,
  useSessionActions,
  useSessionStatus,
} from "../../session/session";
import type { State } from "../../shared/contracts";
import { Button, Card, Label, colors, styles } from "../../shared/ui";
import { questActions } from "../quests/commands/quest-actions";
import { AvatarFigure } from "./avatar-figure";
import {
  accessories,
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
  starterLooks,
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

function Wardrobe({
  state,
  onContinue,
}: Readonly<{ state: State; onContinue?: () => void }>) {
  const { command } = useSessionActions();
  const { busy, pending, offline, queuedCount } = useSessionStatus();
  const [draft, setDraft] = useState<string>();
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [section, setSection] = useState<AvatarSection | "Looks">("Identity");
  const [history, setHistory] = useState<string[]>([]);
  const [future, setFuture] = useState<string[]>([]);
  const [comparing, setComparing] = useState(false);
  const [zoomed, setZoomed] = useState(false);
  const [kept, setKept] = useState<Partial<Record<keyof AvatarLook, boolean>>>(
    {},
  );
  const submitting = useRef(false);
  const saved = state.profile.avatarCode ?? starterAvatar;
  const choice = draft ?? saved;
  const look = decodeAvatar(choice);
  const dirty = choice !== saved;
  const needsInitialSave = state.profile.avatarCode == null;
  const showingSaved = comparing && dirty;
  const wearable = canWear(look, state.entitlements);
  if (draft !== undefined && !dirty && queuedCount === 0 && !saving)
    setDraft(undefined);
  const preview = (code: string) => {
    if (code !== choice) {
      setHistory((previous) => [...previous.slice(-19), choice]);
      setFuture([]);
      setDraft(code);
    }
    setComparing(false);
    setError("");
  };
  const choose = (slot: keyof AvatarLook, value: number) =>
    preview(encodeAvatar({ ...look, [slot]: value }));
  const keep = (title: string, slot: keyof AvatarLook) => (
    <Button
      title={`${kept[slot] ? "Keeping" : "Keep"} ${title.toLowerCase()} when randomizing`}
      secondary={!kept[slot]}
      disabled={saving}
      onPress={() =>
        setKept((current) => ({ ...current, [slot]: !current[slot] }))
      }
    />
  );
  async function save() {
    if (
      submitting.current ||
      (!dirty && !needsInitialSave) ||
      !wearable ||
      busy ||
      pending ||
      offline ||
      queuedCount > 0
    )
      return;
    submitting.current = true;
    setSaving(true);
    setError("");
    try {
      await command({ action: questActions.saveAvatar, avatarCode: choice });
    } catch {
      setError(
        "We couldn’t save your look. Your preview is still here; please try again.",
      );
    } finally {
      submitting.current = false;
      setSaving(false);
    }
  }
  const options = (title: string, slot: keyof AvatarLook, names: string[]) => (
    <View style={{ gap: 8 }}>
      <Text accessibilityRole="header" style={styles.subtitle}>
        {title}
      </Text>
      {keep(title, slot)}
      <View
        accessibilityRole="radiogroup"
        accessibilityLabel={title}
        style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}
      >
        {names.map((name, index) => (
          <Pressable
            key={name}
            accessibilityRole="radio"
            accessibilityState={{
              checked: look[slot] === index,
              disabled: saving,
            }}
            accessibilityLabel={`${look[slot] === index ? "Selected: " : ""}${title}: ${name}`}
            disabled={saving}
            onPress={() => choose(slot, index)}
            style={({ pressed }) => ({
              minHeight: 48,
              paddingVertical: 12,
              paddingHorizontal: 14,
              flexDirection: "row",
              alignItems: "center",
              gap: 8,
              borderRadius: 12,
              borderWidth: 1,
              borderColor: colors.gold,
              backgroundColor:
                look[slot] === index ? colors.gold : colors.paper,
              opacity: saving ? 0.5 : pressed ? 0.8 : 1,
            })}
          >
            {(slot === "skin" ||
              slot === "hairColor" ||
              slot === "scarfColor") && (
              <View
                accessible={false}
                style={{
                  width: 20,
                  height: 20,
                  borderRadius: 10,
                  borderWidth: 1,
                  borderColor: colors.line,
                  backgroundColor: swatchPalette(slot)[index],
                }}
              />
            )}
            <Text
              style={[
                styles.text,
                { color: look[slot] === index ? colors.onGold : colors.gold },
              ]}
            >
              {name}
            </Text>
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
      {keep(title, slot)}
      {items.map((item, index) => {
        const unlocked = availableCosmetic(item.rewardId, state.entitlements);
        const reward = state.entitlements.find(
          (entry) => entry.id === item.rewardId,
        );
        return (
          <View key={item.name} style={{ gap: 4 }}>
            <Button
              title={`${unlocked ? "Wear" : "Preview locked"}: ${item.name}`}
              secondary={look[slot] !== index}
              accessibilityLabel={`${look[slot] === index ? "Selected preview: " : ""}${item.name}${unlocked ? "" : ", locked"}`}
              disabled={saving}
              onPress={() => choose(slot, index)}
            />
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
  );
  return (
    <Card>
      <Text accessibilityRole="header" style={styles.title}>
        Your traveler
      </Text>
      <Label>
        Make this character your own. Every face, build, skin tone and hairstyle
        is free. Mix starter clothing with rewards you earn along the way.
      </Label>
      <View style={{ alignItems: "center" }}>
        <AvatarFigure
          code={showingSaved ? saved : choice}
          portrait={zoomed}
          size={zoomed ? 220 : 320}
        />
      </View>
      <Button
        title={zoomed ? "Show full traveler" : "Zoom in on face"}
        secondary
        onPress={() => setZoomed((current) => !current)}
      />
      <Label>
        {showingSaved
          ? "Showing saved look — your preview is kept"
          : dirty
            ? "Preview — not saved"
            : needsInitialSave
              ? "Starter look"
              : "Your saved look"}
      </Label>
      {dirty && (
        <Button
          title={showingSaved ? "Return to preview" : "Compare saved look"}
          secondary
          onPress={() => setComparing((current) => !current)}
        />
      )}
      {history.length > 0 && (
        <Button
          title="Undo preview edit"
          secondary
          disabled={saving}
          onPress={() => {
            setFuture((previous) => [...previous.slice(-19), choice]);
            setDraft(history[history.length - 1]);
            setHistory((previous) => previous.slice(0, -1));
            setComparing(false);
            setError("");
          }}
        />
      )}
      {future.length > 0 && (
        <Button
          title="Redo preview edit"
          secondary
          disabled={saving}
          onPress={() => {
            setHistory((previous) => [...previous.slice(-19), choice]);
            setDraft(future[future.length - 1]);
            setFuture((previous) => previous.slice(0, -1));
            setComparing(false);
            setError("");
          }}
        />
      )}
      <View style={{ gap: 8 }}>
        <Button
          title="Surprise me"
          secondary
          disabled={saving}
          onPress={() =>
            preview(
              randomizeAvatarSection(choice, "All", state.entitlements, kept),
            )
          }
        />
      </View>
      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
        {(
          ["Identity", "Hair", "Clothing", "Accessories", "Looks"] as const
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
              borderRadius: 12,
              borderWidth: 1,
              borderColor: section === name ? colors.gold : colors.line,
              backgroundColor: section === name ? colors.card : colors.paper,
            }}
          >
            <Text style={styles.text}>{name}</Text>
          </Pressable>
        ))}
      </View>
      {section !== "Looks" && (
        <View style={{ gap: 8 }}>
          <Label>
            Try just this part of your look. Your other choices stay as you made
            them.
          </Label>
          <Button
            title={`Randomize ${section.toLowerCase()} only`}
            secondary
            disabled={saving}
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
            disabled={
              saving || restoreAvatarSection(choice, saved, section) === choice
            }
            onPress={() =>
              preview(restoreAvatarSection(choice, saved, section))
            }
          />
        </View>
      )}
      {section === "Looks" && (
        <View style={{ gap: 8 }}>
          <Label>
            All {starterLooks.length} starting looks are free. Change any detail
            before saving.
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
                <AvatarFigure code={preset.code} size={120} />
                <Button
                  title={preset.name}
                  secondary
                  disabled={saving}
                  onPress={() => {
                    preview(preset.code);
                  }}
                />
              </View>
            ))}
          </View>
        </View>
      )}
      {!wearable && (
        <Label>
          Previewing a locked item. Earn its reward or choose an unlocked item
          before saving.
        </Label>
      )}
      {section === "Identity" && (
        <>
          {options("Face", "face", faceNames)}
          {options("Build", "build", buildNames)}
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
          {options("Scarf color", "scarfColor", scarfNames)}
          {look.accessory !== 5 && look.extra !== 2 && (
            <Label>Choose a linen scarf to see its color.</Label>
          )}
        </>
      )}
      <Label>
        Undo can remove an unlock. If that happens, your look returns to the
        starter item. Frames and their matching clothing also depend on your
        current rank.
      </Label>
      <Button
        title="Save avatar"
        disabled={
          (!dirty && !needsInitialSave) ||
          !wearable ||
          busy ||
          pending ||
          offline ||
          queuedCount > 0 ||
          saving
        }
        loading={saving}
        onPress={() => void save()}
      />
      {onContinue && (
        <Button
          title="Continue with this avatar"
          disabled={
            dirty ||
            needsInitialSave ||
            busy ||
            pending ||
            offline ||
            saving ||
            queuedCount > 0
          }
          onPress={onContinue}
        />
      )}
      {offline && (
        <Label>You can preview offline. Reconnect to save your avatar.</Label>
      )}
      {dirty && (
        <Button
          title="Reset avatar preview"
          secondary
          disabled={saving || queuedCount > 0}
          onPress={() => {
            setDraft(undefined);
            setHistory([]);
            setFuture([]);
            setComparing(false);
            setError("");
          }}
        />
      )}
      {!!error && (
        <Text accessibilityRole="alert" style={styles.text}>
          {error}
        </Text>
      )}
    </Card>
  );
}
export function AvatarEditor({
  onContinue,
}: Readonly<{ onContinue?: () => void }> = {}) {
  const { state } = useAccountSnapshot();
  return state ? <Wardrobe state={state} onContinue={onContinue} /> : null;
}
