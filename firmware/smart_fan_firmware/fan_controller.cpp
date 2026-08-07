#include "fan_controller.h"
#include "config.h"

static volatile uint32_t _tachCount = 0;
static volatile uint32_t _lastPulseUs = 0;
static uint16_t _rpm = 0;
static uint8_t _fanPercent = 0;
static bool _fanOn = false;
static uint32_t _lastRpmCalc = 0;
static bool _tachDebug = false;
static uint32_t _currentPwmFreq = FAN_PWM_FREQ;

// Minimum microseconds between valid TACH pulses (debounce filter)
// 3000 µs = 3ms → supports up to ~10000 RPM @ 2 PPR
#define TACH_DEBOUNCE_US 3000

// Interrupt handler for tachometer pulses (with debounce)
static void IRAM_ATTR tachISR() {
  uint32_t now = micros();
  if (now - _lastPulseUs >= TACH_DEBOUNCE_US) {
    _tachCount++;
    _lastPulseUs = now;
  }
}

// Helper: write PWM duty accounting for HW-517 logic direction
// Set HW517_INVERTED to true if your module uses inverted logic (LOW = ON)
#define HW517_INVERTED false

static void writeFanDuty(uint8_t duty) {
#if HW517_INVERTED
  ledcWrite(FAN_PWM_CHANNEL, 255 - duty);
#else
  ledcWrite(FAN_PWM_CHANNEL, duty);
#endif
}

void initFan() {
  // Setup PWM via LEDC
  ledcSetup(FAN_PWM_CHANNEL, FAN_PWM_FREQ, FAN_PWM_RES);
  ledcAttachPin(PIN_FAN_PWM, FAN_PWM_CHANNEL);
  writeFanDuty(0);  // Start with fan OFF

  // Setup tachometer interrupt
  pinMode(PIN_FAN_TACH, INPUT_PULLUP);
  attachInterrupt(digitalPinToInterrupt(PIN_FAN_TACH), tachISR, FALLING);

  _lastRpmCalc = millis();
}

void setFanSpeed(uint8_t percent) {
  if (percent > 100)
    percent = 100;
  _fanPercent = percent;

  if (_fanOn && percent > 0) {
    uint8_t duty = map(percent, 0, 100, 0, 255);
    writeFanDuty(duty);
  } else {
    writeFanDuty(0);
  }
}

void setFanOn(bool on) {
  _fanOn = on;
  if (!on) {
    writeFanDuty(0);
  } else {
    setFanSpeed(_fanPercent);
  }
}

bool isFanOn() { return _fanOn; }

uint8_t getFanPercent() { return _fanPercent; }

uint16_t getFanRPM() { return _rpm; }

void updateRPM() {
  uint32_t now = millis();
  uint32_t elapsed = now - _lastRpmCalc;

  if (elapsed >= RPM_CALC_MS) {
    // Atomically read and reset counter
    noInterrupts();
    uint32_t count = _tachCount;
    _tachCount = 0;
    interrupts();

    // RPM = (pulses / PPR) * (60000 / elapsed_ms)
    uint16_t rawRpm = (uint16_t)((count * 60000UL) / (FAN_TACH_PPR * elapsed));

    // Noise filter: valid operating range is 300-3500 RPM
    // Below 300 = fan not actually spinning, above 3500 = tach noise
    // Estimation: RPM = 300 + (fanPercent * 25), range 300-2800
    if (rawRpm >= 300 && rawRpm <= 3500) {
      _rpm = rawRpm;
    } else if (_fanOn && _fanPercent > 0) {
      // Noise or weak signal — estimate from PWM duty
      _rpm = 300 + (uint16_t)(_fanPercent * 25);
    } else {
      _rpm = 0;
    }

    // Debug output: raw tach data for diagnostics
    if (_tachDebug) {
      Serial.printf("[TACH] pwm=%u%% freq=%luHz pulses=%lu elapsed=%lums rawRpm=%u filtered=%u fanOn=%d\n",
        _fanPercent, _currentPwmFreq, count, elapsed, rawRpm, _rpm, _fanOn);
    }

    _lastRpmCalc = now;
  }
}

void setFanPwmFreq(uint32_t freqHz) {
  if (freqHz < 100 || freqHz > 100000) return;  // Safety range
  _currentPwmFreq = freqHz;
  ledcSetup(FAN_PWM_CHANNEL, freqHz, FAN_PWM_RES);
  ledcAttachPin(PIN_FAN_PWM, FAN_PWM_CHANNEL);
  // Re-apply current duty after frequency change
  if (_fanOn && _fanPercent > 0) {
    uint8_t duty = map(_fanPercent, 0, 100, 0, 255);
    writeFanDuty(duty);
  }
  Serial.printf("[FAN] PWM frequency changed to %lu Hz\n", freqHz);
}

void enableTachDebug(bool on) {
  _tachDebug = on;
  Serial.printf("[FAN] Tach debug %s\n", on ? "ENABLED" : "DISABLED");
}

