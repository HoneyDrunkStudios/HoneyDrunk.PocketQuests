import { useState, type ReactNode } from "react";
import { Pressable, Text, View } from "react-native";
import Ionicons from "@expo/vector-icons/Ionicons";
import { useAppTheme } from "./ui";

export function SettingsSection({
  title,
  description,
  children,
}: Readonly<{
  title: string;
  description: string;
  children: ReactNode;
}>) {
  const { colors, styles } = useAppTheme();
  const [open, setOpen] = useState(false);
  return (
    <View style={{ borderBottomWidth: 1, borderBottomColor: colors.line }}>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={title}
        accessibilityState={{ expanded: open }}
        onPress={() => setOpen(!open)}
        style={{
          minHeight: 80,
          paddingVertical: 18,
          flexDirection: "row",
          alignItems: "center",
          gap: 12,
        }}
      >
        <View style={{ flex: 1, gap: 5 }}>
          <Text style={[styles.subtitle, { fontSize: 21 }]}>{title}</Text>
          <Text style={styles.muted}>{description}</Text>
        </View>
        <Ionicons
          name={open ? "chevron-up" : "chevron-down"}
          size={20}
          color={colors.gold}
          accessible={false}
        />
      </Pressable>
      <View
        style={{ display: open ? "flex" : "none", gap: 14, paddingBottom: 20 }}
      >
        {children}
      </View>
    </View>
  );
}
