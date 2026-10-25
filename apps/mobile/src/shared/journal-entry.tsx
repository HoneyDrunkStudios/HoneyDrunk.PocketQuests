import type { ReactNode } from "react";
import {
  ImageBackground,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  Text,
  View,
} from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import Ionicons from "@expo/vector-icons/Ionicons";
import { colors, styles } from "./ui";

/** Keep the full-screen illustration outside the scroll owner and its padding. */
export function JournalEntry({ children }: Readonly<{ children: ReactNode }>) {
  const insets = useSafeAreaInsets();
  return (
    <ImageBackground
      source={require("../../assets/journal/signin-background.jpg")}
      resizeMode="cover"
      imageStyle={{ width: "100%", height: "100%" }}
      style={{ flex: 1, backgroundColor: colors.paper }}
    >
      <View
        pointerEvents="none"
        style={{ position: "absolute", inset: 0, backgroundColor: "#091A2933" }}
      />
      <KeyboardAvoidingView
        style={{ flex: 1 }}
        enabled={Platform.OS === "ios"}
        behavior={Platform.OS === "ios" ? "padding" : undefined}
      >
        <ScrollView
          style={{ flex: 1 }}
          showsVerticalScrollIndicator={false}
          contentInsetAdjustmentBehavior="never"
          keyboardShouldPersistTaps="handled"
          keyboardDismissMode={
            Platform.OS === "ios" ? "interactive" : "on-drag"
          }
          contentContainerStyle={{
            flexGrow: 1,
            justifyContent: "center",
            alignItems: "center",
            paddingHorizontal: 24,
            paddingTop: insets.top + 32,
            paddingBottom: insets.bottom + 32,
          }}
        >
          <View
            style={{
              width: "100%",
              maxWidth: 420,
              padding: 24,
              gap: 20,
              backgroundColor: "#091A29F0",
              borderColor: colors.gold,
              borderWidth: 1,
              borderRadius: 24,
            }}
          >
            <View style={{ alignItems: "center", gap: 12 }}>
              <Ionicons
                name="compass-outline"
                size={44}
                color={colors.gold}
                accessible={false}
              />
              <Text
                accessibilityRole="header"
                style={[styles.title, { fontSize: 28, textAlign: "center" }]}
              >
                Pocket Quests
              </Text>
              <Text
                style={[
                  styles.muted,
                  {
                    color: colors.gold,
                    fontSize: 11,
                    letterSpacing: 2,
                    textAlign: "center",
                  },
                ]}
              >
                YOUR EVERYDAY ADVENTURE
              </Text>
            </View>
            {children}
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </ImageBackground>
  );
}
