namespace PocketDeck.SteamVR;

internal readonly record struct OpenVrMenuVisual(bool Hidden, int Opacity, bool DragEnabled, float Multiplier, bool Keypad, int DigitCount, bool KeypadExpanded = false);
