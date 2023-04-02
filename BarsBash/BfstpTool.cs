using Utils;
namespace BfstpTool
{
    public class DspAdpcmInfo
    {
        public ushort[] coefs = new ushort[16];
        public ushort predScale;
        public ushort hist1;
        public ushort hist2;
        public ushort loopPredScale;
        public ushort loopHist1;
        public ushort loopHist2;
        public DspAdpcmInfo(byte[] src, bool isLE)
        {
            for (uint i = 0; i < 16; i++) coefs[i] = Utils.Utils.bytesToUshort(src, i * 2, isLE);
            predScale = Utils.Utils.bytesToUshort(src, 0x20, isLE);
            hist1 = Utils.Utils.bytesToUshort(src, 0x22, isLE);
            hist2 = Utils.Utils.bytesToUshort(src, 0x24, isLE);
            loopPredScale = Utils.Utils.bytesToUshort(src, 0x26, isLE);
            loopHist1 = Utils.Utils.bytesToUshort(src, 0x28, isLE);
            loopHist2 = Utils.Utils.bytesToUshort(src, 0x2C, isLE);
        }
        public byte[] Save(bool isLE){
            byte[] dest = new byte[0x2E];
            for (uint i = 0; i < 16; i++)Utils.Utils.ushortToBytes(dest, i * 2, coefs[i], isLE);
            Utils.Utils.ushortToBytes(dest, 0x20, predScale, isLE);
            Utils.Utils.ushortToBytes(dest, 0x22, hist1, isLE);
            Utils.Utils.ushortToBytes(dest, 0x24, hist2, isLE);
            Utils.Utils.ushortToBytes(dest, 0x26, loopPredScale, isLE);
            Utils.Utils.ushortToBytes(dest, 0x28, loopHist1, isLE);
            Utils.Utils.ushortToBytes(dest, 0x2A, loopHist2, isLE);
            return dest;
        }
    }
    public class BfstmInfo{
        public const byte BSTM_PCM_8 = 0;
        public const byte BSTM_PCM_16 = 1;
        public const byte BSTM_DSP_ADPCM = 2;
        public const byte BSTM_IMA_ADPCM = 3;
        public byte format;
        public DspAdpcmInfo[] daInfos;
        public bool isLooped = false;
        public uint channelCount;
        public uint sampleRate;
        public uint loopStart;
        public uint realLoopStart;
        public uint realLoopEnd;
        public uint sampleLength;
        public uint blockCount;
        public uint blockSize;
        public uint lastBlockSize;
        public uint lastBlockSizeWithPad;
        public uint sampleDataOffset;
        public bool valid = false;
        public BfstmInfo(byte[] src, bool isLE, uint bfstmVersion){
            //stream info
            uint streamInfoOffset = Utils.Utils.bytesToUint(src, 0xC, isLE) + 8;
            if(src.Length < (streamInfoOffset + 0x44))return;
            if(bfstmVersion >= 0x40000){
                if(src.Length < (streamInfoOffset + 0x4C)) return;
            }
            format = src[streamInfoOffset];
            isLooped = Utils.Utils.uintToBool(src[streamInfoOffset + 1]);
            channelCount = src[streamInfoOffset + 2];
            daInfos = new DspAdpcmInfo[channelCount];
            sampleRate = Utils.Utils.bytesToUint(src, streamInfoOffset + 4, isLE);
            loopStart = Utils.Utils.bytesToUint(src, streamInfoOffset + 8, isLE);
            sampleLength = Utils.Utils.bytesToUint(src, streamInfoOffset + 0xC, isLE);
            blockCount = Utils.Utils.bytesToUint(src, streamInfoOffset + 0x10, isLE);
            blockSize = Utils.Utils.bytesToUint(src, streamInfoOffset + 0x14, isLE);
            if (blockSize != Bfstp.BSTM_BLOCK_SIZE) return;
            lastBlockSize = Utils.Utils.bytesToUint(src, streamInfoOffset + 0x1C, isLE);
            lastBlockSizeWithPad = Utils.Utils.bytesToUint(src, streamInfoOffset + 0x24, isLE);
            sampleDataOffset = Utils.Utils.bytesToUint(src, streamInfoOffset + 0x34, isLE) + 8;
            if (bfstmVersion >= 0x40000)
            {
                realLoopStart = Utils.Utils.bytesToUint(src, streamInfoOffset + 0x44, isLE);
                realLoopEnd = Utils.Utils.bytesToUint(src, streamInfoOffset + 0x48, isLE);
            }
            else
            {
                realLoopStart = loopStart;
                realLoopEnd = sampleLength - 1;
            }

            //channel info
            if (format == BSTM_IMA_ADPCM) return;
            if(format != BSTM_DSP_ADPCM)
            {
                for (uint i = 0; i < channelCount; i++) daInfos[i] = null;
                valid = true;
                return;
            }
            uint channelInfoOffset = Utils.Utils.bytesToUint(src, 0x1C, isLE) + 8;
            if(src.Length < (channelInfoOffset + 4))return;
            if(Utils.Utils.bytesToUint(src, channelInfoOffset, isLE) != channelCount)return;
            if(src.Length < (channelInfoOffset + 4 + channelCount * 8))return;
            for(uint i = 0;i < channelCount;i++){
                uint _300StructOffset = Utils.Utils.bytesToUint(src, channelInfoOffset + 4 + i * 8 + 4, isLE) + channelInfoOffset;
                if(src.Length < (_300StructOffset + 8))return;
                uint dspAdpcmInfoOffset = Utils.Utils.bytesToUint(src, _300StructOffset + 4, isLE) + _300StructOffset;
                if(src.Length < (dspAdpcmInfoOffset + 0x2E))return;
                daInfos[i] = new DspAdpcmInfo(Utils.Utils.byteArrayCut(src, dspAdpcmInfoOffset, 0x2E), isLE);
            }
            valid = true;
        }
    }
    public class Bfstp
    {
        public const uint BSTM_BLOCK_SIZE = 8192;
        public const string BSTM_FSTM_TAG = "FSTM";
        public const string BSTM_INFO_TAG = "INFO";
        public const string BSTM_DATA_TAG = "DATA";
        public const uint BLOCK_COUNT_FOR_MK8DX = 5;
        public bool valid = false;
        private bool isLE;
        private byte format;
        private DspAdpcmInfo[] daInfos;
        private uint sampleRate;
        private uint sampleLength;
        private uint channelCount;
        private bool isLooped;
        private uint loopStart;
        private uint realLoopStart;
        private uint realLoopEnd;
        private uint blockCount;
        private uint lastBlockSize;
        private uint lastBlockSizeWithPad;
        private byte[] data;//dataは ch0 * sampleCount→ch1 * sampleCount→ch2 * sampleCount→の順に並ぶ
        public Bfstp(byte[] src){
            //srcはPCM16のBFSTM
            if(src.Length < 0x40)return;
            if(Utils.Utils.bytesToUint(src, 0, false) != 0x4653544D)return;
            if (Utils.Utils.bytesToUshort(src, 4) == 0xFFFE)
            {
                isLE = false;
            }
            else if(Utils.Utils.bytesToUshort(src, 4) == 0xFEFF)
            {
                isLE = true;
            }else{
                return;
            }
            uint bfstmVersion = Utils.Utils.bytesToUint(src, 8, isLE);
            uint chunkCount = Utils.Utils.bytesToUshort(src, 0x10, isLE);
            uint curChunk = 0x14;
            uint headOffset = 0;
            uint headSize = 0;
            uint dataOffset = 0;
            uint dataSize = 0;
            if (chunkCount != 2 && chunkCount != 3) return;
            for (uint i = 0; i < chunkCount; i++)
            {
                ushort curChunkTag = Utils.Utils.bytesToUshort(src, curChunk, isLE);
                switch (curChunkTag)
                {
                    case 0x4000:
                        headOffset = Utils.Utils.bytesToUint(src, curChunk + 4, isLE);
                        headSize = Utils.Utils.bytesToUint(src, curChunk + 8, isLE);
                        if(src.Length < (headOffset + headSize))return;
                        break;
                    case 0x4001:
                        break;
                    case 0x4002:
                        dataOffset = Utils.Utils.bytesToUint(src, curChunk + 4, isLE);
                        dataSize = Utils.Utils.bytesToUint(src, curChunk + 8, isLE);
                        if(src.Length < (dataOffset + dataSize))return;
                        break;
                    default:
                        return;
                }
                curChunk += 0xC;
            }
            BfstmInfo bfi = new BfstmInfo(Utils.Utils.byteArrayCut(src, headOffset, headSize), isLE, bfstmVersion);
            if(!bfi.valid)return;
            daInfos = bfi.daInfos;
            sampleRate = bfi.sampleRate;
            sampleLength = bfi.sampleLength;
            format = bfi.format;
            channelCount = bfi.channelCount;
            data = new byte[CalcDataLengthPerChannel() * channelCount];
            isLooped = bfi.isLooped;
            loopStart = bfi.loopStart;
            realLoopStart = bfi.realLoopStart;
            realLoopEnd = bfi.realLoopEnd;
            blockCount = bfi.blockCount;
            lastBlockSize = bfi.lastBlockSize;
            lastBlockSizeWithPad = bfi.lastBlockSizeWithPad;
            if(!ParseDataChunk(Utils.Utils.byteArrayCut(src, dataOffset, dataSize), channelCount, bfi.sampleDataOffset, bfi.blockCount, bfi.blockSize, bfi.lastBlockSize, bfi.lastBlockSizeWithPad, data))return;
            if(format == BfstmInfo.BSTM_PCM_16 && isLE == false){
                for(uint i = 0;i < (uint)(data.Length / 2);i++){
                    ushort tmpU16 = Utils.Utils.bytesToUshort(data, i * 2, false);
                    Utils.Utils.ushortToBytes(data, i * 2, tmpU16, true);
                }
            }
            valid = true;
            return;
        }
        private uint CalcDataLengthPerChannel(){
            switch(format){
                case BfstmInfo.BSTM_PCM_8:
                    return sampleLength;
                    break;
                case BfstmInfo.BSTM_PCM_16:
                    return 2 * sampleLength;
                    break;
                case BfstmInfo.BSTM_DSP_ADPCM:
                    uint channelLength = (sampleLength / 14) * 8;
                    if((sampleLength % 14) != 0){
                        channelLength += ((((sampleLength % 14) + 1) / 2) + 1);
                    }
                    return channelLength;
            }
            return 0;
        }
        private uint CaluSamplesInBlock(uint blockSize)
        {
            switch (format)
            {
                case BfstmInfo.BSTM_PCM_8:
                    return blockSize;
                    break;
                case BfstmInfo.BSTM_PCM_16:
                    return blockSize / 2;
                    break;
                case BfstmInfo.BSTM_DSP_ADPCM:
                    uint dest = (blockSize / 8) * 14;
                    if (blockSize % 8 != 0) dest += (((blockSize % 8) - 1) * 2);
                    return dest;
            }
            return 0;
        }
        static private bool ParseDataChunk(byte[] dataChunk, uint channelCount, uint sampleDataOffset, uint blockCount, uint blockSize, uint lastBlockSize, uint lastBlockSizeWithPad, byte[] destData){
            uint dataSizePerChannelWithPad = (blockCount - 1) * blockSize + lastBlockSizeWithPad;
            uint dataSizePerChannel = (blockCount - 1) * blockSize + lastBlockSize;
            if(destData.Length != dataSizePerChannel * channelCount)return false;
            if(dataChunk.Length < (dataSizePerChannelWithPad * channelCount + sampleDataOffset))return false;
            for(uint i = 0;i < channelCount;i++){
                for(uint j = 0;j < blockCount;j++){
                    if(j == (blockCount - 1)){
                        Utils.Utils.memcpy(destData, i * dataSizePerChannel + j * blockSize, dataChunk, sampleDataOffset + (channelCount * (blockCount - 1) * blockSize) + i * lastBlockSizeWithPad, lastBlockSize);
                    }else{
                        Utils.Utils.memcpy(destData, i * dataSizePerChannel + j * blockSize, dataChunk, sampleDataOffset + (channelCount * j + i) * blockSize, blockSize);
                    }
                }
            }
            return true;
        }
        static private byte[] CreateChannelInfo_0300(DspAdpcmInfo daInfo, bool isLE){
            byte[] dest = new byte[8 + 0x2E + 2];
            Utils.Utils.ushortToBytes(dest, 0, 0x300, isLE);
            Utils.Utils.uintToBytes(dest, 4, 8, isLE);
            Utils.Utils.memcpy(dest, 8, daInfo.Save(isLE), 0, 0x2E);
            return dest;
        }
        static private byte[] CreateRefenceTable_4102(DspAdpcmInfo[] daInfos, bool isLE){
            byte[] dest;
            if (daInfos[0] == null)
            {
                dest = new byte[4 + daInfos.Length * 8 + 8];
                Utils.Utils.uintToBytes(dest, 0, (uint)(daInfos.Length), isLE);
                for (uint i = 0; i < daInfos.Length; i++)
                {
                    Utils.Utils.ushortToBytes(dest, i * 8 + 4, 0x4102, isLE);
                    Utils.Utils.uintToBytes(dest, i * 8 + 8, 4 + 8 * (uint)daInfos.Length, isLE);
                }
                Utils.Utils.uintToBytes(dest, 4 + (uint)daInfos.Length * 8, 0, isLE);
                Utils.Utils.uintToBytes(dest, 4 + (uint)daInfos.Length * 8 + 4, 0xFFFFFFFF, isLE);
            }
            else
            {
                dest = new byte[4 + (8 + 0x38) * daInfos.Length];
                Utils.Utils.uintToBytes(dest, 0, (uint)(daInfos.Length), isLE);
                for (uint i = 0; i < daInfos.Length; i++)
                {
                    Utils.Utils.ushortToBytes(dest, i * 8 + 4, 0x4102, isLE);
                    Utils.Utils.uintToBytes(dest, i * 8 + 8, (uint)(4 + daInfos.Length * 8 + i * 0x38), isLE);
                    Utils.Utils.memcpy(dest, (uint)(4 + daInfos.Length * 8 + i * 0x38), CreateChannelInfo_0300(daInfos[i], isLE), 0, 0x38);
                }
            }
            return dest;
        }
        public byte[] SavePdatChunkForMK8DX(){
            uint DataLengthPerChannel = CalcDataLengthPerChannel();
            uint dataSize = BLOCK_COUNT_FOR_MK8DX * BSTM_BLOCK_SIZE * channelCount;
            if(data.Length < dataSize)return null;
            uint destSize = dataSize + 0x40;
            byte[] dest = new byte[destSize];
            Utils.Utils.uintToBytes(dest, 0, 0x50444154, false);
            Utils.Utils.uintToBytes(dest, 4, destSize, isLE);
            dest[8] = 1;//unkwnon
            Utils.Utils.uintToBytes(dest, 0x10, dataSize, isLE);
            Utils.Utils.uintToBytes(dest, 0x1C, 0x34, isLE);
            for(uint i = 0;i < channelCount;i++){
                for(uint j = 0;j < BLOCK_COUNT_FOR_MK8DX;j++){
                    Utils.Utils.memcpy(dest, 0x40 + (j * channelCount + i) * BSTM_BLOCK_SIZE, data, DataLengthPerChannel * i + (j * BSTM_BLOCK_SIZE), BSTM_BLOCK_SIZE);
                }
            }
            return dest;
        }
        public byte[] SaveForMK8DX(){
            byte[] channelInfo = CreateRefenceTable_4102(daInfos, true);
            byte[] streamInfo = new byte[0x4C];
            streamInfo[0] = format;
            if (isLooped)
            {
                streamInfo[1] = 1;
                Utils.Utils.uintToBytes(streamInfo, 8, loopStart, true);
                Utils.Utils.uintToBytes(streamInfo, 0x44, realLoopStart, true);
                Utils.Utils.uintToBytes(streamInfo, 0x48, realLoopEnd, true);
            }
            else
            {
                streamInfo[1] = 0;
                Utils.Utils.uintToBytes(streamInfo, 8, 0, true);
                Utils.Utils.uintToBytes(streamInfo, 0x44, 0, true);
                Utils.Utils.uintToBytes(streamInfo, 0x48, sampleLength - 1, true);
            }
            streamInfo[2] = (byte)channelCount;
            Utils.Utils.uintToBytes(streamInfo, 4, sampleRate, true);
            Utils.Utils.uintToBytes(streamInfo, 8, loopStart, true);
            Utils.Utils.uintToBytes(streamInfo, 0xC, sampleLength, true);
            Utils.Utils.uintToBytes(streamInfo, 0x10, blockCount, true);
            Utils.Utils.uintToBytes(streamInfo, 0x14, BSTM_BLOCK_SIZE, true);
            Utils.Utils.uintToBytes(streamInfo, 0x18, CaluSamplesInBlock(BSTM_BLOCK_SIZE), true);
            uint sampleCountInLastBlock = sampleLength % CaluSamplesInBlock(BSTM_BLOCK_SIZE);
            if(sampleCountInLastBlock == 0)sampleCountInLastBlock = CaluSamplesInBlock(BSTM_BLOCK_SIZE);
            Utils.Utils.uintToBytes(streamInfo, 0x1C, lastBlockSize, true);
            Utils.Utils.uintToBytes(streamInfo, 0x20, sampleCountInLastBlock, true);
            Utils.Utils.uintToBytes(streamInfo, 0x24, lastBlockSizeWithPad, true);
            Utils.Utils.uintToBytes(streamInfo, 0x28, 4, true);
            Utils.Utils.uintToBytes(streamInfo, 0x2C, 0x3800, true);
            Utils.Utils.ushortToBytes(streamInfo, 0x30, 0x1F00, true);
            Utils.Utils.uintToBytes(streamInfo, 0x34, 64 * channelCount + 0x100, true);
            Utils.Utils.ushortToBytes(streamInfo, 0x38, 0x100, true);
            Utils.Utils.ushortToBytes(streamInfo, 0x3C, 0, true);
            Utils.Utils.uintToBytes(streamInfo, 0x40, 0xFFFFFFFF, true);
            uint infoChunkHeaderSize = 0x20;
            byte[] infoChunk = new byte[infoChunkHeaderSize];
            Utils.Utils.uintToBytes(infoChunk, 0, 0x494E464F, false);//magic
            Utils.Utils.ushortToBytes(infoChunk, 8, 0x4100, true);
            Utils.Utils.uintToBytes(infoChunk, 0xC, infoChunkHeaderSize - 8, true);
            Utils.Utils.uintToBytes(infoChunk, 0x14, 0xFFFFFFFF, true);
            Utils.Utils.ushortToBytes(infoChunk, 0x18, 0x101, true);
            Utils.Utils.uintToBytes(infoChunk, 0x1C, (uint)(infoChunkHeaderSize + streamInfo.Length - 8), true);
            infoChunk = Utils.Utils.byteArrayCat(infoChunk, streamInfo);
            infoChunk = Utils.Utils.byteArrayCat(infoChunk, channelInfo);
            uint infoChunkPadSize = (uint)(infoChunk.Length % 0x20);
            if(infoChunkPadSize != 0)infoChunk = Utils.Utils.byteArrayCat(infoChunk, new byte[0x20 - (infoChunkPadSize % 0x20)]);
            Utils.Utils.uintToBytes(infoChunk, 4, (uint)infoChunk.Length, true);
            byte[] pdatChunk = SavePdatChunkForMK8DX();
            if (pdatChunk == null)return null;
            uint bfstpHeaderSize = 0x40;
            byte[] bfstpHeader = new byte[bfstpHeaderSize];
            Utils.Utils.uintToBytes(bfstpHeader, 0, 0x46535450, false);//magic
            Utils.Utils.ushortToBytes(bfstpHeader, 4, 0xFEFF, true);//bom
            Utils.Utils.ushortToBytes(bfstpHeader, 6, (ushort)bfstpHeaderSize, true);//bfstpHeaderSize = 0x40
            Utils.Utils.uintToBytes(bfstpHeader, 8, 0x10200, false);//version
            Utils.Utils.uintToBytes(bfstpHeader, 0xC, (uint)(bfstpHeaderSize + infoChunk.Length + pdatChunk.Length), true);//file size
            Utils.Utils.ushortToBytes(bfstpHeader, 0x10, 2, true);//chunk count = 2 (INFO + PDAT)
            Utils.Utils.ushortToBytes(bfstpHeader, 0x14, 0x4000, true);//INFO chunk flag
            Utils.Utils.uintToBytes(bfstpHeader, 0x18, bfstpHeaderSize, true);//INFO chunk Offset
            Utils.Utils.uintToBytes(bfstpHeader, 0x1C, (uint)infoChunk.Length, true);//INFO chunk size
            Utils.Utils.ushortToBytes(bfstpHeader, 0x20, 0x4004, true);//PDAT chunk flag
            Utils.Utils.uintToBytes(bfstpHeader, 0x24, (uint)(bfstpHeaderSize + infoChunk.Length), true);//PDAT chunk offset
            Utils.Utils.uintToBytes(bfstpHeader, 0x28, (uint)pdatChunk.Length, true);//PDAT chunk size
            byte[] dest = Utils.Utils.byteArrayCat(bfstpHeader, infoChunk, pdatChunk);
            return dest;
        }
    }
}