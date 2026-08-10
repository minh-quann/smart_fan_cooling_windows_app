#pragma once
#include <Arduino.h>

void initLeds();
void updateLeds();                 // Call every loop iteration
void setLedMode(uint8_t mode);     // LedMode enum from config.h
uint8_t getLedMode();
void setLedColor(uint8_t r, uint8_t g, uint8_t b);
void setLedBrightness(uint8_t brightness);  // 0-255
uint8_t getLedBrightness();
void setLedSpeed(uint8_t speed);            // 1-100%
uint8_t getLedSpeed();
void setLedDirection(bool reverse);         // false = forward, true = reverse
bool getLedDirection();
void setLedCount(uint16_t count);           // Dynamic LED count (1-150)
uint16_t getLedCount();
void setLedOn(bool on);
bool isLedOn();
void setLedSpeedPercent(uint8_t fanPercent);  // For SPEED_SYNC mode
