#include "ble_service.h"
#include "config.h"
#include "fan_controller.h"
#include "led_effects.h"
#include "wifi_service.h"
#include <ArduinoJson.h>
#include <BLEDevice.h>
#include <BLEServer.h>
#include <BLEUtils.h>
#include <BLE2902.h>

static BLEServer* pServer = nullptr;
static BLECharacteristic* pCharRPM = nullptr;
static BLECharacteristic* pCharStatus = nullptr;
static bool _connected = false;
static float _cpuTemp = 0;
static float _gpuTemp = 0;

// ---- Connection callbacks ----

class ServerCallbacks : public BLEServerCallbacks {
  void onConnect(BLEServer* server) override {
    _connected = true;
    Serial.println("BLE: Client connected");
  }

  void onDisconnect(BLEServer* server) override {
    _connected = false;
    Serial.println("BLE: Client disconnected");
    // Restart advertising
    server->startAdvertising();
  }
};

// ---- Write callbacks ----

class FanSpeedCallback : public BLECharacteristicCallbacks {
  void onWrite(BLECharacteristic* pChar) override {
    uint8_t* data = pChar->getData();
    if (pChar->getLength() >= 1) {
      setFanSpeed(data[0]);
      Serial.printf("BLE: Fan speed set to %d%%\n", data[0]);
    }
  }
};

class FanStateCallback : public BLECharacteristicCallbacks {
  void onWrite(BLECharacteristic* pChar) override {
    uint8_t* data = pChar->getData();
    if (pChar->getLength() >= 1) {
      setFanOn(data[0] != 0);
      Serial.printf("BLE: Fan state set to %s\n", data[0] ? "ON" : "OFF");
    }
  }
};

class LedModeCallback : public BLECharacteristicCallbacks {
  void onWrite(BLECharacteristic* pChar) override {
    uint8_t* data = pChar->getData();
    if (pChar->getLength() >= 1) {
      setLedMode(data[0]);
      Serial.printf("BLE: LED mode set to %d\n", data[0]);
    }
  }
};

class LedColorCallback : public BLECharacteristicCallbacks {
  void onWrite(BLECharacteristic* pChar) override {
    uint8_t* data = pChar->getData();
    if (pChar->getLength() >= 3) {
      setLedColor(data[0], data[1], data[2]);
      Serial.printf("BLE: LED color set to R%d G%d B%d\n", data[0], data[1], data[2]);
    }
  }
};

class LedBrightnessCallback : public BLECharacteristicCallbacks {
  void onWrite(BLECharacteristic* pChar) override {
    uint8_t* data = pChar->getData();
    if (pChar->getLength() >= 1) {
      setLedBrightness(data[0]);
      Serial.printf("BLE: LED brightness set to %d\n", data[0]);
    }
  }
};

class TempCallback : public BLECharacteristicCallbacks {
  void onWrite(BLECharacteristic* pChar) override {
    uint8_t* data = pChar->getData();
    // Format: 4 bytes CPU temp (float) + 4 bytes GPU temp (float)
    if (pChar->getLength() >= 8) {
      memcpy(&_cpuTemp, data, 4);
      memcpy(&_gpuTemp, data + 4, 4);
      Serial.printf("BLE: Temps CPU=%.1f GPU=%.1f\n", _cpuTemp, _gpuTemp);
    }
  }
};

class WiFiConfigCallback : public BLECharacteristicCallbacks {
  void onWrite(BLECharacteristic* pChar) override {
    std::string value = pChar->getValue();
    if (value.length() > 0) {
      JsonDocument doc;
      DeserializationError err = deserializeJson(doc, value);
      if (!err) {
        const char* ssid = doc["ssid"];
        const char* pass = doc["pass"];
        if (ssid && pass) {
          String newIP;
          configureSTAWiFi(ssid, pass, newIP);
        }
      } else {
        Serial.printf("BLE: WiFi config JSON parse error: %s\n", err.c_str());
      }
    }
  }
};

void initBLE() {
  BLEDevice::init(BLE_DEVICE_NAME);

  pServer = BLEDevice::createServer();
  pServer->setCallbacks(new ServerCallbacks());

  BLEService* pService = pServer->createService(BLEUUID(SERVICE_UUID), 40);

  // Fan speed (write)
  BLECharacteristic* pFanSpeed = pService->createCharacteristic(
    CHAR_FAN_SPEED_UUID, BLECharacteristic::PROPERTY_WRITE
  );
  pFanSpeed->setCallbacks(new FanSpeedCallback());

  // Fan state (write)
  BLECharacteristic* pFanState = pService->createCharacteristic(
    CHAR_FAN_STATE_UUID, BLECharacteristic::PROPERTY_WRITE
  );
  pFanState->setCallbacks(new FanStateCallback());

  // LED mode (write)
  BLECharacteristic* pLedMode = pService->createCharacteristic(
    CHAR_LED_MODE_UUID, BLECharacteristic::PROPERTY_WRITE
  );
  pLedMode->setCallbacks(new LedModeCallback());

  // LED color (write)
  BLECharacteristic* pLedColor = pService->createCharacteristic(
    CHAR_LED_COLOR_UUID, BLECharacteristic::PROPERTY_WRITE
  );
  pLedColor->setCallbacks(new LedColorCallback());

  // LED brightness (write)
  BLECharacteristic* pLedBright = pService->createCharacteristic(
    CHAR_LED_BRIGHT_UUID, BLECharacteristic::PROPERTY_WRITE
  );
  pLedBright->setCallbacks(new LedBrightnessCallback());

  // RPM (notify)
  pCharRPM = pService->createCharacteristic(
    CHAR_RPM_UUID,
    BLECharacteristic::PROPERTY_READ | BLECharacteristic::PROPERTY_NOTIFY
  );
  pCharRPM->addDescriptor(new BLE2902());

  // Status (notify)
  pCharStatus = pService->createCharacteristic(
    CHAR_STATUS_UUID,
    BLECharacteristic::PROPERTY_READ | BLECharacteristic::PROPERTY_NOTIFY
  );
  pCharStatus->addDescriptor(new BLE2902());

  // Temperature (write from app)
  BLECharacteristic* pTemp = pService->createCharacteristic(
    CHAR_TEMP_UUID, BLECharacteristic::PROPERTY_WRITE
  );
  pTemp->setCallbacks(new TempCallback());

  // WiFi Config (write JSON from app)
  BLECharacteristic* pWiFiConfig = pService->createCharacteristic(
    CHAR_WIFI_CONFIG_UUID, BLECharacteristic::PROPERTY_WRITE
  );
  pWiFiConfig->setCallbacks(new WiFiConfigCallback());

  pService->start();

  // Start advertising
  BLEAdvertising* pAdvertising = BLEDevice::getAdvertising();
  pAdvertising->addServiceUUID(SERVICE_UUID);
  pAdvertising->setScanResponse(true);
  pAdvertising->setMinPreferred(0x06);
  BLEDevice::startAdvertising();

  Serial.println("BLE: Advertising started");
}

bool isBLEConnected() {
  return _connected;
}

float getBLECpuTemp() {
  return _cpuTemp;
}

float getBLEGpuTemp() {
  return _gpuTemp;
}

void notifyRPM(uint16_t rpm) {
  if (!_connected || !pCharRPM) return;
  pCharRPM->setValue(rpm);
  pCharRPM->notify();
}

void notifyStatus(uint8_t fanPercent, bool fanOn, uint8_t ledMode, bool ledOn) {
  if (!_connected || !pCharStatus) return;
  // Pack status: [fanPercent, fanOn, ledMode, ledOn]
  uint8_t status[4] = { fanPercent, (uint8_t)(fanOn ? 1 : 0), ledMode, (uint8_t)(ledOn ? 1 : 0) };
  pCharStatus->setValue(status, 4);
  pCharStatus->notify();
}
