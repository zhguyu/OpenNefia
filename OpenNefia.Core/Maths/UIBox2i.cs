﻿using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OpenNefia.Core.Maths
{
    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    public struct UIBox2i : IEquatable<UIBox2i>
    {
        [FieldOffset(sizeof(int) * 0)] public int Left;
        [FieldOffset(sizeof(int) * 1)] public int Top;
        [FieldOffset(sizeof(int) * 2)] public int Right;
        [FieldOffset(sizeof(int) * 3)] public int Bottom;

        [FieldOffset(sizeof(int) * 0)] public Vector2i TopLeft;
        [FieldOffset(sizeof(int) * 2)] public Vector2i BottomRight;

        public readonly Vector2i TopRight => new(Right, Top);
        public readonly Vector2i BottomLeft => new(Left, Bottom);
        public readonly int Width => Math.Abs(Right - Left);
        public readonly int Height => Math.Abs(Top - Bottom);
        public readonly Vector2i Size => new(Width, Height);

        public UIBox2i(Vector2i topLeft, Vector2i bottomRight)
        {
            Unsafe.SkipInit(out this);

            TopLeft = topLeft;
            BottomRight = bottomRight;
        }

        public UIBox2i(int left, int top, int right, int bottom)
        {
            Unsafe.SkipInit(out this);

            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }

        public static UIBox2i FromDimensions(int left, int top, int width, int height)
        {
            return new(left, top, left + width, top + height);
        }

        public static UIBox2i FromDimensions(Vector2i position, Vector2i size)
        {
            return FromDimensions(position.X, position.Y, size.X, size.Y);
        }

        public readonly bool Contains(int x, int y)
        {
            return Contains(new Vector2i(x, y));
        }

        public readonly bool Contains(Vector2i point, bool closedRegion = true)
        {
            var xOk = closedRegion
                ? point.X >= Left ^ point.X > Right
                : point.X > Left ^ point.X >= Right;
            var yOk = closedRegion
                ? point.Y >= Top ^ point.Y > Bottom
                : point.Y > Top ^ point.Y >= Bottom;
            return xOk && yOk;
        }

        public readonly bool IsInBounds(int x, int y)
        {
            return IsInBounds(new Vector2i(x, y));
        }

        public readonly bool IsInBounds(Vector2i point)
        {
            var xOk = point.X >= Left ^ point.X > Right - 1;
            var yOk = point.Y >= Top ^ point.Y > Bottom - 1;
            return xOk && yOk;
        }

        public readonly bool IsInBounds(UIBox2i other)
        {
            return IsInBounds(other.TopLeft) && IsInBounds(other.BottomRight - Vector2i.One);
        }

        /// <summary>Returns a UIBox2 translated by the given amount.</summary>
        public readonly UIBox2i Translated(Vector2i point)
        {
            return new(Left + point.X, Top + point.Y, Right + point.X, Bottom + point.Y);
        }

        public readonly UIBox2i Scale(int amount)
        {
            return new(Left - amount, Top - amount, Right + amount * 2, Bottom + amount * 2);
        }

        /// <summary>
        ///     Calculates the "intersection" of this and another box.
        ///     Basically, the smallest region that fits in both boxes.
        /// </summary>
        /// <param name="other">The box to calculate the intersection with.</param>
        /// <returns>
        ///     <c>null</c> if there is no intersection, otherwise the smallest region that fits in both boxes.
        /// </returns>
        public readonly UIBox2i? Intersection(in UIBox2i other)
        {
            if (!Intersects(other))
            {
                return null;
            }

            return new UIBox2i(
                Vector2i.ComponentMax(TopLeft, other.TopLeft),
                Vector2i.ComponentMin(BottomRight, other.BottomRight));
        }

        public readonly bool Intersects(in UIBox2i other)
        {
            return other.Bottom >= this.Top && other.Top <= this.Bottom && other.Right >= this.Left &&
                   other.Left <= this.Right;
        }

        // override object.Equals
        public override readonly bool Equals(object? obj)
        {
            if (obj is UIBox2i box)
            {
                return Equals(box);
            }

            return false;
        }

        public readonly bool Equals(UIBox2i other)
        {
            return other.Left == Left && other.Right == Right && other.Bottom == Bottom && other.Top == Top;
        }

        /// <summary>
        /// Compares two instances for equality.
        /// </summary>
        /// <param name="left">The first instance.</param>
        /// <param name="right">The second instance.</param>
        /// <returns>True, if left equals right; false otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(UIBox2i left, UIBox2i right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two instances for inequality.
        /// </summary>
        /// <param name="left">The first instance.</param>
        /// <param name="right">The second instance.</param>
        /// <returns>True, if left does not equal right; false otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(UIBox2i left, UIBox2i right)
        {
            return !left.Equals(right);
        }

        // override object.GetHashCode
        public override readonly int GetHashCode()
        {
            var code = Left.GetHashCode();
            code = (code * 929) ^ Right.GetHashCode();
            code = (code * 929) ^ Top.GetHashCode();
            code = (code * 929) ^ Bottom.GetHashCode();
            return code;
        }

        public static implicit operator UIBox2(UIBox2i box)
        {
            return new(box.Left, box.Top, box.Right, box.Bottom);
        }

        public static implicit operator Love.Rectangle(UIBox2i box)
        {
            return new(box.Left, box.Top, box.Width, box.Height);
        }

        public static UIBox2i operator +(UIBox2i box, (int lo, int to, int ro, int bo) offsets)
        {
            var (lo, to, ro, bo) = offsets;

            return new UIBox2i(box.Left + lo, box.Top + to, box.Right + ro, box.Bottom + bo);
        }

        public override readonly string ToString()
        {
            return $"({Left}, {Top}, {Right}, {Bottom})";
        }
    }
}
