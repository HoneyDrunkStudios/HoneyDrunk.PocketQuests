import type { ImageSourcePropType } from "react-native";

/** Source rectangles are calibrated against the original, unmodified PNG atlases. */
export type ArtRect = readonly [number, number, number, number];
export type AvatarAtlas = {
  source: ImageSourcePropType;
  width: number;
  height: number;
};
export const avatarArt: Record<
  | "faces"
  | "hair"
  | "outfits"
  | "accessories"
  | "facesExpanded"
  | "facesEast"
  | "facesSouth"
  | "hairExpanded"
  | "builds"
  | "starters"
  | "starterAccessories",
  AvatarAtlas
> = {
  facesEast: {
    source: require("../../../assets/avatar/faces-east.png"),
    width: 2171,
    height: 724,
  },
  facesSouth: {
    source: require("../../../assets/avatar/faces-south.png"),
    width: 2172,
    height: 724,
  },
  facesExpanded: {
    source: require("../../../assets/avatar/faces-expanded.png"),
    width: 2172,
    height: 724,
  },
  hairExpanded: {
    source: require("../../../assets/avatar/hair-expanded.png"),
    width: 2172,
    height: 724,
  },
  builds: {
    source: require("../../../assets/avatar/outfits-builds.png"),
    width: 1774,
    height: 887,
  },
  starters: {
    source: require("../../../assets/avatar/outfits-starters.png"),
    width: 1536,
    height: 1024,
  },
  starterAccessories: {
    source: require("../../../assets/avatar/accessories-starters.png"),
    width: 1774,
    height: 887,
  },
  faces: {
    source: require("../../../assets/avatar/faces.png"),
    width: 1536,
    height: 1024,
  },
  hair: {
    source: require("../../../assets/avatar/hair.png"),
    width: 2172,
    height: 724,
  },
  outfits: {
    source: require("../../../assets/avatar/outfits.png"),
    width: 1774,
    height: 887,
  },
  accessories: {
    source: require("../../../assets/avatar/accessories.png"),
    width: 1774,
    height: 887,
  },
};
export const faceRegions: readonly ArtRect[] = [
  [175, 20, 290, 480],
  [625, 20, 290, 480],
  [1075, 20, 290, 480],
  [175, 512, 290, 480],
  [625, 512, 290, 480],
  [1075, 512, 290, 480],
];
export const outfitRegions: readonly ArtRect[] = [
  [35, 40, 410, 825],
  [461, 40, 410, 825],
  [890, 40, 410, 825],
  [1324, 40, 440, 825],
];
export const hairRegions: readonly ArtRect[] = [
  [0, 0, 543, 724],
  [570, 25, 502, 640],
  [1073, 25, 595, 690],
  [1685, 50, 460, 410],
];
// Offset the hair opening, rather than the sprite bounds, over the same face anchor.
export const hairPlacement: readonly ArtRect[] = [
  [114, 15, 151, 201],
  [120.51, 20.94, 139.6, 177.68],
  [120.38, 24.94, 165.46, 191.56],
  [132.57, 17.88, 127.92, 113.82],
];

/** Preserve painted luminance and alpha; never flatten hair into a solid tint. */
export function hairColorMatrix(index: number): number[] {
  const palettes = [
    [0.31, 0.27, 0.29, 0.015, 0.012, 0.018],
    [0.69, 0.43, 0.25, 0.035, 0.018, 0.008],
    [1.04, 0.82, 0.48, 0.08, 0.045, 0.015],
    [0.91, 0.98, 1.05, 0.08, 0.08, 0.08],
    [0.92, 0.37, 0.16, 0.045, 0.015, 0.005],
    [0.17, 0.16, 0.19, 0.006, 0.006, 0.008],
    [0.57, 0.27, 0.53, 0.025, 0.01, 0.025],
    [0.23, 0.55, 0.56, 0.008, 0.025, 0.025],
  ];
  const [r, g, b, ro, go, bo] = palettes[index] ?? palettes[0];
  return [r, 0, 0, 0, ro, 0, g, 0, 0, go, 0, 0, b, 0, bo, 0, 0, 0, 1, 0];
}

export function faceArtwork(face: number, skin: number) {
  if (face === 0)
    return { atlas: avatarArt.faces, region: faceRegions[skin], split: 350 };
  if (face >= 3) {
    const layouts = [
      {
        starts: [90, 441, 793, 1147, 1500, 1853],
        y: 0,
        width: 244,
        height: 351,
      },
      {
        starts: [82, 434, 786, 1142, 1495, 1848],
        y: 352,
        width: 255,
        height: 368,
      },
      {
        starts: [65, 427, 790, 1152, 1514, 1876],
        y: 0,
        width: 233,
        height: 351,
      },
      {
        starts: [67, 429, 792, 1154, 1516, 1878],
        y: 360,
        width: 228,
        height: 360,
      },
    ];
    const layout = layouts[face - 3];
    return {
      atlas: face < 5 ? avatarArt.facesEast : avatarArt.facesSouth,
      region: [
        layout.starts[skin],
        layout.y,
        layout.width,
        layout.height,
      ] as ArtRect,
      split: 270,
    };
  }
  const starts =
    face === 1
      ? [78, 428, 781, 1145, 1504, 1862]
      : [69, 425, 777, 1141, 1499, 1858];
  return {
    atlas: avatarArt.facesExpanded,
    region: [
      starts[skin],
      face === 1 ? 0 : 361,
      face === 1 ? 238 : 250,
      360,
    ] as ArtRect,
    split: 270,
  };
}

export function hairArtwork(style: number) {
  if (style === 7) return null;
  if (style < 4)
    return {
      atlas: avatarArt.hair,
      region: hairRegions[style],
      placement: hairPlacement[style],
    };
  const regions: readonly ArtRect[] = [
    [75, 0, 665, 724],
    [830, 0, 495, 724],
    [1380, 0, 775, 700],
  ];
  const placements: readonly ArtRect[] = [
    [107, -15, 170, 185],
    [129, -15, 137, 200],
    [92, -10, 217, 196],
  ];
  return {
    atlas: avatarArt.hairExpanded,
    region: regions[style - 4],
    placement: placements[style - 4],
  };
}

export function outfitArtwork(build: number, outfit: number) {
  if (outfit < 4 && build === 0)
    return {
      atlas: avatarArt.outfits,
      region: outfitRegions[outfit],
      placement: (outfit === 3
        ? [33, 151, 334, 628]
        : [44, 151, 312, 628]) as ArtRect,
      waist: 326,
    };
  if (outfit < 4) {
    const centers = [246, 670, 1093, 1525];
    const width = build === 1 ? 350 : 368;
    return {
      atlas: avatarArt.builds,
      region: [
        centers[outfit] - 185,
        build === 1 ? 5 : 456,
        370,
        build === 1 ? 446 : 427,
      ] as ArtRect,
      placement: [(400 - width) / 2, 151, width, 628] as ArtRect,
      waist: build === 1 ? 375 : 365,
    };
  }
  const centers = [335, 761, 1198];
  const widths = [312, 350, 368];
  const width = widths[build];
  return {
    atlas: avatarArt.starters,
    region: [
      centers[build] - 180,
      outfit === 4 ? 8 : 524,
      360,
      outfit === 4 ? 509 : 486,
    ] as ArtRect,
    placement: [(400 - width) / 2, 151, width, 628] as ArtRect,
    waist: outfit === 4 ? 370 : 360,
  };
}
