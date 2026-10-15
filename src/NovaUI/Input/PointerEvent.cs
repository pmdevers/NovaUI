namespace NovaUI.Input;

public enum PointerKind { Down, Up, Move }

public readonly record struct PointerEvent(PointerKind Kind, Point Position);
