#pragma once
#include <Arduino.h>

void initFan();
void setFanSpeed(uint8_t percent);
void setFanOn(bool on);
bool isFanOn();
uint16_t getFanRPM();
uint8_t getFanPercent();
void setTargetRPM(uint16_t rpm);
uint16_t getTargetRPM();
void updateRPM();                       // Recalculate RPM
void setFanPwmFreq(uint32_t freqHz);  // Change PWM frequency at runtime
void enableTachDebug(bool on);        // Toggle raw tach debug output
void setTachDebounce(uint32_t us);     // Change tach debounce at runtime
void setTachPpr(uint8_t ppr);          // Change PPR (pulses per revolution) at runtime
uint8_t getTachPpr();
void runTachDiagnostic();             // Diagnostic hardware test for TACH pin
