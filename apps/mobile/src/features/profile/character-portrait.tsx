import { View } from "react-native";
import { colors } from "../../shared/ui";
export function CharacterPortrait() {
  return (
    <View
      accessible
      accessibilityRole="image"
      accessibilityLabel="Character portrait silhouette"
      style={{
        width: 112,
        minHeight: 144,
        borderWidth: 2,
        borderColor: colors.gold,
        borderRadius: 56,
        alignItems: "center",
        justifyContent: "center",
        backgroundColor: colors.paper,
      }}
    >
      <View
        accessible={false}
        style={{
          width: 38,
          height: 38,
          borderRadius: 19,
          backgroundColor: colors.ink,
        }}
      />
      <View
        accessible={false}
        style={{
          width: 64,
          height: 56,
          borderTopLeftRadius: 32,
          borderTopRightRadius: 32,
          marginTop: 8,
          backgroundColor: colors.ink,
        }}
      />
    </View>
  );
}
