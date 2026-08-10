#pragma once
#include <Arduino.h>

void initDisplays();
void updateMainDisplay(uint16_t rpm, uint8_t fanPercent, uint8_t ledMode, bool fanOn);
void updateSecondaryDisplay(uint16_t smartFanRpm, uint8_t fanPercent,
                            float cpuTemp, float gpuTemp,
                            uint16_t cpuFanRpm, uint16_t gpuFanRpm,
                            bool bleConnected, bool wifiConnected, const char* wifiIP);
void drawCustomBitmap(uint8_t dispIndex, const uint8_t* bitmapData);
void setCustomDisplayMode(uint8_t dispIndex, bool enable);

