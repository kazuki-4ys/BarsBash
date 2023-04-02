using Utils;

namespace BarsTool
{
    public class Amta
    {
        public bool valid = false;
        public bool isLE = false;
        private byte[] strg;
        public Amta(byte[] src){
            if(src.Length < 0x1C)return;
            if(Utils.Utils.bytesToUint(src, 0, false) != 0x414D5441)return;//check magic
            if(Utils.Utils.bytesToUshort(src, 4, false) == 0xFFFE)isLE = true;
            uint dataOffset = Utils.Utils.bytesToUint(src, 0xC, isLE);
            uint markOffset = Utils.Utils.bytesToUint(src, 0x10, isLE);
            uint ext_Offset = Utils.Utils.bytesToUint(src, 0x14, isLE);
            uint strgOffset = Utils.Utils.bytesToUint(src, 0x18, isLE);
            if(src.Length < (strgOffset + 8))return;
            uint strgSize = Utils.Utils.bytesToUint(src, strgOffset + 4, isLE) + 8;
            if(src.Length < (strgOffset + strgSize))return;
            strg = Utils.Utils.byteArrayCut(src, strgOffset, strgSize);
            valid = true;
        }
        public string GetFileName(){
            if(!valid)return "";
            return Utils.Utils.bytesToString(strg, 8, (uint)(strg.Length - 8));
        }
    }
    public class SimpleFile
    {
        public string fileName;
        public byte[] data;
        public SimpleFile(string _fileName, byte[] _data){
            fileName = _fileName;
            data = _data;
        }
    }
    public class AmtaFile
    {
        public string fileName;
        public byte[] data;
        public uint hash;
        public AmtaFile(string _fileName, byte[] _data, uint _hash){
            fileName = _fileName;
            data = _data;
            hash = _hash;
        }
    }
    public class Bars
    {
        public bool valid = false;
        public bool isLE;
        public AmtaFile[] MetaData;
        public SimpleFile[] Audio;
        private uint bfstpCount = 0;
        public Bars(byte[] src){
            if(src.Length < 0x10)return;
            if(Utils.Utils.bytesToUint(src, 0, false) != 0x42415253)return;//check magic
            if(Utils.Utils.bytesToUshort(src, 8, false) == 0xFFFE){
                isLE = true;
            }else{
                isLE = false;
            }
            uint fileCount = Utils.Utils.bytesToUint(src, 0xC, isLE);
            if(src.Length < (0x10 + fileCount * 0xC))return;
            MetaData = new AmtaFile[fileCount];
            Audio = new SimpleFile[fileCount];
            for(uint i = 0;i < fileCount;i++){
                uint amtaOffset = Utils.Utils.bytesToUint(src, 0x10 + fileCount * 4 + i * 8, true);
                if(src.Length < (amtaOffset + 0xC))return;
                bool amtaLE = false;
                if(Utils.Utils.bytesToUshort(src, amtaOffset + 4, false) == 0xFFFE)amtaLE = true;
                uint amtaSize = Utils.Utils.bytesToUint(src, amtaOffset + 8, amtaLE);
                if(src.Length < (amtaOffset + amtaSize))return;
                byte[] amtaData = Utils.Utils.byteArrayCut(src, amtaOffset, amtaSize);
                Amta curAmta = new Amta(amtaData);
                if(!curAmta.valid)return;
                MetaData[i] = new AmtaFile(curAmta.GetFileName() + ".amta", amtaData,  Utils.Utils.bytesToUint(src, 0x10 + i * 4));
                uint audioOffset = Utils.Utils.bytesToUint(src, 0x10 + fileCount * 4 + i * 8 + 4);
                if(src.Length < (audioOffset + 0x10))return;
                bool isBfstp = false;
                uint audioMagicNumber = Utils.Utils.bytesToUint(src, audioOffset, false);
                if(audioMagicNumber == 0x46574156){
                    isBfstp = false;
                }else if(audioMagicNumber == 0x46535450){
                    isBfstp = true;
                }else{
                    return;
                }
                bool audioLE = false;
                if(Utils.Utils.bytesToUshort(src, audioOffset + 4, false) == 0xFFFE)audioLE = true;
                uint audioSize = Utils.Utils.bytesToUint(src, audioOffset + 0xC, audioLE);
                if(src.Length < (audioOffset + audioSize))return;
                if(isBfstp){
                    Audio[i] = new SimpleFile(curAmta.GetFileName() + ".bfstp", Utils.Utils.byteArrayCut(src, audioOffset, audioSize));
                    bfstpCount++;
                }else{
                    Audio[i] = new SimpleFile(curAmta.GetFileName() + ".bfwav", Utils.Utils.byteArrayCut(src, audioOffset, audioSize));
                }
            }
            valid = true;
            return;
        }
        public bool ContainBfstp()
        {
            if (bfstpCount == 0) return false;
            return true;
        }
        public SimpleFile FindAudioFileByFileName(string fileName){
            for(uint i = 0;i < Audio.Length;i++){
                if(fileName == Audio[i].fileName)return Audio[i];
            }
            return null;
        }
        public byte[] Save(){
            uint fileCountToSave = 0;
            AmtaFile[] MetaDataToSave = new AmtaFile[MetaData.Length];
            SimpleFile[] AudioToSave = new SimpleFile[Audio.Length];
            for(uint i = 0;i < MetaData.Length;i++){
                AmtaFile curMetaDataToSave = MetaData[i];
                Amta curAmtaToSave = new Amta(curMetaDataToSave.data);
                SimpleFile curAudioToSave = FindAudioFileByFileName(curAmtaToSave.GetFileName() + ".bfwav");
                if(curAudioToSave == null)curAudioToSave = FindAudioFileByFileName(curAmtaToSave.GetFileName() + ".bfstp");
                if(curAudioToSave == null)continue;
                MetaDataToSave[fileCountToSave] = curMetaDataToSave;
                AudioToSave[fileCountToSave] = curAudioToSave;
                fileCountToSave++;
            }
            byte[] dest = new byte[0x10 + 0xC * fileCountToSave];
            for(uint i = 0;i < fileCountToSave;i++){
                dest = Utils.Utils.byteArrayCat(dest, MetaDataToSave[i].data);
            }
            uint padSize = 0x40 - (uint)(dest.Length % 0x40);
            if(padSize != 0x40) dest = Utils.Utils.byteArrayCat(dest, new byte[padSize]);
            for (uint i = 0;i < fileCountToSave;i++){
                padSize = 0x40 - (uint)(AudioToSave[i].data.Length % 0x40);
                if(padSize == 0x40)
                {
                    dest = Utils.Utils.byteArrayCat(dest, AudioToSave[i].data);
                }
                else
                {
                    dest = Utils.Utils.byteArrayCat(dest, AudioToSave[i].data, new byte[padSize]);
                }
            }
            Utils.Utils.uintToBytes(dest, 0, 0x42415253, false);
            Utils.Utils.uintToBytes(dest, 4, (uint)dest.Length, isLE);
            Utils.Utils.ushortToBytes(dest, 8, 0xFEFF, isLE);
            Utils.Utils.ushortToBytes(dest, 0xA, 0x101, isLE);
            Utils.Utils.uintToBytes(dest, 0xC, fileCountToSave, isLE);
            uint curFileOffset = 0x10 + 0xC * fileCountToSave;
            for(uint i = 0;i < fileCountToSave;i++){
                Utils.Utils.uintToBytes(dest, 0x10 + 4 * i, MetaDataToSave[i].hash, isLE);
                Utils.Utils.uintToBytes(dest, 0x10 + 4 * fileCountToSave + 8 * i, curFileOffset, isLE);
                curFileOffset += (uint)MetaDataToSave[i].data.Length;
            }
            padSize = 0x40 - (uint)(curFileOffset % 0x40);
            if (padSize != 0x40)
            {
                curFileOffset += padSize;
            }
            for (uint i = 0;i < fileCountToSave;i++){
                Utils.Utils.uintToBytes(dest, 0x10 + 4 * fileCountToSave + 8 * i + 4, curFileOffset, isLE);
                curFileOffset += (uint)AudioToSave[i].data.Length;
                padSize = 0x40 - (uint)(AudioToSave[i].data.Length % 0x40);
                if(padSize != 0x40)
                {
                    curFileOffset += padSize;
                }
            }
            return dest;
        }
    }
}