#include "led_effects.h"
#include "config.h"
#include <FastLED.h>

static CRGB _leds[NUM_LEDS];
static uint8_t _mode = LED_RAINBOW;
static CRGB _staticColor = CRGB(0, 120, 255);
static uint8_t _brightness = 128;
static bool _ledOn = true;
static uint8_t _fanPercent = 0;  // For speed sync mode
static uint8_t _hue = 0;        // Animation counter

void initLeds() {
  FastLED.addLeds<LED_TYPE, PIN_LED_DATA, LED_COLOR_ORDER>(_leds, NUM_LEDS);
  FastLED.setBrightness(_brightness);
  FastLED.clear(true);
}

void setLedMode(uint8_t mode) {
  if (mode < LED_MODE_COUNT) _mode = mode;
}

uint8_t getLedMode() {
  return _mode;
}

void setLedColor(uint8_t r, uint8_t g, uint8_t b) {
  _staticColor = CRGB(r, g, b);
}

void setLedBrightness(uint8_t brightness) {
  _brightness = brightness;
  FastLED.setBrightness(_brightness);
}

void setLedOn(bool on) {
  _ledOn = on;
  if (!on) {
    FastLED.clear(true);
  }
}

bool isLedOn() {
  return _ledOn;
}

void setLedSpeedPercent(uint8_t fanPercent) {
  _fanPercent = fanPercent;
}

// ---- Effect implementations ----

static void effectStatic() {
  fill_solid(_leds, NUM_LEDS, _staticColor);
}

static void effectRainbow() {
  fill_rainbow(_leds, NUM_LEDS, _hue, 255 / NUM_LEDS);
  _hue++;
}

static void effectBreathing() {
  // Sine wave brightness modulation
  uint8_t breath = beatsin8(12, 30, 255);  // 12 BPM
  fill_solid(_leds, NUM_LEDS, _staticColor);
  FastLED.setBrightness(scale8(breath, _brightness));
}

static void effectSpeedSync() {
  // Color shifts green→yellow→red based on fan speed
  uint8_t hue = map(_fanPercent, 0, 100, 96, 0);  // Green to red
  fill_solid(_leds, NUM_LEDS, CHSV(hue, 255, 255));

  // Pulse speed proportional to fan speed
  if (_fanPercent > 0) {
    uint8_t bpm = map(_fanPercent, 0, 100, 10, 120);
    uint8_t pulse = beatsin8(bpm, 100, 255);
    FastLED.setBrightness(scale8(pulse, _brightness));
  }
}

static void effectWave() {
  for (int i = 0; i < NUM_LEDS; i++) {
    _leds[i] = CHSV(_hue + (i * 255 / NUM_LEDS), 255, 255);
  }
  _hue += 2;
}

static void effectFire() {
  // Fire2012 simplified
  static uint8_t heat[NUM_LEDS];

  // Cool down
  for (int i = 0; i < NUM_LEDS; i++) {
    heat[i] = qsub8(heat[i], random8(0, 55));
  }

  // Heat drift up
  for (int k = NUM_LEDS - 1; k >= 2; k--) {
    heat[k] = (heat[k - 1] + heat[k - 2] + heat[k - 2]) / 3;
  }

  // Random ignition near bottom
  if (random8() < 120) {
    int y = random8(3);
    heat[y] = qadd8(heat[y], random8(160, 255));
  }

  // Map heat to colors
  for (int j = 0; j < NUM_LEDS; j++) {
    _leds[j] = HeatColor(heat[j]);
  }
}

void updateLeds() {
  if (!_ledOn || _mode == LED_OFF) {
    FastLED.clear(true);
    return;
  }

  // Reset brightness before effects that don't modulate it
  if (_mode != LED_BREATHING && _mode != LED_SPEED_SYNC) {
    FastLED.setBrightness(_brightness);
  }

  switch (_mode) {
    case LED_STATIC:     effectStatic();    break;
    case LED_RAINBOW:    effectRainbow();   break;
    case LED_BREATHING:  effectBreathing(); break;
    case LED_SPEED_SYNC: effectSpeedSync(); break;
    case LED_WAVE:       effectWave();      break;
    case LED_FIRE:       effectFire();      break;
    default:             break;
  }

  FastLED.show();
}
