#pragma once
#include <Arduino.h>

void initLeds();
void updateLeds();                 // Call every loop iteration
void flushLedPrefs();              // Deferred NVS save (call from main loop)
void clearLedCrashCounter();       // Clear crash counter after stable boot
void setLedMode(uint8_t mode);     // LedMode enum from config.h
uint8_t getLedMode();
void setLedColor(uint8_t r, uint8_t g, uint8_t b);
void setLedBrightness(uint8_t brightness);  // 0-255
uint8_t getLedBrightness();
void setLedSpeed(uint8_t speed);            // 1-100%
uint8_t getLedSpeed();
void setLedDirection(bool reverse);         // false = forward, true = reverse
bool getLedDirection();
void setRainbowColorCount(uint8_t count);  // 0 = continuous full spectrum, or 2, 3, 5, 7 colors
uint8_t getRainbowColorCount();
void setLedCount(uint16_t count);           // Dynamic LED count (1-150)
uint16_t getLedCount();
void setLedOn(bool on);
void setLedOnTemporary(bool on);   // Temporary runtime toggle (for PC shutdown/sleep) without overwriting saved NVS state
bool isLedOn();
void setLedSpeedPercent(uint8_t fanPercent);  // For SPEED_SYNC mode
