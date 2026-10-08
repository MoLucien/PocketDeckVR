using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace PocketDeck.App.Ui3;

/// <summary>
/// 全局动画时钟：只有一个 60fps 定时器，只有正在动画的控件会被登记。
/// 控件用 <see cref="Request"/> 声明自己在动，OnTick 返回 false 即自动摘除。
/// </summary>
internal static class Anim
{
	internal interface IAnimated
	{
		/// <returns>仍在动画中返回 true。</returns>
		bool Advance(float deltaSeconds);
	}

	private static readonly HashSet<IAnimated> Active = new HashSet<IAnimated>();

	private static readonly Timer Pulse = new Timer { Interval = 16 };

	private static DateTime _last = DateTime.UtcNow;

	static Anim()
	{
		Pulse.Tick += (_, _) => Step();
	}

	/// <summary>登记一个需要逐帧推进的对象（幂等）。</summary>
	public static void Request(IAnimated item)
	{
		if (Active.Add(item) && Active.Count == 1)
		{
			_last = DateTime.UtcNow;
			Pulse.Start();
		}
	}

	public static void Release(IAnimated item)
	{
		Active.Remove(item);
		if (Active.Count == 0)
		{
			Pulse.Stop();
		}
	}

	private static void Step()
	{
		DateTime now = DateTime.UtcNow;
		float dt = (float)(now - _last).TotalSeconds;
		_last = now;
		if (dt <= 0f)
		{
			return;
		}
		if (dt > 0.1f)
		{
			dt = 0.1f;
		}
		List<IAnimated> finished = null;
		foreach (IAnimated item in Active)
		{
			if (!item.Advance(dt))
			{
				(finished ??= new List<IAnimated>()).Add(item);
			}
		}
		if (finished != null)
		{
			foreach (IAnimated item in finished)
			{
				Release(item);
			}
		}
	}
}

/// <summary>指数平滑的动画值（帧率无关）。</summary>
internal struct Wave
{
	public float Value;

	public float Target;

	public float Speed;

	public static Wave At(float value, float speed = 14f)
	{
		return new Wave { Value = value, Target = value, Speed = speed };
	}

	public bool Advance(float dt)
	{
		float diff = Target - Value;
		if (MathF.Abs(diff) < 0.0015f)
		{
			Value = Target;
			return false;
		}
		Value += diff * Math.Min(1f, Speed * dt);
		return true;
	}

	public void Set(float target)
	{
		Target = target;
	}
}

/// <summary>动画缓动函数。</summary>
internal static class Ease
{
	public static float Out(float t)
	{
		float k = Math.Clamp(t, 0f, 1f) - 1f;
		return k * k * k + 1f;
	}

	public static float InOut(float t)
	{
		float k = Math.Clamp(t, 0f, 1f);
		return (k < 0.5f) ? (4f * k * k * k) : (1f - MathF.Pow(-2f * k + 2f, 3f) / 2f);
	}

	public static float Pulse(float phase)
	{
		return 0.5f + 0.5f * MathF.Sin(phase * MathF.PI * 2f);
	}
}
