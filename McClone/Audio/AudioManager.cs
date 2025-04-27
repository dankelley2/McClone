using System;
using System.Collections.Generic;
using System.Collections.Concurrent; // For ConcurrentDictionary
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
        // Use a concurrent dictionary for sources to handle potential multi-threaded access if needed in the future
        private ConcurrentDictionary<int, bool> _sources = new ConcurrentDictionary<int, bool>();
        private int? _currentMusicSource = null; // Track the source used for the main music playback

        // Sound effect buffers
        private int _digSoundBuffer = 0;
        private int _placeSoundBuffer = 0;

        public bool Initialize()
        {
            bool success = false;
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
                success = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioManager] Exception during initialization: {ex.Message}");
                Dispose(); // Attempt cleanup
                return false;
            }

            if (success)
            {
                 LoadGameSounds(); // Load specific game sounds after successful initialization
            }
            return success;
        }

        private void LoadGameSounds()
        {
            // Construct paths relative to the executable or a known Assets structure
            // Assuming Assets folder is copied to the output directory
            string audioPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Audio");
            _digSoundBuffer = LoadWav(Path.Combine(audioPath, "dig.wav"));
            _placeSoundBuffer = LoadWav(Path.Combine(audioPath, "place.wav"));

            if (_digSoundBuffer == 0) Console.WriteLine("[AudioManager] Warning: Failed to load dig.wav");
            if (_placeSoundBuffer == 0) Console.WriteLine("[AudioManager] Warning: Failed to load place.wav");
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

        // Renamed Play to PlayMusic for clarity
        public void PlayMusic(int bufferHandle, bool loop)
        {
             if (bufferHandle == 0 || !_buffers.Contains(bufferHandle))
             {
                 Console.WriteLine("[AudioManager] Error: Cannot play music with invalid or unloaded buffer handle.");
                 return;
             }

            // Stop previous music playback if any
            StopMusic();

            try
            {
                int source = AL.GenSource();
                if (!_sources.TryAdd(source, true)) // Add to our tracking
                {
                    Console.WriteLine("[AudioManager] Warning: Failed to track generated music source.");
                    AL.DeleteSource(source); // Clean up untracked source
                    return;
                }
                _currentMusicSource = source; // Track this source for music

                AL.Source(source, ALSourcei.Buffer, bufferHandle);
                AL.Source(source, ALSourceb.Looping, loop);
                AL.SourcePlay(source);

                CheckALError("Playing Music");
                Console.WriteLine($"[AudioManager] Playing music buffer {bufferHandle}{(loop ? " (Looping)" : "")}");
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"[AudioManager] Error playing music buffer {bufferHandle}: {ex.Message}");
                 // Attempt to clean up the generated source if playback failed
                 if (_currentMusicSource.HasValue && _sources.TryRemove(_currentMusicSource.Value, out _))
                 {
                     AL.DeleteSource(_currentMusicSource.Value);
                     _currentMusicSource = null;
                 }
            }
        }

        // Renamed Stop to StopMusic
        public void StopMusic()
        {
            if (_currentMusicSource.HasValue)
            {
                try
                {
                    int source = _currentMusicSource.Value;
                    if (_sources.ContainsKey(source))
                    {
                        AL.SourceStop(source);
                        CheckALError("Stopping Music");
                        AL.DeleteSource(source); // Clean up the source immediately after stopping
                        _sources.TryRemove(source, out _);
                        Console.WriteLine($"[AudioManager] Stopped and deleted music source.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AudioManager] Error stopping music source {_currentMusicSource.Value}: {ex.Message}");
                }
                finally
                {
                    _currentMusicSource = null; // Clear the tracked music source
                }
            }
        }

        // Method to play a short sound effect (one-shot)
        private void PlayOneShotSound(int bufferHandle)
        {
            if (bufferHandle == 0 || !_buffers.Contains(bufferHandle))
            {
                // Don't log error spam for missing sounds, LoadGameSounds already warns
                // Console.WriteLine("[AudioManager] Error: Cannot play one-shot sound with invalid buffer handle.");
                return;
            }

            try
            {
                int source = AL.GenSource();
                // We don't strictly need to track one-shot sources if we clean them up,
                // but it can be useful for debugging or advanced management.
                // For simplicity, we won't track them in the main list for now.

                AL.Source(source, ALSourcei.Buffer, bufferHandle);
                AL.Source(source, ALSourceb.Looping, false); // One-shot sounds don't loop
                AL.Source(source, ALSourcef.Gain, 1.0f); // Set volume (optional)
                AL.SourcePlay(source);
                CheckALError("Playing OneShot Sound");

                // IMPORTANT: Clean up the source after it finishes playing.
                // This is tricky because playback is asynchronous.
                // A simple approach is to check source state periodically, but that's inefficient.
                // A better approach involves source pooling or managing finished sources.
                // For now, we'll rely on the main Dispose to clean up any lingering sources,
                // or manually delete after a delay (which is also not ideal).
                // Let's add a basic cleanup mechanism later if needed.
                // For now, we just generate and play.
                // Consider adding source to a temporary list for later cleanup if issues arise.

                // Quick temporary cleanup (less ideal, but better than nothing for now):
                // System.Threading.Tasks.Task.Delay(1000).ContinueWith(_ => { // Delay assumes sound is short
                //     if (AL.IsSource(source)) AL.DeleteSource(source);
                // });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioManager] Error playing one-shot sound for buffer {bufferHandle}: {ex.Message}");
                // Attempt to clean up the source if it was generated but failed
                // This requires knowing the source ID, which might be tricky if AL.GenSource failed.
            }
        }

        public void PlayDigSound()
        {
            PlayOneShotSound(_digSoundBuffer);
        }

        public void PlayPlaceSound()
        {
            PlayOneShotSound(_placeSoundBuffer);
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
            StopMusic(); // Stop any playing music and delete its source

            // Delete all tracked sources (including potentially lingering one-shots if tracked)
            foreach (var source in _sources.Keys)
            {
                 try
                 {
                     // Check if source still exists before deleting
                     if (AL.IsSource(source))
                     {
                         AL.SourceStop(source); // Stop it first
                         AL.DeleteSource(source);
                     }
                 }
                 catch (Exception ex)
                 {
                     Console.WriteLine($"Error deleting source {source}: {ex.Message}");
                 }
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
