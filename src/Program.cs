/*
    dnmp - DotNet Music Player
    Developed By: R.J. Silva -- @dxs-pro
    Compile with: mcs Program.cs -out:dnmp

    Simple, fast, and easy to use music player that supports mp3, wav and ogg audio playback
    Dependencies: libbass.so
*/

using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace dnmp {

    unsafe public class Bass {
        [DllImport("bass.so")]
        public static extern int BASS_Init(int dev, int freq, int flags, int win, IntPtr dsguid);
        [DllImport("bass.so")]
        public static extern int BASS_Free();
        [DllImport("bass.so")]
        public static extern int BASS_ChannelPlay(int handle, int restart);
        [DllImport("bass.so")]
        public static extern int BASS_ChannelStop(int handle);
        [DllImport("bass.so")]
        public static extern int BASS_ChannelPause(int handle);
        [DllImport("bass.so")]
        public static extern long BASS_ChannelGetPosition(int handle, int mode);
        [DllImport("bass.so")]
        public static extern int BASS_ChannelSetPosition(int handle, long pos, int mode);
        [DllImport("bass.so")]
        public static extern double BASS_ChannelBytes2Seconds(int handle, long pos);
        [DllImport("bass.so")]
        public static extern long BASS_ChannelSeconds2Bytes(int handle, double pos);
        [DllImport("bass.so")]
        public static extern int BASS_ChannelIsActive(int handle);
        [DllImport("bass.so")]
        public static extern int BASS_StreamCreateFile(int mem, string file, long offset, long length, int flags);
        [DllImport("bass.so")]
        public static extern int BASS_StreamFree(int handle);
        [DllImport("bass.so")]
        public static extern int BASS_IsStarted();
        [DllImport("bass.so")]
        public static extern float BASS_GetCPU();
        [DllImport("bass.so")]
        public static extern float BASS_GetVolume();
        [DllImport("bass.so")]
        public static extern int BASS_SetVolume(float vol);
        [DllImport("bass.so")]
        public static extern long BASS_ChannelGetLength(int handle, int mode);
        [DllImport("bass.so")]
        public static extern int BASS_ChannelSetAttribute(int handle, int attrib, float val);
        [DllImport("bass.so")]
        public static extern int BASS_ChannelGetAttribute(int handle, int attrib, float *val);
    }

    public class MusicPlayer : IDisposable {

        int handle = 0, index = 0;
        List<string> files;
        float vol = 1.0f;

        public bool IsSwitching = false;

        public EventHandler OnPlaylistFinished = (sender, args) => {};
        public EventHandler<MusicPlayerEventArgs> OnPlayStarted = (sender, args) => {};

        public MusicPlayer(List<string> files) {
            this.files = files;
            if(Bass.BASS_Init(-1, 44100, 0, 0, IntPtr.Zero) == 0) 
                throw new BassException("Failed to initialize BASS");
        }

        public void Play() {
            this.handle = Bass.BASS_StreamCreateFile(0, files[index], 0,0,0);
            if(this.handle == 0 || Bass.BASS_ChannelPlay(this.handle, 1) == 0) {
                this.files.RemoveAt(index);
                index--;
                Next();
            }
            Bass.BASS_ChannelSetAttribute(handle, 2, this.vol);
            OnPlayStarted.Invoke(this, new MusicPlayerEventArgs(files[index]));
        }

        public void Stop() {
            Bass.BASS_ChannelStop(this.handle);
        }

        public bool CheckIndex(int i) {
            return i >= 0 && i < files.Count;
        }

        public void Next() {
            this.IsSwitching = true;
            this.Stop();
            if(!CheckIndex(++this.index)) OnPlaylistFinished.Invoke(this, new EventArgs());
            this.Play();
            this.IsSwitching = false;
        }

        public void Prev() {
            this.IsSwitching = true;
            this.Stop();
            if(!CheckIndex(this.index - 1)) {
                this.IsSwitching = true;
                return;
            }
            index--;
            this.Play();
            this.IsSwitching = false;
        }

        public bool IsPlaying() {
            return Bass.BASS_ChannelIsActive(this.handle) == 1;
        }

        public bool IsPaused() {
            return Bass.BASS_ChannelIsActive(this.handle) == 3;
        }

        public void Pause() {
            Bass.BASS_ChannelPause(this.handle);
        }

        public void Resume() {
            Bass.BASS_ChannelPlay(handle, 0);
        }

        public double GetDuration() {
            long dur = Bass.BASS_ChannelGetLength(this.handle, 0);
            return Bass.BASS_ChannelBytes2Seconds(this.handle, dur);
        }

        public double GetPosition() {
            long pos = Bass.BASS_ChannelGetPosition(handle, 0);
            return Bass.BASS_ChannelBytes2Seconds(handle, pos);
        }

        public void SetPosition(double seconds) {
            long pos = Bass.BASS_ChannelSeconds2Bytes(handle, seconds);
            Bass.BASS_ChannelSetPosition(handle, pos, 0);
        }

        public void SetVolume(float vol) {
            if(vol > 1) return;
            if(vol < 0) return;
            Bass.BASS_ChannelSetAttribute(handle, 2, vol);
        }

        unsafe public float GetVolume() {
            float vol = 0.0f;
            Bass.BASS_ChannelGetAttribute(handle, 2, &vol);
            this.vol = vol;
            return this.vol;
        }

        public void Dispose() {
            Bass.BASS_StreamFree(this.handle);
            Bass.BASS_Free();
        }

        public int GetCurrentIndex() {
            return this.index;
        }

        public int GetCount() {
            return this.files.Count;
        }

        public string GetCurrentSong() {
            return this.files[this.index].Substring(this.files[this.index].LastIndexOf("/") + 1);
        }
    }

    public class BassException : Exception {
        public BassException(string msg = "Internal BASS error") : base(msg) {}
    }
    public class MusicPlayerEventArgs : EventArgs {
        public string Song = "";
        public MusicPlayerEventArgs(string song) : base() {
            this.Song = song;
        }
    }

    static class Program {

        static MusicPlayer player;
        static int initialPos = 0;

        public static void Exit(int status) {
            player.Stop();
            player.Dispose();
            Console.CursorVisible = true;
            Environment.Exit(status);
        }

        public static List<string> ShuffleList(List<string> _list) {
            Random rand = new Random((int)DateTime.Now.Ticks);
            List<string> shuffled = new List<string>();
            List<string> list = _list;

            int n = list.Count;

            for(int i=0;i<n;i++) {
                int pos = rand.Next() % list.Count;
                shuffled.Add(list[pos]);
                list.RemoveAt(pos);
                list.TrimExcess();
            }

            return shuffled;
        }

        public static string GetPlayerState() {
            return player.IsPaused() ? "(Paused)" : string.Empty;
        }

        public static void UpdateTerm() {
            Console.WriteLine($"Playing: ({player.GetCurrentIndex() + 1}/{player.GetCount()}) {player.GetCurrentSong()}".PadRight(Console.BufferWidth).Replace('\n', ' '));
            var pos = TimeSpan.FromSeconds((int)player.GetPosition());
            var dur = TimeSpan.FromSeconds((int)player.GetDuration());
            Console.WriteLine($"{pos.ToString()}/{dur.ToString()} -- Volume: {Convert.ToInt32(player.GetVolume() * 100.0f)}% {GetPlayerState()}".PadRight(Console.BufferWidth));
            Console.WriteLine($"".PadRight(Console.BufferWidth));
            Console.SetCursorPosition(0, initialPos == Console.BufferHeight - 1 ? initialPos - 3 : initialPos);
        }

        public static void Main(string[] args) {

            Console.CursorVisible = false;
            
            List<string> files = new List<string>();
            bool shuffle = false;
            initialPos = Console.CursorTop;

            foreach(var arg in args) {
                if(arg == "-s" || arg == "--shuffle") shuffle = true;
                if(Directory.Exists(arg)) files.AddRange(Directory.GetFiles(arg));
                else if(File.Exists(arg)) files.Add(arg);
            }

            if(files.Count < 1) {
                Console.WriteLine("Please specify at least one input file or directory");
                Console.CursorVisible = true;
                Environment.Exit(-1);
            }

            files.Sort();

            if(shuffle) files = ShuffleList(files);

            player = new MusicPlayer(files);
            player.OnPlaylistFinished += (sender, e) => {
                Exit(0);
            };
            player.OnPlayStarted += (sender, e) => {
                UpdateTerm();
            };
            
            player.Play();

            Timer updater = new Timer((sender) => {
                UpdateTerm();
            });

            updater.Change(1000, 1000);

            while(Bass.BASS_IsStarted() != 0) {

                if(!player.IsPlaying() && !player.IsPaused() && !player.IsSwitching) {
                    player.Next();
                }

                if(Console.KeyAvailable) {
                    var key = Console.ReadKey(true);
                    switch(key.KeyChar) {
                        case '>':
                        player.Next();
                        break;
                        case '<':
                        player.Prev();
                        break;
                        case 'q':
                        Exit(0);
                        break;
                        case ' ':
                        if(player.IsPlaying()) player.Pause();
                        else if(player.IsPaused()) player.Resume();
                        break;
                    }
                    switch(key.Key) {
                        // Backward 5 or 30 seconds
                        case ConsoleKey.LeftArrow:
                        if(key.Modifiers == ConsoleModifiers.Shift)
                            player.SetPosition(player.GetPosition() - 30.0);
                        else player.SetPosition(player.GetPosition() - 5.0);
                        break;
                        // Forward 5 or 30 seconds
                        case ConsoleKey.RightArrow:
                        if(key.Modifiers == ConsoleModifiers.Shift)
                            player.SetPosition(player.GetPosition() + 30.0);
                        else player.SetPosition(player.GetPosition() + 5.0);
                        break;
                        // Volume up (0.01f)
                        case ConsoleKey.UpArrow:
                        player.SetVolume(player.GetVolume() + 0.01f);
                        break;
                        // Volume down (0.01f)
                        case ConsoleKey.DownArrow:
                        player.SetVolume(player.GetVolume() - 0.01f);
                        break;
                        case ConsoleKey.Enter:
                        player.Next();
                        break;
                    }
                    UpdateTerm();
                    continue;
                }
                Thread.Sleep(150);
            }

            Exit(0);
        }
    }
}
