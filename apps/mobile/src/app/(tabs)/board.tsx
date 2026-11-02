import { useLocalSearchParams, useRouter } from "expo-router";
import { QuestBoard, type BoardView } from "../../features/quests/quest-board";

export default function Board() {
  const { view } = useLocalSearchParams<{ view?: string }>();
  const router = useRouter();
  const selected: BoardView =
    view === "Active" || view === "Completed" ? view : "Available";
  return (
    <QuestBoard
      initialView={selected}
      onViewChange={(view) => router.setParams({ view })}
    />
  );
}
