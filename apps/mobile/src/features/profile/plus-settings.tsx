import { useEffect, useState } from "react";
import { Linking, Platform } from "react-native";
import type { BillingStatus } from "../../api/generated";
import { useSessionActions, useSessionStatus } from "../../session/session";
import { Button, Label } from "../../shared/ui";

export function PlusSettings() {
  const { readBilling, startBillingCheckout } = useSessionActions();
  const { offline, busy } = useSessionStatus();
  const [status, setStatus] = useState<BillingStatus | null>(null);
  const [message, setMessage] = useState("");
  const [request, setRequest] = useState(0);

  useEffect(() => {
    if (offline) return;
    let active = true;
    void readBilling()
      .then((value) => {
        if (active) {
          setStatus(value);
          setMessage("");
        }
      })
      .catch(() => {
        if (active) setMessage("Couldn’t load your plan. Try refreshing.");
      });
    return () => {
      active = false;
    };
  }, [offline, readBilling, request]);

  async function checkout(interval: "monthly" | "yearly") {
    setMessage("");
    try {
      const { url } = await startBillingCheckout(interval);
      await Linking.openURL(url);
      setRequest((current) => current + 1);
    } catch (error) {
      setMessage(
        error instanceof Error ? error.message : "Couldn’t open Checkout.",
      );
    }
  }

  return (
    <>
      {status ? (
        <>
          <Label>{status.plan}</Label>
          <Label>
            {status.activeCustomQuests} active custom quests
            {status.plan === "Free" ? ` out of ${status.freeQuestLimit}` : ""}
          </Label>
          {Platform.OS === "web" && status.checkoutAvailable && !offline && (
            <>
              {(!status.pendingInterval ||
                status.pendingInterval === "monthly") && (
                <Button
                  title={
                    status.pendingInterval
                      ? "Resume monthly checkout"
                      : "Choose monthly Plus"
                  }
                  disabled={busy}
                  onPress={() => void checkout("monthly")}
                />
              )}
              {(!status.pendingInterval ||
                status.pendingInterval === "yearly") && (
                <Button
                  title={
                    status.pendingInterval
                      ? "Resume yearly checkout"
                      : "Choose yearly Plus"
                  }
                  disabled={busy}
                  onPress={() => void checkout("yearly")}
                />
              )}
            </>
          )}
          {Platform.OS === "web" && status.portalUrl && (
            <Button
              title="Manage subscription"
              secondary
              onPress={() => {
                if (status.portalUrl)
                  void Linking.openURL(status.portalUrl).catch(() =>
                    setMessage("Couldn’t open subscription settings."),
                  );
              }}
            />
          )}
        </>
      ) : (
        <Label>
          {offline
            ? "Connect to view your plan."
            : "Your plan and custom quest limit."}
        </Label>
      )}
      {message ? (
        <Label accessibilityLiveRegion="polite">{message}</Label>
      ) : null}
      <Button
        title="Refresh plan"
        secondary
        disabled={offline || busy}
        onPress={() => setRequest((current) => current + 1)}
      />
    </>
  );
}
