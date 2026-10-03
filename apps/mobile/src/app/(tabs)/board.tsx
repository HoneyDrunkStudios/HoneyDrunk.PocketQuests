import { useState } from "react";
import { Pressable, Text, View } from "react-native";
import { useSession } from "../../session";
import { Page, Label, QuestCard, colors, styles } from "../../ui";
import { AvailableQuests } from "../../available-quests";
import { ProfileEditor } from "../../profile-editor";

export default function Quests() {
  const { state } = useSession();
  const [section, setSection] = useState<"Available" | "Active" | "Completed">(
    "Available",
  );
  if (!state)
    return (
      <Page>
        <Label>Loading your quests.</Label>
      </Page>
    );
  if (state && !state.profile.onboardingComplete)
    return (
      <Page>
        <ProfileEditor />
      </Page>
    );
  const items = state.occurrences.filter((o) =>
    section === "Completed"
      ? o.status === "Completed"
      : section === "Active"
        ? ["Active", "Frozen"].includes(o.status)
        : o.status === "Offered",
  );
  const past = state.occurrences.filter((o) =>
    ["Missed", "Abandoned"].includes(o.status),
  );
  return (
    <Page>
      <Text accessibilityRole="header" style={styles.title}>
        Your quests
      </Text>
      <View
        accessibilityRole="tablist"
        style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}
      >
        {(["Available", "Active", "Completed"] as const).map((name) => (
          <Pressable
            key={name}
            accessibilityRole="tab"
            accessibilityState={{ selected: section === name }}
            aria-selected={section === name}
            onPress={() => setSection(name)}
            style={{
              minHeight: 48,
              minWidth: 48,
              padding: 14,
              justifyContent: "center",
              borderRadius: 10,
              borderWidth: 1,
              borderColor: colors.gold,
              backgroundColor: section === name ? colors.gold : colors.card,
            }}
          >
            <Text
              style={[
                styles.text,
                {
                  color: section === name ? "#FFFFFF" : colors.gold,
                  fontWeight: "600",
                },
              ]}
            >
              {name}
            </Text>
          </Pressable>
        ))}
      </View>
      {section === "Available" && (
        <>
          <Label>
            Start picks up a quest. Open it from Active when you are ready to
            complete it.
          </Label>
          <AvailableQuests />
        </>
      )}
      {section !== "Available" && items.length === 0 && (
        <Label>
          {section === "Active"
            ? "No active quests yet. Choose one from Available."
            : "Your completed quests will appear here."}
        </Label>
      )}
      {items.map((item) => (
        <QuestCard key={item.occurrence.id} item={item} />
      ))}
      {section === "Active" && past.length > 0 && (
        <>
          <Text accessibilityRole="header" style={styles.subtitle}>
            Past commitments
          </Text>
          {past.map((item) => (
            <QuestCard key={item.occurrence.id} item={item} />
          ))}
        </>
      )}
    </Page>
  );
}
