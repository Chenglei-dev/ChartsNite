using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Common.StreamHelpers;
using UnrealReplayParser.Chunk;
using UnrealReplayParser.UnrealObject;

namespace UnrealReplayParser
{
    /// <summary>
    /// Start to read the header, then the chunks of the replay.
    /// Lot of error handling to process the headers:
    /// If the replay header is not correctly parsed, we can't read the replay.
    /// If a chunk header is not correctly parsed, we don't know where stop this chunk and where start the next.
    /// When we read the content of the chunk everything is protected, so there is less error handling
    /// </summary>
    public partial class UnrealReplayVisitor : IDisposable
    {
        protected const uint FileMagic = 0x1CA2E27F;
        protected const uint DemoHeaderMagic = 0x2CF5A13D;
        public virtual async Task<bool> Visit()
        {
            bool noErrorOrRecovered = true;
            ReplayInfo replayInfo;
            await using( CustomBinaryReaderAsync binaryReader = new CustomBinaryReaderAsync( SubStreamFactory.BaseStream, true, async () =>
             {
                 noErrorOrRecovered = await ErrorOnReplayHeaderParsing();
             } ) )//TODO check if header have a constant size
            {
                if( !await ParseMagicNumber( binaryReader ) )
                {
                    return false;
                }
                ReplayInfo.ReplayVersionHistory fileVersion = (ReplayInfo.ReplayVersionHistory)await binaryReader.ReadUInt32();
                int lengthInMs = await binaryReader.ReadInt32();
                uint networkVersion = await binaryReader.ReadUInt32();
                uint changelist = await binaryReader.ReadUInt32();
                string friendlyName = await binaryReader.ReadString();
                bool isLive = await binaryReader.ReadUInt32() != 0;
                DateTime timestamp = DateTime.MinValue;
                if( fileVersion >= ReplayInfo.ReplayVersionHistory.recordedTimestamp )
                {
                    timestamp = DateTime.FromBinary( await binaryReader.ReadInt64() );
                }
                bool compressed = false;
                if( fileVersion >= ReplayInfo.ReplayVersionHistory.compression )
                {
                    compressed = await binaryReader.ReadUInt32() != 0;
                }
                replayInfo = new ReplayInfo( lengthInMs, networkVersion, changelist, friendlyName, timestamp, 0,
                isLive, compressed, fileVersion );
            }
            var enumerator = ParseChunkHeader( replayInfo ).GetAsyncEnumerator();
            if( !noErrorOrRecovered || !await VisitReplayInfo( replayInfo ) || !await enumerator.MoveNextAsync() || enumerator.Current.chunkType != ChunkType.Header ||enumerator.Current.chunkReader == null )
            {
                return false;
            }
            await using( ChunkReader chunkReader = enumerator.Current.chunkReader )
            {
                await ParseGameSpecificHeaderChunk( chunkReader );
            }
            return await VisitReplayChunks( replayInfo );
        }
        /// <summary>
        /// Error occured while parsing the header.
        /// </summary>
        /// <returns></returns>
        public virtual Task<bool> ErrorOnReplayHeaderParsing()
        {
            return Task.FromResult( false );
        }
        /// <summary>
        /// Does nothing, overload this if you want to grab the <see cref="ReplayInfo"/>
        /// </summary>
        /// <param name="replayInfo"></param>
        /// <returns></returns>
        public virtual Task<bool> VisitReplayInfo( ReplayInfo replayInfo )
        {
            return Task.FromResult( true );
        }
        /// <summary>
        /// I don't know maybe you want to change that ? Why i did this ? I don't know me too.
        /// </summary>
        /// <param name="binaryReader"></param>
        /// <returns></returns>
        public virtual async Task<bool> ParseMagicNumber( CustomBinaryReaderAsync binaryReader )
        {
            return await VisitMagicNumber( await binaryReader.ReadUInt32() );
        }
        /// <summary>
        /// Check that the magic number is equal to <see cref="FileMagic"/>
        /// </summary>
        /// <param name="magicNumber"></param>
        /// <returns><see langword="true"/> if the magic number is correct.</returns>
        public virtual Task<bool> VisitMagicNumber( uint magicNumber )
        {
            return Task.FromResult( magicNumber == FileMagic );
        }

        public enum NetworkVersionHistory
        {
            initial = 1,
            absoluteTime = 2,               // We now save the abs demo time in ms for each frame (solves accumulation errors)
            increasedBuffer = 3,            // Increased buffer size of packets, which invalidates old replays
            engineVersion = 4,              // Now saving engine net version + InternalProtocolVersion
            extraVersion = 5,               // We now save engine/game protocol version, checksum, and changelist
            multiLevels = 6,                // Replays support seamless travel between levels
            multiLevelTimeChange = 7,       // Save out the time that level changes happen
            deletedStartupActors = 8,       // Save DeletedNetStartupActors inside checkpoints
            demoHeaderEnumFlags = 9,        // Save out enum flags with demo header
            levelStreamingFixes = 10,       // Optional level streaming fixes.
            saveFullEngineVersion = 11,     // Now saving the entire FEngineVersion including branch name
            guidDemoHeader = 12,            // Save guid to demo header
            historyCharacterMovement = 13,  // Change to using replicated movement and not interpolation
            newVersion,
            latest = newVersion - 1
        };
        enum ReplayHeaderFlags
        {
            None				= 0,
	        ClientRecorded		= ( 1 << 0 ),
	        HasStreamingFixes	= ( 1 << 1 ),
        };

        /// <summary>
        /// Simply return true and does nothing else. It depends on the implementation of the game.
        /// </summary>
        /// <param name="chunk"></param>
        /// <returns></returns>
        public virtual async ValueTask<bool> ParseGameSpecificHeaderChunk( ChunkReader chunkReader )
        {
            if(await chunkReader.ReadUInt32() != DemoHeaderMagic)
            {
                return false;
            }
            NetworkVersionHistory version = (NetworkVersionHistory) await chunkReader.ReadUInt32();
            if(version < NetworkVersionHistory.saveFullEngineVersion )
            {
                return false;
            }
            uint networkChecksum = await chunkReader.ReadUInt32();
            uint engineNetworkProtocolVersion = await chunkReader.ReadUInt32();
            uint gameNetworkProtocolVerrsion = await chunkReader.ReadUInt32();
            byte[] guid = new byte[0];
            if(version>= NetworkVersionHistory.guidDemoHeader)
            {
                guid = await chunkReader.ReadBytes( 16 );
            }
            ushort major = await chunkReader.ReadUInt16();
            ushort minor = await chunkReader.ReadUInt16();
            ushort patch = await chunkReader.ReadUInt16();
            uint changeList = await chunkReader.ReadUInt32();
            string branch = await chunkReader.ReadString();
            (string, uint)[] LevelNamesAndTimes = await new ArrayParser<(string, uint), TupleParser<StringParser, UInt32Parser, string, uint>>( chunkReader, new TupleParser<StringParser, UInt32Parser, string, uint>( new StringParser( chunkReader ), new UInt32Parser( chunkReader ) ) ).Parse();
            //Headerflags
            ReplayHeaderFlags replayHeaderFlags = ( ReplayHeaderFlags) await chunkReader.ReadUInt32();
            string[] GameSpecificData = await new ArrayParser<string, StringParser>( chunkReader, new StringParser( chunkReader ) ).Parse();
            return true;
        }
    }
}