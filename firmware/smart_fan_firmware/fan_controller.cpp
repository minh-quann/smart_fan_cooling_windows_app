#include "fan_controller.h"
#include "config.h"

static volatile uint32_t _tachCount = 0;
static volatile uint32_t _lastPulseUs = 0;
static uint16_t _rpm = 0;
static uint8_t _fanPercent = 0;
static bool _fanOn = false;
static uint32_t _lastRpmCalc = 0;

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
    _rpm = (uint16_t)((count * 60000UL) / (FAN_TACH_PPR * elapsed));
    _lastRpmCalc = now;
  }
}
