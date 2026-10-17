using NovaUI.Input;
using NovaUI.Rendering;

namespace NovaUI.Widgets;

public sealed record MenuBarMenu(string Label, params MenuBarItem[] Items);
public sealed record MenuBarItem(string Label, Action? OnClick = null);

public sealed class MenuBar(params MenuBarMenu[] menus) : Widget
{
    private const float BarHeight = 32f;
    private const float HeaderPadX = 12f;
    private const float HeaderFontSize = 14f;
    private const float ItemHeight = 28f;
    private const float ItemPadX = 12f;
    private const float MenuGap = 2f;

    private Rect[] _headerBounds = [];
    private Size[] _headerSizes = [];
    private Rect _dropdownBounds;
    private Rect[] _itemBounds = [];
    private int _openMenu = -1;
    private int _pressedHeader = -1;
    private int _pressedItem = -1;

    public override Size Layout(Constraints c, BuildContext ctx)
    {
        _headerSizes = menus.Select(m => ctx.Text.Measure(m.Label, HeaderFontSize)).ToArray();
        _headerBounds = new Rect[menus.Length];

        var naturalWidth = HeaderPadX * 2 + _headerSizes.Sum(s => s.Width) + Math.Max(0, menus.Length - 1) * HeaderPadX;
        var width = float.IsPositiveInfinity(c.MaxWidth) ? naturalWidth : c.MaxWidth;
        return c.Constrain(new Size(width, BarHeight));
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);

        var x = bounds.X + HeaderPadX;
        for (var i = 0; i < menus.Length; i++)
        {
            var menuWidth = _headerSizes[i].Width + HeaderPadX * 2;
            _headerBounds[i] = new Rect(x, bounds.Y, menuWidth, BarHeight);
            x += menuWidth;
        }

        _dropdownBounds = default;
        _itemBounds = [];
        if (_openMenu < 0 || _openMenu >= menus.Length)
            return;

        var menu = menus[_openMenu];
        var itemWidth = Math.Max(_headerBounds[_openMenu].Width, menu.Items.Length == 0 ? 0 : menu.Items.Max(i => i.Label.Length * HeaderFontSize));
        itemWidth = Math.Min(itemWidth + ItemPadX * 2, bounds.Width - HeaderPadX * 2);
        var itemCount = menu.Items.Length;
        if (itemCount == 0)
            return;

        var dropdownX = Math.Min(Math.Max(bounds.X, _headerBounds[_openMenu].X), bounds.Right - itemWidth);
        var dropdownY = bounds.Y + BarHeight;
        _dropdownBounds = new Rect(dropdownX, dropdownY, itemWidth, itemCount * ItemHeight + MenuGap * 2);
        _itemBounds = new Rect[itemCount];

        for (var i = 0; i < itemCount; i++)
            _itemBounds[i] = new Rect(dropdownX + MenuGap, dropdownY + MenuGap + i * ItemHeight, itemWidth - MenuGap * 2, ItemHeight);
    }

    public override void Paint(ICanvas canvas)
    {
        canvas.FillRect(Bounds, new Color(243, 244, 246));
        canvas.FillRect(new Rect(Bounds.X, Bounds.Y + BarHeight - 1, Bounds.Width, 1), new Color(215, 218, 223));

        for (var i = 0; i < menus.Length; i++)
        {
            var header = _headerBounds[i];
            var active = i == _openMenu;
            var pressed = i == _pressedHeader;
            if (active || pressed)
                canvas.FillRect(header, active ? new Color(229, 232, 237) : new Color(236, 238, 242));

            canvas.DrawText(menus[i].Label, new Point(header.X + HeaderPadX, header.Y + 21), HeaderFontSize, new Color(32, 36, 42));
        }

        if (_openMenu < 0 || _openMenu >= menus.Length || menus[_openMenu].Items.Length == 0)
            return;

        canvas.FillRect(_dropdownBounds, Color.White, 6);

        for (var i = 0; i < menus[_openMenu].Items.Length; i++)
        {
            var item = menus[_openMenu].Items[i];
            var itemBounds = _itemBounds[i];
            var selected = i == _pressedItem;
            if (selected)
                canvas.FillRect(itemBounds, new Color(230, 238, 250), 4);

            canvas.DrawText(item.Label, new Point(itemBounds.X + ItemPadX, itemBounds.Y + 19), HeaderFontSize, new Color(32, 36, 42));
        }
    }

    public override bool HandlePointer(PointerEvent e)
    {
        var header = HitHeader(e.Position);
        var item = _openMenu >= 0 ? HitItem(e.Position) : -1;

        switch (e.Kind)
        {
            case PointerKind.Down:
                if (item >= 0)
                {
                    _pressedItem = item;
                    return true;
                }

                if (header >= 0)
                {
                    _pressedHeader = header;
                    return true;
                }

                if (_openMenu >= 0)
                {
                    _openMenu = -1;
                    return true;
                }

                return false;

            case PointerKind.Up:
                if (_pressedItem >= 0)
                {
                    var selectedMenu = _openMenu;
                    var selectedItem = _pressedItem;
                    _pressedItem = -1;
                    _pressedHeader = -1;
                    _openMenu = -1;

                    if (selectedMenu >= 0 && selectedMenu < menus.Length && selectedItem >= 0 && selectedItem < menus[selectedMenu].Items.Length)
                        menus[selectedMenu].Items[selectedItem].OnClick?.Invoke();

                    return true;
                }

                if (_pressedHeader >= 0)
                {
                    _openMenu = _openMenu == _pressedHeader ? -1 : _pressedHeader;
                    _pressedHeader = -1;
                    return true;
                }

                if (_openMenu >= 0)
                {
                    _openMenu = -1;
                    return true;
                }

                return false;

            default:
                return false;
        }
    }

    private int HitHeader(Point point)
    {
        for (var i = 0; i < _headerBounds.Length; i++)
            if (_headerBounds[i].Contains(point))
                return i;

        return -1;
    }

    private int HitItem(Point point)
    {
        for (var i = 0; i < _itemBounds.Length; i++)
            if (_itemBounds[i].Contains(point))
                return i;

        return -1;
    }
}
