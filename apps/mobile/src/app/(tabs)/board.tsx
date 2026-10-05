import { Page } from "../../session/session-page";
import { QuestHistory } from "../../features/quests/quest-history";
import { useState } from "react";
import { Pressable, Text, View } from "react-native";
import { useAccountSnapshot } from "../../session/session";
import { Label, colors, styles } from "../../shared/ui";
import { AvailableQuests } from "../../features/quests/available-quests";
import { ProfileEditor } from "../../features/profile/profile-editor";

export default function Quests() {
  const { state } = useAccountSnapshot();
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
    <QuestHistory sections={[{key:section,data:items}, ...(section === "Active" && past.length ? [{key:"past",title:"Past commitments",data:past}] : [])]}>
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
    </QuestHistory>
  );
}
