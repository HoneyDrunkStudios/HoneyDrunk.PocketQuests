import { ActivityArtwork } from "../../shared/activity-artwork";
export function QuestCardArtwork({
  categoryId,
  size = 88,
}: Readonly<{ categoryId: string; size?: number }>) {
  return <ActivityArtwork categoryId={categoryId} size={size} />;
}
