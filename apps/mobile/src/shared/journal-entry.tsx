import { playfulArt } from "./playful-art";
import { AppBrand } from "./app-brand";
import type { ReactNode } from "react";
import {
  Image,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  Text,
  View,
} from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import { useAppTheme } from "./ui";

/** Keep the full-screen illustration outside the scroll owner and its padding. */
export function JournalEntry({ children }: Readonly<{ children: ReactNode }>) {
  const { colors, styles } = useAppTheme();
  const insets = useSafeAreaInsets();
  return (
    <View style={{ flex: 1, backgroundColor: colors.paper }}>
      <View pointerEvents="none" style={{ position: "absolute", inset: 0 }}>
        <Image
          source={playfulArt.islands}
          resizeMode="cover"
          style={{ width: "100%", height: "100%" }}
          accessible={false}
        />
      </View>
      <View
        pointerEvents="none"
        style={{ position: "absolute", inset: 0, backgroundColor: "#17162C33" }}
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
              backgroundColor: colors.paper,
              borderColor: colors.gold,
              borderWidth: 1,
              borderRadius: 24,
            }}
          >
            <View style={{ alignItems: "center", gap: 12 }}>
              <AppBrand />
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
    </View>
  );
}
