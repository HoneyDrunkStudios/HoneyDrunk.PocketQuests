import { Image, View } from "react-native";
import Ionicons from "@expo/vector-icons/Ionicons";
import { useAppTheme } from "./ui";
const categories = [
  "c01",
  "c02",
  "c03",
  "c04",
  "c05",
  "c06",
  "c07",
  "c08",
  "c09",
  "c10",
];
/** Ten consistent, activity illustrations with painted sky-and-ground vignettes; labels remain real UI text. */
export function ActivityArtwork({
  categoryId,
  size = 64,
}: Readonly<{ categoryId: string; size?: number }>) {
  const { colors } = useAppTheme();
  const index = categories.indexOf(categoryId),
    scale = size / 396.6;
  return (
    <View
      testID="journal-quest-artwork"
      aria-hidden
      pointerEvents="none"
      style={{
        width: size,
        height: size,
        overflow: "hidden",
        flexShrink: 0,
        alignItems: "center",
        justifyContent: "center",
      }}
    >
      {index < 0 ? (
        <Ionicons
          name="compass-outline"
          size={size * 0.75}
          color={colors.green}
          accessible={false}
        />
      ) : (
        <Image
          source={require("../../assets/playful/activity-icons-v2.png")}
          accessible={false}
          resizeMode="stretch"
          style={{
            position: "absolute",
            width: 1983 * scale,
            height: 793 * scale,
            left: -(index % 5) * 396.6 * scale,
            top: -Math.floor(index / 5) * 396.5 * scale,
          }}
        />
      )}
    </View>
  );
}
