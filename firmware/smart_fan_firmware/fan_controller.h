#pragma once
#include <Arduino.h>

void initFan();
void setFanSpeed(uint8_t percent);
void setFanOn(bool on);
bool isFanOn();
uint16_t getFanRPM();
uint8_t getFanPercent();
void updateRPM();           // Call periodically to recalculate RPM
void setFanPwmFreq(uint32_t freqHz);  // Change PWM frequency at runtime
void enableTachDebug(bool on);        // Toggle raw tach debug output
