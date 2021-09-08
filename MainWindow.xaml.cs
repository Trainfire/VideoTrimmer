﻿using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Path = System.IO.Path;

namespace VideoTrimmer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MediaTimeline mediaPlayerTimeline = new MediaTimeline();
        public MediaClock mediaPlayerClock;
        public bool SliderUpdatesPossible = true;

        public VideoProcessing videoProcessing = new VideoProcessing();

        public MainWindow()
        {
            InitializeComponent();

            // Updating the "About" footer
            aboutFooter.Content = Globals.company + " " + Globals.customVersion;

            // Checking if there is a file path passed through command line arguments (e.g. "Open with...")
            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.Length>1) {
                // check if it has a compatible extension
                if (videoProcessing.CheckIfFileIsAccepted(arguments[1]))
                {
                    Console.WriteLine("File passed through arguments has been recognized!");
                    OpenSelectedFile(arguments[1]);
                }
                else
                {
                    Console.WriteLine("File passed through arguments has not been recognized.");
                }
            }
        }

        // Used to enable or disable editable fields
        private void SetFieldsLockStatus(bool NewLockStatus)
        {
            trimVideoButton.IsEnabled = NewLockStatus;
            timecodeStart.IsEnabled = NewLockStatus;
            timecodeEnd.IsEnabled = NewLockStatus;
            removeAudio.IsEnabled = NewLockStatus;
            recompressFile.IsEnabled = NewLockStatus;
            PlayPauseButton.IsEnabled = NewLockStatus;
            TimelineSlider.IsEnabled = NewLockStatus;

            // if the form is unlocking then enable the recompression options depending on the checkbox value. If the form is locking, then just disable everything.
            if (NewLockStatus == true) RecompressFile_ValueChanged(null, null);
            else
            {
                DesiredFileSize.IsEnabled = NewLockStatus;
                DesiredFileSizeLabel.IsEnabled = NewLockStatus;
            }

            return;
        }

        // Fired whenever user checks or unchecks the "Recompess file" checkbox
        private void RecompressFile_ValueChanged(object sender, RoutedEventArgs e)
        {
            bool NewLockStatus = (bool)recompressFile.IsChecked;
            DesiredFileSize.IsEnabled = NewLockStatus;
            DesiredFileSizeLabel.IsEnabled = NewLockStatus;
        }

        // Reset window to state without file selected
        private void ResetFilePicker()
        {
            fileNameLabel.Content = "No video selected";
            fileNameLabel.ToolTip = "No video selected";
            timecodeStart.Text = "00:00:00";
            timecodeEnd.Text = "00:00:00";
            videoProcessing = new VideoProcessing();
            SetFieldsLockStatus(false);

            return;
        }

        // Analyzing a file selected via drag and dopping or using "Open File" dialog
        private void OpenSelectedFile(string FileToOpen)
        {
            Console.WriteLine("Opening file: " + FileToOpen);

            if (videoProcessing.LoadFile(FileToOpen) == true)
            {
                Console.WriteLine("File opened successfully");
            } else
            {
                Console.WriteLine("Couldn't open file");
            }


            // Unlock editable fields
            SetFieldsLockStatus(true);

            // Get video duration and paste it into "End" timecode TextBox
            timecodeStart.Text = "00:00:00";
            timecodeEnd.Text = videoProcessing.GetDuration().ToString(@"hh\:mm\:ss");

            mediaPlayerTimeline.Source = new Uri(videoProcessing.GetFilePath());
            mediaPlayerClock = mediaPlayerTimeline.CreateClock();
            MediaPlayer.Clock = mediaPlayerClock;

            mediaPlayerClock.CurrentTimeInvalidated += delegate (object sender, EventArgs e)
            {
                MediaPlayerOnTimeChanged(sender, e);
            };

            TimelineSlider.AddHandler(MouseLeftButtonUpEvent,
                      new MouseButtonEventHandler(SliderValueManuallyChanged),
                      true);
            TimelineSlider.AddHandler(MouseLeftButtonDownEvent,
                      new MouseButtonEventHandler(SliderInteractionStarted),
                      true);

            // If file path is long, trim it
            String FileNameToDisplay = "";
            if (videoProcessing.GetFilePath().Length > 40) FileNameToDisplay = videoProcessing.GetFileRoot() + "...\\" + videoProcessing.GetFileName();
            else FileNameToDisplay = videoProcessing.GetFilePath();

            // Update display
            fileNameLabel.Content = "✔️ " + FileNameToDisplay;
            fileNameLabel.ToolTip = videoProcessing.GetFilePath();

            return;
        }

        // Called when clicked on the "Select File" button
        private void ButtonFileOpen_Click(object sender, RoutedEventArgs e)
        {
            string FilterParams = "Video files (" + videoProcessing.GetSupportedVideoFormats() + ")|" + videoProcessing.GetSupportedVideoFormats();
            FilterParams += "|Audio files (" + videoProcessing.GetSupportedAudioFormats() + ")|" + videoProcessing.GetSupportedAudioFormats();

            var fileDialog = new OpenFileDialog
            {
                Filter = FilterParams,
                RestoreDirectory = true
            };
            var result = fileDialog.ShowDialog();

            switch (result)
            {
                case System.Windows.Forms.DialogResult.OK:
                    OpenSelectedFile(fileDialog.FileName);
                    break;
                case System.Windows.Forms.DialogResult.Cancel:
                default:
                    break;
            }
        }

        // validates values in timecode text boxes
        private void Timecode_LostFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox senderTextBox = (System.Windows.Controls.TextBox)sender;

            // check if a value is a valid TimeSpan
            if (TimeSpan.TryParse(senderTextBox.Text, out _))
            {
                // check if the value is shorter or equal to FileDuration
                if (TimeSpan.Parse(senderTextBox.Text) <= videoProcessing.GetDuration())
                {
                    // make sure that none of the TextBoxes is null
                    if (timecodeStart == null | timecodeEnd == null)
                    {
                        // one of the fields has not been initialized, do nothing
                        return;
                    }
                    else
                    {
                        // make sure the starting timecode is smaller than the ending timecode
                        if (TimeSpan.Parse(timecodeStart.Text) < TimeSpan.Parse(timecodeEnd.Text))
                        {
                            // accept the value, but parse it to make sure the leading zeros are there
                            senderTextBox.Text = TimeSpan.Parse(senderTextBox.Text).ToString();
                            return;
                        }
                    }
                }
            }

            // if everything failed -- undo editing field
            _ = Dispatcher.BeginInvoke(new Action(() => senderTextBox.Undo()));

            return;
        }


        // validates values in the DesiredFileSize text box
        private void DesiredFileSize_LostFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox senderTextBox = (System.Windows.Controls.TextBox)sender;

            int newIntValue;

            // check if a value is a valid int
            if (int.TryParse(senderTextBox.Text.Split(',')[0], out newIntValue))
            {
                // accept the value, but parse it to make sure the leading zeros are not there
                senderTextBox.Text = newIntValue.ToString();
                return;
            }
            if (int.TryParse(senderTextBox.Text.Split('.')[0], out newIntValue))
            {
                // accept the value, but parse it to make sure the leading zeros are not there
                senderTextBox.Text = newIntValue.ToString();
                return;
            }

            if (senderTextBox.Text == "")
            {
                senderTextBox.Text = "0";
                return;
            }

            // if everything failed -- undo editing field
            _ = Dispatcher.BeginInvoke(new Action(() => senderTextBox.Undo()));

            return;
        }

        // main logic - "TRIM VIDEO" button click
        private void ButtonTrimVideo_Click(object sender, RoutedEventArgs e)
        {
            // Make sure the file exists
            if (!videoProcessing.DoesTheFileExist())
            {
                // TODO: Replace with a custom error window
                System.Windows.MessageBox.Show("Source video has been moved or deleted!");

                // Reset window and block inputs
                ResetFilePicker();

                return;
            }

            // establish Start and Duration for the needs of the console command
            TimeSpan Start = TimeSpan.Parse(timecodeStart.Text);
            TimeSpan End = TimeSpan.Parse(timecodeEnd.Text);

            int DesiredFileSizeInt;
            Int32.TryParse(DesiredFileSize.Text, out DesiredFileSizeInt);

            // set process settings
            videoProcessing.SetParameters(Start, End, (bool)removeAudio.IsChecked, (bool)recompressFile.IsChecked, DesiredFileSizeInt);

            // open the progress window that executes the main process
            ProgressWindow progressWindow = new ProgressWindow();
            progressWindow.Owner = this;
            progressWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            progressWindow.ShowDialog();

        }

        // Displays "About" window
        // TODO: Add custom "About" window
        public void ButtonShowAbout_Click(object sender, RoutedEventArgs e)
        {
            /*
            // CURRENTLY UNUSED
            string Text;
            Version version = Assembly.GetExecutingAssembly().GetName().Version;

            Text = "Rendeer " + version.Major + "." + version.Minor + ".190512";
            System.Windows.Forms.MessageBox.Show(Text);
            */
        }

        // Fired whenever an object is dragged over the app window
        private void Window_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            string[] files = (string[])(e.Data.GetData("FileDrop", false));
            if (files != null)
            {
                // get first file
                string draggedFile = files[0];

                // check if it has a compatible extension
                if (videoProcessing.CheckIfFileIsAccepted(draggedFile))
                {
                    Console.WriteLine("Dragged file recognized!");
                    Background = new SolidColorBrush(Color.FromArgb(0xFF, 0, 0x77, 0));
                }
            }
            else
            {
                Console.WriteLine("Dragged file not recognized.");
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x88, 0, 0));
            }
        }


        // Fired whenever an object is dropped over the app window
        private void Window_DragDrop(object sender, System.Windows.DragEventArgs e)
        {
            string[] files = (string[])(e.Data.GetData("FileDrop", false));
            
            // restore default background
            Background = Brushes.Black;

            // in case the dropped object is not a file
            if (files == null) return;

            // get the first file, ignore the rest
            string droppedFile = files[0];
            Console.WriteLine(Path.GetExtension(droppedFile));
            
            // check if it has a compatible extension
            if (videoProcessing.CheckIfFileIsAccepted(droppedFile))
            {
                Console.WriteLine("Dropped file recognized!");
                OpenSelectedFile(droppedFile);
            }
            else
            {
                Console.WriteLine("Dropped file not recognized.");
            }
        }

        // Fired whenever an object that has been dragged over the app window has left the window
        private void Window_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            Background = Brushes.Black;
        }

        // Methods related to the video player

        // When the media opens, initialize the "Seek To" slider maximum value
        // to the total number of miliseconds in the length of the media clip.
        private void MediaPlayerOnMediaOpened(object sender, EventArgs e)
        {
            TimelineSlider.Maximum = MediaPlayer.NaturalDuration.TimeSpan.TotalMilliseconds;
            TimelineSlider.Value = 0;
            mediaPlayerClock.Controller.Pause();
        }

        // This is the automatic update as video plays
        private void MediaPlayerOnTimeChanged(object sender, EventArgs e)
        {
            if (SliderUpdatesPossible) TimelineSlider.Value = mediaPlayerClock.CurrentTime.Value.TotalMilliseconds;
        }

        // User interaction - button clicked
        private void OnPlayPauseButtonClicked(object sender, RoutedEventArgs e)
        {
            if (mediaPlayerTimeline.Source == null) return;

            if (MediaPlayer.IsLoaded)
            {
                if (MediaPlayer.Position == MediaPlayer.NaturalDuration) mediaPlayerClock.Controller.Seek(TimeSpan.Zero, TimeSeekOrigin.BeginTime);
                if (mediaPlayerClock.IsPaused)
                {
                    PlayPauseButton.Content = "❚❚";
                    mediaPlayerClock.Controller.Resume();
                }
                else
                {
                    PlayPauseButton.Content = "▶";
                    mediaPlayerClock.Controller.Pause();
                }
            }
        }

        // User clicked on the slider, so let's disable automatic slider updates
        private void SliderInteractionStarted(object sender, MouseButtonEventArgs e)
        {
            SliderUpdatesPossible = false;
        }

        // User finished interaction with the slider, let's update the video
        private void SliderValueManuallyChanged(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            bool wasPaused = mediaPlayerClock.IsPaused;
            if (wasPaused == false) mediaPlayerClock.Controller.Pause();
            int SliderValue = (int)TimelineSlider.Value;
            TimeSpan ts = new TimeSpan(0, 0, 0, 0, SliderValue);
            mediaPlayerClock.Controller.Seek(ts, TimeSeekOrigin.BeginTime);
            if (wasPaused == false) mediaPlayerClock.Controller.Resume();
            SliderUpdatesPossible = true;
        }
    }
}
