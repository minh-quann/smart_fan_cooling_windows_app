#pragma once
#include <Arduino.h>

void initUSBSerial();
void loopUSBSerial();          // Call every loop() — reads Serial for JSON commands
bool isUSBConnected();         // App connected via USB Serial?

// Getters for values received from Flutter app via USB
float getUSBCpuTemp();
float getUSBGpuTemp();

// Notify app with current hardware state (newline-delimited JSON)
void usbNotifyStatus(uint8_t fanPercent, bool fanOn, uint8_t ledMode, bool ledOn,
                     float cpuTemp, float gpuTemp);
