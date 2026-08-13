#pragma once
#include <Arduino.h>

void initUSBSerial();
void loopUSBSerial();          // Call every loop() — reads Serial for JSON commands
bool isUSBConnected();         // App connected via USB Serial?

// Getters for values received from host app via USB
float getUSBCpuTemp();
float getUSBGpuTemp();
uint16_t getUSBCpuFanRpm();
uint16_t getUSBGpuFanRpm();

// Extended telemetry getters (CPU/GPU usage, power, clocks, RAM)
float getUSBCpuUsage();
float getUSBGpuUsage();
float getUSBCpuPower();
float getUSBGpuPower();
float getUSBCpuClock();
float getUSBGpuClock();
float getUSBRamUsed();
float getUSBRamTotal();
const char* getUSBTimeStr();

// Notify app with current hardware state (newline-delimited JSON)
void usbNotifyStatus(uint8_t fanPercent, bool fanOn, uint8_t ledMode, bool ledOn,
                     float cpuTemp, float gpuTemp);
