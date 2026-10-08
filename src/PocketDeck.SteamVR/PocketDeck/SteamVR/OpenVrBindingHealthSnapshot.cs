namespace PocketDeck.SteamVR;

public sealed record OpenVrBindingHealthSnapshot(OpenVrBindingHealthState State, bool ShowNotice, string ReasonCode, string Message)
{
	public static OpenVrBindingHealthSnapshot Stopped { get; } = new OpenVrBindingHealthSnapshot(OpenVrBindingHealthState.Stopped, ShowNotice: false, "OPENVR_BINDING_STOPPED", "SteamVR 手柄绑定未运行");
}
