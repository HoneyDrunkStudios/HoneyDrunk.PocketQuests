import { useState } from "react";
import { Text, TextInput, View } from "react-native";
import { useSession } from "../../session";
import { Page, Button, Label, QuestCard, styles } from "../../ui";
export default function Calendar() {
  const { state } = useSession();
  const [date, setDate] = useState("");
  const [week, setWeek] = useState(false);
  const selected = date || state?.today || "";
  const start = Date.parse(selected + "T00:00:00Z");
  const end = start + (week ? 7 : 1) * 86400000;
  const items =
    state?.occurrences
      .filter(
        (o) =>
          o.occurrence.dueDate &&
          Date.parse(o.occurrence.dueDate + "T00:00:00Z") >= start &&
          Date.parse(o.occurrence.dueDate + "T00:00:00Z") < end,
      )
      .sort((a, b) =>
        (a.occurrence.dueDate ?? "").localeCompare(b.occurrence.dueDate ?? ""),
      ) ?? [];
  return (
    <Page>
      <Text selectable style={styles.title}>
        Make space for what matters
      </Text>
      <TextInput
        accessibilityLabel="Calendar date YYYY-MM-DD"
        value={date}
        placeholder={state?.today}
        onChangeText={setDate}
        style={styles.input}
      />
      <View style={{ flexDirection: "row", gap: 12 }}>
        <Button title="Day" secondary={week} onPress={() => setWeek(false)} />
        <Button
          title="7 days"
          secondary={!week}
          onPress={() => setWeek(true)}
        />
      </View>
      <Label>
        {selected} · {state?.zone}
      </Label>
      {items.length ? (
        items.map((o) => <QuestCard key={o.occurrence.id} item={o} />)
      ) : (
        <Label>No quests on these dates. Choose a quest from the board.</Label>
      )}
      <Text selectable style={styles.subtitle}>
        Unscheduled history
      </Text>
      {state?.occurrences
        .filter((o) => !o.occurrence.dueDate)
        .map((o) => (
          <QuestCard key={o.occurrence.id} item={o} />
        ))}
    </Page>
  );
}
