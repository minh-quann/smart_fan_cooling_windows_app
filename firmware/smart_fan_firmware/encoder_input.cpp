#include "encoder_input.h"
#include "config.h"
#include <ESP32Encoder.h>

static ESP32Encoder _encoder;
static int64_t _lastEncCount = 0;

// Button state tracking
static uint32_t _lastPressPSH = 0;
static uint32_t _lastPressCON = 0;
static uint32_t _lastPressBAK = 0;

void initEncoder() {
  // Encoder uses internal pullups
  ESP32Encoder::useInternalWeakPullResistors = puType::up;
  _encoder.attachHalfQuad(PIN_ENC_A, PIN_ENC_B);
  _encoder.setCount(0);
  _lastEncCount = 0;

  // Buttons with internal pullups (active LOW)
  pinMode(PIN_BTN_PSH, INPUT_PULLUP);
  pinMode(PIN_BTN_CON, INPUT_PULLUP);
  pinMode(PIN_BTN_BAK, INPUT_PULLUP);
}

int8_t getEncoderDelta() {
  int64_t current = _encoder.getCount();
  int64_t delta = current - _lastEncCount;

  // Each detent = 2 counts on half-quad mode
  int8_t steps = (int8_t)(delta / 2);
  if (steps != 0) {
    _lastEncCount += steps * 2;
  }
  return steps;
}

ButtonEvent checkButtons() {
  uint32_t now = millis();

  if (digitalRead(PIN_BTN_PSH) == LOW && (now - _lastPressPSH) > DEBOUNCE_MS) {
    _lastPressPSH = now;
    return BTN_PSH;
  }

  if (digitalRead(PIN_BTN_CON) == LOW && (now - _lastPressCON) > DEBOUNCE_MS) {
    _lastPressCON = now;
    return BTN_CON;
  }

  if (digitalRead(PIN_BTN_BAK) == LOW && (now - _lastPressBAK) > DEBOUNCE_MS) {
    _lastPressBAK = now;
    return BTN_BAK;
  }

  return BTN_NONE;
}

// ---- Encoder 2 (scroll wheel) ----
static ESP32Encoder _encoder2;
static int64_t _lastEnc2Count = 0;

void initEncoder2() {
  ESP32Encoder::useInternalWeakPullResistors = puType::up;
  _encoder2.attachHalfQuad(PIN_ENC2_A, PIN_ENC2_B);
  _encoder2.setCount(0);
  _lastEnc2Count = 0;
}

int8_t getEncoder2Delta() {
  int64_t current = _encoder2.getCount();
  int64_t delta = current - _lastEnc2Count;
  int8_t steps = (int8_t)(delta / 2);
  if (steps != 0) {
    _lastEnc2Count += steps * 2;
  }
  return steps;
}

int64_t getEncoder2Count() {
  return _encoder2.getCount();
}
