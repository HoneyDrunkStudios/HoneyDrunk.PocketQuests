import { ImageBackground, Text, View } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import Ionicons from "@expo/vector-icons/Ionicons";
import { journalArt } from "./journal-art";
import { colors, styles } from "./ui";

/** Scrolls with its screen; safe area is owned here when the native header is hidden. */
export function JournalHeading({
  title,
  description,
}: Readonly<{ title: string; description?: string }>) {
  const insets = useSafeAreaInsets();
  return (
    <ImageBackground
      testID="journal-background-heading"
      source={journalArt.landscape}
      resizeMode="cover"
      imageStyle={{ width: "100%", height: "100%" }}
      style={{
        marginHorizontal: -16,
        paddingHorizontal: 26,
        paddingTop: insets.top + 16,
        paddingBottom: 12,
        minHeight: 200 + insets.top,
        justifyContent: "space-between",
      }}
    >
      <View style={{ flexDirection: "row", alignItems: "center", gap: 10 }}>
        <Ionicons
          name="compass-outline"
          size={32}
          color={colors.gold}
          accessible={false}
        />
        <Text style={[styles.subtitle, { fontSize: 24, flexShrink: 1 }]}>
          Pocket Quests
        </Text>
      </View>
      <View style={{ gap: 6, paddingTop: 78 }}>
        <Text accessibilityRole="header" style={styles.title}>
          {title}
        </Text>
        {description && (
          <Text style={[styles.muted, { fontSize: 15 }]}>{description}</Text>
        )}
      </View>
    </ImageBackground>
  );
}
