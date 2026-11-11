import { Image, Text, View } from "react-native";
import { useAppTheme } from "./ui";
const ranks = ["F", "E", "D", "C", "B", "A", "S"];
// Measured pixel regions of the generated 2172 × 724 atlas; higher tiers have wider silhouettes.
const regions = [
  [33, 296, 190, 220],
  [258, 271, 210, 258],
  [501, 257, 228, 278],
  [758, 233, 259, 312],
  [1036, 212, 292, 336],
  [1347, 155, 324, 404],
  [1693, 80, 478, 500],
];
/** Clear rank letters, progressively refined materials, and accessible labels. */
export function RankInsignia({
  rank,
  size = 30,
  label = true,
}: Readonly<{ rank: string; size?: number; label?: boolean }>) {
  const { styles } = useAppTheme();
  const index = ranks.indexOf(rank);
  if (index < 0) return <Text style={styles.muted}>Rank {rank}</Text>;
  const [x, y, width, height] = regions[index];
  const scale = (size * (1 + index * 0.05)) / height;
  return (
    <View
      accessible={label}
      accessibilityLabel={label ? `Rank ${rank}` : undefined}
      aria-hidden={!label}
      style={{
        flexDirection: "row",
        alignItems: "center",
        gap: 7,
        flexShrink: 0,
      }}
    >
      <View
        pointerEvents="none"
        style={{
          width: size * 1.2,
          height: size * 1.3,
          alignItems: "center",
          justifyContent: "center",
        }}
      >
        <View
          style={{
            width: width * scale,
            height: height * scale,
            overflow: "hidden",
          }}
        >
          <Image
            source={require("../../assets/playful/rank-insignia-v2.png")}
            accessible={false}
            resizeMode="stretch"
            style={{
              position: "absolute",
              width: 2172 * scale,
              height: 724 * scale,
              left: -x * scale,
              top: -y * scale,
            }}
          />
        </View>
      </View>
      {label && (
        <Text
          style={[
            styles.text,
            { fontWeight: "700", fontSize: size >= 40 ? 24 : 14 },
          ]}
        >
          Rank {rank}
        </Text>
      )}
    </View>
  );
}
