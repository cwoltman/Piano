using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

#if WINDOWS
using System.Media;
#endif

namespace Piano
{
    public partial class MainPage : ContentPage
    {
        private bool isRecording = false;
        private string? recordingFilePath = null;

        public MainPage()
        {
            InitializeComponent();
        }

        private void OnRecordButtonClicked(object sender, EventArgs e)
        {
            if (!isRecording)
            {
                // Start recording
                recordingFilePath = "recorded.wav";
                // Code to start recording
                recordButton.Text = "Press to Stop Recording";
                isRecording = true;
            }
            else
            {
                // Stop recording
                // Code to stop recording
                recordButton.Text = "Press to Record";
                isRecording = false;
            }
        }

        private async void OnPlaybackButtonClicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(recordingFilePath))
            {
                // Playback recording
                // Assuming recordingFilePath contains the path to the recorded audio file
                playbackStatusLabel.Text = "Playing back";
                await PlayAudio(recordingFilePath);
                playbackStatusLabel.Text = "";
            }
            else
            {
                // Notify user that no recording exists
                await DisplayAlert("Alert", "No recording exists.", "OK");
            }
        }

        // Platform-specific implementation for audio playback
        private async Task PlayAudio(string filePath)
        {
#if WINDOWS
            try
            {
                using (var player = new SoundPlayer(filePath))
                {
                    player.Play();
                    await Task.Delay(100); // Add a short delay to ensure playback starts
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error playing audio: {ex.Message}");
            }
#else
            // Implement audio playback logic specific to other platforms here
#endif
        }
    }
}
