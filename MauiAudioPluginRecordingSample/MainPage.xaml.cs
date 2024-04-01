using Plugin.Maui.Audio;

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
                // Update button text when recording stops
                UpdateButtonText();
            }
        }

        private void UpdateButtonText()
        {
            CounterBtn.Text = _audioRecorder.IsRecording ? "Press to stop Recording" : "Press to Record";
        }
    }
}
