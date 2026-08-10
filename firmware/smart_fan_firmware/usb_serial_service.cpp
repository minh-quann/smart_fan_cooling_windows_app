#include "usb_serial_service.h"
#include "config.h"
#include "fan_controller.h"
#include "led_effects.h"
#include "oled_display.h"
#include "encoder_input.h"
#include "wifi_service.h"
#include <ArduinoJson.h>

static bool _usbConnected = false;
static uint32_t _lastPing = 0;
static float _cpuTemp = 0;
static float _gpuTemp = 0;
static uint16_t _cpuFanRpm = 0;
static uint16_t _gpuFanRpm = 0;
static String _rxBuffer = "";

// USB connection timeout — if no ping received within this period, consider disconnected
#define USB_TIMEOUT_MS  5000

// ---- Process incoming JSON command (same format as WebSocket) ----
static void handleUSBCommand(const char* payload) {
  size_t payloadLen = strlen(payload);
  
  JsonDocument doc;
  DeserializationError err = deserializeJson(doc, payload);
  if (err) {
    Serial.printf("USB: JSON parse FAILED! err=%s len=%u\n", err.c_str(), payloadLen);
    return;
  }

  const char* cmd = doc["cmd"];
  if (!cmd) return;

  if (strcmp(cmd, "ping") == 0) {
    _lastPing = millis();
    if (!_usbConnected) {
      _usbConnected = true;
      Serial.println("{\"cmd\":\"pong\"}");
      Serial.printf("USB: Client connected\n");
    } else {
      Serial.println("{\"cmd\":\"pong\"}");
    }
  }
  else if (strcmp(cmd, "fan_speed") == 0) {
    uint8_t val = doc["value"] | 0;
    setFanSpeed(val);
    Serial.printf("USB: Fan speed -> %d%%\n", val);
  }
  else if (strcmp(cmd, "fan_target_rpm") == 0) {
    uint16_t rpm = doc["value"] | 0;
    setTargetRPM(rpm);
    Serial.printf("USB: Target RPM -> %u\n", rpm);
  }
  else if (strcmp(cmd, "fan_state") == 0) {
    bool on = doc["value"] | 0;
    setFanOn(on);
    Serial.printf("USB: Fan %s\n", on ? "ON" : "OFF");
  }
  else if (strcmp(cmd, "led_mode") == 0) {
    uint8_t mode = doc["value"] | 0;
    setLedMode(mode);
    Serial.printf("USB: LED mode -> %d\n", mode);
  }
  else if (strcmp(cmd, "led_color") == 0) {
    uint8_t r = doc["r"] | 0;
    uint8_t g = doc["g"] | 0;
    uint8_t b = doc["b"] | 0;
    setLedColor(r, g, b);
    Serial.printf("USB: LED color R%d G%d B%d\n", r, g, b);
  }
  else if (strcmp(cmd, "led_brightness") == 0) {
    uint8_t val = doc["value"] | 0;
    setLedBrightness(val);
    Serial.printf("USB: LED brightness -> %d\n", val);
  }
  else if (strcmp(cmd, "led_speed") == 0) {
    uint8_t val = doc["value"] | 50;
    setLedSpeed(val);
    Serial.printf("USB: LED speed -> %d\n", val);
  }
  else if (strcmp(cmd, "led_direction") == 0) {
    bool rev = doc["reverse"] | doc["value"] | false;
    setLedDirection(rev);
    Serial.printf("USB: LED direction reverse -> %d\n", rev);
  }
  else if (strcmp(cmd, "set_led_count") == 0) {
    uint16_t val = doc["value"] | 0;
    setLedCount(val);
    Serial.printf("USB: LED count -> %u\n", val);
  }
  else if (strcmp(cmd, "temp") == 0) {
    _cpuTemp = doc["cpu"] | 0.0f;
    _gpuTemp = doc["gpu"] | 0.0f;
    _cpuFanRpm = doc["cpu_fan"] | 0;
    _gpuFanRpm = doc["gpu_fan"] | 0;
    Serial.printf("USB: Temps CPU=%.1f GPU=%.1f CPU_FAN=%u GPU_FAN=%u\n", _cpuTemp, _gpuTemp, _cpuFanRpm, _gpuFanRpm);
  }
  else if (strcmp(cmd, "draw_bitmap") == 0) {
    uint8_t disp = doc["disp"] | 1;
    const char* hexData = doc["data"];
    Serial.printf("USB: draw_bitmap disp=%d hexData=%s\n", disp, hexData ? "OK" : "NULL");
    if (hexData) {
      size_t len = strlen(hexData);
      Serial.printf("USB: bitmap hex len=%u (need>=2048)\n", len);
      if (len >= 2048) {
        uint8_t bitmap[1024];
        for (size_t i = 0; i < 1024; i++) {
          char high = hexData[i * 2];
          char low = hexData[i * 2 + 1];
          uint8_t valHigh = (high >= 'a') ? (high - 'a' + 10) : ((high >= 'A') ? (high - 'A' + 10) : (high - '0'));
          uint8_t valLow = (low >= 'a') ? (low - 'a' + 10) : ((low >= 'A') ? (low - 'A' + 10) : (low - '0'));
          bitmap[i] = (valHigh << 4) | valLow;
        }
        Serial.printf("USB: Calling drawCustomBitmap(disp=%d)...\n", disp);
        drawCustomBitmap(disp, bitmap);
        Serial.println("USB: drawCustomBitmap DONE!");
      } else {
        Serial.println("USB: bitmap hex too short, SKIPPED!");
      }
    } else {
      Serial.println("USB: bitmap data field is NULL!");
    }
  }
  else if (strcmp(cmd, "custom_oled") == 0) {
    uint8_t disp = doc["disp"] | 1;
    bool enable = doc["enable"] | false;
    Serial.printf("USB: custom_oled disp=%d enable=%d\n", disp, enable);
    setCustomDisplayMode(disp, enable);
  }
  else if (strcmp(cmd, "wifi_config") == 0) {
    const char* ssid = doc["ssid"];
    const char* pass = doc["pass"];
    if (ssid && pass) {
      String newIP;
      bool success = configureSTAWiFi(ssid, pass, newIP);
      char resp[128];
      if (success) {
        snprintf(resp, sizeof(resp),
          "{\"cmd\":\"wifi_config\",\"status\":\"ok\",\"ip\":\"%s\"}", newIP.c_str());
      } else {
        snprintf(resp, sizeof(resp),
          "{\"cmd\":\"wifi_config\",\"status\":\"fail\"}");
      }
      Serial.println(resp);
    }
  }
  else if (strcmp(cmd, "wifi_status") == 0) {
    char resp[200];
    snprintf(resp, sizeof(resp),
      "{\"cmd\":\"wifi_status\",\"sta_connected\":%s,\"sta_ip\":\"%s\",\"sta_ssid\":\"%s\",\"ap_ip\":\"%s\"}",
      isSTAConnected() ? "true" : "false",
      getSTAIP().c_str(), getSTASSID().c_str(), getAPIP().c_str());
    Serial.println(resp);
  }
  else if (strcmp(cmd, "pin_test") == 0) {
    // Read raw GPIO pin states for encoder 1 and buttons
    int encA = digitalRead(PIN_ENC_A);
    int encB = digitalRead(PIN_ENC_B);
    // Enc2: only use interrupt count (analogRead/digitalRead breaks interrupts!)
    int64_t enc2Count = getEncoder2Count();
    int btnPsh = digitalRead(PIN_BTN_PSH);
    int btnCon = digitalRead(PIN_BTN_CON);
    int btnBak = digitalRead(PIN_BTN_BAK);
    char resp[250];
    snprintf(resp, sizeof(resp),
      "{\"cmd\":\"pin_test\",\"enc_a\":%d,\"enc_b\":%d,\"enc2_count\":%lld,\"btn_psh\":%d,\"btn_con\":%d,\"btn_bak\":%d}",
      encA, encB, enc2Count, btnPsh, btnCon, btnBak);
    Serial.println(resp);
  }
  else if (strcmp(cmd, "debug_tach") == 0) {
    bool on = doc["value"] | 0;
    enableTachDebug(on);
  }
  else if (strcmp(cmd, "test_tach") == 0) {
    runTachDiagnostic();
  }
  else if (strcmp(cmd, "set_ppr") == 0) {
    uint8_t ppr = doc["value"] | 14;
    setTachPpr(ppr);
  }
  else if (strcmp(cmd, "set_pwm_freq") == 0) {
    uint32_t freq = doc["value"] | 0;
    if (freq > 0) {
      setFanPwmFreq(freq);
    }
  }
  else if (strcmp(cmd, "set_debounce") == 0) {
    uint32_t us = doc["value"] | 0;
    setTachDebounce(us);
  }
}

void initUSBSerial() {
  // Serial is already initialized in setup() with Serial.begin(115200)
  // Reserve buffer space for large bitmap payloads (~2100 bytes)
  _rxBuffer.reserve(3000);
  Serial.println("USB Serial service ready");
}

void loopUSBSerial() {
  // Read incoming serial data line-by-line
  while (Serial.available()) {
    char c = Serial.read();
    if (c == '\n' || c == '\r') {
      if (_rxBuffer.length() > 0) {
        // Only process lines that look like JSON (start with '{')
        if (_rxBuffer[0] == '{') {
          Serial.printf("USB: RX line len=%u\n", _rxBuffer.length());
          handleUSBCommand(_rxBuffer.c_str());
        }
        _rxBuffer = "";
      }
    } else {
      _rxBuffer += c;
      // Prevent buffer overflow (allow large bitmap payloads up to 3000 chars)
      if (_rxBuffer.length() > 3000) {
        Serial.printf("USB: BUFFER OVERFLOW at %u chars! Discarding.\n", _rxBuffer.length());
        _rxBuffer = "";
      }
    }
  }

  // Check connection timeout
  if (_usbConnected && (millis() - _lastPing > USB_TIMEOUT_MS)) {
    _usbConnected = false;
    Serial.println("USB: Client disconnected (timeout)");
    setLedOn(false);  // Auto turn off LEDs on PC shutdown / disconnect
  }
}

bool isUSBConnected() {
  return _usbConnected;
}

float getUSBCpuTemp() {
  return _cpuTemp;
}

float getUSBGpuTemp() {
  return _gpuTemp;
}

uint16_t getUSBCpuFanRpm() {
  return _cpuFanRpm;
}

uint16_t getUSBGpuFanRpm() {
  return _gpuFanRpm;
}

void usbNotifyStatus(uint8_t fanPercent, bool fanOn, uint8_t ledMode, bool ledOn,
                     float cpuTemp, float gpuTemp) {
  if (!_usbConnected) return;

  char buf[180];
  snprintf(buf, sizeof(buf),
    "{\"fan_pct\":%u,\"fan_on\":%s,\"led_mode\":%u,\"led_on\":%s,\"rpm\":%u,\"target_rpm\":%u,\"cpu\":%.1f,\"gpu\":%.1f}",
    fanPercent, fanOn ? "true" : "false",
    ledMode, ledOn ? "true" : "false",
    getFanRPM(), getTargetRPM(), cpuTemp, gpuTemp
  );
  Serial.println(buf);
}
