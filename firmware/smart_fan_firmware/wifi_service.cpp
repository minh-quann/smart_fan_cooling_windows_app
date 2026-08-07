#include "wifi_service.h"
#include "config.h"
#include "fan_controller.h"
#include "led_effects.h"
#include <WiFi.h>
#include <WebSocketsServer.h>
#include <ESPmDNS.h>
#include <ArduinoJson.h>
#include <Preferences.h>

static WebSocketsServer ws(WS_PORT);
static bool _wsClientConnected = false;
static String _ipAddr = "";
static String _staIp = "";
static String _apIp = "";
static String _staSsid = "";
static bool _staConnected = false;
static float _cpuTemp = 0;
static float _gpuTemp = 0;
static Preferences preferences;


// ---- Process incoming JSON command from app ----
static void handleCommand(uint8_t clientNum, const char* payload) {
  JsonDocument doc;
  DeserializationError err = deserializeJson(doc, payload);
  if (err) {
    Serial.printf("WS: JSON parse error: %s\n", err.c_str());
    return;
  }

  const char* cmd = doc["cmd"];
  if (!cmd) return;

  if (strcmp(cmd, "fan_speed") == 0) {
    uint8_t val = doc["value"] | 0;
    setFanSpeed(val);
    Serial.printf("WS: Fan speed -> %d%%\n", val);
  }
  else if (strcmp(cmd, "fan_state") == 0) {
    bool on = doc["value"] | 0;
    setFanOn(on);
    Serial.printf("WS: Fan %s\n", on ? "ON" : "OFF");
  }
  else if (strcmp(cmd, "led_mode") == 0) {
    uint8_t mode = doc["value"] | 0;
    setLedMode(mode);
    Serial.printf("WS: LED mode -> %d\n", mode);
  }
  else if (strcmp(cmd, "led_color") == 0) {
    uint8_t r = doc["r"] | 0;
    uint8_t g = doc["g"] | 0;
    uint8_t b = doc["b"] | 0;
    setLedColor(r, g, b);
    Serial.printf("WS: LED color R%d G%d B%d\n", r, g, b);
  }
  else if (strcmp(cmd, "led_brightness") == 0) {
    uint8_t val = doc["value"] | 0;
    setLedBrightness(val);
    Serial.printf("WS: LED brightness -> %d\n", val);
  }
  else if (strcmp(cmd, "temp") == 0) {
    _cpuTemp = doc["cpu"] | 0.0f;
    _gpuTemp = doc["gpu"] | 0.0f;
    Serial.printf("WS: Temps CPU=%.1f GPU=%.1f\n", _cpuTemp, _gpuTemp);
  }
  else if (strcmp(cmd, "wifi_config") == 0) {
    const char* ssid = doc["ssid"];
    const char* pass = doc["pass"];
    if (ssid && pass) {
      String newIP;
      bool success = configureSTAWiFi(ssid, pass, newIP);
      char resp[100];
      if (success) {
        snprintf(resp, sizeof(resp), "{\"cmd\":\"wifi_config\",\"status\":\"ok\",\"ip\":\"%s\"}", newIP.c_str());
      } else {
        snprintf(resp, sizeof(resp), "{\"cmd\":\"wifi_config\",\"status\":\"fail\"}");
      }
      ws.sendTXT(clientNum, resp);
    }
  }
  else if (strcmp(cmd, "wifi_status") == 0) {
    char resp[200];
    snprintf(resp, sizeof(resp), "{\"cmd\":\"wifi_status\",\"sta_connected\":%s,\"sta_ip\":\"%s\",\"sta_ssid\":\"%s\",\"ap_ip\":\"%s\"}",
             _staConnected ? "true" : "false", _staIp.c_str(), _staSsid.c_str(), _apIp.c_str());
    ws.sendTXT(clientNum, resp);
  }
  else if (strcmp(cmd, "wifi_reset") == 0) {
    preferences.begin("wifi", false);
    preferences.clear();
    preferences.end();
    WiFi.disconnect();
    _staConnected = false;
    _staIp = "";
    _staSsid = "";
    _ipAddr = _apIp;
    ws.sendTXT(clientNum, "{\"cmd\":\"wifi_reset\",\"status\":\"ok\"}");
  }
  else if (strcmp(cmd, "pin_test") == 0) {
    int encA = digitalRead(PIN_ENC_A);
    int encB = digitalRead(PIN_ENC_B);
    int btnPsh = digitalRead(PIN_BTN_PSH);
    int btnCon = digitalRead(PIN_BTN_CON);
    int btnBak = digitalRead(PIN_BTN_BAK);
    char resp[200];
    snprintf(resp, sizeof(resp),
      "{\"cmd\":\"pin_test\",\"enc_a\":%d,\"enc_b\":%d,\"btn_psh\":%d,\"btn_con\":%d,\"btn_bak\":%d}",
      encA, encB, btnPsh, btnCon, btnBak);
    ws.sendTXT(clientNum, resp);
  }
}

// ---- WebSocket event handler ----
static void onWsEvent(uint8_t num, WStype_t type, uint8_t* payload, size_t length) {
  switch (type) {
    case WStype_CONNECTED:
      _wsClientConnected = true;
      Serial.printf("WS: Client #%u connected\n", num);
      break;

    case WStype_DISCONNECTED:
      _wsClientConnected = false;
      Serial.printf("WS: Client #%u disconnected\n", num);
      break;

    case WStype_TEXT:
      handleCommand(num, (const char*)payload);
      break;

    default:
      break;
  }
}

void initWiFiService() {
  // Start WiFi AP mode (always available, no router needed)
  // Append last 4 hex chars of MAC for unique SSID
  uint8_t mac[6];
  WiFi.macAddress(mac);
  char apSSID[32];
  snprintf(apSSID, sizeof(apSSID), "%s_%02X%02X", WIFI_AP_SSID, mac[4], mac[5]);

  WiFi.mode(WIFI_AP_STA);
  WiFi.softAP(apSSID, WIFI_AP_PASS, WIFI_AP_CHANNEL, 0, WIFI_AP_MAX_CONN);
  _apIp = WiFi.softAPIP().toString();
  _ipAddr = _apIp;
  Serial.printf("WiFi AP: %s @ %s\n", apSSID, _apIp.c_str());

  // Load from NVS
  preferences.begin("wifi", true);
  String savedSSID = preferences.getString("ssid", "");
  String savedPass = preferences.getString("pass", "");
  preferences.end();

  const char* targetSSID = savedSSID.length() > 0 ? savedSSID.c_str() : WIFI_STA_SSID;
  const char* targetPass = savedSSID.length() > 0 ? savedPass.c_str() : WIFI_STA_PASS;

  if (strlen(targetSSID) > 0) {
    Serial.printf("WiFi STA: Connecting to %s...\n", targetSSID);
    WiFi.begin(targetSSID, targetPass);

    uint32_t start = millis();
    while (WiFi.status() != WL_CONNECTED && (millis() - start) < WIFI_STA_TIMEOUT) {
      delay(100);
    }

    if (WiFi.status() == WL_CONNECTED) {
      _staIp = WiFi.localIP().toString();
      _ipAddr = _staIp;
      _staSsid = targetSSID;
      _staConnected = true;
      Serial.printf("WiFi STA: Connected @ %s\n", _staIp.c_str());
    } else {
      Serial.println("WiFi STA: Failed, using AP only");
      WiFi.disconnect();
    }
  }

  // Start mDNS so app can find us at llanofan.local
  if (MDNS.begin(MDNS_NAME)) {
    MDNS.addService("ws", "tcp", WS_PORT);
    Serial.printf("mDNS: %s.local\n", MDNS_NAME);
  }

  // Start WebSocket server
  ws.begin();
  ws.onEvent(onWsEvent);
  Serial.printf("WebSocket: ws://%s:%d\n", _ipAddr.c_str(), WS_PORT);
}

void loopWiFiService() {
  ws.loop();
}

bool isWiFiConnected() {
  return _wsClientConnected;
}

String getWiFiIP() {
  return _ipAddr;
}

bool isSTAConnected() {
  return _staConnected;
}

String getSTAIP() {
  return _staIp;
}

String getAPIP() {
  return _apIp;
}

String getSTASSID() {
  return _staSsid;
}

bool configureSTAWiFi(const char* ssid, const char* pass, String& outIP) {
  preferences.begin("wifi", false);
  preferences.putString("ssid", ssid);
  preferences.putString("pass", pass);
  preferences.end();

  Serial.printf("WiFi STA: Connecting to new SSID %s...\n", ssid);
  WiFi.disconnect();
  delay(100);
  WiFi.begin(ssid, pass);

  uint32_t start = millis();
  while (WiFi.status() != WL_CONNECTED && (millis() - start) < WIFI_STA_TIMEOUT) {
    delay(100);
  }

  if (WiFi.status() == WL_CONNECTED) {
    _staIp = WiFi.localIP().toString();
    _ipAddr = _staIp;
    _staSsid = ssid;
    _staConnected = true;
    outIP = _staIp;
    Serial.printf("WiFi STA: Connected @ %s\n", _staIp.c_str());
    return true;
  } else {
    Serial.println("WiFi STA: Failed to connect");
    WiFi.disconnect();
    _staConnected = false;
    _staIp = "";
    _staSsid = "";
    _ipAddr = _apIp;
    return false;
  }
}

float getWiFiCpuTemp() {
  return _cpuTemp;
}

float getWiFiGpuTemp() {
  return _gpuTemp;
}

void wsNotifyRPM(uint16_t rpm) {
  if (!_wsClientConnected) return;

  char buf[32];
  snprintf(buf, sizeof(buf), "{\"rpm\":%u}", rpm);
  ws.broadcastTXT(buf);
}

void wsNotifyStatus(uint8_t fanPercent, bool fanOn, uint8_t ledMode, bool ledOn,
                    float cpuTemp, float gpuTemp) {
  if (!_wsClientConnected) return;

  // ponytail: snprintf over ArduinoJson for notify — cheaper, fixed schema
  char buf[160];
  snprintf(buf, sizeof(buf),
    "{\"fan_pct\":%u,\"fan_on\":%s,\"led_mode\":%u,\"led_on\":%s,\"rpm\":%u,\"cpu\":%.1f,\"gpu\":%.1f}",
    fanPercent, fanOn ? "true" : "false",
    ledMode, ledOn ? "true" : "false",
    getFanRPM(), cpuTemp, gpuTemp
  );
  ws.broadcastTXT(buf);
}
