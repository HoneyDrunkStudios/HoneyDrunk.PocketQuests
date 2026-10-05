package expo.modules.pocketquestsclock;

import android.content.Context;
import android.os.SystemClock;
import android.provider.Settings;
import java.util.HashMap;
import java.util.Map;

/** Boot-relative, sleep-inclusive event timing. No permissions or network required. */
public final class NativeClock {
  private NativeClock() {}

  public static Map<String, Object> sample(Context context) throws Settings.SettingNotFoundException {
    int before = Settings.Global.getInt(context.getContentResolver(), Settings.Global.BOOT_COUNT);
    long elapsed = SystemClock.elapsedRealtime();
    long utc = System.currentTimeMillis();
    int after = Settings.Global.getInt(context.getContentResolver(), Settings.Global.BOOT_COUNT);
    if (before < 0 || before != after || elapsed < 0) {
      throw new IllegalStateException("Clock epoch could not be verified");
    }
    Map<String, Object> result = new HashMap<>();
    // This OS marker stays in the encrypted local cache; never send it as BootId.
    result.put("epoch", Integer.toString(before));
    result.put("elapsedMilliseconds", (double) elapsed);
    result.put("utcMilliseconds", (double) utc);
    return result;
  }
}
