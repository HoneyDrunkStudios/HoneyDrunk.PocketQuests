import { Redirect } from "expo-router";
import * as WebBrowser from "expo-web-browser";

// Complete the popup on the callback URL before routing away from its auth response.
WebBrowser.maybeCompleteAuthSession();

export default function Callback() {
  return <Redirect href="/" />;
}
