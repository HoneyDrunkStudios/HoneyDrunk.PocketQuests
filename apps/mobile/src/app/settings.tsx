import { DisplayModeChoice } from "../features/profile/display-mode-choice";
import { SettingsSection } from "../shared/settings-section";
import { Link } from "expo-router";
import { Page } from "../session/session-page";
import { questActions } from "../features/quests/commands/quest-actions";
import { Text } from "react-native";
import { useSession } from "../session/session";
import { Button, Label, useAppTheme } from "../shared/ui";
import { SettingsEditor } from "../features/profile/settings-editor";
import { AccountSettings } from "../features/profile/account-settings";
import { ProfileEditor } from "../features/profile/profile-editor";
import { AppearanceEditor } from "../features/profile/appearance-editor";
import { PlusSettings } from "../features/profile/plus-settings";
export default function Settings() {
  const { styles, colors } = useAppTheme();
  const { state, signOut, refresh, busy, pending, command, exportData } =
    useSession();
  if (!state) return null;
  return (
    <Page>
      <Label>A few preferences for your everyday adventures.</Label>
      <SettingsSection
        title="Your traveler"
        description="Name, appearance, interests and skills"
      >
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
        <Link
          href="/wardrobe"
          style={[
            styles.text,
            { color: colors.green, minHeight: 48, paddingVertical: 12 },
          ]}
        >
          Open your wardrobe
        </Link>
        <ProfileEditor />
      </SettingsSection>
      <SettingsSection
        title="Appearance"
        description="Light or dark mode, badges and journal details"
      >
        <DisplayModeChoice />
        <AppearanceEditor />
      </SettingsSection>
      <SettingsSection
        title="Time & reminders"
        description="Timezone and optional phone warnings"
      >
        <SettingsEditor />
      </SettingsSection>
      <SettingsSection
        title="Pocket Quests Plus"
        description="Your plan and custom quest limit"
      >
        <PlusSettings />
      </SettingsSection>
      <SettingsSection
        title="Privacy & account"
        description="Sign-in methods, data export and account deletion"
      >
        <AccountSettings />
        <Text style={styles.subtitle}>Your private data</Text>
        <Label>
          Export server-confirmed data at one cutoff. Unsynced device changes
          are excluded. Downloads contain private quest text; choose where to
          save them.
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
      </SettingsSection>
      <SettingsSection
        title="Pause commitments"
        description="Take a break from all quests or selected categories"
      >
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
      </SettingsSection>
      <Link
        href={{ pathname: "/(tabs)", params: { tour: "start" } }}
        style={[
          styles.text,
          { color: colors.gold, paddingVertical: 16, minHeight: 48 },
        ]}
      >
        Take a tour of Pocket Quests
      </Link>
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
