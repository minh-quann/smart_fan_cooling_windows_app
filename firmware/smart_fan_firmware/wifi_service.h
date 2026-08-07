#pragma once
#include <Arduino.h>

void initWiFiService();
void loopWiFiService();        // Call every loop() — handles WebSocket events
bool isWiFiConnected();        // Any WebSocket client connected?
String getWiFiIP();            // Current IP address (AP or STA)

bool isSTAConnected();
String getSTAIP();
String getAPIP();
String getSTASSID();
bool configureSTAWiFi(const char* ssid, const char* pass, String& outIP);

// Getters for values received from Flutter app via WiFi
float getWiFiCpuTemp();
float getWiFiGpuTemp();

// Notify all connected WebSocket clients
void wsNotifyRPM(uint16_t rpm);
void wsNotifyStatus(uint8_t fanPercent, bool fanOn, uint8_t ledMode, bool ledOn,
                    float cpuTemp, float gpuTemp);
