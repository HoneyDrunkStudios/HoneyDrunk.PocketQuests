import { AppBrand } from "./app-brand";
import { Pressable, Text, View } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import Ionicons from "@expo/vector-icons/Ionicons";
import { Link } from "expo-router";
import { useAppTheme } from "./ui";
/** Compact editorial heading; the app owns the safe-area padding. */
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
  const { colors, styles } = useAppTheme();
  const insets = useSafeAreaInsets();
  return (
    <View
      style={{
        paddingTop: insets.top + 18,
        paddingHorizontal: 8,
        paddingBottom: compact ? 12 : 20,
        gap: 14,
      }}
    >
      <View style={{ flexDirection: "row", alignItems: "center", gap: 10 }}>
        <View style={{ flex: 1 }}>
          <AppBrand />
        </View>
        {showSettings && (
          <Link href="/settings" asChild>
            <Pressable
              accessibilityRole="link"
              accessibilityLabel="Settings and profile"
              style={{
                width: 48,
                height: 48,
                justifyContent: "center",
                alignItems: "center",
              }}
            >
              <Ionicons
                name="settings-outline"
                size={23}
                color={colors.green}
                accessible={false}
              />
            </Pressable>
          </Link>
        )}
      </View>
      <Text accessibilityRole="header" style={styles.title}>
        {title}
      </Text>
      {description && <Text style={styles.muted}>{description}</Text>}
      <View style={{ height: 1, backgroundColor: colors.line }} />
    </View>
  );
}
