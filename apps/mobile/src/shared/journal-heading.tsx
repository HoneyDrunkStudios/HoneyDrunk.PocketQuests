import { Image, Pressable, Text, View } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import Ionicons from "@expo/vector-icons/Ionicons";
import { Link } from "expo-router";
import { journalArt } from "./journal-art";
import { colors, styles } from "./ui";

/** Scrolls with its screen; safe area is owned here when the native header is hidden. */
export function JournalHeading({
  title,
  description,
  compact = false,
  showSettings = true,
}: Readonly<{
  title: string;
  description?: string;
  compact?: boolean;
  showSettings?: boolean;
}>) {
  const insets = useSafeAreaInsets();
  return (
    <View style={{ marginHorizontal: -16, backgroundColor: colors.paper }}>
      <View style={{ width: "100%", aspectRatio: 390 / 210 }}>
        <View pointerEvents="none" style={{ position: "absolute", inset: 0 }}>
          <Image
            testID="journal-background-heading"
            source={journalArt.landscape}
            resizeMode="contain"
            style={{ width: "100%", height: "100%" }}
            accessible={false}
          />
        </View>
        <View
          style={{
            flexDirection: "row",
            alignItems: "center",
            gap: 10,
            paddingHorizontal: 20,
            paddingTop: insets.top + 12,
          }}
        >
          <Ionicons
            name="compass-outline"
            size={32}
            color={colors.gold}
            accessible={false}
            aria-hidden
          />
          <Text style={[styles.subtitle, { fontSize: 23, flex: 1 }]}>
            Pocket Quests
          </Text>
          {showSettings && (
            <Link href="/(tabs)/profile" asChild>
              <Pressable
                accessibilityRole="button"
                accessibilityLabel="Settings and profile"
                style={{
                  padding: 10,
                  minWidth: 48,
                  minHeight: 48,
                  alignItems: "center",
                  justifyContent: "center",
                  backgroundColor: "#091A29CC",
                  borderRadius: 24,
                }}
              >
                <Ionicons
                  name="settings-outline"
                  size={26}
                  color={colors.ink}
                  accessible={false}
                  aria-hidden
                />
              </Pressable>
            </Link>
          )}
        </View>
      </View>
      <View
        style={{
          gap: 6,
          marginTop: -24,
          paddingHorizontal: 24,
          paddingTop: compact ? 0 : 4,
          paddingBottom: 0,
        }}
      >
        <Text accessibilityRole="header" style={styles.title}>
          {title}
        </Text>
        {description && (
          <Text style={[styles.muted, { fontSize: 15 }]}>{description}</Text>
        )}
      </View>
    </View>
  );
}
