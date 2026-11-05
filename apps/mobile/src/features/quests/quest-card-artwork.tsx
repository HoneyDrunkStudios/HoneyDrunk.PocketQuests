import { Image } from "react-native";
import { journalArt } from "../../shared/journal-art";

const artwork: Record<string, number> = {
  c01: require("../../../assets/journal/character/fitness.jpg"),
  c02: require("../../../assets/journal/character/learning.jpg"),
  c03: require("../../../assets/journal/character/creativity.jpg"),
  c04: require("../../../assets/journal/character/work.jpg"),
  c05: require("../../../assets/journal/character/relationships.jpg"),
  c06: require("../../../assets/journal/character/home.jpg"),
  c07: require("../../../assets/journal/character/wellbeing.jpg"),
  c08: require("../../../assets/journal/character/wellbeing.jpg"),
  c09: require("../../../assets/journal/character/finances.jpg"),
  c10: require("../../../assets/journal/character/adventure.jpg"),
};

export function QuestCardArtwork({
  categoryId,
}: Readonly<{ categoryId: string }>) {
  return (
    <Image
      testID="journal-quest-artwork"
      source={artwork[categoryId] ?? journalArt.compass}
      accessible={false}
      aria-hidden
      resizeMode="contain"
      style={{ width: 88, height: 88, borderRadius: 12, flexShrink: 0 }}
    />
  );
}
