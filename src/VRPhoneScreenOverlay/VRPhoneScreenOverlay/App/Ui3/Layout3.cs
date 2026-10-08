using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PocketDeck.App.Ui3;

/// <summary>
/// 极简流式布局引擎：Measure 求期望尺寸 → Arrange 落位。
/// 取代旧版的「绝对坐标 + 全树捕获缩放」，因此窗口可以自由缩放并支持按宽度折叠。
/// 所有数值均为设备像素（调用方用 <see cref="Layout3.U"/> 把逻辑单位换算过来）。
/// </summary>
internal abstract class Node3
{
	public RectangleF Bounds;

	/// <summary>主轴：给定可用宽度/高度，返回期望尺寸。</summary>
	public abstract SizeF Measure(Graphics g, SizeF available);

	public abstract void Arrange(Graphics g, RectangleF rect);

	/// <summary>把布局结果写回真实控件。</summary>
	public virtual void Apply()
	{
	}
}

/// <summary>布局上下文：逻辑→设备像素换算 + 折叠断点。</summary>
internal sealed class Layout3
{
	public float Scale { get; init; } = 1f;

	public float Width { get; set; }

	public float Height { get; set; }

	public bool TwoColumn { get; set; }

	public float U(float logical)
	{
		return logical * Scale;
	}

	/// <summary>主轴栈（默认纵向）。子节点在主轴上按期望尺寸排列，交叉轴拉满。</summary>
	public Stack3 Stack(float gap, bool vertical = true)
	{
		return new Stack3(this, U(gap), vertical);
	}

	public Fixed3 Fixed(float width, float height)
	{
		return new Fixed3(this, width, height);
	}

	public Flex3 Flex(float weight = 1f)
	{
		return new Flex3(this, weight);
	}

	public Host3 Host(Control control)
	{
		return new Host3(this, control);
	}

	public Row3 Row(float gap, params (Node3 node, float weight)[] columns)
	{
		return new Row3(this, U(gap), columns);
	}

	public Gap3 Gap(float logical)
	{
		return new Gap3(this, logical);
	}
}

internal sealed class Stack3 : Node3
{
	private readonly Layout3 _ctx;

	private readonly List<Node3> _items = new List<Node3>();

	private readonly float _gap;

	private readonly bool _vertical;

	private float _padLeft;

	private float _padTop;

	private float _padRight;

	private float _padBottom;

	public Stack3(Layout3 ctx, float gap, bool vertical)
	{
		_ctx = ctx;
		_gap = gap;
		_vertical = vertical;
	}

	/// <summary>内边距（逻辑单位）。</summary>
	public Stack3 Pad(float left, float top, float right, float bottom)
	{
		_padLeft = _ctx.U(left);
		_padTop = _ctx.U(top);
		_padRight = _ctx.U(right);
		_padBottom = _ctx.U(bottom);
		return this;
	}

	public Stack3 Pad(float all)
	{
		return Pad(all, all, all, all);
	}

	public Stack3 Add(Node3 node)
	{
		_items.Add(node);
		return this;
	}

	public Stack3 Add(params Node3[] nodes)
	{
		_items.AddRange(nodes);
		return this;
	}

	public IReadOnlyList<Node3> Items => _items;

	public override SizeF Measure(Graphics g, SizeF available)
	{
		float main = 0f;
		float cross = 0f;
		SizeF inner = _vertical ? new SizeF(available.Width, available.Height) : new SizeF(available.Height, available.Width);
		foreach (Node3 item in _items)
		{
			SizeF want = item.Measure(g, inner);
			float itemMain = _vertical ? want.Height : want.Width;
			float itemCross = _vertical ? want.Width : want.Height;
			main += itemMain;
			cross = Math.Max(cross, itemCross);
			if (_vertical && item is Flex3)
			{
				inner.Height -= itemMain;
			}
		}
		main += _gap * Math.Max(0, _items.Count - 1);
		if (_vertical)
		{
			return new SizeF(cross + _padLeft + _padRight, main + _padTop + _padBottom);
		}
		return new SizeF(main + _padLeft + _padRight, cross + _padTop + _padBottom);
	}

	public override void Arrange(Graphics g, RectangleF rect)
	{
		Bounds = rect;
		if (_items.Count == 0)
		{
			return;
		}
		RectangleF padded = new RectangleF(
			rect.X + _padLeft,
			rect.Y + _padTop,
			Math.Max(0f, rect.Width - _padLeft - _padRight),
			Math.Max(0f, rect.Height - _padTop - _padBottom));
		rect = padded;
		// 先量一次，再把剩余空间分配给 Flex 子节点。
		float fixedMain = 0f;
		int flexCount = 0;
		float flexWeight = 0f;
		List<SizeF> wants = new List<SizeF>(_items.Count);
		SizeF inner = _vertical ? new SizeF(rect.Width, rect.Height) : new SizeF(rect.Height, rect.Width);
		foreach (Node3 item in _items)
		{
			SizeF want = item.Measure(g, inner);
			wants.Add(want);
			float itemMain = _vertical ? want.Height : want.Width;
			if (item is Flex3 flex)
			{
				flexCount++;
				flexWeight += flex.Weight;
			}
			else
			{
				fixedMain += itemMain;
			}
		}
		float gaps = _gap * Math.Max(0, _items.Count - 1);
		float remaining = Math.Max(0f, (_vertical ? rect.Height : rect.Width) - fixedMain - gaps);
		float cursor = _vertical ? rect.Y : rect.X;
		for (int i = 0; i < _items.Count; i++)
		{
			Node3 item = _items[i];
			SizeF want = wants[i];
			float itemMain = _vertical ? want.Height : want.Width;
			if (item is Flex3 flex2 && flexCount > 0)
			{
				itemMain = (flexWeight <= 0f) ? (remaining / flexCount) : (remaining * (flex2.Weight / flexWeight));
				if (flex2.MinHeight > 0f)
				{
					itemMain = Math.Max(itemMain, flex2.MinHeight);
				}
			}
			RectangleF slot = _vertical
				? new RectangleF(rect.X, cursor, rect.Width, itemMain)
				: new RectangleF(cursor, rect.Y, itemMain, rect.Height);
			item.Arrange(g, slot);
			cursor += itemMain + _gap;
		}
	}
}

internal sealed class Row3 : Node3
{
	private readonly Layout3 _ctx;

	private readonly float _gap;

	private readonly (Node3 node, float weight)[] _columns;

	public Row3(Layout3 ctx, float gap, (Node3 node, float weight)[] columns)
	{
		_ctx = ctx;
		_gap = gap;
		_columns = columns;
	}

	public (Node3 node, float weight)[] Columns => _columns;

	public override SizeF Measure(Graphics g, SizeF available)
	{
		float height = 0f;
		foreach ((Node3 node, float weight) in _columns)
		{
			SizeF want = node.Measure(g, new SizeF(available.Width, available.Height));
			height = Math.Max(height, want.Height);
		}
		return new SizeF(available.Width, height);
	}

	public override void Arrange(Graphics g, RectangleF rect)
	{
		Bounds = rect;
		float totalGap = _gap * Math.Max(0, _columns.Length - 1);
		float usable = Math.Max(0f, rect.Width - totalGap);
		// weight <= 0 的列先按自身期望宽度占位，其余列按权重分配剩余空间。
		float fixedWidth = 0f;
		float totalWeight = 0f;
		SizeF[] wants = new SizeF[_columns.Length];
		for (int i = 0; i < _columns.Length; i++)
		{
			wants[i] = _columns[i].node.Measure(g, new SizeF(usable, rect.Height));
			if (_columns[i].weight <= 0f)
			{
				fixedWidth += wants[i].Width;
			}
			else
			{
				totalWeight += _columns[i].weight;
			}
		}
		if (totalWeight <= 0f)
		{
			totalWeight = 1f;
		}
		float remaining = Math.Max(0f, usable - fixedWidth);
		float x = rect.X;
		for (int i = 0; i < _columns.Length; i++)
		{
			(Node3 node, float weight) = _columns[i];
			float w = (weight <= 0f) ? wants[i].Width : (remaining * (weight / totalWeight));
			node.Arrange(g, new RectangleF(x, rect.Y, w, rect.Height));
			x += w + _gap;
		}
	}
}

internal sealed class Fixed3 : Node3
{
	private readonly Layout3 _ctx;

	private readonly float _width;

	private readonly float _height;

	public Fixed3(Layout3 ctx, float width, float height)
	{
		_ctx = ctx;
		_width = width;
		_height = height;
	}

	public override SizeF Measure(Graphics g, SizeF available)
	{
		return new SizeF(_width * _ctx.Scale, _height * _ctx.Scale);
	}

	public override void Arrange(Graphics g, RectangleF rect)
	{
		Bounds = rect;
	}
}

internal sealed class Gap3 : Node3
{
	private readonly float _size;

	public Gap3(Layout3 ctx, float logical)
	{
		_size = logical * ctx.Scale;
	}

	public override SizeF Measure(Graphics g, SizeF available)
	{
		return new SizeF(0f, _size);
	}

	public override void Arrange(Graphics g, RectangleF rect)
	{
		Bounds = rect;
	}
}

internal sealed class Flex3 : Node3
{
	private readonly Layout3 _ctx;

	public Flex3(Layout3 ctx, float weight)
	{
		_ctx = ctx;
		Weight = weight;
	}

	public float Weight { get; }

	public float MinHeight { get; set; }

	public override SizeF Measure(Graphics g, SizeF available)
	{
		return new SizeF(available.Width, MinHeight);
	}

	public override void Arrange(Graphics g, RectangleF rect)
	{
		Bounds = rect;
	}
}

/// <summary>把布局槽位写回真实控件（控件尺寸按设备像素设置）。</summary>
internal sealed class Host3 : Node3
{
	private readonly Layout3 _ctx;

	private readonly Control _control;

	private readonly float _minHeight;

	public Host3(Layout3 ctx, Control control, float minHeight = 0f)
	{
		_ctx = ctx;
		_control = control;
		_minHeight = minHeight;
	}

	public Control Control => _control;

	public override SizeF Measure(Graphics g, SizeF available)
	{
		Size want = _control.PreferredSize;
		float h = Math.Max(want.Height, _minHeight * _ctx.Scale);
		return new SizeF(available.Width, h);
	}

	public override void Arrange(Graphics g, RectangleF rect)
	{
		Bounds = rect;
	}

	public override void Apply()
	{
		Rectangle r = Rectangle.Round(Bounds);
		if (_control.Bounds != r)
		{
			_control.Bounds = r;
		}
	}
}

/// <summary>递归把布局结果写回控件。</summary>
internal static class Node3Extensions
{
	public static void ApplyTree(this Node3 node)
	{
		node.Apply();
		foreach (Node3 child in Children(node))
		{
			child.ApplyTree();
		}
	}

	private static IEnumerable<Node3> Children(Node3 node)
	{
		if (node is Stack3 stack)
		{
			foreach (Node3 item in stack.Items)
			{
				yield return item;
			}
		}
		else if (node is Row3 row && row.Columns != null)
		{
			foreach ((Node3 child, float _) in row.Columns)
			{
				yield return child;
			}
		}
	}
}
