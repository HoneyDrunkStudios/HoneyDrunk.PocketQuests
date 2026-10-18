import { Redirect, useLocalSearchParams, useRouter } from "expo-router";
import { useSession } from "../../../session/session";
import { Page } from "../../../session/session-page";
import { Button, Label } from "../../../shared/ui";
import { AvailableQuestDetails } from "../../../features/quests/available-quest-details";
import { availableQuests } from "../../../features/quests/quest-availability";
import { isCalendarDate } from "../../../features/quests/calendar-plan";

export default function AvailableQuestScreen() {
  const { id, date } = useLocalSearchParams<{ id: string; date?: string }>();
  const router = useRouter();
  const { state, catalog, signedIn, busy } = useSession();
  const back = () =>
    router.canGoBack() ? router.back() : router.replace("/(tabs)/board");
  if (!signedIn && !busy) return <Redirect href="/" />;
  if (busy && (!state || !catalog))
    return (
      <Page>
        <Label>Loading your quest.</Label>
      </Page>
    );
  const quest = availableQuests(catalog, state).find((item) => item.id === id);
  const invalidDate =
    date !== undefined && (typeof date !== "string" || !isCalendarDate(date));
  if (!quest || invalidDate || !state?.profile.onboardingComplete)
    return (
      <Page>
        <Label>
          {invalidDate
            ? "Invalid planning date. Return to the quest list to choose a date."
            : "Quest unavailable. Check Active or return to the quest list and refresh."}
        </Label>
        <Button title="Back to available quests" secondary onPress={back} />
      </Page>
    );
  return (
    <Page>
      <AvailableQuestDetails
        key={`${quest.id}/${date ?? ""}`}
        quest={quest}
        initialDate={date ?? ""}
        onBack={back}
      />
    </Page>
  );
}
