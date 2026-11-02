import { Image, View } from "react-native";
import { colors } from "../../shared/ui";
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
}: Readonly<{ portraitId?: string | null; avatarCode?: string | null }>) {
  if (avatarCode)
    return (
      <View style={{ width: 140 }}>
        <AvatarFigure code={avatarCode} size={250} />
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
