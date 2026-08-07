// ============================================================
// Llano Smart Fan - ESP32-S3 Firmware
// Controls: Fan PWM, RGB LED, Dual OLED, Rotary Encoder
// Comms: USB Serial + BLE + WiFi WebSocket (triple transport)
// Board: YD-ESP32-S3 (N16R8) + 44-Pin Terminal Adapter
// ============================================================

#include "config.h"
#include "fan_controller.h"
#include "encoder_input.h"
#include "led_effects.h"
#include "oled_display.h"
#include "ble_service.h"
#include "wifi_service.h"
#include "usb_serial_service.h"

// Timing trackers
static uint32_t lastDisplayUpdate = 0;
static uint32_t lastCommNotify = 0;
static uint32_t lastLedUpdate = 0;

void setup() {
  Serial.begin(115200);
  while (!Serial && millis() < 3000) delay(10);
  delay(1000);
  Serial.println("\n=== Llano Smart Fan v2.1 (USB + BLE + WiFi) ===");

  Serial.println("[1/7] Fan...");
  initFan();
  Serial.println("[OK] Fan controller");

  Serial.println("[2/7] Encoder...");
  initEncoder();
  Serial.println("[OK] Encoder + buttons");

  Serial.println("[3/7] LEDs...");
  initLeds();
  Serial.println("[OK] LED strip");

  Serial.println("[4/7] OLEDs...");
  initDisplays();
  Serial.println("[OK] Dual OLED displays");

  Serial.println("[5/7] BLE...");
  initBLE();
  Serial.println("[OK] BLE service");

  Serial.println("[6/7] WiFi...");
  initWiFiService();
  Serial.println("[OK] WiFi + WebSocket service");

  Serial.println("[7/7] USB Serial...");
  initUSBSerial();
  Serial.println("[OK] USB Serial service");

  // Setup encoder 2 (scroll wheel) with interrupt
  initEncoder2();
  Serial.println("[OK] Enc2 (scroll wheel) GPIO 15/16 ready");

  // Default state: fan ON at 30%, LED rainbow
  setFanOn(true);
  setFanSpeed(30);
  setLedOn(true);
  setLedMode(LED_RAINBOW);

  Serial.println("=== System Ready ===\n");
}

void loop() {
  uint32_t now = millis();

  // ---- 0. Handle USB Serial + WiFi WebSocket events ----
  loopUSBSerial();
  loopWiFiService();

  // ---- 1. Read encoders for speed adjustment ----
  int8_t encDelta1 = getEncoderDelta();
  int8_t encDelta2 = getEncoder2Delta();
  int8_t totalDelta = encDelta1 + encDelta2;

  if (totalDelta != 0) {
    int16_t newSpeed = (int16_t)getFanPercent() + (totalDelta * ENCODER_STEP);
    newSpeed = constrain(newSpeed, 0, 100);
    setFanSpeed((uint8_t)newSpeed);
    Serial.printf("Encoder: speed -> %d%%\n", newSpeed);
  }

  // ---- 2. Check button presses ----
  ButtonEvent btn = checkButtons();
  switch (btn) {
    case BTN_PSH:
      setFanOn(!isFanOn());
      Serial.printf("PSH: Fan %s\n", isFanOn() ? "ON" : "OFF");
      break;
    case BTN_CON:
      setLedOn(!isLedOn());
      Serial.printf("CON: LED %s\n", isLedOn() ? "ON" : "OFF");
      break;
    case BTN_BAK: {
      uint8_t nextMode = (getLedMode() + 1) % LED_MODE_COUNT;
      if (nextMode == LED_OFF) nextMode = LED_STATIC;  // Skip OFF in cycle
      setLedMode(nextMode);
      Serial.printf("BAK: LED mode -> %d\n", nextMode);
      break;
    }
    default:
      break;
  }

  // ---- 3. Update fan RPM calculation ----
  updateRPM();

  // ---- 4. Update LED effects (target ~30fps) ----
  if (now - lastLedUpdate >= 33) {
    setLedSpeedPercent(getFanPercent());
    updateLeds();
    lastLedUpdate = now;
  }

  // ---- 5. Update OLED displays (every 100ms) ----
  if (now - lastDisplayUpdate >= DISPLAY_UPDATE_MS) {
    // Priority: USB > WiFi > BLE for temperature data
    float cpuT = getUSBCpuTemp() > 0 ? getUSBCpuTemp() :
                 getWiFiCpuTemp() > 0 ? getWiFiCpuTemp() : getBLECpuTemp();
    float gpuT = getUSBGpuTemp() > 0 ? getUSBGpuTemp() :
                 getWiFiGpuTemp() > 0 ? getWiFiGpuTemp() : getBLEGpuTemp();

    updateMainDisplay(getFanRPM(), getFanPercent(), getLedMode(), isFanOn());
    updateSecondaryDisplay(cpuT, gpuT,
                           isBLEConnected(), isWiFiConnected(),
                           isSTAConnected() ? getSTAIP().c_str() : getAPIP().c_str());
    lastDisplayUpdate = now;
  }

  // ---- 6. Notify clients on both transports (every 500ms) ----
  if (now - lastCommNotify >= COMM_NOTIFY_MS) {
    // Priority: USB > WiFi > BLE for temperature data
    float cpuT = getUSBCpuTemp() > 0 ? getUSBCpuTemp() :
                 getWiFiCpuTemp() > 0 ? getWiFiCpuTemp() : getBLECpuTemp();
    float gpuT = getUSBGpuTemp() > 0 ? getUSBGpuTemp() :
                 getWiFiGpuTemp() > 0 ? getWiFiGpuTemp() : getBLEGpuTemp();

    // BLE notifications
    notifyRPM(getFanRPM());
    notifyStatus(getFanPercent(), isFanOn(), getLedMode(), isLedOn());

    // WiFi WebSocket notifications
    wsNotifyStatus(getFanPercent(), isFanOn(), getLedMode(), isLedOn(), cpuT, gpuT);

    // USB Serial notifications
    usbNotifyStatus(getFanPercent(), isFanOn(), getLedMode(), isLedOn(), cpuT, gpuT);

    lastCommNotify = now;
  }
}
