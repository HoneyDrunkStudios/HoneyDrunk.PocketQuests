import { Link } from "expo-router";
import { Page } from "../session/session-page";
import { questActions } from "../features/quests/commands/quest-actions";
import { Text } from "react-native";
import { useSession } from "../session/session";
import { Button, Label, colors, styles } from "../shared/ui";
import { SettingsEditor } from "../features/profile/settings-editor";
import { AccountSettings } from "../features/profile/account-settings";
import { PlusSettings } from "../features/profile/plus-settings";
import { ProfileEditor } from "../features/profile/profile-editor";
import { AppearanceEditor } from "../features/profile/appearance-editor";
export default function Settings() {
  const { state, signOut, refresh, busy, pending, command, exportData } =
    useSession();
  if (!state) return null;
  return (
    <Page>
      <Text accessibilityRole="header" style={styles.title}>
        Make it yours
      </Text>
      <Link
        href="/character-setup"
        style={[styles.text, { paddingVertical: 12, minHeight: 48 }]}
      >
        Name, interests and starting skills
      </Link>
      <Link
        href="/avatar"
        style={[
          styles.text,
          { color: colors.gold, paddingVertical: 12, minHeight: 48 },
        ]}
      >
        Customize your traveler
      </Link>
      <AppearanceEditor />
      <SettingsEditor />
      <Text accessibilityRole="header" style={styles.subtitle}>
        Pocket Quests Plus
      </Text>
      <PlusSettings />
      <AccountSettings />
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
      <Button title="Sign out" onPress={() => void signOut()} secondary />
    </Page>
  );
}
