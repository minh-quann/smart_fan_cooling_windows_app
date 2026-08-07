#pragma once
#include <Arduino.h>

void initBLE();
bool isBLEConnected();

// Getters for values received from Flutter app
float getBLECpuTemp();
float getBLEGpuTemp();

// Notify app with current hardware state
void notifyRPM(uint16_t rpm);
void notifyStatus(uint8_t fanPercent, bool fanOn, uint8_t ledMode, bool ledOn);
