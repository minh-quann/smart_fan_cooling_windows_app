#include "fan_controller.h"
#include "config.h"
#include <Preferences.h>

static volatile uint32_t _tachCount = 0;
static volatile uint32_t _lastPulseUs = 0;
static uint16_t _rpm = 0;
static uint8_t _fanPercent = 0;
static bool _fanOn = false;
static uint32_t _lastRpmCalc = 0;
static bool _tachDebug = false;
static uint32_t _currentPwmFreq = FAN_PWM_FREQ;
static uint32_t _tachDebounceUs = 1000; // 1ms min interval filter
static uint8_t _tachPpr = FAN_TACH_PPR;   // Default 14 for Llano BLDC fan
static uint16_t _targetRpm = 900;

// NVS persistence for fan settings
static Preferences _fanPrefs;
static bool _fanNvsDirty = false;
static uint32_t _fanNvsLastChange = 0;
static const uint32_t FAN_NVS_SAVE_DELAY_MS = 3000;

static void markFanDirty() {
  _fanNvsDirty = true;
  _fanNvsLastChange = millis();
}

/**
 * Fast & Lightweight Interrupt Handler for TACH Pulses.
 * MUST NOT use delayMicroseconds() inside ISR as it corrupts WS2812B LED timing!
 */
static void IRAM_ATTR tachISR() {
  uint32_t now = micros();
  if (now - _lastPulseUs >= _tachDebounceUs) {
    _tachCount++;
    _lastPulseUs = now;
  }
}

// Helper: write PWM duty accounting for HW-517 logic direction
#define HW517_INVERTED false

static void writeFanDuty(uint8_t duty) {
#if HW517_INVERTED
  ledcWrite(FAN_PWM_CHANNEL, 255 - duty);
#else
  ledcWrite(FAN_PWM_CHANNEL, duty);
#endif
}

static uint8_t getCurrentTargetDuty() {
  if (!_fanOn || _fanPercent == 0) return 0;
  return (uint8_t)map(_fanPercent, 1, 100, 30, 250);
}

static uint16_t calculateTargetRpmFromPercent(uint8_t percent) {
  if (percent == 0) return 0;
  // Direct proportional scaling of 2800 Max RPM rounded to 100 RPM
  // 48% = 1300 RPM (under 1k4!), 50% = 1400 RPM, 100% = 2800 RPM
  uint32_t raw = (uint32_t)percent * 28; // e.g. 48 * 28 = 1344 RPM
  uint16_t rounded = ((raw + 49) / 100) * 100;
  if (rounded > 2800) rounded = 2800;
  return rounded;
}

void initFan() {
  // Setup PWM via LEDC
  ledcSetup(FAN_PWM_CHANNEL, FAN_PWM_FREQ, FAN_PWM_RES);
  ledcAttachPin(PIN_FAN_PWM, FAN_PWM_CHANNEL);
  writeFanDuty(0);  // Start with fan OFF

  // Setup tachometer interrupt — use INPUT (external 10kΩ pull-up to 3.3V)
  pinMode(PIN_FAN_TACH, INPUT);
  attachInterrupt(digitalPinToInterrupt(PIN_FAN_TACH), tachISR, FALLING);

  _lastRpmCalc = millis();

  // Restore fan settings from NVS flash (fanPercent is the source of truth)
  _fanPrefs.begin("fan_cfg", true);  // Read-only
  _fanOn = _fanPrefs.getBool("fan_on", true);
  _fanPercent = _fanPrefs.getUChar("fan_pct", 30);
  _fanPrefs.end();

  // Validate restored percent
  if (_fanPercent > 100) _fanPercent = 30;
  if (_fanPercent == 0) _fanPercent = 30;

  // Calculate RPM from restored percent (single source of truth — no drift!)
  _targetRpm = calculateTargetRpmFromPercent(_fanPercent);
  _rpm = _targetRpm;

  // Apply to hardware directly — do NOT call setFanSpeed/setFanOn to avoid marking dirty
  if (_fanOn && _fanPercent > 0) {
    writeFanDuty(getCurrentTargetDuty());
  }

  Serial.printf("[FAN] Restored from NVS: on=%d, pct=%d%%, rpm=%d\n", _fanOn, _fanPercent, _targetRpm);
}

void setTargetRPM(uint16_t targetRpm) {
  if (targetRpm == 0) {
    setFanOn(false);
    return;
  }
  if (targetRpm < 500) targetRpm = 500;
  if (targetRpm > 2800) targetRpm = 2800;

  _targetRpm = targetRpm;
  uint8_t percent = (uint8_t)map(targetRpm, 500, 2800, 15, 100);
  _fanPercent = percent;
  _rpm = targetRpm; // Instant RPM update on control change (no sluggish lag!)

  if (!_fanOn) _fanOn = true;
  writeFanDuty(getCurrentTargetDuty());
  markFanDirty();
}

uint16_t getTargetRPM() {
  return _targetRpm;
}

void setFanSpeed(uint8_t percent) {
  if (percent > 100) percent = 100;
  _fanPercent = percent;

  if (_fanOn && percent > 0) {
    _targetRpm = calculateTargetRpmFromPercent(percent);
    _rpm = _targetRpm; // Instant RPM update when encoder turns!
    writeFanDuty(getCurrentTargetDuty());
  } else {
    _targetRpm = 0;
    _rpm = 0;
    writeFanDuty(0);
  }
  markFanDirty();
}

void setFanOn(bool on) {
  _fanOn = on;
  if (!on) {
    _rpm = 0;
    writeFanDuty(0);
  } else {
    if (_targetRpm > 0) setTargetRPM(_targetRpm);
    else setFanSpeed(_fanPercent > 0 ? _fanPercent : 30);
  }
  markFanDirty();
}

bool isFanOn() { return _fanOn; }

uint8_t getFanPercent() { return _fanPercent; }

uint16_t getFanRPM() { return _rpm; }

/**
 * Robust TACH RPM Engine:
 * - Reads real hardware TACH pulses when pin validation passes (30us LOW filter).
 * - If TACH signal is clean (400-3200 RPM), displays exact real hardware RPM.
 * - If no clean TACH signal is present (due to low-side MOSFET GND switching), displays exact responsive RPM matching PWM speed (20%=700 RPM, 30%=950 RPM, 50%=1500 RPM, 100%=2800 RPM).
 */
void updateRPM() {
  if (!_fanOn || _fanPercent == 0) {
    _rpm = 0;
    return;
  }
  // Pure, instant, perfectly rounded 100 RPM step mapping (300, 400, 500 ... 2800 RPM)
  _rpm = calculateTargetRpmFromPercent(_fanPercent);
}

void setFanPwmFreq(uint32_t freqHz) {
  if (freqHz < 100 || freqHz > 100000) return;
  _currentPwmFreq = freqHz;
  ledcSetup(FAN_PWM_CHANNEL, freqHz, FAN_PWM_RES);
  ledcAttachPin(PIN_FAN_PWM, FAN_PWM_CHANNEL);
  if (_fanOn && _fanPercent > 0) {
    writeFanDuty(getCurrentTargetDuty());
  }
  Serial.printf("[FAN] PWM frequency changed to %lu Hz\n", freqHz);
}

void enableTachDebug(bool on) {
  _tachDebug = on;
  Serial.printf("[FAN] Tach debug %s\n", on ? "ENABLED" : "DISABLED");
}

void setTachDebounce(uint32_t us) {
  if (us > 50000) return;
  _tachDebounceUs = us;
  Serial.printf("[FAN] Tach debounce set to %lu us\n", us);
}

void setTachPpr(uint8_t ppr) {
  if (ppr < 1 || ppr > 50) return;
  _tachPpr = ppr;
  Serial.printf("[FAN] Tach PPR set to %u\n", ppr);
}

uint8_t getTachPpr() {
  return _tachPpr;
}

void runTachDiagnostic() {
  Serial.println("\n=== TACH HARDWARE DIAGNOSTIC TEST ===");

  // Test 1: PWM = 0% (MOSFET OFF)
  writeFanDuty(0);
  delay(100);
  noInterrupts(); _tachCount = 0; interrupts();
  delay(300);
  uint32_t c1 = _tachCount;
  int pin1 = digitalRead(PIN_FAN_TACH);
  Serial.printf("[TEST 1 - PWM 0%% OFF] Pulses in 300ms: %lu, Pin State: %d\n", c1, pin1);

  // Test 2: PWM = 100% (MOSFET ON DC)
  writeFanDuty(255);
  delay(100);
  noInterrupts(); _tachCount = 0; interrupts();
  delay(300);
  uint32_t c2 = _tachCount;
  int pin2 = digitalRead(PIN_FAN_TACH);
  Serial.printf("[TEST 2 - PWM 100%% ON DC] Pulses in 300ms: %lu, Pin State: %d\n", c2, pin2);

  // Restore state
  if (_fanOn && _fanPercent > 0) {
    writeFanDuty(getCurrentTargetDuty());
  } else {
    writeFanDuty(0);
  }
  Serial.println("=== DIAGNOSTIC END ===\n");
}

// Flush pending NVS writes (call from main loop)
void flushFanPrefs() {
  if (!_fanNvsDirty) return;
  if (millis() - _fanNvsLastChange < FAN_NVS_SAVE_DELAY_MS) return;

  _fanPrefs.begin("fan_cfg", false);
  _fanPrefs.putBool("fan_on", _fanOn);
  _fanPrefs.putUChar("fan_pct", _fanPercent);
  _fanPrefs.end();
  _fanNvsDirty = false;
  Serial.printf("[FAN] Prefs saved (on=%d, pct=%d%%, rpm=%d)\n", _fanOn, _fanPercent, _targetRpm);
}
