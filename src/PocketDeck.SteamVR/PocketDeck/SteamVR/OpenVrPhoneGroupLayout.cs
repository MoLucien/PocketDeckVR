using Valve.VR;

namespace PocketDeck.SteamVR;

internal static class OpenVrPhoneGroupLayout
{
	public static HmdMatrix34_t SurfacePose(OpenVrPhoneSurface surface, HmdMatrix34_t root, float phoneWidth, float aspect, bool hidden)
	{
		HmdMatrix34_t hmdMatrix34_t = OpenVrPhoneMenuLayout.DotTransform(hidden, root, phoneWidth, aspect, root);
		return surface switch
		{
			OpenVrPhoneSurface.Dot => hmdMatrix34_t, 
			OpenVrPhoneSurface.Menu => OpenVrPhoneMenuLayout.PanelTransform(hmdMatrix34_t), 
			OpenVrPhoneSurface.Keypad => OpenVrPhoneMenuLayout.KeypadTransform(root, phoneWidth, aspect), 
			_ => root, 
		};
	}

	public static HmdVector3_t Point(OpenVrPhoneSurface surface, HmdMatrix34_t root, float phoneWidth, float aspect, bool hidden, float u, float v)
	{
		HmdMatrix34_t hmdMatrix34_t = SurfacePose(surface, root, phoneWidth, aspect, hidden);
		(float, float) tuple = surface switch
		{
			OpenVrPhoneSurface.Dot => (0.048f, 0.048f), 
			OpenVrPhoneSurface.Menu => (0.32f, 0.5688889f), 
			OpenVrPhoneSurface.Keypad => (0.26f, 0.23111111f), 
			_ => (phoneWidth, phoneWidth / aspect), 
		};
		float item = tuple.Item1;
		float item2 = tuple.Item2;
		float num = (u - 0.5f) * item;
		float num2 = (v - 0.5f) * item2;
		return new HmdVector3_t
		{
			v0 = hmdMatrix34_t.m3 + hmdMatrix34_t.m0 * num + hmdMatrix34_t.m1 * num2,
			v1 = hmdMatrix34_t.m7 + hmdMatrix34_t.m4 * num + hmdMatrix34_t.m5 * num2,
			v2 = hmdMatrix34_t.m11 + hmdMatrix34_t.m8 * num + hmdMatrix34_t.m9 * num2
		};
	}

	public static HmdMatrix34_t AnchorScale(OpenVrPhoneSurface surface, HmdMatrix34_t root, float before, float after, float aspect, bool hidden, float u, float v)
	{
		HmdVector3_t hmdVector3_t = Point(surface, root, before, aspect, hidden, u, v);
		HmdVector3_t hmdVector3_t2 = Point(surface, root, after, aspect, hidden, u, v);
		root.m3 += hmdVector3_t.v0 - hmdVector3_t2.v0;
		root.m7 += hmdVector3_t.v1 - hmdVector3_t2.v1;
		root.m11 += hmdVector3_t.v2 - hmdVector3_t2.v2;
		return root;
	}
}
