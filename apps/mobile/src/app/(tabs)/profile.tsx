import { questActions } from "../../features/quests/commands/quest-actions";
import { Text, View } from "react-native";
import { useSession } from "../../session/session";
import { Page, Button, Label, Card, colors, styles } from "../../shared/ui";
import { SettingsEditor } from "../../features/profile/settings-editor";
import { AccountSettings } from "../../features/profile/account-settings";
import { ProfileEditor } from "../../features/profile/profile-editor";
export default function Profile() {
  const { state, signOut, refresh, busy, pending, command, exportData } =
    useSession();
  if (!state) return null;
  return (
    <Page>
      <Text selectable style={styles.title}>
        Your story so far
      </Text>
      <Card style={{ borderTopWidth: 3, borderTopColor: colors.gold }}>
        <Text
          style={{
            ...styles.muted,
            color: colors.gold,
            letterSpacing: 2,
            fontWeight: "700",
          }}
        >
          CHARACTER STATUS
        </Text>
        <View
          style={{
            flexDirection: "row",
            flexWrap: "wrap",
            gap: 24,
            alignItems: "center",
          }}
        >
          <View
            style={{
              borderWidth: 2,
              borderColor: colors.gold,
              borderRadius: 12,
              padding: 16,
              minWidth: 88,
              alignItems: "center",
            }}
          >
            <Text style={{ ...styles.muted, color: colors.gold }}>RANK</Text>
            <Text style={{ ...styles.title, color: colors.gold }}>
              {state.rank.current}
            </Text>
          </View>
          <View style={{ gap: 6, flexShrink: 1 }}>
            <Text selectable style={styles.subtitle}>
              Overall level {state.overallLevel}
            </Text>
            <Label>{state.overallXp} base XP earned</Label>
          </View>
        </View>
        <Text selectable style={styles.muted}>
          Rank reflects breadth across life’s categories. It is separate from
          skill and attribute levels.
        </Text>
      </Card>
      <SettingsEditor />
      <AccountSettings />
      {(
        [
          ["Categories", state.categories],
          ["Attributes", state.attributes],
          ["Skills", state.skills],
        ] as const
      ).map(([name, balances]) => (
        <View key={name} style={styles.card}>
          <Text selectable style={styles.subtitle}>
            {name}
          </Text>
          {balances.map((b) => (
            <Label key={b.id}>
              {b.name} Â· Level {b.level} Â· {b.xp} XP
            </Label>
          ))}
        </View>
      ))}
      <Text selectable style={styles.subtitle}>
        Achievements and profile rewards
      </Text>
      <Label>
        Selected badge:{" "}
        {state.entitlements.find((e) => e.id === state.profile.badgeId)?.name ??
          "None"}
        . Frame:{" "}
        {state.entitlements.find((e) => e.id === state.profile.frameId)?.name ??
          "Default"}
        .
      </Label>
      <Button
        title="Use default frame"
        secondary
        disabled={busy || pending}
        onPress={() =>
          void command({ action: questActions.selectFrame, rewardId: null })
        }
      />
      <Button
        title="Remove selected badge"
        secondary
        disabled={busy || pending}
        onPress={() =>
          void command({ action: questActions.selectBadge, rewardId: null })
        }
      />
      {state.entitlements.map((e) => (
        <View key={e.id} style={styles.card}>
          <Text selectable style={styles.subtitle}>
            {e.name}
          </Text>
          <Label>
            {e.kind} Â· {e.earned ? "Earned" : "Still growing"} Â· {e.count}/
            {e.requiredCount}
          </Label>
          {e.earned && e.kind !== "Achievement" && (
            <Button
              title={`Select ${e.name}`}
              disabled={busy || pending}
              onPress={() =>
                void command({
                  action:
                    e.kind === "Badge"
                      ? questActions.selectBadge
                      : questActions.selectFrame,
                  rewardId: e.id,
                })
              }
            />
          )}
          {e.requiredRank !== "F" && (
            <Text selectable style={styles.muted}>
              Requires current rank {e.requiredRank} or higher.
            </Text>
          )}
        </View>
      ))}
      <Text style={styles.subtitle}>Your private data</Text>
      <Label>
        Export server-confirmed data at one cutoff. Unsynced device changes are
        excluded. Downloads contain private quest text; choose where to save
        them.
      </Label>
      <Button
        title="Download full JSON export"
        disabled={busy || pending}
        onPress={() => void exportData("json")}
        secondary
      />
      <Button
        title="Download CSV history archive"
        disabled={busy || pending}
        onPress={() => void exportData("csv")}
        secondary
      />
      <Text style={styles.subtitle}>Pause commitments</Text>
      <Label>
        Pause freezes unfinished commitments and holds streaks without growth.
        Resume shifts their dates by local date boundaries crossed. Existing
        misses are preserved.
      </Label>
      <Button
        title={
          state.schedule.accountPaused
            ? "Resume account pause"
            : "Pause all categories"
        }
        disabled={busy || pending}
        onPress={() =>
          void command({
            action: state.schedule.accountPaused
              ? questActions.resume
              : questActions.pause,
          })
        }
      />
      {state.categories.map((c) => (
        <Button
          key={c.id}
          title={`${state.schedule.pausedCategories.includes(c.id) ? "Resume" : "Pause"} ${c.name}`}
          secondary
          disabled={busy || pending}
          onPress={() =>
            void command({
              action: state.schedule.pausedCategories.includes(c.id)
                ? questActions.resume
                : questActions.pause,
              categoryId: c.id,
            })
          }
        />
      ))}
      <ProfileEditor />
      <Button
        title="Refresh progress"
        onPress={() => void refresh()}
        secondary
        disabled={busy}
      />
      <Button
        title="Sign out"
        onPress={() => void signOut()}
        secondary
        disabled={busy}
      />
    </Page>
  );
}
