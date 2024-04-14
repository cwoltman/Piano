using Plugin.Maui.Audio;
using System;
using System.IO;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;

namespace MauiAudioPluginRecordingSample
{
    public partial class MainPage : ContentPage
    {
        readonly IAudioManager _audioManager;
        readonly IAudioRecorder _audioRecorder;

        public MainPage(IAudioManager audioManager)
        {
            InitializeComponent();

            _audioManager = audioManager;
            _audioRecorder = audioManager.CreateRecorder();

            // Set initial button text
            UpdateButtonText();
        }

        private async void OnButtonClicked(object sender, EventArgs e)
        {
            if (await Permissions.RequestAsync<Permissions.Microphone>() != PermissionStatus.Granted)
            {
                // TODO Inform your user
                return;
            }

            if (!_audioRecorder.IsRecording)
            {
                await _audioRecorder.StartAsync();
                // Update button text when recording starts
                UpdateButtonText();
            }
            else
            {
                var recordedAudio = await _audioRecorder.StopAsync();
                var player = AudioManager.Current.CreatePlayer(recordedAudio.GetAudioStream());
                player.Play();

                // Process the recorded audio with the algorithm
                ProcessAudio(recordedAudio.GetAudioStream());

                // Update button text when recording stops
                UpdateButtonText();
            }
        }

        private void UpdateButtonText()
        {
            CounterBtn.Text = _audioRecorder.IsRecording ? "Press to stop Recording" : "Press to Record";
        }

        private void ProcessAudio(Stream audioStream)
        {
            try
            {
                var wavfile = new WavFile(audioStream);
                var song_in = wavfile.Read();

                var song = new double[song_in.Count];
                var song2 = new double[song_in.Count];
                double maxvalue = 0;
                for (int i = 0; i < song_in.Count; i++)
                {
                    song2[i] = double.Parse(song_in[i].ToString());
                    if (Math.Abs(song2[i]) > maxvalue)
                    {
                        maxvalue = Math.Abs(song2[i]);
                    }
                }
                for (int i = 0; i < song_in.Count; i++)
                {
                    song[i] = song2[i] / maxvalue;
                }

                int sampleRate = wavfile.SampleRate;
                int win = (int)Math.Round(0.02 * sampleRate);
                int stp = (int)Math.Round(win / 2.0);
                int Lin = song.Length + 1;
                int bins = (int)Math.Round(((Lin - win) / (double)stp));
                double val = 0;
                double[] energy = new double[bins];
                energy[0] = 0;
                for (int i = 1; i < bins - 1; i++)
                {
                    for (int k = i * stp - stp + 1; k < i * stp + stp; k++)
                    {
                        val += Math.Pow(song[k], 2);
                    }
                    energy[i] = (2.0 / stp) * val;
                    val = 0;
                }

                var peak = Signal.FindPeaks(energy, new Dictionary<string, object> { { "prominence", 0.0001 } });
                var peaks = new int[peak.Item1.Length];
                var p = new double[peak.Item1.Length];
                for (int i = 0; i < peak.Item1.Length; i++)
                {
                    peaks[i] = (int)(double)peak.Item1[i];
                    p[i] = ((double[])peak.Item2["prominences"])[i];
                }

                double pavg = p.Sum() / p.Length;
                double noise = 0;
                int j = 0;
                var locations = new List<int>();
                var locs_p = new List<double>();
                for (int i = 0; i < p.Length; i++)
                {
                    if (p[i] >= 1.5 * pavg && noise == 0)
                    {
                        locations.Add(peaks[i]);
                        locs_p.Add(p[i]);
                        noise = 1;
                        j++;
                    }
                    else
                    {
                        noise = 0;
                    }
                }

                int total_length = 0;
                var delete_these = new List<int>();
                for (int i = 0; i < locations.Count - 2; i++)
                {
                    if (locations[i + 1] - locations[i] < 13 && total_length < 30)
                    {
                        delete_these.Add(i);
                        total_length += Math.Abs(locations[i + 1] - locations[i]);
                    }
                    else
                    {
                        total_length = 0;
                    }
                }

                if (delete_these.Count > 0)
                {
                    for (int i = delete_these.Count - 1; i >= 0; i--)
                    {
                        locations.RemoveAt(delete_these[i]);
                        locs_p.RemoveAt(delete_these[i]);
                    }
                }

                var locations_2 = new List<int>();
                int taps = 0;
                for (int i = 0; i < locations.Count; i++)
                {
                    if (locs_p[i] >= 1.5 * pavg)
                    {
                        taps = 1;
                    }
                    if (locs_p[i] >= 2 * pavg)
                    {
                        taps = 2;
                    }
                    if (locs_p[i] >= 2.5 * pavg)
                    {
                        taps = 3;
                    }
                    locations_2.Add(taps);
                    taps = 0;
                }

                for (int i = 0; i < locations.Count; i++)
                {
                    locations[i] = stp * locations[i];
                }

                delete_these.Clear();
                locs_p.Clear();
                taps = 0;
                j = 0;
                stp = 0;
                win = 0;
                bins = 0;
                total_length = 0;
                p = null;
                noise = 0;
                pavg = 0;
                energy = null;
                song2 = null;
                song_in = null;

                var number = new int[locations.Count, 3];
                var letter = new int[locations.Count, 3];
                int window_length = 4096;
                int X_len = 2049;
                double[] ker = { 27.5, 25.96, 30.87, 29.14, 16.32, 18.35, 17.32, 20.6, 19.45, 21.83, 24.5, 23.12 };
                var note_freqs = new double[9, ker.Length];
                for (int i = 0; i < 9; i++)
                {
                    for (int k = 0; k < ker.Length; k++)
                    {
                        note_freqs[i, k] = ker[k] * Math.Pow(2, i);
                    }
                }

                var X_mat = new Complex[locations.Count, X_len];
                int m = locations.Count;
                var window = Signal.Windows.Flattop(window_length);
                for (int i = 0; i < m - 1; i++)
                {
                    var sig = new double[window_length];
                    var part = song.Skip(locations[i]).Take(window_length - 1).ToArray();
                    for (int k = 0; k < part.Length; k++)
                    {
                        sig[k] = window[k] * part[k];
                    }
                    var Xin = FFT.PerformFFT(sig);
                    var X = Xin.Take(X_len).ToArray();
                    for (int l = 0; l < X_len; l++)
                    {
                        X_mat[i, l] = X[j];
                    }
                }

                for (int i = 0; i < m - 1; i++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        var maxi = GetMaxValue(X_mat, i);
                        var temp = Array.IndexOf(X_mat, maxi);
                        letter[i, k] = temp;
                        X_mat[i, temp] = 0;
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        var maxi = GetMaxValue(X_mat, i);
                        var temp = Array.IndexOf(X_mat, maxi);
                        number[i, k] = temp;
                        X_mat[i, temp] = 0;
                    }
                }

                for (int i = 0; i < m - 1; i++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        number[i, k] = (int)Math.Round(note_freqs[letter[i, k], number[i, k]]);
                    }
                }

                for (int i = 0; i < m - 1; i++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        Console.Write($"{number[i, k]} ");
                    }
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while processing audio: {ex.Message}");
            }
        }

        private static Complex GetMaxValue(Complex[,] array, int row)
        {
            Complex max = array[row, 0];
            for (int i = 1; i < array.GetLength(1); i++)
            {
                if (array[row, i].Real > max.Real)
                {
                    max = array[row, i];
                }
            }
            return max;
        }
    }

    public class WavFile
    {
        private readonly Stream _stream;

        public WavFile(Stream stream)
        {
            _stream = stream;
        }

        public int SampleRate { get; private set; }
        public short NumChannels { get; private set; }
        public short BitsPerSample { get; private set; }

        public List<double[]> Read()
        {
            List<double[]> output = new List<double[]>();
            using (var reader = new BinaryReader(_stream))
            {
                string chunkId = new string(reader.ReadChars(4));
                int chunkSize = reader.ReadInt32();
                string format = new string(reader.ReadChars(4));

                string subchunk1Id = new string(reader.ReadChars(4));
                int subchunk1Size = reader.ReadInt32();
                NumChannels = reader.ReadInt16();
                SampleRate = reader.ReadInt32();
                reader.ReadInt32(); // ByteRate
                reader.ReadInt16(); // BlockAlign
                BitsPerSample = reader.ReadInt16();

                string subchunk2Id = new string(reader.ReadChars(4));
                int subchunk2Size = reader.ReadInt32();

                int bytesPerSample = BitsPerSample / 8;
                int numSamples = subchunk2Size / bytesPerSample;

                for (int i = 0; i < numSamples; i++)
                {
                    if (BitsPerSample == 16)
                    {
                        double[] samples = new double[NumChannels];
                        for (int channel = 0; channel < NumChannels; channel++)
                        {
                            short sample = reader.ReadInt16();
                            samples[channel] = sample / (double)short.MaxValue;
                        }
                        output.Add(samples);
                    }
                    else if (BitsPerSample == 32)
                    {
                        double[] samples = new double[NumChannels];
                        for (int channel = 0; channel < NumChannels; channel++)
                        {
                            int sample = reader.ReadInt32();
                            samples[channel] = sample / (double)int.MaxValue;
                        }
                        output.Add(samples);
                    }
                    else
                    {
                        throw new NotSupportedException($"Unsupported bits per sample: {BitsPerSample}");
                    }
                }
            }
            return output;
        }
    }

    public class Signal
    {
        public static (object[], Dictionary<string, object>) FindPeaks(double[] data, Dictionary<string, object> kwargs = null)
        {
            var result = new object[1];
            result[0] = new object();
            var properties = new Dictionary<string, object>();

            double[] prominences;
            if (kwargs != null && kwargs.ContainsKey("prominence"))
            {
                prominences = (double[])kwargs["prominence"];
            }
            else
            {
                prominences = ComputeProminence(data);
            }

            result = GetPeaks(data, prominences);

            properties["prominences"] = prominences;

            return (result, properties);
        }

        private static double[] ComputeProminence(double[] data)
        {
            var n = data.Length;
            var prominences = new double[n];
            var left_bounds = new double[n];
            var right_bounds = new double[n];
            for (var i = 0; i < n; i++)
            {
                var data_i = data[i];
                var j = i;
                while (j >= 0 && data[j] <= data_i)
                {
                    j--;
                }
                left_bounds[i] = j;
                j = i;
                while (j < n && data[j] <= data_i)
                {
                    j++;
                }
                right_bounds[i] = j;
            }
            for (var i = 0; i < n; i++)
            {
                var h_i = data[i];
                var left = (int)left_bounds[i];
                var right = (int)right_bounds[i];
                var h_min = Math.Min(data[left], data[right]);
                prominences[i] = Math.Max(h_i - h_min, 0);
            }
            return prominences;
        }

        private static object[] GetPeaks(double[] data, double[] prominences)
        {
            var peaks = new List<object>();
            for (var i = 0; i < data.Length; i++)
            {
                var prominence = prominences[i];
                if (prominence > 0)
                {
                    peaks.Add((double)i);
                }
            }
            return peaks.ToArray();
        }

        public class Windows
        {
            public static double[] Flattop(int length)
            {
                var window = new double[length];
                var a0 = 0.21557895;
                var a1 = 0.41663158;
                var a2 = 0.277263158;
                var a3 = 0.083578947;
                var a4 = 0.006947368;
                var twopi = 8 * Math.Atan(1);
                var N = length - 1;
                for (var n = 0; n <= N; n++)
                {
                    window[n] = a0 - a1 * Math.Cos((2 * Math.PI * n) / N) + a2 * Math.Cos((4 * Math.PI * n) / N) - a3 * Math.Cos((6 * Math.PI * n) / N) + a4 * Math.Cos((8 * Math.PI * n) / N);
                }
                return window;
            }
        }
    }

    public class FFT
    {
        public static Complex[] PerformFFT(double[] data)
        {
            try
            {
                var N = data.Length;
                var X = new Complex[N];

                if (N == 0)
                {
                    throw new ArgumentException("Input array cannot be empty.");
                }

                if (N == 1)
                {
                    X[0] = data[0];
                    return X;
                }

                var X_even = new double[N / 2];
                var X_odd = new double[N / 2];

                for (int i = 0; i < N / 2; i++)
                {
                    X_even[i] = data[2 * i];
                    X_odd[i] = data[2 * i + 1];
                }

                var Y_even = PerformFFT(X_even);
                var Y_odd = PerformFFT(X_odd);

                var twiddle = new Complex[N];
                for (int i = 0; i < N / 2; i++)
                {
                    double angle = -2 * Math.PI * i / N;
                    if (N != 0 && angle != 0)
                    {
                        var t = Complex.FromPolarCoordinates(1, angle);
                        twiddle[i] = t * Y_odd[i];
                        twiddle[i + N / 2] = t * Y_odd[i];
                    }
                    else
                    {
                        twiddle[i] = Complex.Zero;
                        twiddle[i + N / 2] = Complex.Zero;
                    }
                }

                for (int i = 0; i < N / 2; i++)
                {
                    X[i] = Y_even[i] + twiddle[i];
                    X[i + N / 2] = Y_even[i] - twiddle[i + N / 2];
                }

                return X;
            }
            catch (Exception ex)
            {
                // Log exception details
                Console.WriteLine($"Exception in PerformFFT: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw; // Re-throw the exception to propagate it further
            }
        }
    }

}
