package expo.modules.pocketquestsclock

import expo.modules.kotlin.modules.Module
import expo.modules.kotlin.modules.ModuleDefinition

class PocketQuestsClockModule : Module() {
  override fun definition() = ModuleDefinition {
    Name("PocketQuestsClock")
    Function("sample") {
      val context = appContext.reactContext
        ?: throw IllegalStateException("Application context unavailable")
      NativeClock.sample(context)
    }
  }
}
