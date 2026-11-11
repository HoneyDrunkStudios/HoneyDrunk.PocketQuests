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
  bodyType: number;
  extra: number;
  scarfColor: number;
  pose: number;
  backdrop: number;
  room: number;
  upperLeft: number;
  upperRight: number;
  lowerLeft: number;
  lowerRight: number;
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
export const buildNames = ["Balanced", "Broad", "Full", "Petite"];
export const bodyTypeNames = ["Male", "Female"];
export const petiteBuild = 3;
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
  { name: "Maker’s vest", rewardId: "PQ-RANK-E" },
  { name: "Cozy knit", rewardId: "PQ-RANK-D" },
  { name: "Scholar’s coat", rewardId: "PQ-RANK-C" },
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

export const frontPose = 2;
export const poses = [
  { name: "At ease", rewardId: null },
  { name: "Relaxed stance", rewardId: "PQ-CAT-B01" },
  { name: "Front-facing", rewardId: null },
];
export const backdrops = [
  { name: "Floating islands", rewardId: null },
  { name: "Golden horizon", rewardId: "PQ-CAT-A01" },
  { name: "Moonlit sky", rewardId: "PQ-CAT-F02" },
];

export function decodeAvatar(code: unknown): AvatarLook {
  const compact =
    typeof code === "string" &&
    ((code.length === 20 &&
      /^v5:[0-5][0-7][0-7][0-5][0-5][0-6][0-2][0-2][0-7][0-1][0-2]:[0-2][0-6][0-6][0-6][0-6]$/.test(
        code,
      )) ||
      (code.length === 21 &&
        /^v6:[0-5][0-7][0-7][0-5][0-5][0-6][0-3][0-2][0-7][0-2][0-2][0-1]:[0-2][0-6][0-6][0-6][0-6]$/.test(
          code,
        )))
      ? code.split(":")
      : null;
  const layout = compact ? compact[2].split("").map(Number) : [0, 0, 0, 0, 0];
  const value =
    typeof code === "string" &&
    ((code.length === 12 && /^v1:[0-5]:[0-3]:[0-3]:[0-3]:[0-3]$/.test(code)) ||
      (code.length === 16 &&
        /^v2:[0-5]:[0-7]:[0-7]:[0-5]:[0-5]:[0-6]:[0-2]$/.test(code)) ||
      (code.length === 24 &&
        /^v4:[0-5]:[0-7]:[0-7]:[0-5]:[0-5]:[0-6]:[0-2]:[0-2]:[0-7]:[0-1]:[0-2]$/.test(
          code,
        )) ||
      (code.length === 20 &&
        /^v3:[0-5]:[0-7]:[0-7]:[0-5]:[0-5]:[0-6]:[0-2]:[0-2]:[0-7]$/.test(
          code,
        )))
      ? code
      : starterAvatar;
  const [
    skin,
    hairColor,
    hairStyle,
    outfit,
    accessory,
    face = 0,
    build = 0,
    extra = 0,
    scarfColor = 0,
    pose = 0,
    backdrop = 0,
    bodyType = 0,
  ] = (compact ? compact[1].split("") : value.split(":").slice(1)).map(Number);
  return {
    skin,
    hairColor,
    hairStyle,
    outfit,
    accessory,
    face,
    build,
    bodyType,
    extra,
    scarfColor,
    pose,
    backdrop,
    room: layout[0],
    upperLeft: layout[1],
    upperRight: layout[2],
    lowerLeft: layout[3],
    lowerRight: layout[4],
  };
}
export function encodeAvatar(look: AvatarLook) {
  const needsExpandedCode =
    look.bodyType === 1 ||
    look.build === petiteBuild ||
    look.pose === frontPose;
  if (
    needsExpandedCode ||
    look.room ||
    look.upperLeft ||
    look.upperRight ||
    look.lowerLeft ||
    look.lowerRight
  ) {
    const core = [
      look.skin,
      look.hairColor,
      look.hairStyle,
      look.outfit,
      look.accessory,
      look.face,
      look.build,
      look.extra,
      look.scarfColor,
      look.pose,
      look.backdrop,
    ].join("");
    const layout = [
      look.room,
      look.upperLeft,
      look.upperRight,
      look.lowerLeft,
      look.lowerRight,
    ].join("");
    return needsExpandedCode
      ? `v6:${core}${look.bodyType}:${layout}`
      : `v5:${core}:${layout}`;
  }
  const choices = `${look.skin}:${look.hairColor}:${look.hairStyle}:${look.outfit}:${look.accessory}`;
  if (look.pose || look.backdrop)
    return `v4:${choices}:${look.face}:${look.build}:${look.extra}:${look.scarfColor}:${look.pose}:${look.backdrop}`;
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
  { name: "Trail tunic", code: "v1:2:0:0:0:0" },
  { name: "Field shirt", code: "v2:2:0:0:4:0:0:0" },
  { name: "City jacket", code: "v2:2:0:0:5:0:0:0" },
];

/** Clothing previews preserve the identity the player just chose. */
export function outfitPreview(current: string, preset: string) {
  return encodeAvatar({
    ...decodeAvatar(current),
    outfit: decodeAvatar(preset).outfit,
  });
}

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
    bodyType: pick(bodyTypeNames.length),
    extra: 0,
    scarfColor: 0,
    pose: 0,
    backdrop: 0,
    room: 0,
    upperLeft: 0,
    upperRight: 0,
    lowerLeft: 0,
    lowerRight: 0,
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
export const roomStyles = [
  { name: "Traveler's Loft", rewardId: null },
  {
    name: "Sunlit Workshop",
    rewardId: "PQ-RANK-E",
  },
  {
    name: "Moonlit Study",
    rewardId: "PQ-RANK-D",
  },
];
export const roomDecorations = [
  { name: "Empty display", rewardId: null },
  { name: "Maker's Crest", rewardId: "PQ-LEVEL-02" },
  { name: "Restful Keepsake", rewardId: "PQ-LEVEL-03" },
  { name: "Scholar's Books", rewardId: "PQ-LEVEL-04" },
  { name: "Explorer's Compass", rewardId: "PQ-LEVEL-05" },
  { name: "Everyday Toolkit", rewardId: "PQ-LEVEL-06" },
  { name: "Horizon Lantern", rewardId: "PQ-LEVEL-07" },
];
export const roomSlots = [
  "upperLeft",
  "upperRight",
  "lowerLeft",
  "lowerRight",
] as const;
export type RoomSlot = (typeof roomSlots)[number];
export function canWear(look: AvatarLook, rewards: State["entitlements"]) {
  return (
    availableCosmetic(outfits[look.outfit].rewardId, rewards) &&
    availableCosmetic(accessories[look.accessory].rewardId, rewards) &&
    availableCosmetic(poses[look.pose].rewardId, rewards) &&
    availableCosmetic(backdrops[look.backdrop].rewardId, rewards) &&
    availableCosmetic(roomStyles[look.room].rewardId, rewards) &&
    roomSlots.every((slot) =>
      availableCosmetic(roomDecorations[look[slot]].rewardId, rewards),
    )
  );
}

/** Names of cosmetics linked to a server-confirmed reward milestone. */
export function rewardCosmetics(rewardId: string) {
  return [
    ...outfits,
    ...accessories,
    ...poses,
    ...backdrops,
    ...roomStyles,
    ...roomDecorations.slice(1),
  ]
    .filter((item) => item.rewardId === rewardId)
    .map((item) => item.name);
}
