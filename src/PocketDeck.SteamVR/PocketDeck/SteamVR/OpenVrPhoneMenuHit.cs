using Valve.VR;

namespace PocketDeck.SteamVR;

internal readonly record struct OpenVrPhoneMenuHit(OpenVrMenuTarget Target, float SliderFraction, VROverlayIntersectionResults_t Intersection, OpenVrPhoneSurface Surface = OpenVrPhoneSurface.Phone);
