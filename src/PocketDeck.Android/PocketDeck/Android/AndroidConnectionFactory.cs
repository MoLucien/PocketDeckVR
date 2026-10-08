using System;
using System.IO;

namespace PocketDeck.Android;

public static class AndroidConnectionFactory
{
	public static IAndroidConnectionLogSink CreateDefaultLog()
	{
		try
		{
			return new JsonLineAndroidConnectionLog();
		}
		catch (UnauthorizedAccessException)
		{
			return NullAndroidConnectionLog.Instance;
		}
		catch (IOException)
		{
			return NullAndroidConnectionLog.Instance;
		}
	}

	public static IAndroidConnectionService CreateDefault(IAndroidConnectionLogSink logSink)
	{
		ArgumentNullException.ThrowIfNull(logSink, "logSink");
		string resourceDirectory = Path.Combine(AppContext.BaseDirectory, "resources", "android-platform-tools");
		return new AndroidConnectionService(resourceDirectory, logSink, new AndroidConnectionServiceOptions());
	}

	public static AndroidSessionFactories CreateDefaultSessionFactories(IAndroidConnectionService connection, IAndroidConnectionLogSink logSink)
	{
		AndroidConnectionService connection2 = RequireDefaultConnection(connection);
		ArgumentNullException.ThrowIfNull(logSink, "logSink");
		string path = Path.Combine(AppContext.BaseDirectory, "resources");
		string executablePath = Path.Combine(path, "android-platform-tools", "adb.exe");
		string serverPath = Path.Combine(path, "scrcpy", "scrcpy-server-v4.1");
		AdbCommandRunner commandRunner = new AdbCommandRunner(executablePath);
		ScrcpySessionLauncher launcher = new ScrcpySessionLauncher(commandRunner, serverPath, logSink);
		return new AndroidSessionFactories(new ScrcpyVideoSessionFactory(connection2, launcher, logSink), new ScrcpyAudioSessionFactory(connection2, launcher, logSink), new ScrcpyControlSessionFactory(connection2, launcher, logSink));
	}

	public static IAndroidVideoSessionFactory CreateDefaultVideoSessions(IAndroidConnectionService connection, IAndroidConnectionLogSink logSink)
	{
		return CreateDefaultSessionFactories(connection, logSink).Video;
	}

	public static IAndroidVideoProbeService CreateDefaultVideoProbe(IAndroidConnectionService connection, IAndroidConnectionLogSink logSink)
	{
		return new AndroidVideoProbeService(CreateDefaultVideoSessions(connection, logSink));
	}

	public static IAndroidControlSessionFactory CreateDefaultControlSessions(IAndroidConnectionService connection, IAndroidConnectionLogSink logSink)
	{
		return CreateDefaultSessionFactories(connection, logSink).Control;
	}

	public static IAndroidAudioSessionFactory CreateDefaultAudioSessions(IAndroidConnectionService connection, IAndroidConnectionLogSink logSink)
	{
		return CreateDefaultSessionFactories(connection, logSink).Audio;
	}

	public static IAndroidMediaPlaybackControl CreateDefaultMediaPlaybackControl(IAndroidConnectionService connection, IAndroidConnectionLogSink logSink)
	{
		AndroidConnectionService connection2 = RequireDefaultConnection(connection);
		ArgumentNullException.ThrowIfNull(logSink, "logSink");
		string executablePath = Path.Combine(AppContext.BaseDirectory, "resources", "android-platform-tools", "adb.exe");
		return new AndroidMediaPlaybackControl(connection2, new AdbCommandRunner(executablePath), logSink);
	}

	public static IAndroidKeyguardStateService CreateDefaultKeyguardStateService(IAndroidConnectionService connection)
	{
		AndroidConnectionService connection2 = RequireDefaultConnection(connection);
		string executablePath = Path.Combine(AppContext.BaseDirectory, "resources", "android-platform-tools", "adb.exe");
		return new AndroidKeyguardStateService(connection2, new AdbCommandRunner(executablePath));
	}

	private static AndroidConnectionService RequireDefaultConnection(IAndroidConnectionService connection)
	{
		ArgumentNullException.ThrowIfNull(connection, "connection");
		if (!(connection is AndroidConnectionService result))
		{
			throw new ArgumentException("The operation requires the default Android connection service.", "connection");
		}
		return result;
	}
}
