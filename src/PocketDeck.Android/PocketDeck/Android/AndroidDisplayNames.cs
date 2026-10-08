namespace PocketDeck.Android;

public static class AndroidDisplayNames
{
	public static string Transport(AndroidTransport transport)
	{
		return transport switch
		{
			AndroidTransport.Usb => "USB", 
			AndroidTransport.Network => "WiFi 无线", 
			AndroidTransport.Emulator => "模拟器", 
			_ => "未知连接", 
		};
	}

	public static string DeviceStatus(AndroidDeviceStatus status)
	{
		return status switch
		{
			AndroidDeviceStatus.Ready => "已授权", 
			AndroidDeviceStatus.Unauthorized => "等待手机授权", 
			AndroidDeviceStatus.Offline => "离线", 
			AndroidDeviceStatus.NoPermissions => "驱动无权限", 
			_ => "未知状态", 
		};
	}

	public static string Codec(AndroidVideoCodec codec)
	{
		return codec switch
		{
			AndroidVideoCodec.H264 => "H.264", 
			AndroidVideoCodec.H265 => "H.265", 
			AndroidVideoCodec.Av1 => "AV1", 
			AndroidVideoCodec.Vp8 => "VP8", 
			AndroidVideoCodec.Vp9 => "VP9", 
			_ => codec.ToString(), 
		};
	}
}
