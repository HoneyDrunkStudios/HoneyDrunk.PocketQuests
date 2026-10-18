import { Image, View } from "react-native";
import { journalArt } from "../../shared/journal-art";

/** Bundled decorative scenery stays behind live labels, statuses and controls. */
export function QuestCardArtwork({
  creative,
}: Readonly<{ creative: boolean }>) {
  return (
    <View
      pointerEvents="none"
      style={{
        position: "absolute",
        top: 0,
        bottom: 0,
        left: 0,
        right: 0,
        overflow: "hidden",
        borderRadius: 14,
      }}
    >
      <Image
        testID="journal-quest-artwork"
        source={creative ? journalArt.sketchScene : journalArt.workScene}
        accessible={false}
        aria-hidden
        resizeMode="cover"
        style={{
          position: "absolute",
          bottom: 0,
          width: "100%",
          aspectRatio: 2,
        }}
      />
    </View>
  );
}
