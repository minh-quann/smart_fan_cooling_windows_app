#pragma once
#include <Arduino.h>

// ---- Configurable OLED Row Widget Types ----
enum OledWidget : uint8_t {
  WIDGET_HEADER_TITLE  = 0,  // Custom title text (e.g. "LLANO SMART FAN")
  WIDGET_CPU_TELEMETRY = 1,  // CPU: 55C | 45%
  WIDGET_GPU_TELEMETRY = 2,  // GPU: 60C | 38%
  WIDGET_FAN_TELEMETRY = 3,  // 1200 RPM | PWM: 43%
  WIDGET_PWM_PCT       = 4,  // PWM: 43%
  WIDGET_RAM_TELEMETRY = 5,  // RAM: 8.2/16.0GB
  WIDGET_POWER         = 6,  // PWR: 28W / 65W
  WIDGET_CLOCK         = 7,  // CLK: 4.2G / 1850M
  WIDGET_TIME          = 8,  // TIME: 19:33
  WIDGET_COUNT         = 9
};

// ---- OLED Layout Configuration (per display) ----
struct OledLayoutConfig {
  uint8_t rowCount;        // 2, 3, or 4 rows
  OledWidget rows[4];      // Widget assignment per row
  bool showTopDivider;     // Divider line after row 1
  bool showBottomDivider;  // Divider line before last row
  bool showPwmBar;         // PWM progress bar at bottom
  char customTitle[24];    // Custom title text for HEADER_TITLE widget
};

// ---- Public API ----
void initDisplays();

// Unified display update — firmware renders locally based on saved config
void updateMainDisplay(uint16_t rpm, uint8_t fanPercent, uint8_t ledMode, bool fanOn,
                       float cpuTemp, float gpuTemp, uint16_t cpuFanRpm, uint16_t gpuFanRpm,
                       float cpuUsage, float gpuUsage, float cpuPower, float gpuPower,
                       float cpuClock, float gpuClock, float ramUsed, float ramTotal,
                       const char* timeStr = "00:00");

void updateSecondaryDisplay(uint16_t smartFanRpm, uint8_t fanPercent,
                            float cpuTemp, float gpuTemp,
                            uint16_t cpuFanRpm, uint16_t gpuFanRpm,
                            bool bleConnected, bool wifiConnected, const char* wifiIP);

// Custom bitmap mode (legacy — send pre-rendered 1024-byte bitmap from PC)
void drawCustomBitmap(uint8_t dispIndex, const uint8_t* bitmapData);
void setCustomDisplayMode(uint8_t dispIndex, bool enable);

// Configurable layout API (new — lightweight config-based rendering)
void setOledLayoutConfig(uint8_t dispIndex, const OledLayoutConfig& config);
OledLayoutConfig& getOledLayoutConfig(uint8_t dispIndex);
void saveOledLayoutToNVS(uint8_t dispIndex);
void loadOledLayoutFromNVS(uint8_t dispIndex);
bool isOledConfigMode(uint8_t dispIndex);
