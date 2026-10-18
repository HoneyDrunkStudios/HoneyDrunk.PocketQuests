import { Image } from "react-native";
import { colors } from "../../shared/ui";
import { journalArt } from "../../shared/journal-art";
export function CharacterPortrait() {
  return (
    <Image
      source={journalArt.traveler}
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
