import { Image, View } from "react-native";
import { useAppTheme } from "../../shared/ui";
import { playfulArt } from "../../shared/playful-art";
import { decodeAvatar } from "./avatar-look";
import { usePreviewAvatar } from "./appearance-autosave";

/** Bundled scene follows the same quietly saved appearance as the traveler. */
export function TravelerBackdrop({
  avatarCode,
  width,
  height,
  portrait = false,
}: Readonly<{
  avatarCode?: string | null;
  width: number;
  height: number | "100%";
  portrait?: boolean;
}>) {
  const { isDark } = useAppTheme();
  const look = decodeAvatar(usePreviewAvatar(avatarCode));
  const sceneWidth =
    portrait && typeof height === "number" ? (height * 390) / 356 : width;
  const sceneHeight = look.backdrop === 0 ? height : (sceneWidth * 881) / 893;
  // Fit the complete wallpaper without stretching, anchored to its landing area.
  const wallpaperWidth = Math.max(
    width,
    typeof height === "number" ? (height * 1312) / 1199 : width,
  );
  const wallpaperHeight =
    typeof height === "number" ? (wallpaperWidth * 1199) / 1312 : height;
  return (
    <View
      aria-hidden
      pointerEvents="none"
      style={{
        position: "absolute",
        left: 0,
        top: 0,
        width,
        height,
        overflow: "hidden",
        opacity: look.backdrop === 0 ? 1 : isDark ? 0.58 : 1,
      }}
    >
      {look.backdrop === 0 ? (
        <Image
          source={isDark ? playfulArt.homeStageNight : playfulArt.homeStage}
          accessible={false}
          resizeMode="stretch"
          style={{
            position: "absolute",
            left: (width - wallpaperWidth) / 2,
            bottom: 0,
            width: wallpaperWidth,
            height: wallpaperHeight,
          }}
        />
      ) : (
        <Image
          source={require("../../../assets/playful/unlockable-stages-v1.png")}
          accessible={false}
          resizeMode="stretch"
          style={{
            position: "absolute",
            left: (width - sceneWidth) / 2 - (look.backdrop - 1) * sceneWidth,
            bottom: 0,
            width: sceneWidth * 2,
            height: sceneHeight,
          }}
        />
      )}
    </View>
  );
}
