#include "oled_display.h"
#include "config.h"
#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SH110X.h>
#include <Adafruit_SSD1306.h>
#include <Preferences.h>

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

// Configurable layout configs (per display)
// Helper to create OledLayoutConfig without C99 designated initializers
static OledLayoutConfig makeOledConfig(uint8_t rows, OledWidget r0, OledWidget r1, OledWidget r2, OledWidget r3,
                                       bool topDiv, bool botDiv, bool pwmBar, const char* title) {
  OledLayoutConfig cfg;
  cfg.rowCount = rows;
  cfg.rows[0] = r0;
  cfg.rows[1] = r1;
  cfg.rows[2] = r2;
  cfg.rows[3] = r3;
  cfg.showTopDivider = topDiv;
  cfg.showBottomDivider = botDiv;
  cfg.showPwmBar = pwmBar;
  strncpy(cfg.customTitle, title, sizeof(cfg.customTitle) - 1);
  cfg.customTitle[sizeof(cfg.customTitle) - 1] = '\0';
  return cfg;
}

static OledLayoutConfig _oled1Config;
static OledLayoutConfig _oled2Config;
static bool _oled1ConfigInited = false;

// Whether each display is in config mode (true) or firmware default mode (false)
static bool _oled1ConfigMode = false;
static bool _oled2ConfigMode = false;

// LED mode names for display
static const char* LED_MODE_NAMES[] = {
  "OFF", "STATIC", "RAINBOW", "BREATH", "SYNC", "WAVE", "FIRE", "COMET", "PULSE", "DUAL SPIN",
  "METEOR", "TWINKLE", "WIPE", "THEATER", "SCANNER", "GRADIENT"
};

// ---- NVS Persistence ----
static Preferences _oledPrefs;

void saveOledLayoutToNVS(uint8_t dispIndex) {
  _oledPrefs.begin("oled_cfg", false);
  char key[16];
  OledLayoutConfig& cfg = (dispIndex == 1) ? _oled1Config : _oled2Config;
  
  snprintf(key, sizeof(key), "d%u_rows", dispIndex);
  _oledPrefs.putUChar(key, cfg.rowCount);
  
  snprintf(key, sizeof(key), "d%u_r0", dispIndex);
  _oledPrefs.putUChar(key, cfg.rows[0]);
  snprintf(key, sizeof(key), "d%u_r1", dispIndex);
  _oledPrefs.putUChar(key, cfg.rows[1]);
  snprintf(key, sizeof(key), "d%u_r2", dispIndex);
  _oledPrefs.putUChar(key, cfg.rows[2]);
  snprintf(key, sizeof(key), "d%u_r3", dispIndex);
  _oledPrefs.putUChar(key, cfg.rows[3]);
  
  snprintf(key, sizeof(key), "d%u_tdiv", dispIndex);
  _oledPrefs.putBool(key, cfg.showTopDivider);
  snprintf(key, sizeof(key), "d%u_bdiv", dispIndex);
  _oledPrefs.putBool(key, cfg.showBottomDivider);
  snprintf(key, sizeof(key), "d%u_bar", dispIndex);
  _oledPrefs.putBool(key, cfg.showPwmBar);
  
  snprintf(key, sizeof(key), "d%u_title", dispIndex);
  _oledPrefs.putString(key, cfg.customTitle);
  
  // Save config mode state
  snprintf(key, sizeof(key), "d%u_cfgon", dispIndex);
  _oledPrefs.putBool(key, (dispIndex == 1) ? _oled1ConfigMode : _oled2ConfigMode);
  
  _oledPrefs.end();
  Serial.printf("OLED: Layout config %u saved to NVS\n", dispIndex);
}

void loadOledLayoutFromNVS(uint8_t dispIndex) {
  _oledPrefs.begin("oled_cfg", true);
  char key[16];
  OledLayoutConfig& cfg = (dispIndex == 1) ? _oled1Config : _oled2Config;
  
  snprintf(key, sizeof(key), "d%u_rows", dispIndex);
  if (_oledPrefs.isKey(key)) {
    cfg.rowCount = _oledPrefs.getUChar(key, cfg.rowCount);
    
    snprintf(key, sizeof(key), "d%u_r0", dispIndex);
    cfg.rows[0] = (OledWidget)_oledPrefs.getUChar(key, cfg.rows[0]);
    snprintf(key, sizeof(key), "d%u_r1", dispIndex);
    cfg.rows[1] = (OledWidget)_oledPrefs.getUChar(key, cfg.rows[1]);
    snprintf(key, sizeof(key), "d%u_r2", dispIndex);
    cfg.rows[2] = (OledWidget)_oledPrefs.getUChar(key, cfg.rows[2]);
    snprintf(key, sizeof(key), "d%u_r3", dispIndex);
    cfg.rows[3] = (OledWidget)_oledPrefs.getUChar(key, cfg.rows[3]);
    
    snprintf(key, sizeof(key), "d%u_tdiv", dispIndex);
    cfg.showTopDivider = _oledPrefs.getBool(key, cfg.showTopDivider);
    snprintf(key, sizeof(key), "d%u_bdiv", dispIndex);
    cfg.showBottomDivider = _oledPrefs.getBool(key, cfg.showBottomDivider);
    snprintf(key, sizeof(key), "d%u_bar", dispIndex);
    cfg.showPwmBar = _oledPrefs.getBool(key, cfg.showPwmBar);
    
    snprintf(key, sizeof(key), "d%u_title", dispIndex);
    String title = _oledPrefs.getString(key, cfg.customTitle);
    strncpy(cfg.customTitle, title.c_str(), sizeof(cfg.customTitle) - 1);
    cfg.customTitle[sizeof(cfg.customTitle) - 1] = '\0';
    
    snprintf(key, sizeof(key), "d%u_cfgon", dispIndex);
    bool cfgMode = _oledPrefs.getBool(key, false);
    if (dispIndex == 1) _oled1ConfigMode = cfgMode;
    else _oled2ConfigMode = cfgMode;
    
    Serial.printf("OLED: Layout config %u loaded from NVS (rows=%u, cfgMode=%d)\n", dispIndex, cfg.rowCount, cfgMode);
  } else {
    Serial.printf("OLED: No saved config for display %u, using defaults\n", dispIndex);
  }
  
  _oledPrefs.end();
}

// ---- Config Accessors ----
void setOledLayoutConfig(uint8_t dispIndex, const OledLayoutConfig& config) {
  if (dispIndex == 1) {
    _oled1Config = config;
    _oled1ConfigMode = true;
  } else if (dispIndex == 2) {
    _oled2Config = config;
    _oled2ConfigMode = true;
  }
}

OledLayoutConfig& getOledLayoutConfig(uint8_t dispIndex) {
  return (dispIndex == 1) ? _oled1Config : _oled2Config;
}

bool isOledConfigMode(uint8_t dispIndex) {
  return (dispIndex == 1) ? _oled1ConfigMode : _oled2ConfigMode;
}

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
  // Initialize default layout configs (can't use C99 designated initializers in C++)
  if (!_oled1ConfigInited) {
    _oled1Config = makeOledConfig(4, WIDGET_HEADER_TITLE, WIDGET_CPU_TELEMETRY, WIDGET_GPU_TELEMETRY, WIDGET_FAN_TELEMETRY,
                                  true, true, true, "LLANO SMART FAN");
    _oled2Config = makeOledConfig(4, WIDGET_FAN_TELEMETRY, WIDGET_CPU_TELEMETRY, WIDGET_GPU_TELEMETRY, WIDGET_PWM_PCT,
                                  false, false, false, "LLANO SMART FAN");
    _oled1ConfigInited = true;
  }

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

  // 4. Initialize BOTH SH1106 and SSD1306 drivers for 1.3" OLED
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

  // Load saved layout configs from NVS
  loadOledLayoutFromNVS(1);
  loadOledLayoutFromNVS(2);
}

// ---- Helper: Build widget text into a char buffer ----
static void buildWidgetText(char* out, size_t outLen, OledWidget widget,
                            const char* customTitle,
                            uint16_t rpm, uint8_t fanPercent,
                            float cpuTemp, float gpuTemp,
                            uint16_t cpuFanRpm, uint16_t gpuFanRpm,
                            float cpuUsage, float gpuUsage,
                            float cpuPower, float gpuPower,
                            float cpuClock, float gpuClock,
                            float ramUsed, float ramTotal,
                            const char* timeStr) {
  switch (widget) {
    case WIDGET_HEADER_TITLE:
      snprintf(out, outLen, "%s", customTitle);
      break;
    case WIDGET_CPU_TELEMETRY:
      if (cpuPower > 0 && cpuClock > 0) {
        snprintf(out, outLen, "CPU:%.0f%% %.0fC %.1fG %.0fW", cpuUsage, cpuTemp > 0 ? cpuTemp : 0, cpuClock, cpuPower);
      } else if (cpuClock > 0) {
        snprintf(out, outLen, "CPU:%.0f%% %.0fC %.1fG", cpuUsage, cpuTemp > 0 ? cpuTemp : 0, cpuClock);
      } else if (cpuTemp > 0) {
        snprintf(out, outLen, "CPU:%.0f%% %.0fC", cpuUsage, cpuTemp);
      } else {
        snprintf(out, outLen, "CPU:%.0f%% --C", cpuUsage);
      }
      break;
    case WIDGET_GPU_TELEMETRY:
      if (gpuPower > 0 && gpuClock >= 1000) {
        snprintf(out, outLen, "GPU:%.0f%% %.0fC %.1fG %.0fW", gpuUsage, gpuTemp > 0 ? gpuTemp : 0, gpuClock / 1000.0f, gpuPower);
      } else if (gpuPower > 0 && gpuClock > 0) {
        snprintf(out, outLen, "GPU:%.0f%% %.0fC %.0fM %.0fW", gpuUsage, gpuTemp > 0 ? gpuTemp : 0, gpuClock, gpuPower);
      } else if (gpuClock >= 1000) {
        snprintf(out, outLen, "GPU:%.0f%% %.0fC %.1fG", gpuUsage, gpuTemp > 0 ? gpuTemp : 0, gpuClock / 1000.0f);
      } else if (gpuClock > 0) {
        snprintf(out, outLen, "GPU:%.0f%% %.0fC %.0fM", gpuUsage, gpuTemp > 0 ? gpuTemp : 0, gpuClock);
      } else if (gpuTemp > 0) {
        snprintf(out, outLen, "GPU:%.0f%% %.0fC", gpuUsage, gpuTemp);
      } else {
        snprintf(out, outLen, "GPU:%.0f%% --C", gpuUsage);
      }
      break;
    case WIDGET_FAN_TELEMETRY: {
      uint16_t cleanRpm = (rpm > 0) ? (((rpm + 49) / 100) * 100) : 0;
      if (cleanRpm > 2800) cleanRpm = 2800;
      snprintf(out, outLen, "%u RPM | PWM %u%%", cleanRpm, fanPercent);
      break;
    }
    case WIDGET_PWM_PCT:
      snprintf(out, outLen, "PWM: %u%%", fanPercent);
      break;
    case WIDGET_RAM_TELEMETRY:
      if (ramTotal > 0) {
        float pct = (ramUsed / ramTotal) * 100.0f;
        snprintf(out, outLen, "RAM: %.0f%% %.1f/%.1fG", pct, ramUsed, ramTotal);
      } else {
        snprintf(out, outLen, "RAM: --/--GB");
      }
      break;
    case WIDGET_POWER:
      snprintf(out, outLen, "PWR: %.0fW/%.0fW", cpuPower, gpuPower);
      break;
    case WIDGET_CLOCK:
      snprintf(out, outLen, "CLK: %.1fG/%.0fM", cpuClock, gpuClock);
      break;
    case WIDGET_TIME:
      snprintf(out, outLen, "TIME: %s", (timeStr && timeStr[0]) ? timeStr : "--:--");
      break;
    default:
      snprintf(out, outLen, "---");
      break;
  }
}

// ---- Render configurable 4-row layout on a given GFX display ----
// Template to avoid duplicating for SH1106 and SSD1306 drivers
template<typename Display>
static void renderConfigLayout(Display& disp, uint16_t whiteColor,
                               const OledLayoutConfig& cfg,
                               uint16_t rpm, uint8_t fanPercent,
                               float cpuTemp, float gpuTemp,
                               uint16_t cpuFanRpm, uint16_t gpuFanRpm,
                               float cpuUsage, float gpuUsage,
                               float cpuPower, float gpuPower,
                               float cpuClock, float gpuClock,
                               float ramUsed, float ramTotal,
                               const char* timeStr) {
  disp.clearDisplay();
  disp.setTextColor(whiteColor);
  
  char lineBuf[32];
  
  if (cfg.rowCount <= 2) {
    // ---- 2-Row Layout: Large text (Size 2) ----
    buildWidgetText(lineBuf, sizeof(lineBuf), cfg.rows[0], cfg.customTitle,
                    rpm, fanPercent, cpuTemp, gpuTemp, cpuFanRpm, gpuFanRpm,
                    cpuUsage, gpuUsage, cpuPower, gpuPower, cpuClock, gpuClock, ramUsed, ramTotal, timeStr);
    disp.setTextSize(2);
    disp.setCursor(0, 2);
    disp.print(lineBuf);
    
    if (cfg.showTopDivider) disp.drawLine(0, 20, 127, 20, whiteColor);
    
    buildWidgetText(lineBuf, sizeof(lineBuf), cfg.rows[1], cfg.customTitle,
                    rpm, fanPercent, cpuTemp, gpuTemp, cpuFanRpm, gpuFanRpm,
                    cpuUsage, gpuUsage, cpuPower, gpuPower, cpuClock, gpuClock, ramUsed, ramTotal, timeStr);
    disp.setTextSize(2);
    disp.setCursor(0, 26);
    disp.print(lineBuf);
    
    if (cfg.showBottomDivider) disp.drawLine(0, 48, 127, 48, whiteColor);
    
  } else if (cfg.rowCount == 3) {
    // ---- 3-Row Layout: Header Size 2 + 2 rows Size 1 ----
    buildWidgetText(lineBuf, sizeof(lineBuf), cfg.rows[0], cfg.customTitle,
                    rpm, fanPercent, cpuTemp, gpuTemp, cpuFanRpm, gpuFanRpm,
                    cpuUsage, gpuUsage, cpuPower, gpuPower, cpuClock, gpuClock, ramUsed, ramTotal, timeStr);
    disp.setTextSize(2);
    disp.setCursor(0, 2);
    disp.print(lineBuf);
    
    if (cfg.showTopDivider) disp.drawLine(0, 20, 127, 20, whiteColor);
    
    buildWidgetText(lineBuf, sizeof(lineBuf), cfg.rows[1], cfg.customTitle,
                    rpm, fanPercent, cpuTemp, gpuTemp, cpuFanRpm, gpuFanRpm,
                    cpuUsage, gpuUsage, cpuPower, gpuPower, cpuClock, gpuClock, ramUsed, ramTotal, timeStr);
    disp.setTextSize(1);
    disp.setCursor(0, 24);
    disp.print(lineBuf);
    
    buildWidgetText(lineBuf, sizeof(lineBuf), cfg.rows[2], cfg.customTitle,
                    rpm, fanPercent, cpuTemp, gpuTemp, cpuFanRpm, gpuFanRpm,
                    cpuUsage, gpuUsage, cpuPower, gpuPower, cpuClock, gpuClock, ramUsed, ramTotal, timeStr);
    disp.setTextSize(1);
    disp.setCursor(0, 36);
    disp.print(lineBuf);
    
    if (cfg.showBottomDivider) disp.drawLine(0, 48, 127, 48, whiteColor);
    
  } else {
    // ---- 4-Row Layout: All Size 1, compact detail view ----
    buildWidgetText(lineBuf, sizeof(lineBuf), cfg.rows[0], cfg.customTitle,
                    rpm, fanPercent, cpuTemp, gpuTemp, cpuFanRpm, gpuFanRpm,
                    cpuUsage, gpuUsage, cpuPower, gpuPower, cpuClock, gpuClock, ramUsed, ramTotal, timeStr);
    disp.setTextSize(1);
    disp.setCursor(0, 0);
    disp.print(lineBuf);
    
    if (cfg.showTopDivider) disp.drawLine(0, 10, 127, 10, whiteColor);
    
    buildWidgetText(lineBuf, sizeof(lineBuf), cfg.rows[1], cfg.customTitle,
                    rpm, fanPercent, cpuTemp, gpuTemp, cpuFanRpm, gpuFanRpm,
                    cpuUsage, gpuUsage, cpuPower, gpuPower, cpuClock, gpuClock, ramUsed, ramTotal, timeStr);
    disp.setTextSize(1);
    disp.setCursor(0, 14);
    disp.print(lineBuf);
    
    buildWidgetText(lineBuf, sizeof(lineBuf), cfg.rows[2], cfg.customTitle,
                    rpm, fanPercent, cpuTemp, gpuTemp, cpuFanRpm, gpuFanRpm,
                    cpuUsage, gpuUsage, cpuPower, gpuPower, cpuClock, gpuClock, ramUsed, ramTotal, timeStr);
    disp.setTextSize(1);
    disp.setCursor(0, 28);
    disp.print(lineBuf);
    
    if (cfg.showBottomDivider) disp.drawLine(0, 42, 127, 42, whiteColor);
    
    buildWidgetText(lineBuf, sizeof(lineBuf), cfg.rows[3], cfg.customTitle,
                    rpm, fanPercent, cpuTemp, gpuTemp, cpuFanRpm, gpuFanRpm,
                    cpuUsage, gpuUsage, cpuPower, gpuPower, cpuClock, gpuClock, ramUsed, ramTotal, timeStr);
    disp.setTextSize(1);
    disp.setCursor(0, 46);
    disp.print(lineBuf);
  }
  
  // PWM Progress Bar at bottom
  if (cfg.showPwmBar) {
    int yPos = (cfg.rowCount >= 4) ? 55 : 52;
    disp.setTextSize(1);
    disp.setCursor(0, yPos);
    disp.print("PWM:");
    int barWidth = map(fanPercent, 0, 100, 0, 80);
    disp.drawRect(30, yPos - 1, 82, 8, whiteColor);
    disp.fillRect(31, yPos, barWidth, 6, whiteColor);
  }
  
  disp.display();
}

// ---- Firmware default layout for OLED 1 (1.3" display) ----
template<typename Display>
static void renderDefaultOled1(Display& disp, uint16_t whiteColor,
                               uint16_t rpm, uint8_t fanPercent, uint8_t ledMode, bool fanOn,
                               float cpuTemp, float gpuTemp, float cpuUsage, float gpuUsage,
                               float cpuPower, float gpuPower,
                               float cpuClock, float gpuClock, float ramUsed, float ramTotal,
                               const char* timeStr) {
  disp.clearDisplay();
  disp.setTextColor(whiteColor);
  disp.setTextSize(1);

  // ---- Dòng 1 (Y=0): RPM Fan & % Fan ----
  disp.setCursor(0, 0);
  uint16_t cleanRpm = (rpm > 0) ? (((rpm + 49) / 100) * 100) : 0;
  if (cleanRpm > 2800) cleanRpm = 2800;
  if (fanOn) {
    disp.printf("%u RPM | PWM %u%%", cleanRpm, fanPercent);
  } else {
    disp.print("FAN: OFF | 0 RPM");
  }

  disp.drawLine(0, 10, 127, 10, whiteColor);

  // ---- Khung dưới: CPU, GPU, RAM ----
  // Line 2 (Y=14): CPU Usage %, Temp, Speed (GHz), Power (W)
  disp.setCursor(0, 14);
  if (cpuPower > 0 && cpuClock > 0) {
    disp.printf("CPU:%.0f%% %.0fC %.1fG %.0fW", cpuUsage, cpuTemp > 0 ? cpuTemp : 0, cpuClock, cpuPower);
  } else if (cpuClock > 0) {
    disp.printf("CPU:%.0f%% %.0fC %.1fG", cpuUsage, cpuTemp > 0 ? cpuTemp : 0, cpuClock);
  } else if (cpuTemp > 0) {
    disp.printf("CPU:%.0f%% %.0fC", cpuUsage, cpuTemp);
  } else {
    disp.printf("CPU:%.0f%% --C", cpuUsage);
  }

  // Line 3 (Y=26): GPU Usage %, Temp, Speed (GHz/MHz), Power (W)
  disp.setCursor(0, 26);
  if (gpuPower > 0 && gpuClock >= 1000) {
    disp.printf("GPU:%.0f%% %.0fC %.1fG %.0fW", gpuUsage, gpuTemp > 0 ? gpuTemp : 0, gpuClock / 1000.0f, gpuPower);
  } else if (gpuPower > 0 && gpuClock > 0) {
    disp.printf("GPU:%.0f%% %.0fC %.0fM %.0fW", gpuUsage, gpuTemp > 0 ? gpuTemp : 0, gpuClock, gpuPower);
  } else if (gpuClock >= 1000) {
    disp.printf("GPU:%.0f%% %.0fC %.1fG", gpuUsage, gpuTemp > 0 ? gpuTemp : 0, gpuClock / 1000.0f);
  } else if (gpuClock > 0) {
    disp.printf("GPU:%.0f%% %.0fC %.0fM", gpuUsage, gpuTemp > 0 ? gpuTemp : 0, gpuClock);
  } else if (gpuTemp > 0) {
    disp.printf("GPU:%.0f%% %.0fC", gpuUsage, gpuTemp);
  } else {
    disp.printf("GPU:%.0f%% --C", gpuUsage);
  }

  // Line 4 (Y=38): RAM Usage % & GB
  disp.setCursor(0, 38);
  if (ramTotal > 0) {
    float ramPct = (ramUsed / ramTotal) * 100.0f;
    disp.printf("RAM: %.0f%% %.1f/%.1fG", ramPct, ramUsed, ramTotal);
  } else {
    disp.print("RAM: --/--GB");
  }

  disp.drawLine(0, 49, 127, 49, whiteColor);

  // ---- Dưới cùng (Y=53): Thời gian hiện tại (Giờ:Phút) ----
  disp.setCursor(0, 53);
  disp.printf("TIME: %s", (timeStr && timeStr[0]) ? timeStr : "--:--");
  if (ledMode < sizeof(LED_MODE_NAMES) / sizeof(LED_MODE_NAMES[0])) {
    disp.printf(" | %s", LED_MODE_NAMES[ledMode]);
  }

  disp.display();
}

void updateMainDisplay(uint16_t rpm, uint8_t fanPercent, uint8_t ledMode, bool fanOn,
                       float cpuTemp, float gpuTemp, uint16_t cpuFanRpm, uint16_t gpuFanRpm,
                       float cpuUsage, float gpuUsage, float cpuPower, float gpuPower,
                       float cpuClock, float gpuClock, float ramUsed, float ramTotal,
                       const char* timeStr) {
  if (!_oled1Ok || _customOled1Active) return;
  
  if (_oled1ConfigMode) {
    // Render using configurable layout
    if (_oled1IsSsd) {
      renderConfigLayout(oled1_ssd, SSD1306_WHITE, _oled1Config,
                        rpm, fanPercent, cpuTemp, gpuTemp, cpuFanRpm, gpuFanRpm,
                        cpuUsage, gpuUsage, cpuPower, gpuPower, cpuClock, gpuClock, ramUsed, ramTotal, timeStr);
    } else {
      renderConfigLayout(oled1_sh, SH110X_WHITE, _oled1Config,
                        rpm, fanPercent, cpuTemp, gpuTemp, cpuFanRpm, gpuFanRpm,
                        cpuUsage, gpuUsage, cpuPower, gpuPower, cpuClock, gpuClock, ramUsed, ramTotal, timeStr);
    }
  } else {
    // Render firmware default layout
    if (_oled1IsSsd) {
      renderDefaultOled1(oled1_ssd, SSD1306_WHITE, rpm, fanPercent, ledMode, fanOn,
                         cpuTemp, gpuTemp, cpuUsage, gpuUsage, cpuPower, gpuPower, cpuClock, gpuClock, ramUsed, ramTotal, timeStr);
    } else {
      renderDefaultOled1(oled1_sh, SH110X_WHITE, rpm, fanPercent, ledMode, fanOn,
                         cpuTemp, gpuTemp, cpuUsage, gpuUsage, cpuPower, gpuPower, cpuClock, gpuClock, ramUsed, ramTotal, timeStr);
    }
  }
}

void updateSecondaryDisplay(uint16_t smartFanRpm, uint8_t fanPercent,
                            float cpuTemp, float gpuTemp,
                            uint16_t cpuFanRpm, uint16_t gpuFanRpm,
                            bool bleConnected, bool wifiConnected, const char* wifiIP,
                            float boardTemp) {
  if (!_oled2Ok || _customOled2Active) return;

  // OLED 2 always uses its firmware default bicolor layout (no config mode for 0.96" screen)
  oled2.clearDisplay();

  // ---- Yellow zone (Y 0-15): Smart Fan RPM (Size 2) + PWM % (Size 2) ----
  uint16_t cleanRpm = (smartFanRpm > 0) ? (((smartFanRpm + 49) / 100) * 100) : 0;
  if (cleanRpm > 2800) cleanRpm = 2800;

  oled2.setTextColor(SSD1306_WHITE);

  // 1. RPM number in LARGE text size 2 on the left
  oled2.setTextSize(2);
  oled2.setCursor(0, 0);
  oled2.printf("%u", cleanRpm);

  // Calculate width of RPM digits to place small "RPM" label right after it
  char rpmBuf[8];
  snprintf(rpmBuf, sizeof(rpmBuf), "%u", cleanRpm);
  uint8_t rpmDigitsLen = strlen(rpmBuf);
  uint8_t rpmWidth = rpmDigitsLen * 12;

  // 2. Small "RPM" label in text size 1 in the middle
  oled2.setTextSize(1);
  oled2.setCursor(rpmWidth + 3, 4);
  oled2.print("RPM");

  // 3. PWM percentage number in LARGE text size 2 on the right
  oled2.setTextSize(2);
  char pctBuf[8];
  snprintf(pctBuf, sizeof(pctBuf), "%u%%", fanPercent);
  uint8_t pctLen = strlen(pctBuf);
  int16_t pctX = 128 - (pctLen * 12);
  if (pctX < (rpmWidth + 24)) pctX = rpmWidth + 24;
  oled2.setCursor(pctX, 0);
  oled2.print(pctBuf);

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

  // Board Temperature (Y=52) — replaces old PWM & USB line
  oled2.setCursor(0, 52);
  if (boardTemp > 0.0f) {
    oled2.printf("BOARD: %.0fC", boardTemp);
  } else {
    float chipTemp = temperatureRead();
    if (chipTemp > 0.0f && !isnan(chipTemp)) {
      oled2.printf("BOARD: %.0fC", chipTemp);
    } else {
      oled2.print("BOARD: --C");
    }
  }

  oled2.display();
}
