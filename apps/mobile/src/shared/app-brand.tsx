import { Image, Text, View } from "react-native";
import { useAppTheme } from "./ui";
import { playfulArt } from "./playful-art";
/** The selected overlapping mountain mark, kept as its supplied raster artwork. */
export function AppBrand() {
  const { styles } = useAppTheme();
  const scale = 36 / 977;
  return (
    <View
      style={{
        flexDirection: "row",
        alignItems: "center",
        gap: 8,
        flexShrink: 1,
      }}
    >
      <View
        aria-hidden
        pointerEvents="none"
        style={{ width: 36, height: 586 * scale, overflow: "hidden" }}
      >
        <Image
          source={playfulArt.mark}
          accessible={false}
          resizeMode="stretch"
          style={{
            position: "absolute",
            width: 1448 * scale,
            height: 1086 * scale,
            left: -236 * scale,
            top: -250 * scale,
          }}
        />
      </View>
      <View style={{ flexShrink: 1, gap: 1 }}>
        <Text
          style={[styles.subtitle, { fontSize: 22, lineHeight: 26 }]}
          numberOfLines={1}
        >
          Pocket Quests
        </Text>
      </View>
    </View>
  );
}
