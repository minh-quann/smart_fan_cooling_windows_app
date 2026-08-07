#pragma once
#include <Arduino.h>

void initFan();
void setFanSpeed(uint8_t percent);
void setFanOn(bool on);
bool isFanOn();
uint16_t getFanRPM();
uint8_t getFanPercent();
void updateRPM();  // Call periodically to recalculate RPM
