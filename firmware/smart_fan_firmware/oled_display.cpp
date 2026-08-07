#include "oled_display.h"
#include "config.h"
#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SH110X.h>
#include <Adafruit_SSD1306.h>

// I2C buses
static TwoWire I2C1 = TwoWire(0);
static TwoWire I2C2 = TwoWire(1);

// Display objects
static Adafruit_SH1106G oled1(OLED_WIDTH, OLED1_HEIGHT, &I2C1, -1);
static Adafruit_SSD1306 oled2(OLED_WIDTH, OLED2_HEIGHT, &I2C2, -1);

// LED mode names for display
static const char* LED_MODE_NAMES[] = {
  "OFF", "STATIC", "RAINBOW", "BREATH", "SYNC", "WAVE", "FIRE"
};

void initDisplays() {
  // Init I2C buses with assigned pins
  I2C1.begin(PIN_OLED1_SDA, PIN_OLED1_SCL, 400000);
  I2C2.begin(PIN_OLED2_SDA, PIN_OLED2_SCL, 400000);

  // Scan I2C2 bus to find OLED address
  Serial.println("  I2C2 scan (GPIO17/18):");
  uint8_t oled2Addr = 0;
  for (uint8_t addr = 0x01; addr < 0x7F; addr++) {
    I2C2.beginTransmission(addr);
    if (I2C2.endTransmission() == 0) {
      Serial.printf("  Found device at 0x%02X\n", addr);
      if (addr == 0x3C || addr == 0x3D) oled2Addr = addr;
    }
  }

  // Scan I2C1 bus too
  Serial.println("  I2C1 scan (GPIO8/9):");
  for (uint8_t addr = 0x01; addr < 0x7F; addr++) {
    I2C1.beginTransmission(addr);
    if (I2C1.endTransmission() == 0) {
      Serial.printf("  Found device at 0x%02X\n", addr);
    }
  }

  // Init main 1.3" OLED (SH1106)
  if (oled1.begin(OLED_ADDR, true)) {
    oled1.clearDisplay();
    oled1.setTextColor(SH110X_WHITE);
    oled1.setTextSize(1);
    oled1.setCursor(20, 28);
    oled1.print("Llano Smart Fan");
    oled1.display();
  }

  // Init secondary 0.96" OLED (SSD1306) - use detected address
  uint8_t addr2 = oled2Addr ? oled2Addr : OLED_ADDR;
  Serial.printf("  OLED2 using addr 0x%02X\n", addr2);
  if (oled2.begin(SSD1306_SWITCHCAPVCC, addr2)) {
    oled2.clearDisplay();
    oled2.setTextColor(SSD1306_WHITE);
    oled2.setTextSize(1);
    oled2.setCursor(30, 28);
    oled2.print("System Info");
    oled2.display();
    Serial.println("  OLED2 init OK!");
  } else {
    Serial.println("  OLED2 init FAILED!");
  }
}

void updateMainDisplay(uint16_t rpm, uint8_t fanPercent, uint8_t ledMode, bool fanOn) {
  oled1.clearDisplay();

  // Title bar
  oled1.setTextSize(1);
  oled1.setCursor(0, 0);
  oled1.print("LLANO SMART FAN");

  // Horizontal divider
  oled1.drawLine(0, 10, 127, 10, SH110X_WHITE);

  // Fan status - large text
  oled1.setTextSize(2);
  oled1.setCursor(0, 14);
  if (fanOn) {
    oled1.print(fanPercent);
    oled1.print("%");
  } else {
    oled1.print("OFF");
  }

  // RPM display
  oled1.setTextSize(1);
  oled1.setCursor(75, 14);
  oled1.print("RPM");
  oled1.setTextSize(2);
  oled1.setCursor(75, 24);
  oled1.print(rpm);

  // Divider
  oled1.drawLine(0, 42, 127, 42, SH110X_WHITE);

  // Fan speed bar (visual gauge)
  oled1.setTextSize(1);
  oled1.setCursor(0, 46);
  oled1.print("PWM:");

  // Draw progress bar
  int barWidth = map(fanOn ? fanPercent : 0, 0, 100, 0, 80);
  oled1.drawRect(30, 45, 82, 8, SH110X_WHITE);
  oled1.fillRect(31, 46, barWidth, 6, SH110X_WHITE);

  // LED mode
  oled1.setCursor(0, 56);
  oled1.print("LED: ");
  if (ledMode < LED_MODE_COUNT) {
    oled1.print(LED_MODE_NAMES[ledMode]);
  }

  oled1.display();
}

void updateSecondaryDisplay(float cpuTemp, float gpuTemp,
                            bool bleConnected, bool wifiConnected, const char* wifiIP) {
  oled2.clearDisplay();

  // ---- Yellow zone (Y 0-15): Title ----
  oled2.setTextSize(1);
  oled2.setCursor(16, 4);
  oled2.print("SYSTEM MONITOR");

  // ---- Blue zone (Y 16-63): Content ----

  // CPU & GPU temps (Y=18)
  oled2.setCursor(0, 18);
  oled2.print("CPU:");
  if (cpuTemp > 0) {
    oled2.print(cpuTemp, 0);
    oled2.print("C");
  } else {
    oled2.print("N/A");
  }
  oled2.setCursor(68, 18);
  oled2.print("GPU:");
  if (gpuTemp > 0) {
    oled2.print(gpuTemp, 0);
    oled2.print("C");
  } else {
    oled2.print("N/A");
  }

  // Divider
  oled2.drawLine(0, 28, 127, 28, SSD1306_WHITE);

  // Connection status (Y=32)
  oled2.setCursor(0, 32);
  oled2.print("BLE:");
  oled2.print(bleConnected ? "OK" : "--");
  oled2.setCursor(68, 32);
  oled2.print("WS:");
  oled2.print(wifiConnected ? "OK" : "--");

  // Divider
  oled2.drawLine(0, 42, 127, 42, SSD1306_WHITE);

  // IP + port (Y=46)
  oled2.setCursor(0, 46);
  oled2.print(wifiIP);
  oled2.setCursor(98, 46);
  oled2.print(":");
  oled2.print(WS_PORT);

  // mDNS name (Y=56)
  oled2.setCursor(0, 56);
  oled2.print(MDNS_NAME);
  oled2.print(".local");

  oled2.display();
}
