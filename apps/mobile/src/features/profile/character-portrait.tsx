import { usePreviewAvatar } from "./appearance-autosave";
import { Image, View } from "react-native";
import { useAppTheme } from "../../shared/ui";
import { AvatarFigure } from "./avatar-figure";
import { journalArt } from "../../shared/journal-art";
export const characterPortraits = [
  {
    id: "portrait-1",
    name: "Teal traveler",
    source: require("../../../assets/journal/character/portrait-1.jpg"),
  },
  {
    id: "portrait-2",
    name: "Auburn traveler",
    source: require("../../../assets/journal/character/portrait-2.jpg"),
  },
  {
    id: "portrait-3",
    name: "Night traveler",
    source: require("../../../assets/journal/character/portrait-3.jpg"),
  },
  {
    id: "portrait-4",
    name: "Violet traveler",
    source: require("../../../assets/journal/character/portrait-4.jpg"),
  },
  {
    id: "portrait-5",
    name: "Forest traveler",
    source: require("../../../assets/journal/character/portrait-5.jpg"),
  },
  {
    id: "portrait-6",
    name: "Silver traveler",
    source: require("../../../assets/journal/character/portrait-6.jpg"),
  },
] as const;
export function CharacterPortrait({
  portraitId,
  avatarCode,
  bust = false,
  size,
  pose = "relaxed",
}: Readonly<{
  portraitId?: string | null;
  avatarCode?: string | null;
  bust?: boolean;
  size?: number;
  pose?: "neutral" | "relaxed";
}>) {
  const { colors } = useAppTheme();
  const previewAvatar = usePreviewAvatar(avatarCode);
  if (previewAvatar)
    return (
      <View style={{ width: bust ? (size ?? 176) : (size ?? 250) * 0.55 }}>
        <AvatarFigure
          code={previewAvatar}
          portrait={bust}
          size={size ?? 250}
          pose={pose}
        />
      </View>
    );
  return (
    <Image
      source={
        characterPortraits.find((item) => item.id === portraitId)?.source ??
        journalArt.traveler
      }
      accessible
      accessibilityRole="image"
      accessibilityLabel="Illustrated traveler character portrait"
      style={{
        width: 112,
        height: 112,
        borderRadius: 56,
        borderWidth: 2,
        borderColor: colors.gold,
      }}
    />
  );
}

export { TravelerBackdrop } from "./avatar-backdrop";
