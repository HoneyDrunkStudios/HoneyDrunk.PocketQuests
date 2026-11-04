import type { State } from "../../shared/contracts";
import {
  accessories,
  availableCosmetic,
  decodeAvatar,
  encodeAvatar,
  outfits,
  randomAvatar,
  type AvatarLook,
} from "./avatar-look";

export const avatarSectionSlots = {
  Identity: ["face", "build", "skin"],
  Hair: ["hairColor", "hairStyle"],
  Clothing: ["outfit"],
  Accessories: ["accessory"],
} as const satisfies Record<string, readonly (keyof AvatarLook)[]>;
export type AvatarSection = keyof typeof avatarSectionSlots;

/** Changes only the selected group; kept traits and current reward ownership apply. */
export function randomizeAvatarSection(
  code: string,
  section: AvatarSection | "All",
  rewards: State["entitlements"],
  kept: Partial<Record<keyof AvatarLook, boolean>>,
  random: () => number = Math.random,
) {
  const current = decodeAvatar(code);
  const candidate = decodeAvatar(randomAvatar(rewards, random));
  const slots =
    section === "All"
      ? Object.values(avatarSectionSlots).flat()
      : avatarSectionSlots[section];
  for (const slot of slots) {
    const owned =
      slot === "outfit"
        ? availableCosmetic(outfits[current.outfit].rewardId, rewards)
        : slot === "accessory"
          ? availableCosmetic(accessories[current.accessory].rewardId, rewards)
          : true;
    if (!kept[slot] || !owned) current[slot] = candidate[slot];
  }
  return encodeAvatar(current);
}

/** An explicit preview reset affects one group, leaving all other draft choices alone. */
export function restoreAvatarSection(
  code: string,
  saved: string,
  section: AvatarSection,
) {
  const current = decodeAvatar(code);
  const original = decodeAvatar(saved);
  for (const slot of avatarSectionSlots[section])
    current[slot] = original[slot];
  return encodeAvatar(current);
}
