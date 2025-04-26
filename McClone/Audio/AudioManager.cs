using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices; // Added for GCHandle
using OpenTK.Audio.OpenAL;

namespace VoxelGame.Audio
{
    public class AudioManager : IDisposable
    {
        private ALDevice _device;
        private ALContext _context;
        private List<int> _buffers = new List<int>();
        private List<int> _sources = new List<int>();
        private int? _currentSource = null; // Track the source used for the main playback

        public bool Initialize()
        {
            try
            {
                _device = ALC.OpenDevice(null); // Open default device
                if (_device.Equals(ALDevice.Null))
                {
                    Console.WriteLine("[AudioManager] Error: Failed to open audio device.");
                    return false;
                }

                _context = ALC.CreateContext(_device, new int[0]); // Use empty array instead of null
                if (_context.Equals(ALContext.Null))
                {
                    Console.WriteLine("[AudioManager] Error: Failed to create OpenAL context.");
                    ALC.CloseDevice(_device);
                    return false;
                }

                if (!ALC.MakeContextCurrent(_context))
                {
                    Console.WriteLine("[AudioManager] Error: Failed to make OpenAL context current.");
                    ALC.DestroyContext(_context);
                    ALC.CloseDevice(_device);
                    return false;
                }

                CheckALError("Initialization");
                Console.WriteLine($"[AudioManager] Initialized OpenAL Context on device: {ALC.GetString(_device, AlcGetString.DeviceSpecifier)}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioManager] Exception during initialization: {ex.Message}");
                Dispose(); // Attempt cleanup
                return false;
            }
        }

        public int LoadWav(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[AudioManager] Error: WAV file not found: {filePath}");
                return 0; // Return 0 or handle error appropriately
            }

            int buffer = 0; // Initialize buffer outside the try block
            GCHandle handle = default; // Initialize GCHandle
            try
            {
                buffer = AL.GenBuffer(); // Assign inside the try block
                _buffers.Add(buffer);

                using (var stream = new FileStream(filePath, FileMode.Open))
                using (var reader = new BinaryReader(stream))
                {
                    // Basic WAV Header Parsing (Simplified - Assumes standard PCM format)
                    // RIFF chunk descriptor
                    if (new string(reader.ReadChars(4)) != "RIFF") throw new NotSupportedException("Invalid RIFF header");
                    reader.ReadInt32(); // ChunkSize (ignore)
                    if (new string(reader.ReadChars(4)) != "WAVE") throw new NotSupportedException("Invalid WAVE format");

                    // Find 'fmt ' chunk
                    string chunkId;
                    int chunkSize;
                    while (true)
                    {
                        chunkId = new string(reader.ReadChars(4));
                        chunkSize = reader.ReadInt32();
                        if (chunkId == "fmt ") break;
                        reader.BaseStream.Seek(chunkSize, SeekOrigin.Current); // Skip chunk data
                        if (reader.BaseStream.Position >= reader.BaseStream.Length) throw new InvalidDataException("fmt chunk not found");
                    }

                    // Read fmt chunk data
                    short audioFormat = reader.ReadInt16(); // 1 = PCM
                    short numChannels = reader.ReadInt16();
                    int sampleRate = reader.ReadInt32();
                    reader.ReadInt32(); // byteRate (ignore)
                    reader.ReadInt16(); // blockAlign (ignore)
                    short bitsPerSample = reader.ReadInt16();
                    if (audioFormat != 1) throw new NotSupportedException("Only PCM WAV files are supported.");

                    // Seek past any extra fmt data
                    if (chunkSize > 16) reader.BaseStream.Seek(chunkSize - 16, SeekOrigin.Current);

                    // Find 'data' chunk
                     while (true)
                    {
                        chunkId = new string(reader.ReadChars(4));
                        chunkSize = reader.ReadInt32();
                        if (chunkId == "data") break;
                        reader.BaseStream.Seek(chunkSize, SeekOrigin.Current); // Skip chunk data
                         if (reader.BaseStream.Position >= reader.BaseStream.Length) throw new InvalidDataException("data chunk not found");
                    }

                    // Read audio data
                    byte[] audioData = reader.ReadBytes(chunkSize);

                    // Determine OpenAL format
                    ALFormat alFormat = GetSoundFormat(numChannels, bitsPerSample);
                    if (alFormat == (ALFormat)0) throw new NotSupportedException($"Unsupported WAV format: {numChannels} channels, {bitsPerSample} bits");

                    // Pin the audio data and get a pointer
                    handle = GCHandle.Alloc(audioData, GCHandleType.Pinned);
                    IntPtr audioDataPtr = handle.AddrOfPinnedObject();

                    // Upload data to OpenAL buffer using the pointer
                    AL.BufferData(buffer, alFormat, audioDataPtr, audioData.Length, sampleRate);
                    CheckALError($"Loading WAV: {Path.GetFileName(filePath)}");

                    Console.WriteLine($"[AudioManager] Loaded WAV: {Path.GetFileName(filePath)} ({numChannels}ch, {bitsPerSample}bit, {sampleRate}Hz)");
                    return buffer;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioManager] Error loading WAV {filePath}: {ex.Message}");
                // Clean up buffer if generated but failed later
                if (buffer != 0 && _buffers.Contains(buffer)) // Check if buffer was assigned and added
                {
                    AL.DeleteBuffer(buffer);
                    _buffers.Remove(buffer);
                }
                return 0;
            }
            finally
            {
                // Ensure the GCHandle is freed
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }
        }

        public void Play(int bufferHandle, bool loop)
        {
             if (bufferHandle == 0 || !_buffers.Contains(bufferHandle))
             {
                 Console.WriteLine("[AudioManager] Error: Cannot play invalid or unloaded buffer handle.");
                 return;
             }

            // Stop previous playback if any
            Stop();

            try
            {
                int source = AL.GenSource();
                _sources.Add(source);
                _currentSource = source; // Track this source

                AL.Source(source, ALSourcei.Buffer, bufferHandle);
                AL.Source(source, ALSourceb.Looping, loop);
                AL.SourcePlay(source);

                CheckALError("Playing Sound");
                Console.WriteLine($"[AudioManager] Playing buffer {bufferHandle}{(loop ? " (Looping)" : "")}");
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"[AudioManager] Error playing buffer {bufferHandle}: {ex.Message}");
                 // Attempt to clean up the generated source if playback failed
                 if (_currentSource.HasValue && _sources.Contains(_currentSource.Value))
                 {
                     AL.DeleteSource(_currentSource.Value);
                     _sources.Remove(_currentSource.Value);
                     _currentSource = null;
                 }
            }
        }

        public void Stop()
        {
            if (_currentSource.HasValue)
            {
                try
                {
                    int source = _currentSource.Value;
                    if (_sources.Contains(source))
                    {
                        AL.SourceStop(source);
                        CheckALError("Stopping Sound");
                        AL.DeleteSource(source); // Clean up the source immediately after stopping
                        _sources.Remove(source);
                        Console.WriteLine($"[AudioManager] Stopped and deleted source for buffer.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AudioManager] Error stopping source {_currentSource.Value}: {ex.Message}");
                }
                finally
                {
                    _currentSource = null; // Clear the tracked source
                }
            }
        }


        private static ALFormat GetSoundFormat(int channels, int bits)
        {
            switch (channels)
            {
                case 1: return bits == 8 ? ALFormat.Mono8 : bits == 16 ? ALFormat.Mono16 : (ALFormat)0;
                case 2: return bits == 8 ? ALFormat.Stereo8 : bits == 16 ? ALFormat.Stereo16 : (ALFormat)0;
                default: return (ALFormat)0; // Unsupported format
            }
        }

        private static void CheckALError(string context)
        {
            ALError error = AL.GetError();
            if (error != ALError.NoError)
            {
                Console.WriteLine($"!!!!!!!! OpenAL Error [{context}]: {AL.GetErrorString(error)} !!!!!!!!");
            }
             // Use AlcError enum for ALC errors
             AlcError alcError = ALC.GetError(ALC.GetContextsDevice(ALC.GetCurrentContext()));
             if (alcError != AlcError.NoError)
             {
                 // Use alcError variable here
                 Console.WriteLine($"!!!!!!!! OpenALC Error [{context}]: {alcError} !!!!!!!!"); // ALC errors often don't have string names
             }
        }

        public void Dispose()
        {
            Console.WriteLine("[AudioManager] Disposing...");
            Stop(); // Stop any playing sound and delete its source

            // Delete any remaining sources (shouldn't be any if Stop works correctly)
            foreach (var source in _sources)
            {
                 try { AL.DeleteSource(source); } catch (Exception ex) { Console.WriteLine($"Error deleting source {source}: {ex.Message}"); }
            }
            _sources.Clear();

            // Delete all buffers
            foreach (var buffer in _buffers)
            {
                 try { AL.DeleteBuffer(buffer); } catch (Exception ex) { Console.WriteLine($"Error deleting buffer {buffer}: {ex.Message}"); }
            }
            _buffers.Clear();

            // Destroy context and close device
            if (!_context.Equals(ALContext.Null))
            {
                try
                {
                    ALC.MakeContextCurrent(ALContext.Null);
                    ALC.DestroyContext(_context);
                } catch (Exception ex) { Console.WriteLine($"Error destroying context: {ex.Message}"); }
                _context = ALContext.Null; // Mark as disposed
            }
            if (!_device.Equals(ALDevice.Null))
            {
                 try { ALC.CloseDevice(_device); } catch (Exception ex) { Console.WriteLine($"Error closing device: {ex.Message}"); }
                _device = ALDevice.Null; // Mark as disposed
            }
            Console.WriteLine("[AudioManager] Disposed.");
            GC.SuppressFinalize(this);
        }

        ~AudioManager()
        {
             Console.WriteLine("[AudioManager] Finalizer called. Dispose was likely not called.");
             // Don't call Dispose() here directly if it accesses managed objects that might already be finalized.
             // The Dispose pattern handles cleanup correctly if Dispose() is called.
             // If absolutely necessary, only release unmanaged resources (AL handles) here,
             // but it's better to ensure Dispose() is always called.
        }
    }
}
