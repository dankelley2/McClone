using OpenTK.Audio.OpenAL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using NAudio.Wave;
using NAudio.Vorbis;

namespace VoxelGame.Audio
{
    public class AudioManager : IDisposable
    {
        private ALDevice _device;
        private ALContext _context;
        private Dictionary<string, int> _soundBuffers = new Dictionary<string, int>();
        private List<int> _activeSources = new List<int>();

        public AudioManager()
        {
            // Get the default audio device
            _device = ALC.OpenDevice(null);
            if (_device.Handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to open audio device.");
            }

            // Create an OpenAL context
            _context = ALC.CreateContext(_device, new int[0]); // Pass empty array instead of null
            if (_context.Handle == IntPtr.Zero)
            {
                ALC.CloseDevice(_device);
                throw new InvalidOperationException("Failed to create OpenAL context.");
            }

            // Make the context current
            if (!ALC.MakeContextCurrent(_context))
            {
                ALC.DestroyContext(_context);
                ALC.CloseDevice(_device);
                throw new InvalidOperationException("Failed to make OpenAL context current.");
            }

            CheckALError("Context Creation");
            Console.WriteLine($"OpenAL Version: {AL.Get(ALGetString.Version)}");
            Console.WriteLine($"Audio Device: {ALC.GetString(_device, AlcGetString.DefaultDeviceSpecifier)}");
        }

        private void CheckALError(string stage)
        {
            ALError error = AL.GetError();
            if (error != ALError.NoError)
            {
                Console.WriteLine($"!!!!!!!! OpenAL Error [{stage}]: {error} !!!!!!!!");
                // Consider throwing an exception in critical stages
            }
        }

        // Basic WAV file loader (supports uncompressed PCM)
        private byte[] LoadWav(string filename, out ALFormat format, out int sampleRate)
        {
            using (FileStream fs = File.OpenRead(filename))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                // RIFF header
                string signature = new string(reader.ReadChars(4));
                if (signature != "RIFF") throw new NotSupportedException("Specified stream is not a wave file.");
                reader.ReadInt32(); // Riff chunk size
                string wformat = new string(reader.ReadChars(4));
                if (wformat != "WAVE") throw new NotSupportedException("Specified stream is not a wave file.");

                // WAVE header
                string format_signature = new string(reader.ReadChars(4));
                while (format_signature != "fmt ")
                {
                    reader.ReadBytes(reader.ReadInt32()); // Skip chunk data
                    format_signature = new string(reader.ReadChars(4));
                }
                int format_chunk_size = reader.ReadInt32();

                // Format information
                int audio_format = reader.ReadInt16(); // 1 = PCM
                int num_channels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                reader.ReadInt32(); // Byte rate
                reader.ReadInt16(); // Block align
                int bits_per_sample = reader.ReadInt16();

                if (audio_format != 1) throw new NotSupportedException("Only uncompressed PCM wave files are supported.");

                // Determine OpenAL format
                if (num_channels == 1)
                {
                    format = bits_per_sample == 8 ? ALFormat.Mono8 : ALFormat.Mono16;
                }
                else if (num_channels == 2)
                {
                    format = bits_per_sample == 8 ? ALFormat.Stereo8 : ALFormat.Stereo16;
                }
                else
                {
                    throw new NotSupportedException("The specified number of channels is not supported.");
                }

                // Skip extra format bytes (if any)
                if (format_chunk_size > 16)
                    reader.ReadBytes(format_chunk_size - 16);

                // Find data chunk
                string data_signature = new string(reader.ReadChars(4));
                while (data_signature.ToLowerInvariant() != "data")
                {
                    int chunkSize = reader.ReadInt32();
                    if (fs.Position + chunkSize > fs.Length) // Prevent reading past end of file
                    {
                         throw new InvalidDataException("Could not find data chunk or chunk size is invalid.");
                    }
                    reader.ReadBytes(chunkSize);
                    if (fs.Position >= fs.Length - 4) // Check if near end of file
                    {
                         throw new InvalidDataException("Reached end of file without finding data chunk.");
                    }
                    data_signature = new string(reader.ReadChars(4));
                }
                if (data_signature.ToLowerInvariant() != "data")
                    throw new FormatException("Specified stream is not a wave file.");

                int data_chunk_size = reader.ReadInt32();

                return reader.ReadBytes(data_chunk_size);
            }
        }

        public int LoadSound(string filePath)
        {
            string fullPath = Path.GetFullPath(filePath);
            if (_soundBuffers.TryGetValue(fullPath, out int existingBuffer))
            {
                return existingBuffer;
            }

            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"Error: Sound file not found at {fullPath}");
                return -1;
            }

            try
            {
                byte[] audioData;
                ALFormat format;
                int sampleRate;
                WaveFormat waveFormat;
                string extension = Path.GetExtension(fullPath).ToLowerInvariant();

                using (WaveStream reader = extension == ".ogg"
                    ? new VorbisWaveReader(fullPath)
                    : extension == ".wav"
                        ? new WaveFileReader(fullPath)
                        : null)
                {
                    if (reader == null)
                    {
                        Console.WriteLine($"!!!!!!!! Unsupported audio file format: {extension} !!!!!!!!");
                        return -1;
                    }

                    waveFormat = reader.WaveFormat;
                    sampleRate = waveFormat.SampleRate;
                    if (waveFormat.BitsPerSample != 16 || waveFormat.Encoding != WaveFormatEncoding.Pcm)
                    {
                        throw new NotSupportedException($"Only 16-bit PCM WAV or OGG files are supported. File: {Path.GetFileName(fullPath)}");
                    }
                    if (waveFormat.Channels == 1)
                        format = ALFormat.Mono16;
                    else if (waveFormat.Channels == 2)
                        format = ALFormat.Stereo16;
                    else
                        throw new NotSupportedException($"Unsupported number of channels ({waveFormat.Channels}) in audio file: {Path.GetFileName(fullPath)}");

                    using (var memoryStream = new MemoryStream())
                    {
                        var buffer = new byte[4096];
                        int bytesRead;
                        while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            memoryStream.Write(buffer, 0, bytesRead);
                        }
                        audioData = memoryStream.ToArray();
                    }
                }

                if (audioData == null || audioData.Length == 0)
                {
                    Console.WriteLine($"!!!!!!!! Failed to decode audio data from '{fullPath}' !!!!!!!!");
                    return -1;
                }

                int bufferHandle = AL.GenBuffer();
                CheckALError($"GenBuffer for {Path.GetFileName(fullPath)}");

                GCHandle handle = GCHandle.Alloc(audioData, GCHandleType.Pinned);
                try
                {
                    IntPtr ptr = handle.AddrOfPinnedObject();
                    AL.BufferData(bufferHandle, format, ptr, audioData.Length, sampleRate);
                    CheckALError($"BufferData for {Path.GetFileName(fullPath)}");
                }
                finally
                {
                    if (handle.IsAllocated)
                    {
                        handle.Free();
                    }
                }

                _soundBuffers.Add(fullPath, bufferHandle);
                Console.WriteLine($"Loaded sound: {Path.GetFileName(fullPath)} (Buffer ID: {bufferHandle})");
                return bufferHandle;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"!!!!!!!! Failed to load sound '{fullPath}': {ex.Message} !!!!!!!!");
                Console.WriteLine(ex.ToString());
                return -1;
            }
        }

        public int PlaySound(int bufferHandle, bool loop = false, float gain = 1.0f, float pitch = 1.0f)
        {
            if (bufferHandle < 0) return -1; // Invalid buffer handle

            int source = AL.GenSource();
            CheckALError("GenSource");
            AL.Source(source, ALSourcei.Buffer, bufferHandle);
            CheckALError("Attach Buffer to Source");
            AL.Source(source, ALSourceb.Looping, loop);
            CheckALError("Set Looping");
            AL.Source(source, ALSourcef.Gain, gain); // Set volume (0.0 to 1.0 typically)
            CheckALError("Set Gain");
            AL.Source(source, ALSourcef.Pitch, pitch); // Set pitch (1.0 is normal)
            CheckALError("Set Pitch");

            AL.SourcePlay(source);
            CheckALError("Play Source");

            _activeSources.Add(source);
            return source;
        }

        public void StopSound(int sourceHandle)
        {
            if (_activeSources.Contains(sourceHandle))
            {
                AL.SourceStop(sourceHandle);
                CheckALError("Stop Source");
                AL.DeleteSource(sourceHandle);
                CheckALError("Delete Source");
                _activeSources.Remove(sourceHandle);
            }
        }

        public void Dispose()
        {
            Console.WriteLine("Disposing AudioManager...");

            // Stop and delete all active sources
            foreach (int source in _activeSources.ToArray()) // Use ToArray to avoid modification issues
            {
                StopSound(source); // This also removes from _activeSources
            }
            _activeSources.Clear();
            CheckALError("Cleanup Sources");

            // Delete all loaded buffers
            foreach (var buffer in _soundBuffers.Values)
            {
                AL.DeleteBuffer(buffer);
            }
            _soundBuffers.Clear();
            CheckALError("Cleanup Buffers");

            // Destroy context and close device
            if (_context.Handle != IntPtr.Zero)
            {
                ALC.MakeContextCurrent(ALContext.Null);
                ALC.DestroyContext(_context);
                _context = ALContext.Null; // Mark as disposed
            }
            if (_device.Handle != IntPtr.Zero)
            {
                ALC.CloseDevice(_device);
                _device = ALDevice.Null; // Mark as disposed
            }
            Console.WriteLine("AudioManager Disposed.");
        }
    }
}
