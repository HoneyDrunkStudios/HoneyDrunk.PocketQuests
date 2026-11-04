import { useState } from "react";
import { Pressable, ScrollView, Text, View } from "react-native";
import { AvatarFigure } from "../../src/features/profile/avatar-figure";
import { hairStyles, scarfNames } from "../../src/features/profile/avatar-look";

/** Render-only entry for a separate development app; never connects to an account. */
export default function AvatarRenderFixture() {
  const [color, setColor] = useState(0);
  const [hair, setHair] = useState(0);
  const [portrait, setPortrait] = useState(false);
  const code = (accessory: number, palette: number) =>
    `v3:2:1:${hair}:1:${accessory}:2:1:2:${palette}`;
  const cards = [
    ["Original linen / satchel", code(1, 0)],
    [`${scarfNames[color]} / satchel`, code(1, color)],
    [`${scarfNames[color]} / glasses`, code(4, color)],
    [`${scarfNames[color]} / cape`, code(3, color)],
  ];
  const controls = [
    ["Next scarf color", scarfNames[color], () => setColor((color + 1) % 8)],
    ["Next hair style", hairStyles[hair], () => setHair((hair + 1) % 8)],
    [
      "Toggle portrait",
      portrait ? "Portrait" : "Full body",
      () => setPortrait(!portrait),
    ],
  ] as const;
  return (
    <ScrollView
      style={{ flex: 1, backgroundColor: "#0b1822" }}
      contentContainerStyle={{ padding: 16, paddingTop: 52, paddingBottom: 32 }}
    >
      <Text style={{ color: "#edbd72", fontSize: 19, fontWeight: "700" }}>
        Native avatar acceptance
      </Text>
      <Text style={{ color: "white", marginVertical: 6 }}>
        Production AvatarFigure / native SVG
      </Text>
      {controls.map(([label, value, onPress]) => (
        <Pressable
          key={label}
          accessibilityRole="button"
          accessibilityLabel={label}
          onPress={onPress}
          style={{
            backgroundColor: "#edbd72",
            padding: 10,
            borderRadius: 8,
            marginBottom: 8,
          }}
        >
          <Text style={{ color: "#0b1822", fontWeight: "700" }}>
            {label}: {value}
          </Text>
        </Pressable>
      ))}
      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
        {cards.map(([label, value], index) => (
          <View
            key={index}
            style={{
              width: "48%",
              backgroundColor: "#142936",
              padding: 4,
              borderRadius: 8,
              marginBottom: 8,
              alignItems: "center",
            }}
          >
            <Text style={{ color: "white", fontSize: 12, textAlign: "center" }}>
              {label}
            </Text>
            <AvatarFigure
              code={value}
              portrait={portrait}
              size={portrait ? 150 : 220}
            />
            <Text style={{ color: "#b4c3ca", fontSize: 8 }}>{value}</Text>
          </View>
        ))}
      </View>
      <Text style={{ color: "white", fontSize: 11 }}>
        Render-only fixture. No account, API or progress writes.
      </Text>
    </ScrollView>
  );
}
