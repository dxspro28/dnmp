/*
    dnmp - DotNet Music Player
    Developed By: R.J. Silva -- @dxs-pro

    Simple, fast, and easy to use music player that supports mp3, wav and ogg audio playback
    Dependencies: libbass.so
*/

using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace dnmp {

    public static class Bass {
        [DllImport("bass")]
        public static extern int BASS_Init(int dev, int freq, int flags, IntPtr win, IntPtr dsguid);
        [DllImport("bass")]
        public static extern int BASS_StreamCreateFile(bool mem, string path, int offset, int len, int flags);
        [DllImport("bass")]
        public static extern int BASS_ChannelPlay(int handle, bool restart);
        [DllImport("bass")]
        public static extern int BASS_ChannelPause(int handle);
        [DllImport("bass")]
        public static extern int BASS_ChannelStop(int handle);
        [DllImport("bass")]
        public static extern int BASS_ChannelIsActive(int handle);
        [DllImport("bass")]
        public static extern int BASS_Free();
        [DllImport("bass")]
        public static extern int BASS_StreamFree(int handle);
        [DllImport("bass")]
        public static extern int BASS_ChannelFree(int handle);
        [DllImport("bass")]
        public static extern double BASS_ChannelBytes2Seconds(int handle, long pos);
        [DllImport("bass")]
        public static extern long BASS_ChannelSeconds2Bytes(int handle, double pos);
        [DllImport("bass")]
        unsafe public static extern int BASS_ChannelGetAttribute(int handle, int attrib, float *val);
        [DllImport("bass")]
        public static extern int BASS_ChannelSetAttribute(int handle, int attrib, float val);
        [DllImport("bass")]
        public static extern long BASS_ChannelGetLength(int handle, int mode);
        [DllImport("bass")]
        public static extern long BASS_ChannelGetPosition(int handle, int mode);
        [DllImport("bass")]
        public static extern int BASS_ChannelSetPosition(int handle, long pos, int mode);
        [DllImport("bass")]
        public static extern string BASS_ChannelGetTags(int handle, int tags);
    }

    public class MusicPlayerException : Exception {
        public MusicPlayerException(string err) : base(err) {}
    }

    public class MusicPlayer : IDisposable {

        List<string> playlist = null;
        int pl_index = 0;
        int handle;
        public event EventHandler<EventArgs> OnPlaylistFinished;
        float lastVolume = 1.0f;
        public bool Loading {
            get;
            private set;
        }
        public MusicPlayer() {
            if (Bass.BASS_Init(-1, 44100, 0, IntPtr.Zero, IntPtr.Zero) == 0) throw new MusicPlayerException("Failed to initialize device");
            this.playlist = new List<string>();
            this.OnPlaylistFinished += (sender, args) => { };
        }

        public void Dispose() {
            Bass.BASS_ChannelFree(this.handle);
            Bass.BASS_Free();
            this.playlist = null;
        }

        public void AddFile(string path) {
            this.playlist.Add(path);
        }

        public void AddFiles(string[] files) {
            foreach (var f in files) this.AddFile(f);
        }

        public void ShufflePlaylist() {
            var pl = this.playlist;
            List<string> newpl = new List<string>();
            var r = new Random();
            while (pl.Count > 0) {
                int idx = r.Next(pl.Count);
                if (newpl.Contains(pl[idx])) continue;
                newpl.Add(pl[idx]);
                pl.RemoveAt(idx);
                pl.TrimExcess();
            }
            this.playlist = newpl;
        }

        public string GetCurrentSong() {
            string current = this.playlist[this.pl_index];
            current = current.Substring(current.LastIndexOf('/') + 1);
            return current;
        }

        public bool Play() {
            this.Loading = true;
            string path = this.playlist[this.pl_index];
            this.handle = Bass.BASS_StreamCreateFile(false, path, 0, 0, 0);
            if(this.handle == 0) {
                this.Loading = false;
                return false;
            }
            if (Bass.BASS_ChannelPlay(this.handle, false) == 0) {
                this.Loading = false;
                return false;
            }
            this.Loading = false;
            this.SetVolume(this.lastVolume);
            return true;
        }

        public void Stop() {
            Bass.BASS_ChannelStop(this.handle);
        }

        public void Pause() {
            if(this.IsPlaying()) Bass.BASS_ChannelPause(this.handle);
        }

        public void Resume() {
            if(this.IsPaused()) Bass.BASS_ChannelPlay(this.handle, false);
        }

        public bool IsPaused() {
            return Bass.BASS_ChannelIsActive(this.handle) == 3;
        }

        public bool IsPlaying() {
            return Bass.BASS_ChannelIsActive(this.handle) == 1;
        }

        private bool CheckIndex(int idx) {
            return idx > 0 && idx < this.playlist.Count;
        }

        public int GetPlaylistIndex() => this.pl_index + 1;
        public int GetPlaylistLength() => this.playlist.Count;

        public void Next() {
            if (!CheckIndex(this.pl_index + 1)) {
                this.OnPlaylistFinished(this, new EventArgs());
                return;
            }
            this.Stop();
            this.pl_index ++;
            while (!this.Play()) pl_index ++;
        }

        public void Prev() {
            if (!CheckIndex(this.pl_index - 1)) return;
            this.Stop();
            this.pl_index --;
            while (!this.Play()) pl_index --;
        }

        public double GetPositionInSeconds() {
            long pos = Bass.BASS_ChannelGetPosition(this.handle, 0);
            return Bass.BASS_ChannelBytes2Seconds(this.handle, pos);
        }

        public bool SetPositionInSeconds(double secs) {
            long pos = Bass.BASS_ChannelSeconds2Bytes(this.handle, secs);
            return Bass.BASS_ChannelSetPosition(this.handle, pos, 0) != 0;
        }

        public double GetLengthInSeconds() {
            long len = Bass.BASS_ChannelGetLength(this.handle, 0);
            return Bass.BASS_ChannelBytes2Seconds(this.handle, len);
        }

        public void SetVolume(float vol) {
            if (vol > 1.5f || vol < 0.0f) return;
            vol = (float) Math.Round(vol, 3);
            this.lastVolume = vol;
            Bass.BASS_ChannelSetAttribute(this.handle, 2 /* BASS_ATTRIB_VOL */, vol);
        }

        unsafe public float GetVolume() {
            float vol = 0;
            Bass.BASS_ChannelGetAttribute(this.handle, 2, &vol);
            return vol;
        }
    }

    public class Program {

        public static List<string> GetFiles(string dir, string fmt = "") {
            var files = new List<string>();
            foreach(var entry in Directory.GetFiles(dir)) if (entry.EndsWith(fmt)) files.Add(entry);
            return files;
        }

        private static void WriteAt(string s, int x, int y, int origCol, int origRow) {
            try {
                Console.SetCursorPosition(origCol + x, origRow + y);
                Console.Write(s.PadRight(Console.BufferWidth));
            } catch {
                // Console.WriteLine($"{ex.Message}");
            }
        }

        public static void Update(MusicPlayer player, int origCol, int origRow) {
            var duration = TimeSpan.FromSeconds(player.GetLengthInSeconds());
            var pos = TimeSpan.FromSeconds(player.GetPositionInSeconds());
            WriteAt($"Playing ({player.GetPlaylistIndex()}/{player.GetPlaylistLength()}): {player.GetCurrentSong()}", 0, 0, origCol, origRow);
            WriteAt($"{pos.ToString(@"hh\:mm\:ss")}/{duration.ToString(@"hh\:mm\:ss")} -- Volume: {player.GetVolume() * 100}%  {(player.IsPaused() ? "(Paused)" : "")}", 0, 1, origCol, origRow);
            WriteAt("", 0, 2, origCol, origRow);
        }

        public static void Exit(int code, MusicPlayer player) {
            player.Dispose();
            Console.CursorVisible = true;
            Environment.Exit(code);
        }

        public static void Main(string[] args) {
            
            if(args.Length == 0) {
                Console.WriteLine("usage: dnmp [files | folders] [-s | --shuffle]");
                Environment.Exit(0);
            }
            
            var player = new MusicPlayer();
            player.OnPlaylistFinished += (sender, e) => Exit(0, player);
            int origRow = Console.CursorTop;
            int origCol = 0;
            if (origRow > Console.BufferHeight - 3) {
                Console.Write("\n\n\n");
                origRow -= 3;
            }
            Console.SetCursorPosition(origCol, origRow);


            bool shufflePlaylist = false;
            double currentPosition = 0.0;
            Console.CursorVisible = false;

            Console.CancelKeyPress += (sender, e) => Exit(0, player);

            foreach (var arg in args) {
                if (arg == "-s" || arg == "--shuffle") shufflePlaylist = true;
                if (arg == "-v" || arg == "--version") {
                    Console.WriteLine("dnmp (v1.0.0)");
                    Exit(0, player);
                }
                else if (Directory.Exists(arg)) {
                    var arr = GetFiles(arg).ToArray();
                    Array.Sort(arr);
                    player.AddFiles(arr);
                }
                else if (File.Exists(arg)) player.AddFile(arg);
            }

            if (shufflePlaylist) player.ShufflePlaylist();

            player.Play();

            var updater = new Thread(() => {
                while (true) {
                    Update(player, origCol, origRow);
                    Thread.Sleep(1000);
                }
            });
            updater.Start();

            while (true) {
                if (!player.IsPlaying() && !player.IsPaused() && !player.Loading)
                    player.Next();
                
                if (Console.KeyAvailable) {
                    var keyInfo = Console.ReadKey(true);
                    switch (keyInfo.KeyChar) {
                        case 'q':
                            Exit(0, player);
                            break;
                        case '>':
                            player.Next();
                            break;
                        case '<':
                            player.Prev();
                            break;
                        case ' ':
                            if (player.IsPaused()) player.Resume();
                            else if (player.IsPlaying()) player.Pause();
                            break;
                        default:
                            break;
                    }

                    switch (keyInfo.Key) {
                        case ConsoleKey.Enter:
                            player.Next();
                            break;
                        case ConsoleKey.UpArrow:
                            player.SetVolume(player.GetVolume() + 0.05f);
                            break;
                        case ConsoleKey.DownArrow:
                            player.SetVolume(player.GetVolume() - 0.05f);
                            break;
                        case ConsoleKey.LeftArrow:
                            currentPosition = player.GetPositionInSeconds();
                            if ((keyInfo.Modifiers & ConsoleModifiers.Shift) != 0)
                                player.SetPositionInSeconds(currentPosition - 30.0);
                            else
                                player.SetPositionInSeconds(currentPosition - 5.0);
                            break;
                        case ConsoleKey.RightArrow:
                            currentPosition = player.GetPositionInSeconds();
                            if ((keyInfo.Modifiers & ConsoleModifiers.Shift) != 0)
                                player.SetPositionInSeconds(currentPosition + 30.0);
                            else
                                player.SetPositionInSeconds(currentPosition + 5.0);
                            break;
                    }

                    Update(player, origCol, origRow);
                    continue;
                }
                Thread.Sleep(200);
            }
        }
    }
}
