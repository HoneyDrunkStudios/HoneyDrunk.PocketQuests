import { Page } from "../session/session-page";
import { AvatarEditor } from "../features/profile/avatar-editor";
export default function Wardrobe() {
  return (
    <Page>
      <AvatarEditor equipmentOnly />
    </Page>
  );
}
