#pragma once

// ============================================================
// GPIO Pin Assignments (YD-ESP32-S3 N16R8)
// ============================================================

// Fan control
#define PIN_FAN_PWM       4   // PWM output to MOSFET HW-517
#define PIN_FAN_TACH      5   // RPM interrupt from Opto PC817

// LED strip
#define PIN_LED_DATA      7   // WS2812B DIN

// OLED 1.3" main display (I2C1) - Standard Hardware I2C Pins
#define PIN_OLED1_SDA     8   // GPIO 8 (SDA)
#define PIN_OLED1_SCL     9   // GPIO 9 (SCL)

// Rotary encoder 1 (on OLED 1.3" module) - Bottom terminal row
#define PIN_ENC_A         35  // TRA - Channel A (GPIO 35)
#define PIN_ENC_B         36  // TRB - Channel B (GPIO 36)

// Rotary encoder 2 (mouse scroll wheel)
#define PIN_ENC2_A        15  // Enc2 Channel A
#define PIN_ENC2_B        16  // Enc2 Channel B

// Buttons on OLED 1.3" module - Bottom terminal row
#define PIN_BTN_CON       21  // CON - Toggle LED on/off (GPIO 21)
#define PIN_BTN_BAK       37  // BAK - Cycle LED mode (GPIO 37)
#define PIN_BTN_PSH       47  // PSH - Toggle fan on/off (GPIO 47)

// OLED 0.96" secondary display (I2C2)
#define PIN_OLED2_SDA     17
#define PIN_OLED2_SCL     18

// ============================================================
// Hardware constants
// ============================================================

#define FAN_PWM_FREQ      25000   // 25kHz for PC fan
#define FAN_PWM_RES       8       // 8-bit resolution (0-255)
#define FAN_PWM_CHANNEL   0
#define FAN_TACH_PPR      2       // 2 pulses per revolution (Standard PC / Blower fan hardware TACH)
#define NUM_LEDS          64      // 64 LEDs (Llano RGB ring)
#define LED_TYPE          WS2812B
#define LED_COLOR_ORDER   GRB

#define OLED_WIDTH        128
#define OLED1_HEIGHT      64      // 1.3" SH1106
#define OLED2_HEIGHT      64      // 0.96" SSD1306
#define OLED_ADDR         0x3C

// Timing
#define DEBOUNCE_MS       200
#define ENCODER_STEP      4       // 4% per detent = exactly 100 RPM per click (300 to 2800 RPM)
#define COMM_NOTIFY_MS    500     // BLE + WiFi notify interval
#define DISPLAY_UPDATE_MS 100
#define RPM_CALC_MS       1000

// ============================================================
// WiFi Configuration
// ============================================================

// AP Mode (creates own hotspot, no router needed)
#define WIFI_AP_SSID      "LlanoFan"     // AP name (MAC suffix auto-appended)
#define WIFI_AP_PASS      "fan12345"     // Min 8 chars, empty = open
#define WIFI_AP_CHANNEL   1
#define WIFI_AP_MAX_CONN  2

// STA Mode (connect to home router) — leave empty to skip STA
#define WIFI_STA_SSID     ""
#define WIFI_STA_PASS     ""
#define WIFI_STA_TIMEOUT  10000          // ms to wait for connection

// WebSocket
#define WS_PORT           81
#define MDNS_NAME         "llanofan"     // llanofan.local

// ============================================================
// BLE UUIDs
// ============================================================

#define BLE_DEVICE_NAME       "Llano Smart Fan"
#define SERVICE_UUID           "4fafc201-1fb5-459e-8fcc-c5c9c331914b"
#define CHAR_FAN_SPEED_UUID    "beb5483e-36e1-4688-b7f5-ea07361b26a8"
#define CHAR_FAN_STATE_UUID    "beb5483e-36e1-4688-b7f5-ea07361b26a9"
#define CHAR_LED_MODE_UUID     "beb5483e-36e1-4688-b7f5-ea07361b26aa"
#define CHAR_LED_COLOR_UUID    "beb5483e-36e1-4688-b7f5-ea07361b26ab"
#define CHAR_LED_BRIGHT_UUID   "beb5483e-36e1-4688-b7f5-ea07361b26ac"
#define CHAR_RPM_UUID          "beb5483e-36e1-4688-b7f5-ea07361b26ad"
#define CHAR_STATUS_UUID       "beb5483e-36e1-4688-b7f5-ea07361b26ae"
#define CHAR_TEMP_UUID         "beb5483e-36e1-4688-b7f5-ea07361b26af"
#define CHAR_WIFI_CONFIG_UUID  "beb5483e-36e1-4688-b7f5-ea07361b26b0"  // write, JSON string
// ============================================================
// LED effect modes (matches Flutter RgbBloc)
// ============================================================

enum LedMode : uint8_t {
  LED_OFF        = 0,
  LED_STATIC     = 1,
  LED_RAINBOW    = 2,
  LED_BREATHING  = 3,
  LED_SPEED_SYNC = 4,
  LED_WAVE       = 5,
  LED_FIRE       = 6,
  LED_COMET      = 7,
  LED_PULSE      = 8,
  LED_DUAL_SPIN  = 9,
  LED_MODE_COUNT = 10
};
