using System;
using System.IO;
using System.Runtime.Intrinsics.Arm;

namespace Utils
{
    class Utils
    {
        public static byte[] byteArrayCat(params byte[][] src)
        {
            uint length = 0;
            uint offset = 0;
            for (uint i = 0; i < src.Length; i++) length += (uint)src[i].Length;
            byte[] dest = new byte[length];
            for (uint i = 0; i < src.Length; i++)
            {
                memcpy(dest, offset, src[i], 0, (uint)src[i].Length);
                offset += (uint)src[i].Length;
            }
            return dest;
        }
        public static byte[] byteArrayCut(byte[] src, uint offset, uint length)
        {
            byte[] dest = new byte[length];
            Array.Copy(src, offset, dest, 0, length);
            return dest;
        }
        public static void memcpy(byte[] dest, uint destOffset, byte[] src, uint srcOffset, uint length)
        {
            Array.Copy(src, srcOffset, dest, destOffset, length);
        }
        /*public unsafe static float bytesToFloat(byte[] data, uint offset)
        {
            uint tmpInt = bytesToUint(data, offset);
            float dest = *((float*)&tmpInt);
            return dest;
        }*/
        public static ulong bytesToUlong(byte[] data, uint offset)
        {
            ulong dest = 0;
            for (int i = 0; i < 8; i++)
            {
                dest <<= 8;
                dest += data[offset + (7 - i)];
            }
            return dest;
        }
        public static uint bytesToUint(byte[] data, uint offset, bool isLE)
        {
            uint dest = 0;
            for (int i = 0; i < 4; i++)
            {
                dest <<= 8;
                if (isLE)
                {
                    dest += data[offset + (3 - i)];
                }
                else
                {
                    dest += data[offset + i];
                }
            }
            return dest;
        }
        public static uint bytesToUint(byte[] data, uint offset)
        {
            return bytesToUint(data, offset, true);
        }
        public static uint bytesToUint24(byte[] data, uint offset)
        {
            uint dest = 0;
            for (int i = 0; i < 3; i++)
            {
                dest <<= 8;
                dest += data[offset + (2 - i)];
            }
            return dest;
        }
        public static ushort bytesToUshort(byte[] data, uint offset, bool isLE)
        {
            ushort dest = 0;
            for (int i = 0; i < 2; i++)
            {
                dest <<= 8;
                if (isLE)
                {
                    dest += data[offset + (1 - i)];
                }
                else
                {
                    dest += data[offset + i];
                }
            }
            return dest;
        }
        public static ushort bytesToUshort(byte[] data, uint offset)
        {
            return bytesToUshort(data, offset, true);
        }
        public static string bytesToString(byte[] data, uint offset, uint length)
        {
            string dest = "";
            char tmpChar;
            for (uint i = 0; i < length; i++)
            {
                if (data[offset + i] == 0) break;
                tmpChar = (char)data[offset + i];
                dest += new string(tmpChar, 1);
            }
            return dest;
        }
        public static void ushortToBytes(byte[] data, uint offset, ushort val, bool isLE)
        {
            if(isLE){
                for (int i = 0; i < 2; i++)
                {
                    data[offset + i] = (byte)((val >> (8 * i)) & 0xFF);
                }
            }
            else{
                for (int i = 0; i < 2; i++)
                {
                    data[offset + i] = (byte)((val >> (8 * (1 - i))) & 0xFF);
                }
            }
            
        }
        public static void uintToBytes(byte[] data, uint offset, uint val, bool isLE)
        {
            if(isLE){
                for (int i = 0; i < 4; i++)
                {
                    data[offset + i] = (byte)((val >> (8 * i)) & 0xFF);
                }
            }
            else{
                for (int i = 0; i < 4; i++)
                {
                    data[offset + i] = (byte)((val >> (8 * (3 - i))) & 0xFF);
                }
            }
        }
        public static void stringToBytes(byte[] data, uint offset, uint length, string val)
        {
            for (uint i = 0; i < length; i++)
            {
                if (i >= val.Length)
                {
                    data[offset + i] = 0;
                }
                else
                {
                    data[offset + i] = (byte)val[(int)i];
                }
            }
        }
        public static short clamp16(int val)
        {
            if (val > short.MaxValue) return short.MaxValue;
            if (val < short.MinValue) return short.MinValue;
            return (short)val;
        }
        public static bool uintToBool(uint val)
        {
            if (val == 0) return false;
            return true;
        }
        public static uint calcCrc32(byte[] data){
            Crc32 c = new Crc32();
            return c.Calc(data);
        }
    }
    class Crc32
    {
        static private uint[] crcTable = null;
        public Crc32(){
            if(crcTable != null)return;
            crcTable = new uint[256];
            for(uint i = 0;i < 256;i++){
                uint c = i;
                for(uint j = 0;j < 8;j++){
                    c = Utils.uintToBool(c & 1) ? (0xEDB88320 ^ (c >> 1)) : (c >> 1);
                }
                crcTable[i] = c;
            }
        }
        public uint Calc(byte[] data){
            uint c = 0xFFFFFFFF;
            for(uint i = 0;i < data.Length;i++){
                c = crcTable[(c ^ data[i]) & 0xFF ^ (c >> 8)];
            }
            return c ^ 0xFFFFFFFF;
        }
    }
}