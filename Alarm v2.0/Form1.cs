using System;
using System.IO;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WMPLib;

namespace Alarm_v2._0
{
    public partial class Main : Form
    {

        //These are properties that will be mapped straight onto the GUI componenets
        #region GUI Values
        private DateTime _alarmTime = DateTime.Now;
        private FileInfo _alarmSong;
        private bool _repeatSong = false;
        private int _repeatSongTimes = 1;
        private bool _increaseVolume = false;
        private int _increaseVolumeIntervalSeconds = 1;
        private int _startVolume = 100;
        private bool _narrateTime = false;
        private int _narrateTimeIntervalMinutes = 0;
        #endregion

        //We need these properties for internal machinery
        #region Internal working values
        private WindowsMediaPlayer _mediaPlayer;
        private AlarmTimer _alarmObject;
        private int _playCount = 0;
        private SpeechSynthesizer _synth;
        private Task _narrationTask;
        private CancellationTokenSource _tokenSource;
        private CancellationToken _cancelToken;
        #endregion


        public Main()
        {
            InitializeComponent();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
        }

        //Others
        private void CheckInput(object sender, KeyPressEventArgs e)
        {
            //This method is used to check wether or not a keypress is numerical
            //Used to make sure we can only insert numbers.
            const char delete = (char)8;
            e.Handled = !Char.IsDigit(e.KeyChar) && e.KeyChar != delete;
        }
        private void btnBrowseSong_Click(object sender, EventArgs e)
        {
            //Method used to browse for an MP3 file
            using (var od = new OpenFileDialog())
            {
                od.Filter = "MP3 Files (*.mp3)|*.mp3";
                od.ShowDialog();
                if (!od.FileName.Equals(""))
                {
                    _alarmSong = new FileInfo(od.FileName);
                    lblSongtitle.Text = _alarmSong.FullName.Substring(_alarmSong.FullName.LastIndexOf(@"\", StringComparison.Ordinal) + 1);
                    btnSet.Enabled = true;
                }
            }
        }

        #region Setting the alarm
        private AlarmTimer CreateAlarmTimer(DateTime alarmtime)
        {
            //Factory method for creating a timer object
            //We instantiate a new AlarmTimer object
            //This will raise an event when the alarm time is hit (time to wake up ;-))
            //We hook a procedure to the AlarmHit event so we can do some work when the alarm is hit
            //Return the object
            var t = new AlarmTimer(alarmtime);
            t.AlarmHit += AlarmHit;
            return t;
        }
        private void btnSet_Click(object sender, EventArgs e)
        {
            //When we click btnSet it means that we
            //1) want to set our alarm
            //2) Made a mistake and changed options and pressed set again

            //If there was an alarm set, and timenarration is on, we need to turn it off
            if (_narrationTask != null)
                _tokenSource.Cancel();

            //Just to be sure, delete the media player too
            if (_mediaPlayer != null)
                _mediaPlayer.close();

            //If there is a timer running stop it
            if (_alarmObject != null)
                _alarmObject.Exit();

            //Use the factorymethod to create a timerobject and start it
            _alarmObject = CreateAlarmTimer(_alarmTime);
            _alarmObject.Start();
        }
        private void AlarmHit(object sender, EventArgs e)
        {
            //This is what happens when the alarm is hit, and we need to do our work

            //We want to play our song
            PlaySong(_alarmSong);



            //If time needs to be narrated, invoke the method
            if (_narrateTime)
                NarrateCurrentTime();
        }
        private void timeSelector_ValueChanged(object sender, EventArgs e)
        {
            //When we change the datetime our alarm has to ring
            //We update this in the private property
            _alarmTime = DateTime.Now > ((DateTimePicker)sender).Value ?
                ((DateTimePicker)sender).Value.AddDays(1.0) : ((DateTimePicker)sender).Value;
        }

        #endregion

        #region Playing a song
        //Media player
        private WindowsMediaPlayer CreateNewPlayer()
        {
            //Factory method to create a mediaplayer object
            //Also, hook on the event raised when the playerstatus is changed (stopped, paused,..)

            //Remove the old player, which might still be playing!
            if (_mediaPlayer != null)
                _mediaPlayer.close();

            var player = new WindowsMediaPlayer();
            player.StatusChange += PlayerStatusChanged;

            return player;
        }
        private void PlaySong(FileInfo song)
        {
            //Create a mediaplayer object, put in the song and make it play!
            _mediaPlayer = CreateNewPlayer();
            _mediaPlayer.settings.autoStart = false;
            _mediaPlayer.settings.volume = _startVolume;
            _mediaPlayer.URL = song.FullName;
            _mediaPlayer.controls.play();

            //if we need to repeat the song x times we need to initiate our counter on 1.
            if (_repeatSong)
            {
                _playCount = 1;
            }


            //If we don't need to raise the volume gradually we can immediatly return!
            if (!_increaseVolume) return;

            //Create a thread that will update the mediaplayer volume
            //Calculate how much miliseconds it should take to increase volume by 1
            float volumeToIncrease = 100 - _startVolume;
            float toIncreasePerSecond = volumeToIncrease / _increaseVolumeIntervalSeconds;
            float factor = 1 / toIncreasePerSecond;
            int oneVolumePerMiliSeconds = Int32.Parse(Math.Round((factor * 1000), 0).ToString());

            //We create a new task which will gradually raise the volume of the mediaplayer.
            var increaseTask = new Task(delegate
                {
                    var increaseIterationCountdown = volumeToIncrease;
                    while (increaseIterationCountdown > 0)
                    {
                        Thread.Sleep(oneVolumePerMiliSeconds);
                        IncreasePlayerVolumeByOne();
                    }
                });
            increaseTask.Start();
        }

        //Playercontrols
        private void IncreasePlayerVolumeByOne()
        {
            //If this method is called from another thread, reinvoke the method
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(IncreasePlayerVolumeByOne));
            }
            else
            {
                _mediaPlayer.settings.volume++;
            }
        }

        //Media player events
        private void PlayerStatusChanged()
        {
            //If the song stopped playing AND we need to replay
            //Then we make the player repeat the song!
            if (_mediaPlayer.playState == WMPPlayState.wmppsStopped
               && _repeatSong && _playCount < _repeatSongTimes)
            {
                _playCount++;
                _mediaPlayer.controls.play();
            }
        }
        #endregion

        #region Repeating a song
        private void cbxRepeatSong_CheckedChanged(object sender, EventArgs e)
        {
            _repeatSong = ((CheckBox)sender).Checked;
            txtRepeatTimes.Enabled = _repeatSong;
        }
        private void txtRepeatTimes_TextChanged(object sender, EventArgs e)
        {
            //If the value is blank after backspasing we make the value zero, instead of raising an error.
            try
            {
                _repeatSongTimes = string.IsNullOrWhiteSpace(((TextBox)sender).Text) ? 0 : Int32.Parse(((TextBox)sender).Text);
            }
            catch (Exception)
            {
                MessageBox.Show("Not a valid value!" + ((TextBox)sender).Text);
            }

        }
        #endregion

        #region Telling Time
        private void NarrateCurrentTime()
        {
            // Initialize a new instance of the SpeechSynthesizer.
            _synth = new SpeechSynthesizer();

            // Configure the audio output. 
            _synth.SetOutputToDefaultAudioDevice();
            _synth.Rate = -3;
            _synth.Volume = 100;

            // Speak a string asynchronously.
            _tokenSource = new CancellationTokenSource();
            _cancelToken = _tokenSource.Token;

            //We start a new task which can be cancelled.
            //This makes sure that when we reset our alarm in a same session and the alarm was already ringing, we can now safely shut it down.
            //We create a new task, and just pass along the cancellationtoken created above.
            //We can then call the tokensource.cancel. This makes sure that all threads that use that specific token (only one here) will be cancelled.
            _narrationTask = Task.Factory.StartNew(() =>
                {
                    while (true)
                    {
                        if (_cancelToken.IsCancellationRequested)
                        {
                        }
                        else
                        {
                            Thread.Sleep(_narrateTimeIntervalMinutes * 60000);
                            _synth.Speak("It is now " + DateTime.Now.Minute + " minutes past " + (DateTime.Now.Hour % 12));
                        }
                    }
                }, _cancelToken);
        }
        private void cbxNarrateTime_CheckedChanged(object sender, EventArgs e)
        {
            _narrateTime = ((CheckBox)sender).Checked;
            txtNarrateInterval.Enabled = _narrateTime;
        }
        private void txtNarrateInterval_TextChanged(object sender, EventArgs e)
        {
            //Same as above..
            try
            {
                _narrateTimeIntervalMinutes = string.IsNullOrWhiteSpace(((TextBox)sender).Text) ? 0 : Int32.Parse(((TextBox)sender).Text);
            }
            catch (Exception)
            {
                MessageBox.Show("Not a valid value!" + ((TextBox)sender).Text);
            }
        }
        #endregion

        #region Increasing Volume
        private void cbxGradualVolume_CheckedChanged(object sender, EventArgs e)
        {
            _increaseVolume = ((CheckBox)sender).Checked;
            txtIncreaseInterval.Enabled = _increaseVolume;
            tbStart.Enabled = _increaseVolume;

            _startVolume = !_increaseVolume ? 100 : tbStart.Value;
        }
        private void txtIncreaseInterval_TextChanged(object sender, EventArgs e)
        {
            try
            {
                _increaseVolumeIntervalSeconds = string.IsNullOrWhiteSpace(((TextBox)sender).Text) ? 0 : Int32.Parse(((TextBox)sender).Text);
            }
            catch (Exception)
            {
                MessageBox.Show("Not a valid value!" + ((TextBox)sender).Text);
            }
        }
        private void tbStart_ValueChanged(object sender, EventArgs e)
        {
            _startVolume = ((TrackBar)sender).Value;
        }
        #endregion


    }
}
