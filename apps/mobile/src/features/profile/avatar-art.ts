import type { ImageSourcePropType } from "react-native";

export type ArtRect = readonly [number, number, number, number];
export type AvatarAtlas = {
  source: ImageSourcePropType;
  width: number;
  height: number;
};

export { hairColorMatrix } from "./avatar-palette";
