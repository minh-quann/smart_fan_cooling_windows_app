#pragma once
#include <Arduino.h>

void initDisplays();
void updateMainDisplay(uint16_t rpm, uint8_t fanPercent, uint8_t ledMode, bool fanOn);
void updateSecondaryDisplay(float cpuTemp, float gpuTemp,
                            bool bleConnected, bool wifiConnected, const char* wifiIP);

