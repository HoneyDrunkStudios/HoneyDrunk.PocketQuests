import type { State } from "../../shared/contracts";

export const starterAvatar = "v1:2:0:0:0:0";
export type AvatarLook = {
  skin: number;
  hairColor: number;
  hairStyle: number;
  outfit: number;
  accessory: number;
  face: number;
  build: number;
  extra: number;
  scarfColor: number;
};
export const skinTones = [
  "#F5D5BC",
  "#E8B78D",
  "#C88D64",
  "#A46744",
  "#784932",
  "#4C3028",
];
export const hairColors = [
  "#30282D",
  "#79513A",
  "#D9B46D",
  "#BFCAD4",
  "#A84E30",
  "#242124",
  "#76516E",
  "#366F70",
];
export const hairNames = [
  "Midnight",
  "Chestnut",
  "Honey",
  "Silver",
  "Copper",
  "Raven",
  "Plum",
  "Teal",
];
export const hairStyles = [
  "Swept",
  "Curls",
  "Long",
  "Cropped",
  "Tied back",
  "Braided",
  "Coils",
  "Shaved",
];
export const faceNames = [
  "Soft",
  "Angular",
  "Round",
  "Gentle",
  "Defined",
  "Heart",
  "Oval",
];
export const buildNames = ["Balanced", "Broad", "Full"];
export const extraAccessories = [
  "No extra accessory",
  "Add round glasses",
  "Add linen scarf",
];
export const scarfNames = [
  "Original linen",
  "Moss",
  "Ocean",
  "Plum",
  "Rust",
  "Midnight",
  "Cream",
  "Gold",
];
export const scarfColors = [
  "#B8A98C",
  "#5C8057",
  "#397D99",
  "#86568E",
  "#B86342",
  "#33455F",
  "#E4D7BB",
  "#C59B48",
];
export const outfits = [
  { name: "Trail tunic", rewardId: null },
  { name: "Maker’s vest", rewardId: "PQ-CAT-A01" },
  { name: "Cozy knit", rewardId: "PQ-CAT-A02" },
  { name: "Scholar’s coat", rewardId: "PQ-CAT-F01" },
  { name: "Field shirt", rewardId: null },
  { name: "City jacket", rewardId: null },
];
export const accessories = [
  { name: "No accessory", rewardId: null },
  { name: "Explorer’s satchel", rewardId: "PQ-CAT-B01" },
  { name: "Everyday toolkit", rewardId: "PQ-CAT-B02" },
  { name: "Horizon cape", rewardId: "PQ-CAT-F02" },
  { name: "Round glasses", rewardId: null },
  { name: "Linen scarf", rewardId: null },
];

export function decodeAvatar(code: unknown): AvatarLook {
  const value =
    typeof code === "string" &&
    ((code.length === 12 && /^v1:[0-5]:[0-3]:[0-3]:[0-3]:[0-3]$/.test(code)) ||
      (code.length === 16 &&
        /^v2:[0-5]:[0-7]:[0-7]:[0-5]:[0-5]:[0-6]:[0-2]$/.test(code)) ||
      (code.length === 20 &&
        /^v3:[0-5]:[0-7]:[0-7]:[0-5]:[0-5]:[0-6]:[0-2]:[0-2]:[0-7]$/.test(
          code,
        )))
      ? code
      : starterAvatar;
  const [
    ,
    skin,
    hairColor,
    hairStyle,
    outfit,
    accessory,
    face = 0,
    build = 0,
    extra = 0,
    scarfColor = 0,
  ] = value.split(":").map(Number);
  return {
    skin,
    hairColor,
    hairStyle,
    outfit,
    accessory,
    face,
    build,
    extra,
    scarfColor,
  };
}
export function encodeAvatar(look: AvatarLook) {
  const choices = `${look.skin}:${look.hairColor}:${look.hairStyle}:${look.outfit}:${look.accessory}`;
  if (look.extra || look.scarfColor)
    return `v3:${choices}:${look.face}:${look.build}:${look.extra}:${look.scarfColor}`;
  return look.face === 0 &&
    look.build === 0 &&
    look.hairColor < 4 &&
    look.hairStyle < 4 &&
    look.outfit < 4 &&
    look.accessory < 4
    ? `v1:${choices}`
    : `v2:${choices}:${look.face}:${look.build}`;
}

export const starterLooks = [
  { name: "Quiet reader", code: "v2:1:1:0:4:4:0:0" },
  { name: "City roamer", code: "v2:4:0:3:5:4:1:1" },
  { name: "Garden day", code: "v2:3:3:6:4:5:2:2" },
  { name: "Night maker", code: "v2:5:6:5:5:0:0:2" },
  { name: "Trail walker", code: "v2:0:4:4:0:5:1:0" },
  { name: "Fresh start", code: "v2:2:2:7:4:0:2:1" },
  { name: "Morning stroll", code: "v2:1:5:4:4:0:3:0" },
  { name: "Workshop day", code: "v2:2:0:3:5:4:4:1" },
  { name: "Open road", code: "v2:3:1:7:0:5:5:2" },
  { name: "Evening pages", code: "v2:4:5:2:5:4:6:0" },
];

/** A local preview only; randomness never grants or saves an entitlement. */
export function randomAvatar(
  rewards: State["entitlements"],
  random: () => number = Math.random,
): string {
  const pick = (count: number) =>
    Math.min(count - 1, Math.max(0, Math.floor(random() * count)));
  const owned = (items: typeof outfits) =>
    items
      .map((item, index) => ({ item, index }))
      .filter(({ item }) => availableCosmetic(item.rewardId, rewards))
      .map(({ index }) => index);
  const clothing = owned(outfits);
  const extras = owned(accessories);
  return encodeAvatar({
    skin: pick(skinTones.length),
    hairColor: pick(hairNames.length),
    hairStyle: pick(hairStyles.length),
    outfit: clothing[pick(clothing.length)],
    accessory: extras[pick(extras.length)],
    face: pick(faceNames.length),
    build: pick(buildNames.length),
    extra: 0,
    scarfColor: 0,
  });
}
export function availableCosmetic(
  rewardId: string | null,
  rewards: State["entitlements"],
) {
  return (
    rewardId === null ||
    rewards.some((reward) => reward.id === rewardId && reward.earned)
  );
}
export function canWear(look: AvatarLook, rewards: State["entitlements"]) {
  return (
    availableCosmetic(outfits[look.outfit].rewardId, rewards) &&
    availableCosmetic(accessories[look.accessory].rewardId, rewards)
  );
}

/** Cosmetics share the existing reward milestone, including its current-rank gate. */
export function rewardCosmetics(rewardId: string) {
  return [...outfits, ...accessories]
    .filter((item) => item.rewardId === rewardId)
    .map((item) => item.name);
}
