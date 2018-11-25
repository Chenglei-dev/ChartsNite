﻿using ReplayAnalyzer;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Common.StreamHelpers;
using UnrealReplayAnalyzer;

namespace FortniteReplayAnalyzer
{
    public class FortniteReplayReader : ReplayReader
    {
        FortniteReplayReader(ChunkReader reader) : base(reader)
        {
        }


        public static async Task<FortniteReplayReader> FortniteReplayFromStream(Stream stream)
        {
            return new FortniteReplayReader(await FromStream(stream));
        }

        public override async Task<ChunkInfo> ReadChunk()
        {
            ChunkInfo chunk = await base.ReadChunk();
            switch (chunk)
            {
                case EventInfo eventInfo:
                    switch (eventInfo.Group)
                    {
                        case "playerElim":
                            //using (StreamWriter writer = File.AppendText("dump"))
                            //{
                            //    await writer.WriteLineAsync(BitConverter.ToString(await eventInfo.Stream.ReadBytes(eventInfo.EventSizeInBytes)));
                            //}
                            return chunk;
                            //if (eventInfo.EventSizeInBytes < 45)
                            //{
                            //    byte[] bytes = await eventInfo.Stream.ReadBytes(eventInfo.EventSizeInBytes);
                            //    Console.WriteLine("WEIRD UNKNOWN DATA:" +BitConverter.ToString(bytes) +"  " + Encoding.ASCII.GetString(bytes));
                            //    return chunk;
                            //}
                            //byte[] unknownData = await chunk.Stream.ReadBytes(45);
                            //string killed = await chunk.Stream.ReadString();
                            //if (!UserNameChecker.CheckUserName(killed)) throw new InvalidDataException("Invalid user name.");
                            //string killer = await chunk.Stream.ReadString();
                            //if (!UserNameChecker.CheckUserName(killer)) throw new InvalidDataException("Invalid user name.");
                            //KillEventChunk.WeaponType weapon = (KillEventChunk.WeaponType)await chunk.Stream.ReadByteOnce();
                            //KillEventChunk.State victimState = (KillEventChunk.State)await chunk.Stream.ReadInt32();
                            //return new KillEventChunk(eventInfo, unknownData, killed, killer, weapon, victimState);
                        case "AthenaMatchStats":
                            return chunk;
                        case "AthenaMatchTeamStats":
                            return chunk;
                        case "checkpoint":
                            return chunk;
                        default:
                            //Console.WriteLine("UNKNOWN CASE" + eventInfo.Group); //TODO
                            return chunk;
                    }
                default:
                    return chunk;
            }
        }
    }
}
