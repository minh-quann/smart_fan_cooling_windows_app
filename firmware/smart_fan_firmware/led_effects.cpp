#include "led_effects.h"
#include "config.h"
#include <Adafruit_NeoPixel.h>
#include <Preferences.h>

// Fixed strip size — NEVER call updateLength() on ESP32-S3!
static Adafruit_NeoPixel _strip(NUM_LEDS, PIN_LED_DATA, NEO_GRB + NEO_KHZ800);
static uint16_t _numLeds = NUM_LEDS;
static uint8_t _mode = LED_RAINBOW;
static uint8_t _staticR = 0, _staticG = 120, _staticB = 255;
static uint8_t _brightness = 180;
static uint8_t _speed = 50;
static bool _reverse = false;
static uint8_t _rainbowColorCount = 0; // 0 = continuous 360°, 2, 3, 5, 7 colors
static bool _ledOn = true;
static bool _userLedOn = true;
static uint8_t _fanPercent = 0;
static uint32_t _hueStep = 0;
static Preferences _prefs;

// Deferred NVS save — only write after 3s of stability to prevent WDT crash
static bool _nvsDirty = false;
static uint32_t _nvsLastChange = 0;
static const uint32_t NVS_SAVE_DELAY_MS = 3000;

// Crash protection — track consecutive rapid crashes
static const uint32_t CRASH_WINDOW_MS = 8000;
static const uint8_t MAX_CRASH_COUNT = 3;

// Flush pending NVS writes (call from main loop)
void flushLedPrefs() {
  if (!_nvsDirty) return;
  if (millis() - _nvsLastChange < NVS_SAVE_DELAY_MS) return;
  
  _prefs.begin("led_cfg", false);
  _prefs.putUChar("led_mode", _mode);
  _prefs.putUChar("led_r", _staticR);
  _prefs.putUChar("led_g", _staticG);
  _prefs.putUChar("led_b", _staticB);
  _prefs.putUChar("led_spd", _speed);
  _prefs.putUChar("led_bri", _brightness);
  _prefs.putBool("led_dir", _reverse);
  _prefs.putUChar("rb_cnt", _rainbowColorCount);
  _prefs.putBool("led_on", _ledOn);
  _prefs.end();
  _nvsDirty = false;
  Serial.printf("[LED] Prefs saved (mode=%d)\n", _mode);
}

static void markDirty() {
  _nvsDirty = true;
  _nvsLastChange = millis();
}

// Helper: scale a color by brightness (0-255) without touching _strip.setBrightness()
static uint32_t scaleColor(uint8_t r, uint8_t g, uint8_t b, uint8_t bri) {
  return _strip.Color((r * bri) >> 8, (g * bri) >> 8, (b * bri) >> 8);
}

static uint32_t scaleColor32(uint32_t c, uint8_t bri) {
  uint8_t r = (uint8_t)(c >> 16);
  uint8_t g = (uint8_t)(c >> 8);
  uint8_t b = (uint8_t)(c);
  return _strip.Color((r * bri) >> 8, (g * bri) >> 8, (b * bri) >> 8);
}

void initLeds() {
  _prefs.begin("led_cfg", false);
  
  // Crash protection: increment boot counter, reset if stable
  uint8_t crashCount = _prefs.getUChar("crash_cnt", 0);
  uint32_t lastBoot = _prefs.getULong("last_boot", 0);
  
  // If last boot was recent (within CRASH_WINDOW_MS), increment crash counter
  crashCount++;
  _prefs.putUChar("crash_cnt", crashCount);
  _prefs.putULong("last_boot", millis());
  
  if (crashCount >= MAX_CRASH_COUNT) {
    // Too many rapid crashes — force safe defaults
    Serial.printf("[LED] CRASH PROTECTION: %d crashes detected! Resetting to STATIC mode\n", crashCount);
    _mode = LED_STATIC;
    _prefs.putUChar("led_mode", LED_STATIC);
    _prefs.putUChar("crash_cnt", 0);  // Reset counter
  } else {
    _mode = _prefs.getUChar("led_mode", LED_RAINBOW);
    if (_mode >= LED_MODE_COUNT) _mode = LED_RAINBOW;
  }
  
  _staticR = _prefs.getUChar("led_r", 0);
  _staticG = _prefs.getUChar("led_g", 120);
  _staticB = _prefs.getUChar("led_b", 255);
  _speed = _prefs.getUChar("led_spd", 50);
  if (_speed == 0 || _speed > 100) _speed = 50;
  _brightness = _prefs.getUChar("led_bri", 180);
  if (_brightness == 0) _brightness = 180;
  _reverse = _prefs.getBool("led_dir", false);
  _rainbowColorCount = _prefs.getUChar("rb_cnt", 0);
  _userLedOn = _prefs.getBool("led_on", true);
  _ledOn = _userLedOn;
  _prefs.end();

  _strip.begin();
  _strip.setBrightness(255);  // Set ONCE to max, we scale manually
  if (_ledOn && _mode != LED_OFF) {
    updateLeds();  // Render last saved LED effect immediately on boot!
  } else {
    _strip.clear();
    _strip.show();
  }

  Serial.printf("[LED] Initialized %u LEDs on GPIO %d (Mode: %d, On: %d, CrashCnt: %d)\n", 
                NUM_LEDS, PIN_LED_DATA, _mode, _ledOn, crashCount);
}

// Call after system is stable (e.g. 5s after boot) to clear crash counter
void clearLedCrashCounter() {
  _prefs.begin("led_cfg", false);
  _prefs.putUChar("crash_cnt", 0);
  _prefs.end();
}

void setLedCount(uint16_t count) {
  if (count == 0 || count > NUM_LEDS) return;
  _numLeds = count;
  Serial.printf("[LED] Active count set to %u\n", _numLeds);
}

uint16_t getLedCount() { return _numLeds; }

void setLedMode(uint8_t mode) {
  if (mode < LED_MODE_COUNT) {
    _mode = mode;
    markDirty();  // Deferred save — written after 3s stability
  }
}

uint8_t getLedMode() { return _mode; }

void setLedColor(uint8_t r, uint8_t g, uint8_t b) {
  _staticR = r; _staticG = g; _staticB = b;
  markDirty();
}

void setLedBrightness(uint8_t brightness) {
  _brightness = brightness;
  markDirty();
}

uint8_t getLedBrightness() { return _brightness; }

void setLedSpeed(uint8_t speed) {
  if (speed < 1) speed = 1;
  if (speed > 100) speed = 100;
  _speed = speed;
  markDirty();
}

uint8_t getLedSpeed() { return _speed; }

void setLedDirection(bool reverse) {
  _reverse = reverse;
  markDirty();
}

bool getLedDirection() { return _reverse; }

void setRainbowColorCount(uint8_t count) {
  _rainbowColorCount = count;
  markDirty();
}

uint8_t getRainbowColorCount() { return _rainbowColorCount; }

void setLedOn(bool on) {
  _userLedOn = on;
  _ledOn = on;
  markDirty();
  if (!on) {
    _strip.clear();
    _strip.show();
  }
}

void setLedOnTemporary(bool on) {
  // Toggle runtime state (e.g., PC shutdown) without modifying _userLedOn or NVS
  _ledOn = _userLedOn ? on : false;
  if (!_ledOn) {
    _strip.clear();
    _strip.show();
  }
}

bool isLedOn() { return _ledOn; }

void setLedSpeedPercent(uint8_t fanPercent) {
  _fanPercent = fanPercent;
}

// ---- Effect implementations ----

static void effectStatic() {
  uint32_t c = scaleColor(_staticR, _staticG, _staticB, _brightness);
  _strip.fill(c, 0, _numLeds);
}

static void effectRainbow() {
  // Smooth speed scaling (20 to 400 hue points per frame)
  uint32_t step = map(_speed, 1, 100, 20, 400);
  _hueStep += _reverse ? -step : step;

  for (uint16_t i = 0; i < _numLeds; i++) {
    uint16_t idx = _reverse ? (_numLeds - 1 - i) : i;
    uint32_t rawHue = _hueStep + (idx * 65536L / _numLeds);
    uint32_t hue = rawHue;
    if (_rainbowColorCount > 0) {
      uint32_t bandSize = 65536L / _rainbowColorCount;
      hue = (rawHue / bandSize) * bandSize;
    }
    uint32_t c = _strip.gamma32(_strip.ColorHSV(hue));
    _strip.setPixelColor(i, scaleColor32(c, _brightness));
  }
}

static void effectBreathing() {
  // Smooth breathing wave (~3-5 second cycle)
  float speedF = map(_speed, 1, 100, 10, 80) / 1000.0f;
  float val = (sin(millis() * speedF) + 1.0f) / 2.0f;
  uint8_t bri = (uint8_t)(val * _brightness);
  uint32_t c = scaleColor(_staticR, _staticG, _staticB, bri);
  _strip.fill(c, 0, _numLeds);
}

static void effectSpeedSync() {
  // Cool Blue (0%) -> Cyan (30%) -> Green (50%) -> Yellow (75%) -> Red (100%)
  uint16_t hue = (uint16_t)((100 - _fanPercent) * 43690L / 100);
  uint32_t c = _strip.ColorHSV(hue, 255, 255);
  _strip.fill(scaleColor32(c, _brightness), 0, _numLeds);
}

static void effectWave() {
  uint32_t step = map(_speed, 1, 100, 30, 500);
  _hueStep += _reverse ? -step : step;

  for (uint16_t i = 0; i < _numLeds; i++) {
    uint16_t idx = _reverse ? (_numLeds - 1 - i) : i;
    uint32_t rawHue = _hueStep + (idx * 65536L / _numLeds);
    uint32_t hue = rawHue;
    if (_rainbowColorCount > 0) {
      uint32_t bandSize = 65536L / _rainbowColorCount;
      hue = (rawHue / bandSize) * bandSize;
    }
    uint32_t c = _strip.ColorHSV(hue, 255, 255);
    _strip.setPixelColor(i, scaleColor32(c, _brightness));
  }
}

static void effectFire() {
  for (uint16_t i = 0; i < _numLeds; i++) {
    uint8_t r = random(180, 255);
    uint8_t g = random(30, 90);
    uint8_t b = random(0, 15);
    _strip.setPixelColor(i, scaleColor(r, g, b, _brightness));
  }
}

static void effectComet() {
  static uint16_t cometPos = 0;
  static uint32_t lastMove = 0;
  uint16_t speedDelay = map(_speed, 1, 100, 60, 10);

  if (millis() - lastMove > speedDelay) {
    cometPos = (cometPos + 1) % _numLeds;
    lastMove = millis();
  }

  _strip.clear();
  uint16_t headPos = _reverse ? (_numLeds - 1 - cometPos) : cometPos;

  int tailLength = _numLeds / 4;
  if (tailLength < 6) tailLength = 6;

  for (int t = 0; t < tailLength; t++) {
    int tailIdx = _reverse ? (headPos + t) % _numLeds : (headPos - t + _numLeds) % _numLeds;
    uint8_t fade = 255 - (t * (255 / tailLength));
    uint32_t c = _strip.ColorHSV((millis() * 10) + (tailIdx * 300), 255, fade);
    _strip.setPixelColor(tailIdx, scaleColor32(c, _brightness));
  }
}


static void effectPulse() {
  float speedF = map(_speed, 1, 100, 20, 100) / 1000.0f;
  float val = (sin(millis() * speedF) + 1.0f) / 2.0f;
  uint8_t bri = (uint8_t)(val * _brightness);
  uint32_t c = scaleColor(_staticR, _staticG, _staticB, bri);
  _strip.fill(c, 0, _numLeds);
}

static void effectDualSpin() {
  static uint16_t spinPos = 0;
  static uint32_t lastMove = 0;
  uint16_t speedDelay = map(_speed, 1, 100, 50, 8);

  if (millis() - lastMove > speedDelay) {
    spinPos = (spinPos + 1) % _numLeds;
    lastMove = millis();
  }

  _strip.clear();
  uint16_t h1 = _reverse ? (_numLeds - 1 - spinPos) : spinPos;
  uint16_t h2 = (h1 + (_numLeds / 2)) % _numLeds;

  int tailLen = _numLeds / 6;
  if (tailLen < 4) tailLen = 4;

  // Draw 2 perfectly symmetrical comets 180 degrees apart
  for (int t = 0; t < tailLen; t++) {
    uint8_t fade = 255 - (t * (255 / tailLen));

    // Tail for head 1
    int tail1 = _reverse ? (h1 + t) % _numLeds : (h1 - t + _numLeds) % _numLeds;
    uint32_t c1 = _strip.ColorHSV((millis() * 8) + (tail1 * 400), 255, fade);
    _strip.setPixelColor(tail1, scaleColor32(c1, _brightness));

    // Tail for head 2
    int tail2 = _reverse ? (h2 + t) % _numLeds : (h2 - t + _numLeds) % _numLeds;
    uint32_t c2 = _strip.ColorHSV((millis() * 8) + (tail2 * 400), 255, fade);
    _strip.setPixelColor(tail2, scaleColor32(c2, _brightness));
  }
}

// ---- Meteor Rain: falling particles with decaying trails ----
static void effectMeteor() {
  static uint16_t meteorPos = 0;
  static uint32_t lastMove = 0;
  uint16_t speedDelay = map(_speed, 1, 100, 80, 8);

  if (millis() - lastMove > speedDelay) {
    meteorPos = (meteorPos + 1) % (_numLeds * 2);
    lastMove = millis();
  }

  // Decay existing pixels randomly for organic trail
  for (uint16_t i = 0; i < _numLeds; i++) {
    if (random(0, 10) > 4) {
      uint32_t c = _strip.getPixelColor(i);
      uint8_t r = (uint8_t)(c >> 16);
      uint8_t g = (uint8_t)(c >> 8);
      uint8_t b = (uint8_t)(c);
      r = (r > 20) ? r - 20 : 0;
      g = (g > 20) ? g - 20 : 0;
      b = (b > 20) ? b - 20 : 0;
      _strip.setPixelColor(i, r, g, b);
    }
  }

  // Draw meteor head (size = 4 bright pixels)
  int meteorSize = 4;
  uint16_t pos = _reverse ? (_numLeds - 1 - (meteorPos % _numLeds)) : (meteorPos % _numLeds);
  for (int j = 0; j < meteorSize; j++) {
    int idx = pos - j;
    if (_reverse) idx = pos + j;
    if (idx >= 0 && idx < _numLeds) {
      uint32_t hue = (millis() * 6) + (idx * 500);
      uint32_t c = _strip.gamma32(_strip.ColorHSV(hue));
      _strip.setPixelColor(idx, scaleColor32(c, _brightness));
    }
  }
}

// ---- Twinkle Star: random LEDs sparkle like stars ----
static void effectTwinkle() {
  static uint32_t lastTwinkle = 0;
  uint16_t interval = map(_speed, 1, 100, 120, 15);

  // Fade all pixels gradually
  for (uint16_t i = 0; i < _numLeds; i++) {
    uint32_t c = _strip.getPixelColor(i);
    uint8_t r = (uint8_t)(c >> 16);
    uint8_t g = (uint8_t)(c >> 8);
    uint8_t b = (uint8_t)(c);
    r = (r > 15) ? r - 15 : 0;
    g = (g > 15) ? g - 15 : 0;
    b = (b > 15) ? b - 15 : 0;
    _strip.setPixelColor(i, r, g, b);
  }

  // Spark new random pixels
  if (millis() - lastTwinkle > interval) {
    uint16_t numSparks = max(1, (int)(_numLeds / 8));
    for (uint16_t s = 0; s < numSparks; s++) {
      uint16_t idx = random(0, _numLeds);
      uint32_t hue = random(0, 65536);
      uint32_t c = _strip.gamma32(_strip.ColorHSV(hue, 200, 255));
      _strip.setPixelColor(idx, scaleColor32(c, _brightness));
    }
    lastTwinkle = millis();
  }
}

// ---- Color Wipe: fill strip pixel-by-pixel then clear ----
static void effectColorWipe() {
  static uint16_t wipePos = 0;
  static bool wipeFilling = true;
  static uint32_t wipeHue = 0;
  static uint32_t lastMove = 0;
  uint16_t speedDelay = map(_speed, 1, 100, 80, 5);

  if (millis() - lastMove > speedDelay) {
    uint16_t idx = _reverse ? (_numLeds - 1 - wipePos) : wipePos;
    if (wipeFilling) {
      uint32_t c = _strip.gamma32(_strip.ColorHSV(wipeHue));
      _strip.setPixelColor(idx, scaleColor32(c, _brightness));
    } else {
      _strip.setPixelColor(idx, 0);
    }

    wipePos++;
    if (wipePos >= _numLeds) {
      wipePos = 0;
      wipeFilling = !wipeFilling;
      if (wipeFilling) {
        wipeHue += 8000;  // Shift color each cycle
      }
    }
    lastMove = millis();
  }
}

// ---- Theater Chase: classic Broadway marquee chasing lights ----
static void effectTheater() {
  static uint8_t theaterStep = 0;
  static uint32_t lastMove = 0;
  static uint32_t theaterHue = 0;
  uint16_t speedDelay = map(_speed, 1, 100, 150, 25);

  if (millis() - lastMove > speedDelay) {
    _strip.clear();
    for (uint16_t i = 0; i < _numLeds; i++) {
      uint16_t idx = _reverse ? (_numLeds - 1 - i) : i;
      if ((i + theaterStep) % 3 == 0) {
        uint32_t hue = theaterHue + (idx * 65536L / _numLeds);
        uint32_t c = _strip.gamma32(_strip.ColorHSV(hue));
        _strip.setPixelColor(idx, scaleColor32(c, _brightness));
      }
    }
    theaterStep = (theaterStep + 1) % 3;
    theaterHue += 400;
    lastMove = millis();
  }
}

// ---- Larson Scanner: KITT-style bouncing beam ----
static void effectScanner() {
  static int scanPos = 0;
  static int scanDir = 1;
  static uint32_t lastMove = 0;
  uint16_t speedDelay = map(_speed, 1, 100, 80, 8);

  if (millis() - lastMove > speedDelay) {
    // Decay existing pixels for smooth trail
    for (uint16_t i = 0; i < _numLeds; i++) {
      uint32_t c = _strip.getPixelColor(i);
      uint8_t r = (uint8_t)(c >> 16);
      uint8_t g = (uint8_t)(c >> 8);
      uint8_t b = (uint8_t)(c);
      r = (r > 30) ? r - 30 : 0;
      g = (g > 30) ? g - 30 : 0;
      b = (b > 30) ? b - 30 : 0;
      _strip.setPixelColor(i, r, g, b);
    }

    // Draw scanner beam (3 pixels wide)
    int beamWidth = 3;
    for (int j = -beamWidth / 2; j <= beamWidth / 2; j++) {
      int idx = scanPos + j;
      if (idx >= 0 && idx < _numLeds) {
        uint8_t fade = 255 - abs(j) * 80;
        uint32_t c = scaleColor(_staticR, _staticG, _staticB, fade);
        _strip.setPixelColor(idx, scaleColor32(c, _brightness));
      }
    }

    // Bounce direction
    scanPos += _reverse ? -scanDir : scanDir;
    if (scanPos >= (int)_numLeds - 1 || scanPos <= 0) {
      scanDir = -scanDir;
    }
    lastMove = millis();
  }
}

// ---- Gradient Flow: smooth multi-color gradient scrolling ----
static void effectGradient() {
  uint32_t step = map(_speed, 1, 100, 15, 300);
  _hueStep += _reverse ? -step : step;

  for (uint16_t i = 0; i < _numLeds; i++) {
    // Create a smooth 3-color gradient using sine waves for organic blending
    float pos = (float)i / _numLeds;
    float phase = (_hueStep / 65536.0f) + pos;

    // Generate smooth RGB using offset sine waves
    uint8_t r = (uint8_t)(127.0f + 127.0f * sin(phase * 6.2832f));
    uint8_t g = (uint8_t)(127.0f + 127.0f * sin(phase * 6.2832f + 2.094f));
    uint8_t b = (uint8_t)(127.0f + 127.0f * sin(phase * 6.2832f + 4.189f));

    _strip.setPixelColor(i, scaleColor(r, g, b, _brightness));
  }
}

void updateLeds() {
  if (!_ledOn || _mode == LED_OFF) {
    _strip.clear();
    _strip.show();
    return;
  }

  // DO NOT call setBrightness() here — scale colors manually instead!

  switch (_mode) {
    case LED_STATIC:     effectStatic();    break;
    case LED_RAINBOW:    effectRainbow();   break;
    case LED_BREATHING:  effectBreathing(); break;
    case LED_SPEED_SYNC: effectSpeedSync(); break;
    case LED_WAVE:       effectWave();      break;
    case LED_FIRE:       effectFire();      break;
    case LED_COMET:      effectComet();     break;
    case LED_PULSE:      effectPulse();     break;
    case LED_DUAL_SPIN:  effectDualSpin();  break;
    case LED_METEOR:     effectMeteor();    break;
    case LED_TWINKLE:    effectTwinkle();   break;
    case LED_COLOR_WIPE: effectColorWipe(); break;
    case LED_THEATER:    effectTheater();   break;
    case LED_SCANNER:    effectScanner();   break;
    case LED_GRADIENT:   effectGradient();  break;
    default:             break;
  }

  _strip.show();
}

