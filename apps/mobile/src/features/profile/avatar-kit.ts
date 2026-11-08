import type { ArtRect, AvatarAtlas } from "./avatar-art";
import { femaleBodies, malePetite } from "./avatar-body-data";
import { frontBodies, frontFaces, frontHair } from "./avatar-front-data";
import { avatarKitData } from "./avatar-kit-data";
import { buildNames, frontPose, petiteBuild } from "./avatar-look";
export const avatarRig = {
  crownY: 93,
  cranialCenterX: 200,
  chinY: 183,
  bodyTop: 164,
  soleY: 779,
  headScale: 90 / 340,
} as const;
export type KitSprite = {
  atlas: AvatarAtlas;
  region: ArtRect;
  placement: ArtRect;
  skinMask?: string;
  skinReference?: readonly number[];
};
/** Fit every head layer around the same fixed jaw joint, with adult proportions. */
export function headPlacement([x, y, width, height]: ArtRect): ArtRect {
  const scale = 90 / 111;
  return [
    avatarRig.cranialCenterX + (x - avatarRig.cranialCenterX) * scale,
    avatarRig.chinY + (y - avatarRig.chinY) * scale,
    width * scale,
    height * scale,
  ];
}
const bodyAtlases: readonly AvatarAtlas[] = [
  {
    source: require("../../../assets/avatar/kit-v7/outfit-0.png"),
    width: 1086,
    height: 1448,
  },
  {
    source: require("../../../assets/avatar/kit-v7/outfit-1.png"),
    width: 1086,
    height: 1448,
  },
  {
    source: require("../../../assets/avatar/kit-v7/outfit-2.png"),
    width: 1086,
    height: 1448,
  },
  {
    source: require("../../../assets/avatar/kit-v7/outfit-3.png"),
    width: 1086,
    height: 1448,
  },
  {
    source: require("../../../assets/avatar/kit-v7/outfit-4.png"),
    width: 1086,
    height: 1448,
  },
  {
    source: require("../../../assets/avatar/kit-v7/outfit-5.png"),
    width: 1086,
    height: 1448,
  },
];
const hairAtlases: readonly AvatarAtlas[] = [
  {
    source: require("../../../assets/avatar/kit-v7/hair-0.png"),
    width: 2172,
    height: 724,
  },
  {
    source: require("../../../assets/avatar/kit-v7/hair-1.png"),
    width: 2172,
    height: 724,
  },
  {
    source: require("../../../assets/avatar/kit-v7/hair-2.png"),
    width: 2172,
    height: 724,
  },
  {
    source: require("../../../assets/avatar/kit-v7/hair-3.png"),
    width: 2172,
    height: 724,
  },
  {
    source: require("../../../assets/avatar/kit-v7/hair-4.png"),
    width: 2172,
    height: 724,
  },
  {
    source: require("../../../assets/avatar/kit-v7/hair-5.png"),
    width: 2172,
    height: 724,
  },
  {
    source: require("../../../assets/avatar/kit-v7/hair-6.png"),
    width: 2172,
    height: 724,
  },
];
const faceAtlas: AvatarAtlas = {
  source: require("../../../assets/avatar/kit-v7/faces.png"),
  width: 1774,
  height: 887,
};
export function kitBody(
  outfit: number,
  build: number,
  pose: number,
  bodyType: number,
): KitSprite {
  if (pose === frontPose) {
    const sheet = frontBodies[bodyType * buildNames.length + build];
    return { atlas: sheet.atlas, ...sheet.frames[outfit] };
  }
  if (bodyType === 1 || build === petiteBuild) {
    const sheet = bodyType === 1 ? femaleBodies[build][pose] : malePetite[pose];
    return { atlas: sheet.atlas, ...sheet.frames[outfit] };
  }
  return {
    atlas: bodyAtlases[outfit],
    ...avatarKitData.bodies[outfit][pose * 3 + build],
  };
}
export function kitFace(face: number, pose: number): KitSprite {
  if (pose === frontPose)
    return { atlas: frontFaces.atlas, ...frontFaces.frames[face] };
  const sprite = avatarKitData.faces[face];
  return {
    atlas: faceAtlas,
    ...sprite,
    placement: headPlacement(sprite.placement),
  };
}
export function kitHair(style: number, pose: number): KitSprite | null {
  if (style === 7) return null;
  if (pose === frontPose)
    return { atlas: frontHair.atlas, ...frontHair.frames[style] };
  const sprite = avatarKitData.hairs[style];
  return {
    atlas: hairAtlases[style],
    ...sprite,
    placement: headPlacement(sprite.placement),
  };
}
