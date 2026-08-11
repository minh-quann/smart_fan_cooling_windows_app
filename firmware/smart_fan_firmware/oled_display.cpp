#include "oled_display.h"
#include "config.h"
#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SH110X.h>
#include <Adafruit_SSD1306.h>

// I2C buses
static TwoWire I2C1 = TwoWire(0);
static TwoWire I2C2 = TwoWire(1);

// Display objects — Dual driver support for 1.3" OLED (SH1106 or SSD1306)
static Adafruit_SH1106G oled1_sh(OLED_WIDTH, OLED1_HEIGHT, &I2C1, -1);
static Adafruit_SSD1306 oled1_ssd(OLED_WIDTH, OLED1_HEIGHT, &I2C1, -1);
static Adafruit_SSD1306 oled2(OLED_WIDTH, OLED2_HEIGHT, &I2C2, -1);

static bool _oled1IsSsd = false;
static bool _oled1Ok = false;
static bool _oled2Ok = false;

static bool _customOled1Active = false;
static bool _customOled2Active = false;

// LED mode names for display
static const char* LED_MODE_NAMES[] = {
  "OFF", "STATIC", "RAINBOW", "BREATH", "SYNC", "WAVE", "FIRE", "COMET", "PULSE", "DUAL SPIN"
};

void setCustomDisplayMode(uint8_t dispIndex, bool enable) {
  if (dispIndex == 1) _customOled1Active = enable;
  else if (dispIndex == 2) _customOled2Active = enable;
}

void drawCustomBitmap(uint8_t dispIndex, const uint8_t* bitmapData) {
  if (!bitmapData) return;

  if (dispIndex == 1 && _oled1Ok) {
    _customOled1Active = true;
    if (_oled1IsSsd) {
      oled1_ssd.clearDisplay();
      oled1_ssd.drawBitmap(0, 0, bitmapData, OLED_WIDTH, OLED1_HEIGHT, SSD1306_WHITE);
      oled1_ssd.display();
    } else {
      oled1_sh.clearDisplay();
      oled1_sh.drawBitmap(0, 0, bitmapData, OLED_WIDTH, OLED1_HEIGHT, SH110X_WHITE);
      oled1_sh.display();
    }
  } else if (dispIndex == 2 && _oled2Ok) {
    _customOled2Active = true;
    oled2.clearDisplay();
    oled2.drawBitmap(0, 0, bitmapData, OLED_WIDTH, OLED2_HEIGHT, SSD1306_WHITE);
    oled2.display();
  }
}

void initDisplays() {
  // Init I2C buses with standard 100kHz clock for maximum stability over dupont wires
  I2C1.begin(PIN_OLED1_SDA, PIN_OLED1_SCL, 100000);
  I2C2.begin(PIN_OLED2_SDA, PIN_OLED2_SCL, 100000);

  Serial.println("==================================================");
  Serial.println(">>> COMPREHENSIVE DUAL OLED DIAGNOSTIC SCAN <<<");

  // 1. Scan I2C1 (Primary bus: GPIO 8 / 9)
  Serial.printf("  Scanning I2C1 Bus A (SDA=GPIO %d, SCL=GPIO %d)...\n", PIN_OLED1_SDA, PIN_OLED1_SCL);
  uint8_t i2c1Addr = 0;
  for (uint8_t addr = 0x01; addr < 0x7F; addr++) {
    I2C1.beginTransmission(addr);
    if (I2C1.endTransmission() == 0) {
      Serial.printf("    ==> FOUND I2C1 Device at Address 0x%02X!\n", addr);
      if (addr == 0x3C || addr == 0x3D) i2c1Addr = addr;
    }
  }

  // NOTE: Fallback I2C on GPIO 47/48 removed — GPIO 47 is used for PSH button

  // 3. Scan I2C2 (Secondary bus: GPIO 17 / 18)
  Serial.printf("  Scanning I2C2 Bus (SDA=GPIO %d, SCL=GPIO %d)...\n", PIN_OLED2_SDA, PIN_OLED2_SCL);
  uint8_t i2c2Addr1 = 0, i2c2Addr2 = 0;
  for (uint8_t addr = 0x01; addr < 0x7F; addr++) {
    I2C2.beginTransmission(addr);
    if (I2C2.endTransmission() == 0) {
      Serial.printf("    ==> FOUND I2C2 Device at Address 0x%02X!\n", addr);
      if (!i2c2Addr1) i2c2Addr1 = addr;
      else i2c2Addr2 = addr;
    }
  }

  // 4. Initialize BOTH SH1106 and SSD1306 drivers for 1.3" OLED to guarantee display activation regardless of chip model!
  uint8_t addr1 = i2c1Addr ? i2c1Addr : OLED_ADDR;
  
  bool ok1_sh = oled1_sh.begin(addr1, true);
  if (ok1_sh) {
    oled1_sh.setContrast(255);
  }
  
  bool ok1_ssd = oled1_ssd.begin(SSD1306_SWITCHCAPVCC, addr1);

  _oled1Ok = ok1_sh || ok1_ssd;
  _oled1IsSsd = ok1_ssd;
  Serial.printf("  [RESULT] OLED1 (1.3\" Display) Init: SH1106=%d, SSD1306=%d at Address 0x%02X\n", ok1_sh, ok1_ssd, addr1);

  if (_oled1Ok) {
    // Render splash screen on SH1106 driver
    oled1_sh.clearDisplay();
    oled1_sh.setTextColor(SH110X_WHITE);
    oled1_sh.setTextSize(2);
    oled1_sh.setCursor(0, 0);
    oled1_sh.print("1.3\" OLED");
    oled1_sh.setTextSize(1);
    oled1_sh.setCursor(0, 24);
    oled1_sh.print("Llano Smart Fan");
    oled1_sh.setCursor(0, 42);
    oled1_sh.print("SH1106 Active!");
    oled1_sh.display();

    // Render splash screen on SSD1306 driver
    oled1_ssd.clearDisplay();
    oled1_ssd.setTextColor(SSD1306_WHITE);
    oled1_ssd.setTextSize(2);
    oled1_ssd.setCursor(0, 0);
    oled1_ssd.print("1.3\" OLED");
    oled1_ssd.setTextSize(1);
    oled1_ssd.setCursor(0, 24);
    oled1_ssd.print("Llano Smart Fan");
    oled1_ssd.setCursor(0, 42);
    oled1_ssd.print("SSD1306 Active!");
    oled1_ssd.display();
  }

  // 5. Init secondary 0.96" OLED (SSD1306)
  uint8_t addr2 = i2c2Addr1 ? i2c2Addr1 : OLED_ADDR;
  bool ok2 = oled2.begin(SSD1306_SWITCHCAPVCC, addr2);
  if (!ok2 && addr2 == 0x3C) {
    ok2 = oled2.begin(SSD1306_SWITCHCAPVCC, 0x3D);
    if (ok2) addr2 = 0x3D;
  }

  if (ok2) {
    _oled2Ok = true;
    Serial.printf("  [SUCCESS] OLED2 (0.96\" SSD1306) Active at Address 0x%02X!\n", addr2);
    oled2.clearDisplay();
    oled2.setTextColor(SSD1306_WHITE);
    oled2.setTextSize(1);
    oled2.setCursor(20, 24);
    oled2.print("System Info OK!");
    oled2.display();
  } else {
    _oled2Ok = false;
    Serial.println("  [ERROR] OLED2 (0.96\" Display) FAILED!");
  }
  Serial.println("==================================================");
}

void updateMainDisplay(uint16_t rpm, uint8_t fanPercent, uint8_t ledMode, bool fanOn) {
  if (!_oled1Ok || _customOled1Active) return;

  if (_oled1IsSsd) {
    // SSD1306 driver update only (0 column offset)
    oled1_ssd.clearDisplay();
    oled1_ssd.setTextSize(1);
    oled1_ssd.setCursor(0, 0);
    oled1_ssd.print("LLANO SMART FAN");
    oled1_ssd.drawLine(0, 10, 127, 10, SSD1306_WHITE);

    oled1_ssd.setTextSize(2);
    oled1_ssd.setCursor(0, 14);
    if (fanOn) {
      oled1_ssd.print(fanPercent);
      oled1_ssd.print("%");
    } else {
      oled1_ssd.print("OFF");
    }

    oled1_ssd.setTextSize(1);
    oled1_ssd.setCursor(75, 14);
    oled1_ssd.print("RPM");
    oled1_ssd.setTextSize(2);
    oled1_ssd.setCursor(75, 24);
    oled1_ssd.print(rpm);

    oled1_ssd.drawLine(0, 42, 127, 42, SSD1306_WHITE);
    oled1_ssd.setTextSize(1);
    oled1_ssd.setCursor(0, 46);
    oled1_ssd.print("PWM:");
    int barWidth = map(fanOn ? fanPercent : 0, 0, 100, 0, 80);
    oled1_ssd.drawRect(30, 45, 82, 8, SSD1306_WHITE);
    oled1_ssd.fillRect(31, 46, barWidth, 6, SSD1306_WHITE);

    oled1_ssd.setCursor(0, 56);
    oled1_ssd.print("LED: ");
    if (ledMode < sizeof(LED_MODE_NAMES) / sizeof(LED_MODE_NAMES[0])) {
      oled1_ssd.print(LED_MODE_NAMES[ledMode]);
    }
    oled1_ssd.display();
  } else {
    // SH1106 driver update only (standard SH1106 offset)
    oled1_sh.clearDisplay();
    oled1_sh.setTextSize(1);
    oled1_sh.setCursor(0, 0);
    oled1_sh.print("LLANO SMART FAN");
    oled1_sh.drawLine(0, 10, 127, 10, SH110X_WHITE);

    oled1_sh.setTextSize(2);
    oled1_sh.setCursor(0, 14);
    if (fanOn) {
      oled1_sh.print(fanPercent);
      oled1_sh.print("%");
    } else {
      oled1_sh.print("OFF");
    }

    oled1_sh.setTextSize(1);
    oled1_sh.setCursor(75, 14);
    oled1_sh.print("RPM");
    oled1_sh.setTextSize(2);
    oled1_sh.setCursor(75, 24);
    oled1_sh.print(rpm);

    oled1_sh.drawLine(0, 42, 127, 42, SH110X_WHITE);
    oled1_sh.setTextSize(1);
    oled1_sh.setCursor(0, 46);
    oled1_sh.print("PWM:");
    int barWidth = map(fanOn ? fanPercent : 0, 0, 100, 0, 80);
    oled1_sh.drawRect(30, 45, 82, 8, SH110X_WHITE);
    oled1_sh.fillRect(31, 46, barWidth, 6, SH110X_WHITE);

    oled1_sh.setCursor(0, 56);
    oled1_sh.print("LED: ");
    if (ledMode < sizeof(LED_MODE_NAMES) / sizeof(LED_MODE_NAMES[0])) {
      oled1_sh.print(LED_MODE_NAMES[ledMode]);
    }
    oled1_sh.display();
  }
}

void updateSecondaryDisplay(uint16_t smartFanRpm, uint8_t fanPercent,
                            float cpuTemp, float gpuTemp,
                            uint16_t cpuFanRpm, uint16_t gpuFanRpm,
                            bool bleConnected, bool wifiConnected, const char* wifiIP) {
  if (!_oled2Ok || _customOled2Active) return;

  oled2.clearDisplay();

  // ---- Yellow zone (Y 0-15): Smart Fan RPM (Large Text Size 2) ----
  // Always display completely rounded even 100 RPM numbers (300, 400, 500 ... 2800 RPM)
  uint16_t cleanRpm = (smartFanRpm > 0) ? (((smartFanRpm + 49) / 100) * 100) : 0;
  if (cleanRpm > 2800) cleanRpm = 2800;

  oled2.setTextSize(2);
  oled2.setTextColor(SSD1306_WHITE);
  oled2.setCursor(0, 0);
  oled2.printf("%u RPM", cleanRpm);

  // ---- Blue zone (Y 16-63): Content ----
  oled2.setTextSize(1);

  // CPU Fan RPM + Temp (Y=20)
  oled2.setCursor(0, 20);
  oled2.printf("CPU: %u", cpuFanRpm);
  oled2.print(" | ");
  if (cpuTemp > 0) {
    oled2.printf("%.0fC", cpuTemp);
  } else {
    oled2.print("--C");
  }

  // GPU Fan RPM + Temp (Y=36)
  oled2.setCursor(0, 36);
  oled2.printf("GPU: %u", gpuFanRpm);
  oled2.print(" | ");
  if (gpuTemp > 0) {
    oled2.printf("%.0fC", gpuTemp);
  } else {
    oled2.print("--C");
  }

  // PWM & Transport Status (Y=52)
  oled2.setCursor(0, 52);
  oled2.printf("PWM: %u%%", fanPercent);
  oled2.setCursor(75, 52);
  if (wifiConnected) {
    oled2.print("WiFi");
  } else if (bleConnected) {
    oled2.print("BLE");
  } else {
    oled2.print("USB");
  }

  oled2.display();
}
