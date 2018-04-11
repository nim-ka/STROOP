﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using System.Drawing;
using STROOP.Structs;

namespace STROOP.M64Editor
{
    public class M64InputFrame
    {
        public static FrameInputRelationType FrameInputRelation = FrameInputRelationType.FrameAfterInput;
        public static int ClassIdIndex = 0;

        public int FrameIndex;
        public uint RawValue;
        public readonly int IdIndex;

        public M64InputFrame(int frameIndex, uint rawValue)
        {
            FrameIndex = frameIndex;
            RawValue = rawValue;
            IdIndex = ClassIdIndex;
            ClassIdIndex++;

            _X = X;
            _Y = Y;
            _A = A;
            _B = B;
            _Z = Z;
            _S = S;
            _R = R;
            _C_Up = C_Up;
            _C_Down = C_Down;
            _C_Left = C_Left;
            _C_Right = C_Right;
            _L = L;
            _D_Up = D_Up;
            _D_Down = D_Down;
            _D_Left = D_Left;
            _D_Right = D_Right;
        }

        public int Frame { get => FrameIndex + GetFrameInputRelationOffset(); }
        public int Id { get => IdIndex + GetFrameInputRelationOffset(); }
        public sbyte X { get => (sbyte)GetByte(2); set { SetByte(2, (byte)value); UpdateEditedCellColor(2, X != _X); } }
        public sbyte Y { get => (sbyte)GetByte(3); set { SetByte(3, (byte)value); UpdateEditedCellColor(3, Y != _Y); } }
        public bool A { get => GetBit(7); set { SetBit(7, value); UpdateEditedCellColor(4, A != _A); } }
        public bool B { get => GetBit(6); set { SetBit(6, value); UpdateEditedCellColor(5, B != _B); } }
        public bool Z { get => GetBit(5); set { SetBit(5, value); UpdateEditedCellColor(6, Z != _Z); } }
        public bool S { get => GetBit(4); set { SetBit(4, value); UpdateEditedCellColor(7, S != _S); } }
        public bool R { get => GetBit(12); set { SetBit(12, value); UpdateEditedCellColor(8, R != _R); } }
        public bool C_Up { get => GetBit(11); set { SetBit(11, value); UpdateEditedCellColor(9, C_Up != _C_Up); } }
        public bool C_Down { get => GetBit(10); set { SetBit(10, value); UpdateEditedCellColor(10, C_Down != _C_Down); } }
        public bool C_Left { get => GetBit(9); set { SetBit(9, value); UpdateEditedCellColor(11, C_Left != _C_Left); } }
        public bool C_Right { get => GetBit(8); set { SetBit(8, value); UpdateEditedCellColor(12, C_Right != _C_Right); } }
        public bool L { get => GetBit(13); set { SetBit(13, value); UpdateEditedCellColor(13, L != _L); } }
        public bool D_Up { get => GetBit(3); set { SetBit(3, value); UpdateEditedCellColor(14, D_Up != _D_Up); } }
        public bool D_Down { get => GetBit(2); set { SetBit(2, value); UpdateEditedCellColor(15, D_Down != _D_Down); } }
        public bool D_Left { get => GetBit(1); set { SetBit(1, value); UpdateEditedCellColor(16, D_Left != _D_Left); } }
        public bool D_Right { get => GetBit(0); set { SetBit(0, value); UpdateEditedCellColor(17, D_Right != _D_Right); } }

        private readonly sbyte _X;
        private readonly sbyte _Y;
        private readonly bool _A;
        private readonly bool _B;
        private readonly bool _Z;
        private readonly bool _S;
        private readonly bool _R;
        private readonly bool _C_Up;
        private readonly bool _C_Down;
        private readonly bool _C_Left;
        private readonly bool _C_Right;
        private readonly bool _L;
        private readonly bool _D_Up;
        private readonly bool _D_Down;
        private readonly bool _D_Left;
        private readonly bool _D_Right;

        private void UpdateEditedCellColor(int index, bool changed)
        {

        }

        private void SetByte(int num, byte value)
        {
            uint mask = ~(uint)(0xFF << (num * 8));
            RawValue = ((uint)(value << (num * 8)) | (RawValue & mask));
        }

        private byte GetByte(int num)
        {
            return (byte)(RawValue >> (num * 8));
        }

        private void SetBit(int bit, bool value)
        {
            uint mask = (uint)(1 << bit);
            if (value)
            {
                RawValue |= mask;
            }
            else
            {
                RawValue &= ~mask;
            }
        }

        private bool GetBit(int bit)
        {
            return ((RawValue >> bit) & 0x01) == 0x01;
        }

        private int GetFrameInputRelationOffset()
        {
            switch (FrameInputRelation)
            {
                case FrameInputRelationType.FrameOfInput:
                    return -1;
                case FrameInputRelationType.FrameAfterInput:
                    return 0;
                case FrameInputRelationType.FrameWhenObserved:
                    return 1;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public byte[] ToBytes()
        {
            return BitConverter.GetBytes(RawValue).ToArray();
        }
    }
}
