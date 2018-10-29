﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FortniteReplayAnalyzer;
using ReplayAnalyzer;

namespace Analyzer
{
    class Program
    {
        static async Task Main(string[] args)
        {

            //await ParseReplay(@"UnsavedReplay-2018.10.28-23.50.48.replay");
            //Console.WriteLine(await ReadString());
            // const string saveName =;
            foreach (string s in Directory.GetFiles(".", "*.replay"))
            {
                try
                {
                    await ParseReplay(s);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

            }
            Console.WriteLine("Done.");
            Console.ReadKey();
        }

        static async Task ParseReplay(string saveName)
        {
            Console.WriteLine("______________________________________"+saveName);
            using (var saveFile = File.OpenRead(saveName))
            using (var replayStream = await FortniteReplayStream.FortniteReplayFromStream(saveFile))
            {
                bool mhhh = false;
                while (replayStream.Position < replayStream.Length)
                {
                    using (var chunkInfo = await replayStream.ReadChunk())
                    using (var reader = new BinaryReader(chunkInfo))
                    {
                        if (chunkInfo is KillEventChunk kill)
                        {

                            if (kill.PlayerKilling == "Kuinox_" || kill.PlayerKilled == "Kuinox_" || kill.PlayerKilled == "DexterNeo" || kill.PlayerKilling == "DexterNeo")
                            {
                                if (!Enum.IsDefined(typeof(KillEventChunk.WeaponType), (byte)kill.Weapon) || mhhh)
                                {
                                    mhhh = true;
                                    Console.WriteLine(kill.Weapon + " " + kill.VictimState + " Killer: " + kill.PlayerKilling + " Killed: " + kill.PlayerKilled +"time: "+ kill.Time1+ "state: "+kill.VictimState);
                                }
                            }
                        }
                    }
                }
            }
        }

        public static async Task<byte[]> ReadBytes(Stream stream, int count)
        {
            byte[] buffer = new byte[count];
            if (await stream.ReadAsync(buffer, 0, count) != count) throw new InvalidDataException("Did not read the expected number of bytes.");
            return buffer;
        }


        //public async Task<byte[]> ReadToEnd() => await ReadBytes((int)(_stream.Length - _stream.Position));
        public static async Task<byte> ReadByteOnce(Stream stream) => (await ReadBytes(stream, 1))[0];
        public static async Task<uint> ReadUInt32(Stream stream) => BitConverter.ToUInt32(await ReadBytes(stream, 4));
        public static async Task<int> ReadInt32(Stream stream) => BitConverter.ToInt32(await ReadBytes(stream, 4));
        public static async Task<long> ReadInt64(Stream stream) => BitConverter.ToInt64(await ReadBytes(stream, 8));

        public static async Task<string> ReadString()
        {
            List<byte> test = new List<byte> { 0xFB, 0xFF, 0xFF, 0xFF, 0x53, 0x00, 0x61, 0x00, 0xEF, 0x00, 0x2E, 0x00, 0x00, 0x00 };
            Stream stream = new MemoryStream(test.ToArray());
            var length = await ReadInt32(stream);
            var isUnicode = length < 0;
            byte[] data;
            string value;

            if (isUnicode)
            {
                length = -length;
                data = await ReadBytes(stream, length * 2);
                value = Encoding.Unicode.GetString(data);
            }
            else
            {
                data = await ReadBytes(stream, length);
                value = Encoding.Default.GetString(data);
            }
            return value.Trim(' ', '\0');
        }
    }
}