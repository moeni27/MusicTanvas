using Microsoft.Win32;
using NAudio.Wave;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Python.Runtime;
using System.Windows.Shapes;
using Tanvas.TanvasTouch.Resources;
using Tanvas.TanvasTouch.WpfUtilities;

namespace MusicTanvas
{
    public partial class MainWindow : Window
    {
        TSprite mySprite;
        TanvasTouchViewTracker viewTracker;

        TView myView
        {
            get
            {
                return viewTracker.View;
            }
        }

        TanvasTouchViewTracker viewSingleTracker;

        TView mySingleView
        {
            get
            {
                return viewSingleTracker.View;
            }
        }


        private const string AudioFolder = "Assets/Audio";
        private const int MaxFiles = 4;
        private TimeSpan? referenceDuration = null;
        private bool userInitiatedStop = false;
        private bool isStopped = true;
        private float volume = 1.0f;
        private bool playbackStoppedNaturally = true;
        private bool mfccMode = true;

        private DispatcherTimer sphereUpdateTimer;
        private DispatcherTimer colorUpdateTimer;
        private DispatcherTimer stripesUpdateTimer;

        private List<AudioFileReader> audioStreams = new List<AudioFileReader>();
        private List<WaveOut> audioOutputs = new List<WaveOut>();
        private List<long> streamPosition = new List<long>();

        private MeshGeometry3D mesh1;
        private MeshGeometry3D mesh2;
        private MeshGeometry3D mesh3;
        private MeshGeometry3D mesh4;

        private int tDiv = 30;
        private int pDiv = 30;
        private double baseRadius = 1;

        private byte[][] buffers;

        private int centroidIndex = 0;
        private int mfccIndex = 0;

        private byte[] buffer1;
        private byte[] buffer2;
        private byte[] buffer3;
        private byte[] buffer4;
        private int bufferLength = 1024;

        private float[] ampValues;
        private float[] test;
        private List<List<float>> amplitudeBuffer = new List<List<float>>();

        private const int smoothingWindowSize = 20;

        private string[] singleTrackAudio;
        private WaveStream streamVoice;
        private WaveOut outVoice;

        private WaveStream streamBass;
        private WaveOut outBass;

        private WaveStream streamDrum;
        private WaveOut outDrum;

        private WaveStream streamOther;
        private WaveOut outOther;

        private List<string> audioFilePaths = new List<string>();
        private List<string> predictedInstruments = new List<string>();
        private List<string> instrumentList = new List<string>();

        private List<List<float>> spectralCentroid = new List<List<float>>();
        private List<List<float>> onSetDetection = new List<List<float>>();
        private float maxCentroid;

        private List<List<List<float>>> mfccMatrix = new List<List<List<float>>>();
        private List<List<List<float>>> mfccMatrixPrediction = new List<List<List<float>>>();
        private List<List<List<float>>> chromaMatrix = new List<List<List<float>>>();

        // Instrument-Color mapping dictionary
        private readonly Dictionary<string, Color> InstrumentColorMap = new Dictionary<string, Color>
        {
            {"cello", Color.FromRgb(0, 0, 139)}, // Deep Blue
            {"clarinet", Color.FromRgb(0, 100, 0)}, // Dark Green
            {"flute", Color.FromRgb(173, 216, 230)}, // Light Blue
            {"acoustic guitar", Color.FromRgb(255, 165, 0)}, // Orange
            {"electric guitar", Color.FromRgb(148, 0, 211)}, // Violet
            {"organ", Color.FromRgb(75, 0, 130)}, // Deep Purple
            {"piano", Colors.DarkGray}, // Special case for gradient
            {"saxophone", Color.FromRgb(139, 69, 19)}, // Brown
            {"trumpet", Color.FromRgb(247, 255, 0)}, // Light Yellow
            {"violin", Color.FromRgb(238, 130, 238)}, // Light Violet
            {"voice", Color.FromRgb(255, 215, 0)} // Gold
        };

        public MainWindow()
        {
            Tanvas.TanvasTouch.API.Initialize();
            InitializeComponent();
            buffers = new byte[4][];
            float[] ampValues = { 1.0f, 1.0f, 1.0f, 1.0f };
            float[] test = { 1.0f, 1.0f, 1.0f, 1.0f };
            InitializePython();
            ClearAudioFolder();
            InitializeStreamPosition(4);
            spectralCentroid = new List<List<float>>(Enumerable.Range(0, 4).Select(_ => new List<float>()));
            onSetDetection = new List<List<float>>(Enumerable.Range(0, 4).Select(_ => new List<float>()));
        }

        private static string GetPythonDllPath(string configFilePath)
        {
            foreach (var line in System.IO.File.ReadLines(configFilePath))
            {
                if (line.StartsWith("pythonDllPath="))
                {
                    return line.Substring("pythonDllPath=".Length);
                }
            }
            throw new Exception("pythonDllPath not found in config file.");
        }

        private void InitializePython()
        {
            string configFilePath = @"..\..\..\config.txt";
            string pythonDllPath = GetPythonDllPath(configFilePath);
            Runtime.PythonDLL = pythonDllPath;
            PythonEngine.Initialize();
        }

        public void InitializeStreamPosition(int numberOfStreams)
        {
            streamPosition = new List<long>(new long[numberOfStreams]);
        }


        private void ClearAudioFolder()
{
    string audioFolderPath = "Assets/audio";

    try
    {
        // Check if the folder exists
        if (Directory.Exists(audioFolderPath))
        {
            // Delete all files within the folder
            foreach (string file in Directory.GetFiles(audioFolderPath))
            {
                System.IO.File.Delete(file);
            }
            // Optionally, delete subdirectories if needed
            foreach (string dir in Directory.GetDirectories(audioFolderPath))
            {
                Directory.Delete(dir, true);
            }
        }
        else
        {
            // Create the audio folder if it does not exist
            Directory.CreateDirectory(audioFolderPath);
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error clearing audio folder: {ex.Message}");
    }
}

        public void onClickSingleBack(object sender, EventArgs e)
        {
            // Dispose the existing streams and outputs
            foreach (var output in audioOutputs)
            {
                output.Dispose();
            }
            foreach (var stream in audioStreams)
            {
                stream.Dispose();
            }

            audioStreams.Clear();
            audioOutputs.Clear();
            streamPosition.Clear();
            ClearAudioFolder();
            mfccMode = true;
            mfccIndex = 0;
            centroidIndex = 0;
            spectralCentroid.Clear();
            onSetDetection.Clear();
            maxCentroid = 0.0f;
            mfccMatrix.Clear();
            mfccMatrixPrediction.Clear();
            chromaMatrix.Clear();
            amplitudeBuffer.Clear();

            spectralCentroid = new List<List<float>>(Enumerable.Range(0, 4).Select(_ => new List<float>()));
            onSetDetection = new List<List<float>>(Enumerable.Range(0, 4).Select(_ => new List<float>()));

            mySingleView.RemoveAllSprites();
            myView.RemoveAllSprites();

            SingleTrackScreen.Visibility = Visibility.Hidden;
            SingleUploadScreen.Visibility = Visibility.Visible;
        }

        public void onClickMultiBack(object sender, EventArgs e)
        {
            // Dispose the existing streams and outputs
            foreach (var output in audioOutputs)
            {
                output.Dispose();
            }
            foreach (var stream in audioStreams)
            {
                stream.Dispose();
            }

            audioStreams.Clear();
            audioOutputs.Clear();
            streamPosition.Clear();
            ClearAudioFolder();
            mfccMode = true;
            mfccIndex = 0;
            centroidIndex = 0;
            spectralCentroid.Clear();
            onSetDetection.Clear();
            maxCentroid = 0.0f;
            mfccMatrix.Clear();
            mfccMatrixPrediction.Clear();
            chromaMatrix.Clear();
            amplitudeBuffer.Clear();
            instrumentList.Clear();
            predictedInstruments.Clear();

            spectralCentroid = new List<List<float>>(Enumerable.Range(0, 4).Select(_ => new List<float>()));
            onSetDetection = new List<List<float>>(Enumerable.Range(0, 4).Select(_ => new List<float>()));

            SelectedFilesListBox.Items.Clear();
            SelectedFilesListBox.Visibility = Visibility.Hidden;

            if (MultiOneScreen.Visibility == Visibility.Visible)
            {
                MultiOneScreen.Visibility = Visibility.Hidden;
            }
            else if (MultiTwoScreen.Visibility == Visibility.Visible)
            {
                MultiTwoScreen.Visibility = Visibility.Hidden;
            }
            else if (MultiThreeScreen.Visibility == Visibility.Visible)
            {
                MultiThreeScreen.Visibility = Visibility.Hidden;
            }
            else
            {
                MultiFourScreen.Visibility = Visibility.Hidden;
            }

            myView.RemoveAllSprites();

            UploadScreen.Visibility = Visibility.Visible;
        }

        public void onClickBack(object sender, EventArgs e)
        {
            if (UploadScreen.Visibility == Visibility.Visible)
            {
                UploadScreen.Visibility = Visibility.Hidden;
                SelectedFilesListBox.Items.Clear();
                SelectedFilesListBox.Visibility = Visibility.Hidden;
                ClearAudioFolder();
            }

            else if (SingleUploadScreen.Visibility == Visibility.Visible)
            {
                SingleUploadScreen.Visibility = Visibility.Hidden;
                ClearAudioFolder();
            }

            GenreScreen.Visibility = Visibility.Visible;
        }

        public void onClickPlayButton(object sender, EventArgs e)
        {
            userInitiatedStop = false;
            isStopped = false;

            if (playbackStoppedNaturally)
            {
                ResetStreamsAndOutputs(); // Reset the streams and outputs if playback stopped naturally and not by user
                if (SingleTrackScreen.Visibility == Visibility.Visible)
                {
                    UpdateStripesColor();
                }
                playbackStoppedNaturally = false;
            }

            var i = 0;
            foreach (var stream in audioStreams)
            {
                if (i < streamPosition.Count)
                {
                    stream.Position = streamPosition[i];
                }
                i++;
            }

            foreach (var output in audioOutputs)
            {
                if (output.PlaybackState == PlaybackState.Stopped)
                {
                    output.Play();
                }
            }


            if (MultiOneScreen.Visibility == Visibility.Visible)
            {
                buttonOnePlay.Visibility = Visibility.Hidden;
                buttonOneStop.Visibility = Visibility.Visible;
                sphereUpdateTimer.Start();
                colorUpdateTimer.Start();
                stripesUpdateTimer.Start();
                UpdateMultiStripesColor();
                UpdateMultiStripesColor();
            }
            else if (MultiTwoScreen.Visibility == Visibility.Visible)
            {
                buttonTwoPlay.Visibility = Visibility.Hidden;
                buttonTwoStop.Visibility = Visibility.Visible;
                sphereUpdateTimer.Start();
                colorUpdateTimer.Start();
                stripesUpdateTimer.Start();
                UpdateMultiStripesColor();
            }
            else if (MultiThreeScreen.Visibility == Visibility.Visible)
            {
                buttonThreePlay.Visibility = Visibility.Hidden;
                buttonThreeStop.Visibility = Visibility.Visible;
                sphereUpdateTimer.Start();
                colorUpdateTimer.Start();
                stripesUpdateTimer.Start();
                UpdateMultiStripesColor();
            }
            else if (MultiFourScreen.Visibility == Visibility.Visible)
            {
                buttonFourPlay.Visibility = Visibility.Hidden;
                buttonFourStop.Visibility = Visibility.Visible;
                sphereUpdateTimer.Start();
                colorUpdateTimer.Start();
                stripesUpdateTimer.Start();
                UpdateMultiStripesColor();
            }
            else
            {
                singlePlayButton.Visibility = Visibility.Hidden;
                singleStopButton.Visibility = Visibility.Visible;
                sphereUpdateTimer.Start();
                colorUpdateTimer.Start();
                stripesUpdateTimer.Start();
                UpdateStripesColor();
            }
        }

        private void ResetStreamsAndOutputs()
        {
            // Dispose the existing streams and outputs
            foreach (var output in audioOutputs)
            {
                output.Dispose();
            }
            foreach (var stream in audioStreams)
            {
                stream.Dispose();
            }

            audioStreams.Clear();
            audioOutputs.Clear();
            streamPosition.Clear();
            InitializeStreamPositionOne();

            // Reinitialize the streams and outputs with the same files
            foreach (var file in Directory.GetFiles(AudioFolder, "*.mp3").Concat(Directory.GetFiles(AudioFolder, "*.wav")))
            {
                var stream = new AudioFileReader(file);
                var output = new WaveOut();
                output.Init(stream);

                // Add event handler for PlaybackStopped event
                output.PlaybackStopped += OnPlaybackStopped;

                audioStreams.Add(stream);
                audioOutputs.Add(output);
            }
        }

        public void InitializeStreamPositionOne()
        {
            for (int i = 0; i < MaxFiles; i++)
            {
                streamPosition.Add(0);
            }
        }

        public void onClickStopButton(object sender, EventArgs e)
        {
            userInitiatedStop = true;
            var i = 0;

            foreach (var output in audioOutputs)
            {
                output.Stop();
            }

            foreach (var stream in audioStreams)
            {
                if (i < streamPosition.Count)
                {
                    streamPosition[i] = stream.Position;
                }
                i++;
            }

            if (MultiOneScreen.Visibility == Visibility.Visible)
            {
                buttonOnePlay.Visibility = Visibility.Visible;
                buttonOneStop.Visibility = Visibility.Hidden;
                sphereUpdateTimer.Stop();
                colorUpdateTimer.Stop();
                stripesUpdateTimer.Stop();

            }
            else if (MultiTwoScreen.Visibility == Visibility.Visible)
            {
                buttonTwoPlay.Visibility = Visibility.Visible;
                buttonTwoStop.Visibility = Visibility.Hidden;
                sphereUpdateTimer.Stop();
                colorUpdateTimer.Stop();
                stripesUpdateTimer.Stop();
            }
            else if (MultiThreeScreen.Visibility == Visibility.Visible)
            {
                buttonThreePlay.Visibility = Visibility.Visible;
                buttonThreeStop.Visibility = Visibility.Hidden;
                sphereUpdateTimer.Stop();
                colorUpdateTimer.Stop();
                stripesUpdateTimer.Stop();
            }
            else if (MultiFourScreen.Visibility == Visibility.Visible)
            {
                buttonFourPlay.Visibility = Visibility.Visible;
                buttonFourStop.Visibility = Visibility.Hidden;
                sphereUpdateTimer.Stop();
                colorUpdateTimer.Stop();
                stripesUpdateTimer.Stop();

            }
            else
            {
                singlePlayButton.Visibility = Visibility.Visible;
                singleStopButton.Visibility = Visibility.Hidden;
                sphereUpdateTimer.Stop();
                colorUpdateTimer.Stop();
                stripesUpdateTimer.Stop();
            }
            isStopped = true;
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (!userInitiatedStop)
            {
                playbackStoppedNaturally = true; // Set the flag

                foreach (var output in audioOutputs)
                {
                    output.Dispose();
                }

                foreach (var stream in audioStreams)
                {
                    stream.Dispose();
                }

                streamPosition.Clear();
                InitializeStreamPosition(4);

                if (MultiOneScreen.Visibility == Visibility.Visible)
                {
                    buttonOnePlay.Visibility = Visibility.Visible;
                    buttonOneStop.Visibility = Visibility.Hidden;
                    sphereUpdateTimer.Stop();
                    colorUpdateTimer.Stop();
                    stripesUpdateTimer.Stop();

                    centroidIndex = 0;
                    mfccIndex = 0;

                    isStopped = true;

                    SetStripesMultiMode();
                }
                else if (MultiTwoScreen.Visibility == Visibility.Visible)
                {
                    buttonTwoPlay.Visibility = Visibility.Visible;
                    buttonTwoStop.Visibility = Visibility.Hidden;
                    sphereUpdateTimer.Stop();
                    colorUpdateTimer.Stop();
                    stripesUpdateTimer.Stop();

                    centroidIndex = 0;
                    mfccIndex = 0;

                    isStopped = true;

                    SetStripesMultiMode();


                }
                else if (MultiThreeScreen.Visibility == Visibility.Visible)
                {
                    buttonThreePlay.Visibility = Visibility.Visible;
                    buttonThreeStop.Visibility = Visibility.Hidden;
                    sphereUpdateTimer.Stop();
                    colorUpdateTimer.Stop();
                    stripesUpdateTimer.Stop();

                    centroidIndex = 0;
                    mfccIndex = 0;

                    isStopped = true;

                    SetStripesMultiMode();
                }
                else if (MultiFourScreen.Visibility == Visibility.Visible)
                {
                    buttonFourPlay.Visibility = Visibility.Visible;
                    buttonFourStop.Visibility = Visibility.Hidden;
                    sphereUpdateTimer.Stop();
                    colorUpdateTimer.Stop();
                    stripesUpdateTimer.Stop();

                    centroidIndex = 0;
                    mfccIndex = 0;

                    isStopped = true;

                    SetStripesMultiMode();

                }
                else
                {
                    singlePlayButton.Visibility = Visibility.Visible;
                    singleStopButton.Visibility = Visibility.Hidden;
                    myView.RemoveAllSprites();
                    mySingleView.RemoveAllSprites();

                    sphereUpdateTimer.Stop();
                    colorUpdateTimer.Stop();
                    stripesUpdateTimer.Stop();

                    centroidIndex = 0;
                    mfccIndex = 0;

                    isStopped = true;

                    SetStripesMode();
                }
            }
        }


        private void SetStripesMode()
        {

            if (mfccMode == true && isStopped == true)
            {
                SetStripes(7, gridBass, new[] { "#00008B", "#00008B", "#00008B", "#00008B", "#00008B", "#00008B", "#00008B" }, "#00008B", 0);
                SetStripes(7, gridDrum, new[] { "#CC0000", "#CC0000", "#CC0000", "#CC0000", "#CC0000", "#CC0000", "#CC0000" }, "#CC0000", 1);
                SetStripes(7, gridOther, new[] { "#00CC00", "#00CC00", "#00CC00", "#00CC00", "#00CC00", "#00CC00", "#00CC00" }, "#00CC00", 2);
                SetStripes(7, gridVoice, new[] { "#FFD700", "#FFD700", "#FFD700", "#FFD700", "#FFD700", "#FFD700", "#FFD700" }, "#FFD700", 3);
            }
            else if (mfccMode == false && isStopped == true)
            {
                SetStripes(12, gridBass, GenerateColorGradient("#00008B", "#00008B", 12), "#00008B", 0);
                SetStripes(12, gridDrum, GenerateColorGradient("#CC0000", "#CC0000", 12), "#CC0000", 1);
                SetStripes(12, gridOther, GenerateColorGradient("#00CC00", "#00CC00", 12), "#00CC00", 2);
                SetStripes(12, gridVoice, GenerateColorGradient("#FFD700", "#FFD700", 12), "#FFD700", 3);
            }
            else if (mfccMode == false && isStopped == false)
            {
                SetStripes(12, gridBass, GenerateColorGradient("#00008B", "#00008B", 12), "#00008B", 0);
                SetStripes(12, gridDrum, GenerateColorGradient("#CC0000", "#CC0000", 12), "#CC0000", 1);
                SetStripes(12, gridOther, GenerateColorGradient("#00CC00", "#00CC00", 12), "#00CC00", 2);
                SetStripes(12, gridVoice, GenerateColorGradient("#FFD700", "#FFD700", 12), "#FFD700", 3);
                UpdateStripesColor();
            }
            else if (mfccMode == true && isStopped == false)
            {
                SetStripes(7, gridBass, new[] { "#00008B", "#00008B", "#00008B", "#00008B", "#00008B", "#00008B", "#00008B" }, "#00008B", 0);
                SetStripes(7, gridDrum, new[] { "#CC0000", "#CC0000", "#CC0000", "#CC0000", "#CC0000", "#CC0000", "#CC0000" }, "#CC0000", 1);
                SetStripes(7, gridOther, new[] { "#00CC00", "#00CC00", "#00CC00", "#00CC00", "#00CC00", "#00CC00", "#00CC00" }, "#00CC00", 2);
                SetStripes(7, gridVoice, new[] { "#FFD700", "#FFD700", "#FFD700", "#FFD700", "#FFD700", "#FFD700", "#FFD700" }, "#FFD700", 3);
                UpdateStripesColor();
            }
        }


        private void SetStripesMultiMode()
        {
            // Determine the number of stripes based on the mode
            int stripeCount = mfccMode ? 7 : 12;

            // Set stripes for a grid
            void SetStripesForGrid(Grid grid, Color instrumentColor, int numStripes, int position)
            {
                SetStripes(numStripes, grid, GenerateColorGradient(instrumentColor.ToString(), instrumentColor.ToString(), numStripes), instrumentColor.ToString(), position);
            }

            // Handle the MultiOneScreen case
            void HandleMultiOneScreen()
            {
                if (InstrumentColorMap.TryGetValue(instrumentList[0].ToLower(), out Color instrumentColor))
                {
                    SetStripesForGrid(gridOne, instrumentColor, stripeCount, 0);

                    if (!isStopped)
                    {
                        UpdateMultiStripesColor();
                    }
                }
            }

            // Handle the MultiTwoScreen case
            void HandleMultiTwoScreen()
            {
                if (InstrumentColorMap.TryGetValue(instrumentList[0].ToLower(), out Color instrumentColor1))
                {
                    SetStripesForGrid(gridTwoFirst, instrumentColor1, stripeCount, 0);
                }

                if (InstrumentColorMap.TryGetValue(instrumentList[1].ToLower(), out Color instrumentColor2))
                {
                    SetStripesForGrid(gridTwoSecond, instrumentColor2, stripeCount, 1);
                }

                if (!isStopped)
                {
                    UpdateMultiStripesColor();
                }
            }

            // Handle the MultiThreeScreen case
            void HandleMultiThreeScreen()
            {
                if (InstrumentColorMap.TryGetValue(instrumentList[0].ToLower(), out Color instrumentColor1))
                {
                    SetStripesForGrid(gridThreeFirst, instrumentColor1, stripeCount, 0);
                }

                if (InstrumentColorMap.TryGetValue(instrumentList[1].ToLower(), out Color instrumentColor2))
                {
                    SetStripesForGrid(gridThreeSecond, instrumentColor2, stripeCount, 1);
                }

                if (InstrumentColorMap.TryGetValue(instrumentList[2].ToLower(), out Color instrumentColor3))
                {
                    SetStripesForGrid(gridThreeThird, instrumentColor3, stripeCount, 2);
                }

                if (!isStopped)
                {
                    UpdateMultiStripesColor();
                }
            }

            // Handle the MultiFourScreen case
            void HandleMultiFourScreen()
            {
                if (InstrumentColorMap.TryGetValue(instrumentList[0].ToLower(), out Color instrumentColor1))
                {
                    SetStripesForGrid(gridFourFirst, instrumentColor1, stripeCount, 0);
                }

                if (InstrumentColorMap.TryGetValue(instrumentList[1].ToLower(), out Color instrumentColor2))
                {
                    SetStripesForGrid(gridFourSecond, instrumentColor2, stripeCount, 1);
                }

                if (InstrumentColorMap.TryGetValue(instrumentList[2].ToLower(), out Color instrumentColor3))
                {
                    SetStripesForGrid(gridFourThird, instrumentColor3, stripeCount, 2);
                }

                if (InstrumentColorMap.TryGetValue(instrumentList[3].ToLower(), out Color instrumentColor4))
                {
                    SetStripesForGrid(gridFourFourth, instrumentColor4, stripeCount, 3);
                }

                if (!isStopped)
                {
                    UpdateMultiStripesColor();
                }
            }

            // Determining visibility and calling appropriate handlers
            if (MultiOneScreen.Visibility == Visibility.Visible)
            {
                HandleMultiOneScreen();
            }
            else if (MultiTwoScreen.Visibility == Visibility.Visible)
            {
                HandleMultiTwoScreen();
            }
            else if (MultiThreeScreen.Visibility == Visibility.Visible)
            {
                HandleMultiThreeScreen();
            }
            else if (MultiFourScreen.Visibility == Visibility.Visible)
            {
                HandleMultiFourScreen();
            }
        }

        private void onClickChroma(object sender, RoutedEventArgs e)
        {
            mfccMode = false;
            if (SingleTrackScreen.Visibility == Visibility.Visible)
            {
                singleChromaButton.Visibility = Visibility.Hidden;
                singleMfccButton.Visibility = Visibility.Visible;

                // Set stripes for Chroma mode
                SetStripesMode();
            }
            else
            {
                if (MultiOneScreen.Visibility == Visibility.Visible)
                {
                    oneChromaButton.Visibility = Visibility.Hidden;
                    oneMfccButton.Visibility = Visibility.Visible;
                    SetStripesMultiMode();
                }
                else if (MultiTwoScreen.Visibility == Visibility.Visible)
                {
                    twoChromaButton.Visibility = Visibility.Hidden;
                    twoMfccButton.Visibility = Visibility.Visible;
                    SetStripesMultiMode();
                }
                else if (MultiThreeScreen.Visibility == Visibility.Visible)
                {
                    threeChromaButton.Visibility = Visibility.Hidden;
                    threeMfccButton.Visibility = Visibility.Visible;
                    SetStripesMultiMode();
                }
                else if (MultiFourScreen.Visibility == Visibility.Visible)
                {
                    fourChromaButton.Visibility = Visibility.Hidden;
                    fourMfccButton.Visibility = Visibility.Visible;
                    SetStripesMultiMode();
                }

            }

        }

        private void onClickMfcc(object sender, RoutedEventArgs e)
        {
            mfccMode = true;

            if (SingleTrackScreen.Visibility == Visibility.Visible)
            {
                singleChromaButton.Visibility = Visibility.Visible;
                singleMfccButton.Visibility = Visibility.Hidden;
                // Set stripes for Mfcc mode
                SetStripesMode();
            }
            else
            {
                if (MultiOneScreen.Visibility == Visibility.Visible)
                {
                    oneChromaButton.Visibility = Visibility.Visible;
                    oneMfccButton.Visibility = Visibility.Hidden;
                    SetStripesMultiMode();
                }
                else if (MultiTwoScreen.Visibility == Visibility.Visible)
                {
                    twoChromaButton.Visibility = Visibility.Visible;
                    twoMfccButton.Visibility = Visibility.Hidden;
                    SetStripesMultiMode();
                }
                else if (MultiThreeScreen.Visibility == Visibility.Visible)
                {
                    threeChromaButton.Visibility = Visibility.Visible;
                    threeMfccButton.Visibility = Visibility.Hidden;
                    SetStripesMultiMode();
                }
                else if (MultiFourScreen.Visibility == Visibility.Visible)
                {
                    fourChromaButton.Visibility = Visibility.Visible;
                    fourMfccButton.Visibility = Visibility.Hidden;
                    SetStripesMultiMode();
                }
            }
        }

        private void SetStripes(int count, Grid grid, string[] colors, string defaultColor, int position)
        {
            grid.ColumnDefinitions.Clear();
            grid.Children.Clear();

            for (int i = 0; i < count; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                if (isStopped == false)
                {
                    var rect = new Rectangle
                    {
                        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString((ampValues[position] * volume) == 0 ? defaultColor : colors[i]))

                    };
                    Grid.SetColumn(rect, i);  // Set the column for the rectangle
                    grid.Children.Add(rect);
                }
                else
                {
                    var rect = new Rectangle
                    {
                        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(defaultColor))

                    };
                    Grid.SetColumn(rect, i);  // Set the column for the rectangle
                    grid.Children.Add(rect);
                }
            }
        }



        private string[] GenerateColorGradient(string startColor, string endColor, int count)
        {
            Color start = (Color)ColorConverter.ConvertFromString(startColor);
            Color end = (Color)ColorConverter.ConvertFromString(endColor);
            List<string> colors = new List<string>();

            for (int i = 0; i < count; i++)
            {
                byte r = (byte)(start.R + (end.R - start.R) * i / (count - 1));
                byte g = (byte)(start.G + (end.G - start.G) * i / (count - 1));
                byte b = (byte)(start.B + (end.B - start.B) * i / (count - 1));
                colors.Add($"#{r:X2}{g:X2}{b:X2}");
            }

            return colors.ToArray();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            viewTracker = new TanvasTouchViewTracker(this);

            // Create home sprite (Background)
            var uri_home = new Uri("pack://application:,,/Assets/Haptic/music_tanvas_bg.png");
            var sprite_home = PNGToTanvasTouch.CreateSpriteFromPNG(uri_home);

            // Create button sprite
            var uri_button = new Uri("pack://application:,,/Assets/Haptic/start_bt.png");
            var sprite_button = PNGToTanvasTouch.CreateSpriteFromPNG(uri_button);

            // Initial size and position update for the sprites
            UpdateSpriteSize(sprite_home, sprite_button);

            // Add sprites to the view
            myView.AddSprite(sprite_button);  // Add the button sprite
            myView.AddSprite(sprite_home);  // Add the background sprite

            // Subscribe to SizeChanged event
            this.SizeChanged += (s, args) =>
            {
                // Update both the background and button sprites
                UpdateSpriteSize(sprite_home, sprite_button);
            };


        }

        private void UpdateSpriteSize(TSprite homeSprite, TSprite buttonSprite)
        {
            var dpi = VisualTreeHelper.GetDpi(this);

            // Update the background sprite size
            double screenWidth = this.ActualWidth;  // Width in DIPs
            double screenHeight = this.ActualHeight;  // Height in DIPs

            int widthInPixels = (int)(screenWidth * dpi.DpiScaleX);  // Convert DIPs to pixels
            int heightInPixels = (int)(screenHeight * dpi.DpiScaleY);  // Convert DIPs to pixels

            homeSprite.Width = widthInPixels;
            homeSprite.Height = heightInPixels;

            // Update button sprite size and position
            Point btPosition = StartButton.TransformToAncestor(StartScreen).Transform(new Point(0, 0));  // Get button position

            double btWidth = StartButton.ActualWidth;
            double btHeight = StartButton.ActualHeight;

            buttonSprite.Width = (int)(btWidth * dpi.DpiScaleX);  // Set button sprite width
            buttonSprite.Height = (int)(btHeight * dpi.DpiScaleY);  // Set button sprite height

            // Update button sprite position
            buttonSprite.X = (float)(btPosition.X * dpi.DpiScaleX);
            buttonSprite.Y = (float)(btPosition.Y * dpi.DpiScaleY);
        }

        private void AdjustVolume(WaveOut waveOut, float volume)
        {
            try
            {
                if (waveOut != null && waveOut.PlaybackState != PlaybackState.Stopped)
                {
                    waveOut.Volume = volume;
                }
                else
                {
                }
            }
            catch (Exception ex)
            {
            }
        }

        public void OnVolumeSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            volume = (float)e.NewValue;
            foreach (var output in audioOutputs)
            {
                AdjustVolume(output, volume);
            }
        }

        private void Multi_Track_Click(object sender, RoutedEventArgs e)
        {
            GenreScreen.Visibility = Visibility.Collapsed;
            UploadScreen.Visibility = Visibility.Visible;
            myView.RemoveAllSprites();

            var uri_bg = new Uri("pack://application:,,/Assets/Haptic/upload_bg.png");
            var sprite_bg = PNGToTanvasTouch.CreateSpriteFromPNG(uri_bg);

            var uri_play = new Uri("pack://application:,,/Assets/Haptic/bPlay.png");
            var sprite_play = PNGToTanvasTouch.CreateSpriteFromPNG(uri_play);

            var uri_upload = new Uri("pack://application:,,/Assets/Haptic/bUpload.png");
            var sprite_upload = PNGToTanvasTouch.CreateSpriteFromPNG(uri_upload);

            var uri_left = new Uri("pack://application:,,/Assets/Haptic/bLeft.png");
            var sprite_left = PNGToTanvasTouch.CreateSpriteFromPNG(uri_left);

            myView.AddSprite(sprite_left);
            myView.AddSprite(sprite_upload);
            myView.AddSprite(sprite_play);
            myView.AddSprite(sprite_bg);

            // Attach LayoutUpdated event to ensure the layout is complete before updating the sprite positions
            UploadScreen.LayoutUpdated += (s, args) =>
            {
                UpdateSpriteMultiUpload(sprite_bg, sprite_left, sprite_upload, sprite_play);
            };

            // Subscribe to SizeChanged event for window resizing
            this.SizeChanged += (s, args) =>
            {
                UpdateSpriteMultiUpload(sprite_bg, sprite_left, sprite_upload, sprite_play);
            };
        }

        private void UpdateSpriteMultiUpload(TSprite sprite_bg, TSprite sprite_left, TSprite sprite_upload, TSprite sprite_play)
        {
            var dpi = VisualTreeHelper.GetDpi(this);

            // Update the background sprite size
            double screenWidth = this.ActualWidth;  // Width in DIPs
            double screenHeight = this.ActualHeight;  // Height in DIPs

            int widthInPixels = (int)(screenWidth * dpi.DpiScaleX);  // Convert DIPs to pixels
            int heightInPixels = (int)(screenHeight * dpi.DpiScaleY);  // Convert DIPs to pixels

            sprite_bg.Width = widthInPixels;
            sprite_bg.Height = heightInPixels;

            // Update button sprite size and position
            Point leftPosition = backButton.TransformToAncestor(UploadScreen).Transform(new Point(0, 0));  // Get new button position

            double leftWidth = backButton.ActualWidth;
            double leftHeight = backButton.ActualHeight;

            sprite_left.Width = (int)(leftWidth * dpi.DpiScaleX);
            sprite_left.Height = (int)(leftHeight * dpi.DpiScaleY);

            sprite_left.X = (float)(leftPosition.X * dpi.DpiScaleX);
            sprite_left.Y = (float)(leftPosition.Y * dpi.DpiScaleY);

            Point uploadPosition = UploadButton.TransformToAncestor(UploadScreen).Transform(new Point(0, 0));

            double uploadWidth = UploadButton.ActualWidth;
            double uploadHeight = UploadButton.ActualHeight;

            sprite_upload.Width = (int)(uploadWidth * dpi.DpiScaleX);
            sprite_upload.Height = (int)(uploadHeight * dpi.DpiScaleY);

            sprite_upload.X = (float)(uploadPosition.X * dpi.DpiScaleX);
            sprite_upload.Y = (float)(uploadPosition.Y * dpi.DpiScaleY);

            Point playPosition = PlayButton.TransformToAncestor(UploadScreen).Transform(new Point(0, 0));

            double playWidth = PlayButton.ActualWidth;
            double playHeight = PlayButton.ActualHeight;

            sprite_play.Width = (int)(playWidth * dpi.DpiScaleX);
            sprite_play.Height = (int)(playHeight * dpi.DpiScaleY);


            sprite_play.X = (float)(playPosition.X * dpi.DpiScaleX);
            sprite_play.Y = (float)(playPosition.Y * dpi.DpiScaleY);
        }

        private void Single_Track_Click(object sender, RoutedEventArgs e)
        {
            GenreScreen.Visibility = Visibility.Collapsed;
            SingleUploadScreen.Visibility = Visibility.Visible;
            myView.RemoveAllSprites();

            var uri_bg = new Uri("pack://application:,,/Assets/Haptic/upload_bg.png");
            var sprite_bg = PNGToTanvasTouch.CreateSpriteFromPNG(uri_bg);

            var uri_play = new Uri("pack://application:,,/Assets/Haptic/bPlay.png");
            var sprite_play = PNGToTanvasTouch.CreateSpriteFromPNG(uri_play);

            var uri_upload = new Uri("pack://application:,,/Assets/Haptic/bUpload.png");
            var sprite_upload = PNGToTanvasTouch.CreateSpriteFromPNG(uri_upload);

            var uri_left = new Uri("pack://application:,,/Assets/Haptic/bLeft.png");
            var sprite_left = PNGToTanvasTouch.CreateSpriteFromPNG(uri_left);

            myView.AddSprite(sprite_left);
            myView.AddSprite(sprite_upload);
            myView.AddSprite(sprite_play);
            myView.AddSprite(sprite_bg);

            // Attach LayoutUpdated event to ensure the layout is complete before updating the sprite positions
            SingleUploadScreen.LayoutUpdated += (s, args) =>
            {
                UpdateSpriteSingleUpload(sprite_bg, sprite_left, sprite_upload, sprite_play);
            };

            // Subscribe to SizeChanged event for window resizing
            this.SizeChanged += (s, args) =>
            {
                UpdateSpriteSingleUpload(sprite_bg, sprite_left, sprite_upload, sprite_play);
            };
        }


        private void UpdateSpriteSingleUpload(TSprite sprite_bg, TSprite sprite_left, TSprite sprite_upload, TSprite sprite_play)
        {
            var dpi = VisualTreeHelper.GetDpi(this);

            // Update the background sprite size
            double screenWidth = this.ActualWidth;  // Width in DIPs
            double screenHeight = this.ActualHeight;  // Height in DIPs

            int widthInPixels = (int)(screenWidth * dpi.DpiScaleX);  // Convert DIPs to pixels
            int heightInPixels = (int)(screenHeight * dpi.DpiScaleY);  // Convert DIPs to pixels

            sprite_bg.Width = widthInPixels;
            sprite_bg.Height = heightInPixels;

            Point leftPosition = backSingleButton.TransformToAncestor(SingleUploadScreen).Transform(new Point(0, 0));

            double leftWidth = backSingleButton.ActualWidth;
            double leftHeight = backSingleButton.ActualHeight;

            sprite_left.Width = (int)(leftWidth * dpi.DpiScaleX);
            sprite_left.Height = (int)(leftHeight * dpi.DpiScaleY);

            sprite_left.X = (float)(leftPosition.X * dpi.DpiScaleX);
            sprite_left.Y = (float)(leftPosition.Y * dpi.DpiScaleY);

            Point uploadPosition = singleUploadButton.TransformToAncestor(SingleUploadScreen).Transform(new Point(0, 0));

            double uploadWidth = singleUploadButton.ActualWidth;
            double uploadHeight = singleUploadButton.ActualHeight;

            sprite_upload.Width = (int)(uploadWidth * dpi.DpiScaleX);
            sprite_upload.Height = (int)(uploadHeight * dpi.DpiScaleY);

            sprite_upload.X = (float)(uploadPosition.X * dpi.DpiScaleX);
            sprite_upload.Y = (float)(uploadPosition.Y * dpi.DpiScaleY);

            Point playPosition = singlePlayUpButton.TransformToAncestor(SingleUploadScreen).Transform(new Point(0, 0));

            double playWidth = singlePlayUpButton.ActualWidth;
            double playHeight = singlePlayUpButton.ActualHeight;

            sprite_play.Width = (int)(playWidth * dpi.DpiScaleX);
            sprite_play.Height = (int)(playHeight * dpi.DpiScaleY);

            sprite_play.X = (float)(playPosition.X * dpi.DpiScaleX);
            sprite_play.Y = (float)(playPosition.Y * dpi.DpiScaleY);
        }

        private void SingleUploadButton_Click(object sender, RoutedEventArgs e)
        {
            // Show a file dialog to select an audio file
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Audio files (*.mp3, *.wav)|*.mp3;*.wav";

            if (openFileDialog.ShowDialog() == true)
            {
                string fileName = openFileDialog.FileName;
                string destinationFolder = "Assets/audio";

                // Clear the existing files in the destination folder
                ClearAudioFolder();

                try
                {
                    Stopwatch stopwatch = new Stopwatch();


                    // Run the Spleeter separation process
                    stopwatch.Start();  // Start timing
                    RunSpleeter(fileName, destinationFolder);
                    stopwatch.Stop();  // Stop timing

                    // Check if output folder has the separated tracks
                    if (Directory.GetFiles(destinationFolder).Length > 0)
                    {
                        
                        double elapsedTimeInSeconds = stopwatch.Elapsed.TotalSeconds; // Get elapsed time in seconds
                        Debug.WriteLine($"Time taken to generate {fileName}: {elapsedTimeInSeconds:F2} seconds");
                        MessageBox.Show("File processed successfully!");
                    }
                    else
                    {
                        MessageBox.Show("No files generated. Please check the Spleeter logs for details.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error processing file: {ex.Message}");
                }
            }
        }

        private void RunSpleeter(string inputFilePath, string targetFolderPath)
        {
            MessageBox.Show("Your file is being separated. This may take a few moments.", "Processing", MessageBoxButton.OK, MessageBoxImage.Information);

            // Create a temporary folder for Spleeter output
            string tempOutputFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "spleeter_temp_output");
            Directory.CreateDirectory(tempOutputFolder);

            using (Py.GIL())
            {
                try
                {
                    dynamic subprocess = Py.Import("subprocess");

                    string[] command = new string[]
                    {
                "python",
                "-m",
                "spleeter",
                "separate",
                "-p",
                "spleeter:4stems",
                "-o",
                tempOutputFolder,
                inputFilePath
                    };

                    var pyCommand = new PyList(command.Select(c => new PyString(c)).ToArray());
                    subprocess.run(pyCommand, shell: true, check: true);

                    // Move files from tempOutputFolder to targetFolderPath
                    MoveFilesToTargetFolder(tempOutputFolder, targetFolderPath);
                }
                catch (PythonException ex)
                {
                    MessageBox.Show($"Python error: {ex.Message}");
                }
            }


            foreach (var file in Directory.GetFiles(AudioFolder, "*.mp3").Concat(Directory.GetFiles(AudioFolder, "*.wav")))
            {
                var stream = new AudioFileReader(file);
                var output = new WaveOut();
                output.Init(stream);

                // Add event handler for PlaybackStopped event
                output.PlaybackStopped += OnPlaybackStopped;

                audioStreams.Add(stream);
                audioOutputs.Add(output);
            }

        }

        private void MoveFilesToTargetFolder(string sourceFolder, string targetFolder)
        {
            // Ensure the target folder exists
            Directory.CreateDirectory(targetFolder);

            // Move all files from the source folder to the target folder
            foreach (string file in Directory.GetFiles(sourceFolder, "*.*", SearchOption.AllDirectories))
            {
                string fileName = System.IO.Path.GetFileName(file);
                string destFile = System.IO.Path.Combine(targetFolder, fileName);
                System.IO.File.Move(file, destFile);
            }

            // Optionally, remove the temporary folder
            Directory.Delete(sourceFolder, true);
        }

        private void SinglePlayButton_Click(object sender, RoutedEventArgs e)
        {

            myView.RemoveAllSprites();
            string audioFolderPath = "Assets/audio"; // Folder path where audio files are stored

            // Check if the folder exists
            if (Directory.Exists(audioFolderPath))
            {
                // Get all files with .mp3 or .wav extension in the folder
                singleTrackAudio = Directory.GetFiles(audioFolderPath, "*.mp3").Concat(Directory.GetFiles(audioFolderPath, "*.wav")).ToArray();
                // Check if any audio files exist
                if (singleTrackAudio.Length > 0)
                {
                    SingleUploadScreen.Visibility = Visibility.Collapsed;
                    SingleTrackScreen.Visibility = Visibility.Visible;

                    // Clear any previously stored file paths and audio resources
                    audioFilePaths.Clear();
                    ClearAudioResources();

                    // Reinitialize the streams and outputs with the same files
                    foreach (var file in Directory.GetFiles(AudioFolder, "*.mp3").Concat(Directory.GetFiles(AudioFolder, "*.wav")))
                    {
                        audioFilePaths.Add(file); // Add file path to the list
                        var stream = new AudioFileReader(file);
                        var output = new WaveOut();
                        output.Init(stream);

                        // Add event handler for PlaybackStopped event
                        output.PlaybackStopped += OnPlaybackStopped;

                        audioStreams.Add(stream);
                        audioOutputs.Add(output);
                    }
                    CreateSphere(ref mesh1, sphereBass, baseRadius);
                    CreateSphere(ref mesh2, sphereDrum, baseRadius);
                    CreateSphere(ref mesh3, sphereOther, baseRadius);
                    CreateSphere(ref mesh4, sphereVoice, baseRadius);

                    for (int i = 0; i < audioFilePaths.Count; i++)
                    {
                        spectralCentroid[i] = GetSpectralCentroid(audioFilePaths[i]);
                        onSetDetection[i] = AMethodForNovelty(audioFilePaths[i]);
                        var (_, aggregatedMfccMatrix) = GetMFCCMatrices(audioFilePaths[i]);
                        mfccMatrix.Add(aggregatedMfccMatrix);
                        chromaMatrix.Add(GetChromagramMatrix(audioFilePaths[i]));

                    }

                    maxCentroid = spectralCentroid.SelectMany(centroidList => centroidList).Max();
                    NormalizeSpectralCentroids();

                    double audioDurationInSeconds = audioStreams[0].TotalTime.TotalSeconds;
                    double updateInterval = audioDurationInSeconds / spectralCentroid[0].Count;

                    var uri_singleTrack = new Uri("pack://application:,,/Assets/Haptic/01_bc.png");
                    var sprite_singleTrack = PNGToTanvasTouch.CreateSpriteFromPNG(uri_singleTrack);

                    var uri_singleLeft = new Uri("pack://application:,,/Assets/Haptic/bLeft.png");
                    var sprite_singleLeft = PNGToTanvasTouch.CreateSpriteFromPNG(uri_singleLeft);

                    var uri_singlePlay = new Uri("pack://application:,,/Assets/Haptic/bPlay.png");
                    var sprite_singlePlay = PNGToTanvasTouch.CreateSpriteFromPNG(uri_singlePlay);

                    var uri_singleChroma = new Uri("pack://application:,,/Assets/Haptic/chroma.png");
                    var sprite_singleChroma = PNGToTanvasTouch.CreateSpriteFromPNG(uri_singleChroma);

                    myView.AddSprite(sprite_singleChroma);
                    myView.AddSprite(sprite_singleLeft);
                    myView.AddSprite(sprite_singlePlay);
                    myView.AddSprite(sprite_singleTrack);

                    // Attach LayoutUpdated event to ensure the layout is complete before updating the sprite positions
                    SingleTrackScreen.LayoutUpdated += (s, args) =>
                    {
                        UpdateSpriteSingleTrack(sprite_singleTrack, sprite_singleLeft, sprite_singlePlay, sprite_singleChroma);
                    };

                    // Subscribe to SizeChanged event
                    this.SizeChanged += (s, args) =>
                    {
                        // Update both the background and button sprites
                        UpdateSpriteSingleTrack(sprite_singleTrack, sprite_singleLeft, sprite_singlePlay, sprite_singleChroma);
                    };

                    sphereUpdateTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(updateInterval)
                    };
                    sphereUpdateTimer.Tick += SphereUpdateTimer_Tick;

                    colorUpdateTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(updateInterval)
                    };
                    colorUpdateTimer.Tick += ColorUpdateTimer_Tick;

                    stripesUpdateTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(5)
                    };
                    stripesUpdateTimer.Tick += StripesUpdateTimer_Tick;
                }
                else
                {
                    MessageBox.Show("No audio files found (.mp3 or .wav) in the audio folder.");
                }
            }
            else
            {
                MessageBox.Show("Audio folder not found. Please upload an audio file first.");
            }
        }

        private void UpdateSpriteSingleTrack(TSprite sprite_singleTrack, TSprite sprite_singleLeft, TSprite sprite_singlePlay, TSprite sprite_singleChroma)
        {
            var dpi = VisualTreeHelper.GetDpi(this);

            double screenHeight = this.ActualHeight;  // Height in DIPs

            int widthInPixels = (int)(SingleStack.ActualWidth * dpi.DpiScaleX);  // Convert DIPs to pixels
            int heightInPixels = (int)(screenHeight * dpi.DpiScaleY);  // Convert DIPs to pixels

            sprite_singleTrack.Width = widthInPixels;
            sprite_singleTrack.Height = heightInPixels;

            Point leftPosition = singleBack.TransformToAncestor(SingleTrackScreen).Transform(new Point(0, 0));  // Get new button position

            double leftWidth = singleBack.ActualWidth;
            double leftHeight = singleBack.ActualHeight;

            sprite_singleLeft.Width = (int)(leftWidth * dpi.DpiScaleX);  // Set button sprite width
            sprite_singleLeft.Height = (int)(leftHeight * dpi.DpiScaleY);  // Set button sprite height

            // Update button sprite position
            sprite_singleLeft.X = (float)(leftPosition.X * dpi.DpiScaleX);
            sprite_singleLeft.Y = (float)(leftPosition.Y * dpi.DpiScaleY);

            Point playPosition = singlePlayButton.TransformToAncestor(SingleTrackScreen).Transform(new Point(0, 0));  // Get new button position

            double playWidth = singlePlayButton.ActualWidth;
            double playHeight = singlePlayButton.ActualHeight;

            sprite_singlePlay.Width = (int)(playWidth * dpi.DpiScaleX);  // Set button sprite width
            sprite_singlePlay.Height = (int)(playHeight * dpi.DpiScaleY);  // Set button sprite height

            // Update button sprite position
            sprite_singlePlay.X = (float)(playPosition.X * dpi.DpiScaleX);
            sprite_singlePlay.Y = (float)(playPosition.Y * dpi.DpiScaleY);


            Point chromaPosition = singleChromaButton.TransformToAncestor(SingleTrackScreen).Transform(new Point(0, 0));  // Get new button position

            double chromaWidth = singleChromaButton.ActualWidth;
            double chromaHeight = singleChromaButton.ActualHeight;

            sprite_singleChroma.Width = (int)(chromaWidth * dpi.DpiScaleX);  // Set button sprite width
            sprite_singleChroma.Height = (int)(chromaHeight * dpi.DpiScaleY);  // Set button sprite height

            // Update button sprite position
            sprite_singleChroma.X = (float)(chromaPosition.X * dpi.DpiScaleX);
            sprite_singleChroma.Y = (float)(chromaPosition.Y * dpi.DpiScaleY);
        }



        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StartScreen.Visibility = Visibility.Collapsed;
            GenreScreen.Visibility = Visibility.Visible;
            myView.RemoveAllSprites();

            var uri_single = new Uri("pack://application:,,/Assets/Haptic/single_mode.png");
            var sprite_single = PNGToTanvasTouch.CreateSpriteFromPNG(uri_single);

            var uri_multi = new Uri("pack://application:,,/Assets/Haptic/multi_mode.png");
            var sprite_multi = PNGToTanvasTouch.CreateSpriteFromPNG(uri_multi);

            var uri_mode = new Uri("pack://application:,,/Assets/Haptic/upload_bg.png");
            var sprite_mode = PNGToTanvasTouch.CreateSpriteFromPNG(uri_mode);

            myView.AddSprite(sprite_single);
            myView.AddSprite(sprite_multi);
            myView.AddSprite(sprite_mode);

            // Attach LayoutUpdated event to ensure the layout is complete before updating the sprite positions
            GenreScreen.LayoutUpdated += (s, args) =>
            {
                UpdateSpriteSizeMode(sprite_single, sprite_multi, sprite_mode);
            };

            // Subscribe to SizeChanged event
            this.SizeChanged += (s, args) =>
            {
                // Update both the background and button sprites
                UpdateSpriteSizeMode(sprite_single, sprite_multi, sprite_mode);
            };

        }

        private void UpdateSpriteSizeMode(TSprite sprite_single, TSprite sprite_multi, TSprite sprite_mode)
        {
            var dpi = VisualTreeHelper.GetDpi(this);

            // Update the background sprite size
            double screenWidth = this.ActualWidth;  // Width in DIPs
            double screenHeight = this.ActualHeight;  // Height in DIPs

            int widthInPixels = (int)(screenWidth * dpi.DpiScaleX);  // Convert DIPs to pixels
            int heightInPixels = (int)(screenHeight * dpi.DpiScaleY);  // Convert DIPs to pixels

            sprite_mode.Width = widthInPixels;
            sprite_mode.Height = heightInPixels;

            // Update button sprite size and position
            Point singlePosition = singleMode.TransformToAncestor(GenreScreen).Transform(new Point(0, 0));  // Get new button position

            double singleWidth = singleMode.ActualWidth;
            double singleHeight = singleMode.ActualHeight;

            sprite_single.Width = (int)(singleWidth * dpi.DpiScaleX);  // Set button sprite width
            sprite_single.Height = (int)(singleHeight * dpi.DpiScaleY);  // Set button sprite height

            // Update button sprite position
            sprite_single.X = (float)(singlePosition.X * dpi.DpiScaleX);
            sprite_single.Y = (float)(singlePosition.Y * dpi.DpiScaleY);

            // Update button sprite size and position
            Point multiPosition = multiMode.TransformToAncestor(GenreScreen).Transform(new Point(0, 0));  // Get new button position

            double multiWidth = multiMode.ActualWidth;
            double multiHeight = multiMode.ActualHeight;

            sprite_multi.Width = (int)(multiWidth * dpi.DpiScaleX);  // Set button sprite width
            sprite_multi.Height = (int)(multiHeight * dpi.DpiScaleY);  // Set button sprite height

            // Update button sprite position
            sprite_multi.X = (float)(multiPosition.X * dpi.DpiScaleX);
            sprite_multi.Y = (float)(multiPosition.Y * dpi.DpiScaleY);

        }

        private void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Audio files (*.mp3;*.wav)|*.mp3;*.wav",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFileName = System.IO.Path.GetFileName(openFileDialog.FileName);

                if (SelectedFilesListBox.Items.Contains(selectedFileName))
                {
                    MessageBox.Show("This file has already been selected. Please choose another file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (SelectedFilesListBox.Items.Count == 0)
                {
                    referenceDuration = GetAudioDuration(openFileDialog.FileName);
                }
                else
                {
                    var newFileDuration = GetAudioDuration(openFileDialog.FileName);
                    if (newFileDuration != referenceDuration)
                    {
                        MessageBox.Show("All audio files must have the same duration.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                if (SelectedFilesListBox.Items.Count < MaxFiles)
                {
                    string destFile = System.IO.Path.Combine(AudioFolder, selectedFileName);
                    System.IO.File.Copy(openFileDialog.FileName, destFile);
                    SelectedFilesListBox.Items.Add(selectedFileName);
                    SelectedFilesListBox.Visibility = Visibility.Visible;
                }
                else
                {
                    MessageBox.Show($"You can only upload a maximum of {MaxFiles} files.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        private TimeSpan GetAudioDuration(string filePath)
        {
            using (var audioFileReader = new AudioFileReader(filePath))
            {
                return audioFileReader.TotalTime;
            }
        }

        private void UpdateColorsBasedOnInstrument()
        {
            if (MultiOneScreen.Visibility == Visibility.Visible)
            {
                if (InstrumentColorMap.TryGetValue(instrumentList[0], out Color instrumentColor))
                {
                    var newMaterial = new DiffuseMaterial(new SolidColorBrush(instrumentColor));
                    Dispatcher.Invoke(() =>
                    {
                        sphereOne.Material = newMaterial;
                    });
                    SetStripes(7, gridOne, GenerateColorGradient(instrumentColor.ToString(), instrumentColor.ToString(), 7), instrumentColor.ToString(), 0);
                }
            }

            if (MultiTwoScreen.Visibility == Visibility.Visible)
            {
                if (InstrumentColorMap.TryGetValue(instrumentList[0], out Color instrumentColor))
                {
                    var newMaterial = new DiffuseMaterial(new SolidColorBrush(instrumentColor));
                    Dispatcher.Invoke(() =>
                    {
                        sphereTwoFirst.Material = newMaterial;
                    });
                    SetStripes(7, gridTwoFirst, GenerateColorGradient(instrumentColor.ToString(), instrumentColor.ToString(), 7), instrumentColor.ToString(), 0);
                }

                if (InstrumentColorMap.TryGetValue(instrumentList[1], out Color instrumentColor2))
                {
                    var newMaterial = new DiffuseMaterial(new SolidColorBrush(instrumentColor2));  // Corrected here
                    Dispatcher.Invoke(() =>
                    {
                        sphereTwoSecond.Material = newMaterial;
                    });
                    SetStripes(7, gridTwoSecond, GenerateColorGradient(instrumentColor2.ToString(), instrumentColor2.ToString(), 7), instrumentColor2.ToString(), 0);
                }
            }

            if (MultiThreeScreen.Visibility == Visibility.Visible)
            {
                if (InstrumentColorMap.TryGetValue(instrumentList[0], out Color instrumentColor))
                {
                    var newMaterial = new DiffuseMaterial(new SolidColorBrush(instrumentColor));
                    Dispatcher.Invoke(() =>
                    {
                        sphereThreeFirst.Material = newMaterial;
                    });
                    SetStripes(7, gridThreeFirst, GenerateColorGradient(instrumentColor.ToString(), instrumentColor.ToString(), 7), instrumentColor.ToString(), 0);
                }

                if (InstrumentColorMap.TryGetValue(instrumentList[1], out Color instrumentColor2))
                {
                    var newMaterial = new DiffuseMaterial(new SolidColorBrush(instrumentColor2));
                    Dispatcher.Invoke(() =>
                    {
                        sphereThreeSecond.Material = newMaterial;
                    });
                    SetStripes(7, gridThreeSecond, GenerateColorGradient(instrumentColor2.ToString(), instrumentColor2.ToString(), 7), instrumentColor2.ToString(), 0);
                }

                if (InstrumentColorMap.TryGetValue(instrumentList[2], out Color instrumentColor3))
                {
                    var newMaterial = new DiffuseMaterial(new SolidColorBrush(instrumentColor3));
                    Dispatcher.Invoke(() =>
                    {
                        sphereThreeThird.Material = newMaterial;
                    });
                    SetStripes(7, gridThreeThird, GenerateColorGradient(instrumentColor3.ToString(), instrumentColor3.ToString(), 7), instrumentColor3.ToString(), 0);
                }
            }
            if (MultiFourScreen.Visibility == Visibility.Visible)
            {
                if (InstrumentColorMap.TryGetValue(instrumentList[0], out Color instrumentColor))
                {
                    var newMaterial = new DiffuseMaterial(new SolidColorBrush(instrumentColor));
                    Dispatcher.Invoke(() =>
                    {
                        sphereFourFirst.Material = newMaterial;
                    });
                    SetStripes(7, gridFourFirst, GenerateColorGradient(instrumentColor.ToString(), instrumentColor.ToString(), 7), instrumentColor.ToString(), 0);
                }

                if (InstrumentColorMap.TryGetValue(instrumentList[1], out Color instrumentColor2))
                {
                    var newMaterial = new DiffuseMaterial(new SolidColorBrush(instrumentColor2));
                    Dispatcher.Invoke(() =>
                    {
                        sphereFourSecond.Material = newMaterial;
                    });
                    SetStripes(7, gridFourSecond, GenerateColorGradient(instrumentColor2.ToString(), instrumentColor2.ToString(), 7), instrumentColor2.ToString(), 0);
                }

                if (InstrumentColorMap.TryGetValue(instrumentList[2], out Color instrumentColor3))
                {
                    var newMaterial = new DiffuseMaterial(new SolidColorBrush(instrumentColor3));
                    Dispatcher.Invoke(() =>
                    {
                        sphereFourThird.Material = newMaterial;
                    });
                    SetStripes(7, gridFourThird, GenerateColorGradient(instrumentColor3.ToString(), instrumentColor3.ToString(), 7), instrumentColor3.ToString(), 0);
                }

                if (InstrumentColorMap.TryGetValue(instrumentList[3], out Color instrumentColor4))
                {
                    var newMaterial = new DiffuseMaterial(new SolidColorBrush(instrumentColor4));
                    Dispatcher.Invoke(() =>
                    {
                        sphereFourFourth.Material = newMaterial;
                    });
                    SetStripes(7, gridFourFourth, GenerateColorGradient(instrumentColor4.ToString(), instrumentColor4.ToString(), 7), instrumentColor4.ToString(), 0);
                }
            }
        }


        public string PredictInstrument(string filePath, List<List<float>> fullMFCCMatrix, List<List<float>> chromaMatrix)
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string fullPath = System.IO.Path.Combine(currentDirectory, filePath);

            // Create temporary files
            string mfccFilePath = System.IO.Path.GetTempFileName();
            string chromaFilePath = System.IO.Path.GetTempFileName();
            string fileName = System.IO.Path.GetFileName(filePath);

            // Get the base directory (bin/Debug/net8.0-windows)
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Navigate three levels up to get to the project root (MusicTanvas)
            string projectRoot = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(baseDir).FullName).FullName).FullName).FullName;

            // Combine the relative path to your script from the project root
            string scriptPath = System.IO.Path.Combine(projectRoot, "predict_instrument.py");
            Debug.WriteLine(scriptPath);
            try
            {
                // Write the MFCC and Chroma matrices to temporary files
                System.IO.File.WriteAllText(mfccFilePath, Newtonsoft.Json.JsonConvert.SerializeObject(fullMFCCMatrix));
                System.IO.File.WriteAllText(chromaFilePath, Newtonsoft.Json.JsonConvert.SerializeObject(chromaMatrix));
                //System.IO.File.WriteAllText(audioFilePath, fullPath);

                // Define the process start info
                ProcessStartInfo start = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\" \"{mfccFilePath}\" \"{chromaFilePath}\" \"{fileName}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(start))
                {
                    string result = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (string.IsNullOrEmpty(result))
                    {
                        string error = process.StandardError.ReadToEnd();
                        throw new Exception($"Error: {error}");
                    }

                    string instrument = result.Trim().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Last();
                    instrumentList.Add(instrument);
                    return instrument;
                }
            }
            catch (Exception ex)
            {
                return "Prediction error";
            }
            finally
            {
                // Clean up temporary files
                System.IO.File.Delete(mfccFilePath);
                System.IO.File.Delete(chromaFilePath);
            }
        }

        public void PlayButton_Click(object sender, EventArgs e)
        {
            if (SelectedFilesListBox.Items.Count == 0)
            {
                MessageBox.Show("You need to upload at least one audio file before playing.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Clear any previously initialized streams and outputs
            audioFilePaths.Clear();
            ClearAudioResources();

            predictedInstruments.Clear();  // Ensure this list is cleared before adding new items

            
            

            for (int i = 0; i < SelectedFilesListBox.Items.Count; i++)
            {
                string fileName = SelectedFilesListBox.Items[i].ToString();
                string selectedFilePath = System.IO.Path.Combine(AudioFolder, fileName);
                audioFilePaths.Add(selectedFilePath);

                
                var chromaSingleMatrix = GetChromagramMatrix(audioFilePaths[i]);
                chromaMatrix.Add(chromaSingleMatrix);

                var (fullMfccMatrix, aggregatedMfccMatrix) = GetMFCCMatrices(audioFilePaths[i]);
                mfccMatrix.Add(aggregatedMfccMatrix);
                
                


                string instrument = PredictInstrument(selectedFilePath, fullMfccMatrix, chromaSingleMatrix);
                predictedInstruments.Add(instrument);
                Debug.WriteLine("predicted" + instrument);


                string extension = System.IO.Path.GetExtension(selectedFilePath).ToLower();
                if (extension == ".mp3" || extension == ".wav")
                {
                    var stream = new AudioFileReader(selectedFilePath);
                    var output = new WaveOut();
                    output.Init(stream);

                    output.PlaybackStopped += OnPlaybackStopped;

                    audioStreams.Add(stream);
                    audioOutputs.Add(output);
                }
            }

            UploadScreen.Visibility = Visibility.Collapsed;

            if (SelectedFilesListBox.Items.Count == 1)
            {
                MultiOneScreen.Visibility = Visibility.Visible;
                CreateSphere(ref mesh1, sphereOne, baseRadius);
                UpdateColorsBasedOnInstrument();
                var uri_oneSlider = new Uri("pack://application:,,/Assets/Haptic/col9.png");
                var sprite_oneSlider = PNGToTanvasTouch.CreateSpriteFromPNG(uri_oneSlider);

                var uri_oneTrack = new Uri("pack://application:,,/Assets/Haptic/01_bc.png");
                var sprite_oneTrack = PNGToTanvasTouch.CreateSpriteFromPNG(uri_oneTrack);

                var uri_oneLeft = new Uri("pack://application:,,/Assets/Haptic/bLeft.png");
                var sprite_oneLeft = PNGToTanvasTouch.CreateSpriteFromPNG(uri_oneLeft);

                var uri_onePlay = new Uri("pack://application:,,/Assets/Haptic/bPlay.png");
                var sprite_onePlay = PNGToTanvasTouch.CreateSpriteFromPNG(uri_onePlay);

                var uri_oneChroma = new Uri("pack://application:,,/Assets/Haptic/chroma.png");
                var sprite_oneChroma = PNGToTanvasTouch.CreateSpriteFromPNG(uri_oneChroma);


                myView.AddSprite(sprite_oneChroma);
                myView.AddSprite(sprite_oneLeft);
                myView.AddSprite(sprite_onePlay);
                myView.AddSprite(sprite_oneSlider);
                myView.AddSprite(sprite_oneTrack);

                instrumentTextOne.Text = predictedInstruments[0].ToUpper();


                // Attach LayoutUpdated event to ensure the layout is complete before updating the sprite positions
                MultiOneScreen.LayoutUpdated += (s, args) =>
                {
                    
                    UpdateSpriteMultiTrack(sprite_oneTrack, sprite_oneSlider, sprite_oneLeft, sprite_onePlay, sprite_oneChroma);
                };

                // Subscribe to SizeChanged event for window resizing
                this.SizeChanged += (s, args) =>
                {
                    UpdateSpriteMultiTrack(sprite_oneTrack, sprite_oneSlider, sprite_oneLeft, sprite_onePlay, sprite_oneChroma);
                };

            }
            else if (SelectedFilesListBox.Items.Count == 2)
            {
                MultiTwoScreen.Visibility = Visibility.Visible;

                UpdateColorsBasedOnInstrument();
                CreateSphere(ref mesh1, sphereTwoFirst, baseRadius);
                CreateSphere(ref mesh2, sphereTwoSecond, baseRadius);

                var uri_twoSlider = new Uri("pack://application:,,/Assets/Haptic/col9.png");
                var sprite_twoSlider = PNGToTanvasTouch.CreateSpriteFromPNG(uri_twoSlider);

                var uri_twoTrack = new Uri("pack://application:,,/Assets/Haptic/01_bc.png");
                var sprite_twoTrack = PNGToTanvasTouch.CreateSpriteFromPNG(uri_twoTrack);

                var uri_twoLeft = new Uri("pack://application:,,/Assets/Haptic/bLeft.png");
                var sprite_twoLeft = PNGToTanvasTouch.CreateSpriteFromPNG(uri_twoLeft);

                var uri_twoPlay = new Uri("pack://application:,,/Assets/Haptic/bPlay.png");
                var sprite_twoPlay = PNGToTanvasTouch.CreateSpriteFromPNG(uri_twoPlay);

                var uri_twoChroma = new Uri("pack://application:,,/Assets/Haptic/chroma.png");
                var sprite_twoChroma = PNGToTanvasTouch.CreateSpriteFromPNG(uri_twoChroma);

                myView.AddSprite(sprite_twoChroma);
                myView.AddSprite(sprite_twoPlay);
                myView.AddSprite(sprite_twoLeft);
                myView.AddSprite(sprite_twoSlider);
                myView.AddSprite(sprite_twoTrack);

                instrumentTextTwoFirst.Text = predictedInstruments[0].ToUpper();
                instrumentTextTwoSecond.Text = predictedInstruments[1].ToUpper();


                // Attach LayoutUpdated event to ensure the layout is complete before updating the sprite positions
                MultiTwoScreen.LayoutUpdated += (s, args) =>
                {
                    UpdateSpriteMultiTrack(sprite_twoTrack, sprite_twoSlider, sprite_twoLeft, sprite_twoPlay, sprite_twoChroma);
                };

                // Subscribe to SizeChanged event for window resizing
                this.SizeChanged += (s, args) =>
                {
                    UpdateSpriteMultiTrack(sprite_twoTrack, sprite_twoSlider, sprite_twoLeft, sprite_twoPlay, sprite_twoChroma);
                };


            }
            else if (SelectedFilesListBox.Items.Count == 3)
            {
                MultiThreeScreen.Visibility = Visibility.Visible;
                UpdateColorsBasedOnInstrument();
                CreateSphere(ref mesh1, sphereThreeFirst, baseRadius);
                CreateSphere(ref mesh2, sphereThreeSecond, baseRadius);
                CreateSphere(ref mesh3, sphereThreeThird, 0.5);

                var uri_threeSlider = new Uri("pack://application:,,/Assets/Haptic/col9.png");
                var sprite_threeSlider = PNGToTanvasTouch.CreateSpriteFromPNG(uri_threeSlider);

                var uri_threeTrack = new Uri("pack://application:,,/Assets/Haptic/01_bc.png");
                var sprite_threeTrack = PNGToTanvasTouch.CreateSpriteFromPNG(uri_threeTrack);

                var uri_threeLeft = new Uri("pack://application:,,/Assets/Haptic/bLeft.png");
                var sprite_threeLeft = PNGToTanvasTouch.CreateSpriteFromPNG(uri_threeLeft);

                var uri_threePlay = new Uri("pack://application:,,/Assets/Haptic/bPlay.png");
                var sprite_threePlay = PNGToTanvasTouch.CreateSpriteFromPNG(uri_threePlay);

                var uri_threeChroma = new Uri("pack://application:,,/Assets/Haptic/chroma.png");
                var sprite_threeChroma = PNGToTanvasTouch.CreateSpriteFromPNG(uri_threeChroma);

                myView.AddSprite(sprite_threeChroma);
                myView.AddSprite(sprite_threePlay);
                myView.AddSprite(sprite_threeLeft);
                myView.AddSprite(sprite_threeSlider);
                myView.AddSprite(sprite_threeTrack);

                instrumentTextThreeFirst.Text = predictedInstruments[0].ToUpper();
                instrumentTextThreeSecond.Text = predictedInstruments[1].ToUpper();
                instrumentTextThreeThird.Text = predictedInstruments[2].ToUpper();

                // Attach LayoutUpdated event to ensure the layout is complete before updating the sprite positions
                MultiThreeScreen.LayoutUpdated += (s, args) =>
                {
                    UpdateSpriteMultiTrack(sprite_threeTrack, sprite_threeSlider, sprite_threeLeft, sprite_threePlay, sprite_threeChroma);
                };

                // Subscribe to SizeChanged event for window resizing
                this.SizeChanged += (s, args) =>
                {
                    UpdateSpriteMultiTrack(sprite_threeTrack, sprite_threeSlider, sprite_threeLeft, sprite_threePlay, sprite_threeChroma);
                };
            }
            else
            {
                MultiFourScreen.Visibility = Visibility.Visible;
                UpdateColorsBasedOnInstrument();
                CreateSphere(ref mesh1, sphereFourFirst, baseRadius);
                CreateSphere(ref mesh2, sphereFourSecond, baseRadius);
                CreateSphere(ref mesh3, sphereFourThird, baseRadius);
                CreateSphere(ref mesh4, sphereFourFourth, baseRadius);

                var uri_fourSlider = new Uri("pack://application:,,/Assets/Haptic/col9.png");
                var sprite_fourSlider = PNGToTanvasTouch.CreateSpriteFromPNG(uri_fourSlider);

                var uri_fourTrack = new Uri("pack://application:,,/Assets/Haptic/01_bc.png");
                var sprite_fourTrack = PNGToTanvasTouch.CreateSpriteFromPNG(uri_fourTrack);

                var uri_fourLeft = new Uri("pack://application:,,/Assets/Haptic/bLeft.png");
                var sprite_fourLeft = PNGToTanvasTouch.CreateSpriteFromPNG(uri_fourLeft);

                var uri_fourPlay = new Uri("pack://application:,,/Assets/Haptic/bPlay.png");
                var sprite_fourPlay = PNGToTanvasTouch.CreateSpriteFromPNG(uri_fourPlay);

                var uri_fourChroma = new Uri("pack://application:,,/Assets/Haptic/chroma.png");
                var sprite_fourChroma = PNGToTanvasTouch.CreateSpriteFromPNG(uri_fourChroma);

                myView.AddSprite(sprite_fourChroma);
                myView.AddSprite(sprite_fourPlay);
                myView.AddSprite(sprite_fourLeft);
                myView.AddSprite(sprite_fourSlider);
                myView.AddSprite(sprite_fourTrack);

                instrumentTextFourFirst.Text = predictedInstruments[0].ToUpper();
                instrumentTextFourSecond.Text = predictedInstruments[1].ToUpper();
                instrumentTextFourThird.Text = predictedInstruments[2].ToUpper();
                instrumentTextFourFourth.Text = predictedInstruments[3].ToUpper();

                // Attach LayoutUpdated event to ensure the layout is complete before updating the sprite positions
                MultiFourScreen.LayoutUpdated += (s, args) =>
                {
                    UpdateSpriteMultiTrack(sprite_fourTrack, sprite_fourSlider, sprite_fourLeft, sprite_fourPlay, sprite_fourChroma);
                };

                // Subscribe to SizeChanged event for window resizing
                this.SizeChanged += (s, args) =>
                {
                    UpdateSpriteMultiTrack(sprite_fourTrack, sprite_fourSlider, sprite_fourLeft, sprite_fourPlay, sprite_fourChroma);
                };
            }

            for (int i = 0; i < SelectedFilesListBox.Items.Count; i++)
            {
                spectralCentroid[i] = GetSpectralCentroid(audioFilePaths[i]);
                onSetDetection[i] = AMethodForNovelty(audioFilePaths[i]);
            }

            maxCentroid = spectralCentroid.SelectMany(centroidList => centroidList).Max();
            NormalizeSpectralCentroids();

            double audioDurationInSeconds = audioStreams[0].TotalTime.TotalSeconds;
            double updateInterval = audioDurationInSeconds / spectralCentroid[0].Count;

            sphereUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(updateInterval)
            };
            sphereUpdateTimer.Tick += SphereUpdateTimer_Tick;

            colorUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(updateInterval)
            };
            colorUpdateTimer.Tick += ColorUpdateTimer_Tick;

            stripesUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            stripesUpdateTimer.Tick += StripesUpdateTimer_Tick;
        }

        private void UpdateSpriteMultiTrack(TSprite sprite_multiTrack, TSprite sprite_multiSlider, TSprite sprite_leftMulti, TSprite sprite_playMulti, TSprite sprite_chromaMulti)
        {
            var dpi = VisualTreeHelper.GetDpi(this);

            var volumeSliderMulti = volumeOneSlider;
            var screenVisibleMulti = MultiOneScreen;
            var stackMulti = oneStack;
            var leftButtonMulti = oneBack;
            var playButtonMulti = buttonOnePlay;
            var chromaButtonMulti = oneChromaButton;

            double screenHeight = this.ActualHeight;

            if (MultiOneScreen.Visibility == Visibility.Visible)
            {
                volumeSliderMulti = volumeOneSlider;
                screenVisibleMulti = MultiOneScreen;
                stackMulti = oneStack;
                leftButtonMulti = oneBack;
                playButtonMulti = buttonOnePlay;
                chromaButtonMulti = oneChromaButton;
            }
            else if (MultiTwoScreen.Visibility == Visibility.Visible)
            {
                volumeSliderMulti = volumeTwoSlider;
                screenVisibleMulti = MultiTwoScreen;
                stackMulti = twoStack;
                leftButtonMulti = twoBack;
                playButtonMulti = buttonTwoPlay;
                chromaButtonMulti = twoChromaButton;
            }

            else if (MultiThreeScreen.Visibility == Visibility.Visible)
            {
                volumeSliderMulti = volumeThreeSlider;
                screenVisibleMulti = MultiThreeScreen;
                stackMulti = threeStack;
                leftButtonMulti = threeBack;
                playButtonMulti = buttonThreePlay;
                chromaButtonMulti = threeChromaButton;
            }

            else if (MultiFourScreen.Visibility == Visibility.Visible)
            {
                volumeSliderMulti = volumeFourSlider;
                screenVisibleMulti = MultiFourScreen;
                stackMulti = fourStack;
                leftButtonMulti = fourBack;
                playButtonMulti = buttonFourPlay;
                chromaButtonMulti = fourChromaButton;
            }

            int widthSliderInPixels = (int)(volumeSliderMulti.ActualWidth * dpi.DpiScaleX);
            int heightSliderInPixels = (int)(volumeSliderMulti.ActualHeight * dpi.DpiScaleY);

            sprite_multiSlider.Width = widthSliderInPixels;
            sprite_multiSlider.Height = heightSliderInPixels;

            Point sliderPosition = volumeSliderMulti.TransformToAncestor(screenVisibleMulti).Transform(new Point(0, 0));

            sprite_multiSlider.X = (float)(sliderPosition.X * dpi.DpiScaleX);
            sprite_multiSlider.Y = (float)(sliderPosition.Y * dpi.DpiScaleY);

            int widthInPixels = (int)(stackMulti.ActualWidth * dpi.DpiScaleX);
            int heightInPixels = (int)(screenHeight * dpi.DpiScaleY);

            sprite_multiTrack.Width = widthInPixels;
            sprite_multiTrack.Height = heightInPixels;

            Point leftPosition = leftButtonMulti.TransformToAncestor(screenVisibleMulti).Transform(new Point(0, 0));

            double leftWidth = leftButtonMulti.ActualWidth;
            double leftHeight = leftButtonMulti.ActualHeight;

            sprite_leftMulti.Width = (int)(leftWidth * dpi.DpiScaleX);
            sprite_leftMulti.Height = (int)(leftHeight * dpi.DpiScaleY);

            sprite_leftMulti.X = (float)(leftPosition.X * dpi.DpiScaleX);
            sprite_leftMulti.Y = (float)(leftPosition.Y * dpi.DpiScaleY);

            Point playPosition = playButtonMulti.TransformToAncestor(screenVisibleMulti).Transform(new Point(0, 0));

            double playWidth = playButtonMulti.ActualWidth;
            double playHeight = playButtonMulti.ActualHeight;

            sprite_playMulti.Width = (int)(playWidth * dpi.DpiScaleX);
            sprite_playMulti.Height = (int)(playHeight * dpi.DpiScaleY);


            sprite_playMulti.X = (float)(playPosition.X * dpi.DpiScaleX);
            sprite_playMulti.Y = (float)(playPosition.Y * dpi.DpiScaleY);

            Point chromaPosition = chromaButtonMulti.TransformToAncestor(screenVisibleMulti).Transform(new Point(0, 0));

            double chromaWidth = chromaButtonMulti.ActualWidth;
            double chromaHeight = chromaButtonMulti.ActualHeight;

            sprite_chromaMulti.Width = (int)(chromaWidth * dpi.DpiScaleX);
            sprite_chromaMulti.Height = (int)(chromaHeight * dpi.DpiScaleY);


            sprite_chromaMulti.X = (float)(chromaPosition.X * dpi.DpiScaleX);
            sprite_chromaMulti.Y = (float)(chromaPosition.Y * dpi.DpiScaleY);

        }



        private void SphereUpdateTimer_Tick(object sender, EventArgs e)
        {
            int streamCount = audioStreams.Count;
            float[] maxAmplitudes = new float[streamCount];
            float[] smoothedAmplitudes = new float[streamCount];
            float[] surfaces = new float[streamCount];
            float[] radii = new float[streamCount];
            float[] prevAmpValues = new float[4];
            ampValues = new float[4];
            test = new float[4];


            for (int i = 0; i < streamCount; i++)
            {
                buffers[i] = new byte[bufferLength];
                amplitudeBuffer.Add(new List<float>());
                maxAmplitudes[i] = GetMaxAmplitude(audioStreams[i], buffers[i]);
                smoothedAmplitudes[i] = SmoothAmplitude(maxAmplitudes[i], amplitudeBuffer[i]);

                ampValues[i] = (smoothedAmplitudes[i] < 0.002) ? 0 : smoothedAmplitudes[i];
                radii[i] = Math.Min(smoothedAmplitudes[i] * volume * 4, 0.9f);
                surfaces[i] = (volume == 0) ? 0 : Math.Min(onSetDetection[i][centroidIndex] * smoothedAmplitudes[i] * 10, 0.05f);

            }

            if (MultiOneScreen.Visibility == Visibility.Visible)
            {
                UpdateSphere(mesh1, radii[0], tDiv, pDiv, surfaces[0]);
            }
            else if (MultiTwoScreen.Visibility == Visibility.Visible)
            {
                UpdateSphere(mesh1, radii[0], tDiv, pDiv, surfaces[0]);
                UpdateSphere(mesh2, radii[1], tDiv, pDiv, surfaces[1]);
            }
            else if (MultiThreeScreen.Visibility == Visibility.Visible)
            {
                UpdateSphere(mesh1, radii[0], tDiv, pDiv, surfaces[0]);
                UpdateSphere(mesh2, radii[1], tDiv, pDiv, surfaces[1]);
                UpdateSphere(mesh3, radii[2] * 0.5, tDiv, pDiv, surfaces[2]);
            }
            else if (MultiFourScreen.Visibility == Visibility.Visible || SingleTrackScreen.Visibility == Visibility.Visible)
            {
                UpdateSphere(mesh1, radii[0], tDiv, pDiv, surfaces[0]);
                UpdateSphere(mesh2, radii[1], tDiv, pDiv, surfaces[1]);
                UpdateSphere(mesh3, radii[2], tDiv, pDiv, surfaces[2]);
                UpdateSphere(mesh4, radii[3], tDiv, pDiv, surfaces[3]);
            }
        }

        private void ColorUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (SingleTrackScreen.Visibility == Visibility.Visible)
            {
                UpdateModelColor();
            }
            else
            {
                UpdateMultiModelColor();
            }
            centroidIndex = (centroidIndex + 1);
        }


        private void UpdateMultiModelColor()
        {
            if (MultiOneScreen.Visibility == Visibility.Visible)
            {
                // Check if the detected instrument for the first file is in the map
                if (instrumentList.Count > 0 && InstrumentColorMap.TryGetValue(instrumentList[0], out Color instrumentColorOne))
                {
                    UpdateSphereColor(sphereOne, instrumentColorOne, spectralCentroid[0][centroidIndex]);
                }
                else
                {
                    // Handle the case where the instrument is not found in the map for the first file
                    Console.WriteLine($"Instrument {instrumentList[0]} not found in map. Using default color.");
                    Dispatcher.Invoke(() =>
                    {
                        sphereOne.Material = new DiffuseMaterial(new SolidColorBrush(Colors.Gray)); // Default color
                    });
                }
            }
            else if (MultiTwoScreen.Visibility == Visibility.Visible)
            {
                if (instrumentList.Count > 1 && InstrumentColorMap.TryGetValue(instrumentList[0], out Color instrumentColorTwoOne))
                {
                    // Update the second sphere (sphereTwoSecond)
                    UpdateSphereColor(sphereTwoFirst, instrumentColorTwoOne, spectralCentroid[0][centroidIndex]);
                }
                if (instrumentList.Count > 1 && InstrumentColorMap.TryGetValue(instrumentList[1], out Color instrumentColorTwoTwo))
                {
                    // Update the second sphere (sphereTwoSecond)
                    UpdateSphereColor(sphereTwoSecond, instrumentColorTwoTwo, spectralCentroid[1][centroidIndex]);
                }
                else
                {
                    Console.WriteLine($"Instrument {instrumentList[1]} not found in map. Using default color.");
                    Dispatcher.Invoke(() =>
                    {
                        sphereTwoSecond.Material = new DiffuseMaterial(new SolidColorBrush(Colors.Gray)); // Default color
                    });

                }
            }
            else if (MultiThreeScreen.Visibility == Visibility.Visible)
            {
                if (instrumentList.Count > 1 && InstrumentColorMap.TryGetValue(instrumentList[0], out Color instrumentColorThreeOne))
                {
                    UpdateSphereColor(sphereThreeFirst, instrumentColorThreeOne, spectralCentroid[0][centroidIndex]);
                }
                if (instrumentList.Count > 1 && InstrumentColorMap.TryGetValue(instrumentList[1], out Color instrumentColorThreeTwo))
                {
                    UpdateSphereColor(sphereThreeSecond, instrumentColorThreeTwo, spectralCentroid[1][centroidIndex]);
                }
                if (instrumentList.Count > 1 && InstrumentColorMap.TryGetValue(instrumentList[2], out Color instrumentColorThreeThree))
                {
                    UpdateSphereColor(sphereThreeThird, instrumentColorThreeThree, spectralCentroid[2][centroidIndex]);
                }

                else
                {
                    Console.WriteLine($"Instrument {instrumentList[1]} not found in map. Using default color.");
                    Dispatcher.Invoke(() =>
                    {
                        sphereTwoSecond.Material = new DiffuseMaterial(new SolidColorBrush(Colors.Gray)); // Default color
                    });

                }

            }
            else if (MultiFourScreen.Visibility == Visibility.Visible)
            {
                if (instrumentList.Count > 1 && InstrumentColorMap.TryGetValue(instrumentList[0], out Color instrumentColorFourOne))
                {
                    UpdateSphereColor(sphereFourFirst, instrumentColorFourOne, spectralCentroid[0][centroidIndex]);
                }
                if (instrumentList.Count > 1 && InstrumentColorMap.TryGetValue(instrumentList[1], out Color instrumentColorFourTwo))
                {
                    UpdateSphereColor(sphereFourSecond, instrumentColorFourTwo, spectralCentroid[1][centroidIndex]);
                }
                if (instrumentList.Count > 1 && InstrumentColorMap.TryGetValue(instrumentList[2], out Color instrumentColorFourThree))
                {
                    UpdateSphereColor(sphereFourThird, instrumentColorFourThree, spectralCentroid[2][centroidIndex]);
                }
                if (instrumentList.Count > 1 && InstrumentColorMap.TryGetValue(instrumentList[3], out Color instrumentColorFourFourth))
                {
                    UpdateSphereColor(sphereFourFourth, instrumentColorFourFourth, spectralCentroid[3][centroidIndex]);
                }

                else
                {
                    Console.WriteLine($"Instrument {instrumentList[1]} not found in map. Using default color.");
                    Dispatcher.Invoke(() =>
                    {
                        sphereTwoSecond.Material = new DiffuseMaterial(new SolidColorBrush(Colors.Gray)); // Default color
                    });
                }
            }
        }

        // Helper method to update the sphere color
        private void UpdateSphereColor(GeometryModel3D sphere, Color instrumentColor, float intensity)
        {
            // Calculate the dark version of the instrument color
            byte darkR = (byte)(instrumentColor.R / 2);
            byte darkG = (byte)(instrumentColor.G / 2);
            byte darkB = (byte)(instrumentColor.B / 2);

            // Calculate the interpolated RGB values based on intensity
            byte r = (byte)(darkR + (intensity * (instrumentColor.R - darkR)));
            byte g = (byte)(darkG + (intensity * (instrumentColor.G - darkG)));
            byte b = (byte)(darkB + (intensity * (instrumentColor.B - darkB)));

            // Create the brush and material
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            var material = new DiffuseMaterial(brush);

            // Update the sphere material on the UI thread
            Dispatcher.Invoke(() =>
            {
                sphere.Material = material;
            });
        }


        private void UpdateModelColor()
        {
            float bass_intensity = spectralCentroid[0][centroidIndex];
            float drum_intensity = spectralCentroid[1][centroidIndex];
            float other_intensity = spectralCentroid[2][centroidIndex];
            float voice_intensity = spectralCentroid[3][centroidIndex];

            // Define the RGB values for light blue and dark blue
            byte lightBlueR = 94, lightBlueG = 164, lightBlueB = 255;
            byte darkBlueR = 0, darkBlueG = 0, darkBlueB = 119;

            // Define the RGB values for light red and dark red
            byte lightRedR = 255, lightRedG = 100, lightRedB = 100;
            byte darkRedR = 139, darkRedG = 0, darkRedB = 0;

            // Define the RGB values for light green and dark green
            byte lightGreenR = 144, lightGreenG = 238, lightGreenB = 144;
            byte darkGreenR = 0, darkGreenG = 100, darkGreenB = 0;

            // Define the RGB values for light yellow and dark yellow
            byte lightYellowR = 255, lightYellowG = 255, lightYellowB = 153;
            byte darkYellowR = 139, darkYellowG = 117, darkYellowB = 0;

            // Calculate the interpolated RGB values for blue - bass
            byte r_bass = (byte)(darkBlueR + (voice_intensity * (lightBlueR - darkBlueR)));
            byte g_bass = (byte)(darkBlueG + (voice_intensity * (lightBlueG - darkBlueG)));
            byte b_bass = (byte)(darkBlueB + (voice_intensity * (lightBlueB - darkBlueB)));

            // Calculate the interpolated RGB values for red - drum
            byte r_drum = (byte)(darkRedR + (drum_intensity * (lightRedR - darkRedR)));
            byte g_drum = (byte)(darkRedG + (drum_intensity * (lightRedG - darkRedG)));
            byte b_drum = (byte)(darkRedB + (drum_intensity * (lightRedB - darkRedB)));

            // Calculate the interpolated RGB values for green - other
            byte r_other = (byte)(darkGreenR + (other_intensity * (lightGreenR - darkGreenR)));
            byte g_other = (byte)(darkGreenG + (other_intensity * (lightGreenG - darkGreenG)));
            byte b_other = (byte)(darkGreenB + (other_intensity * (lightGreenB - darkGreenB)));

            // Calculate the interpolated RGB values for yellow - voice
            byte r_voice = (byte)(darkYellowR + (bass_intensity * (lightYellowR - darkYellowR)));
            byte g_voice = (byte)(darkYellowG + (bass_intensity * (lightYellowG - darkYellowG)));
            byte b_voice = (byte)(darkYellowB + (bass_intensity * (lightYellowB - darkYellowB)));

            // Create the brush and material
            var brush_bass = new SolidColorBrush(Color.FromRgb(r_bass, g_bass, b_bass));
            var material_bass = new DiffuseMaterial(brush_bass);

            var brush_drum = new SolidColorBrush(Color.FromRgb(r_drum, g_drum, b_drum));
            var material_drum = new DiffuseMaterial(brush_drum);

            var brush_other = new SolidColorBrush(Color.FromRgb(r_other, g_other, b_other));
            var material_other = new DiffuseMaterial(brush_other);

            var brush_voice = new SolidColorBrush(Color.FromRgb(r_voice, g_voice, b_voice));
            var material_voice = new DiffuseMaterial(brush_voice);

            Dispatcher.Invoke(() =>
            {
                sphereBass.Material = material_bass;
                sphereDrum.Material = material_drum;
                sphereOther.Material = material_other;
                sphereVoice.Material = material_voice;
            });

        }

        private void StripesUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (SingleTrackScreen.Visibility == Visibility.Visible)
            {
                UpdateStripesColor();
            }
            else
            {
                UpdateMultiStripesColor();
            }

            mfccIndex = (mfccIndex + 1);
        }

        private void SingleLoaded(object sender, RoutedEventArgs e)
        {
            viewSingleTracker = new TanvasTouchViewTracker(this);
        }
        private void SingleTrack_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateStripesColor();
        }

        private void UpdateStripesColor()
        {
            // Define the RGB values for light and dark colors
            var colorRanges = new (byte lightR, byte lightG, byte lightB, byte darkR, byte darkG, byte darkB)[]
            {
        (94, 164, 255, 0, 0, 119),    // Blue for bass
        (255, 102, 102, 139, 0, 0),   // Red for drum
        (144, 238, 144, 0, 100, 0),   // Green for other
        (255, 255, 0, 204, 153, 0)    // Yellow for voice
            };

            // Determine the number of columns and matrices based on the mode
            int numColumns = mfccMode ? 7 : 12;
            var matrices = mfccMode ? mfccMatrix : chromaMatrix;
            // Define the URIs for the images
            var uris = new[]
            {
                new Uri("pack://application:,,/Assets/Haptic/col0.png"),
                new Uri("pack://application:,,/Assets/Haptic/col1.png"),
                new Uri("pack://application:,,/Assets/Haptic/col2.png"),
                new Uri("pack://application:,,/Assets/Haptic/col3.png"),
                new Uri("pack://application:,,/Assets/Haptic/col4.png"),
                new Uri("pack://application:,,/Assets/Haptic/col5.png"),
                new Uri("pack://application:,,/Assets/Haptic/col6.png"),
                new Uri("pack://application:,,/Assets/Haptic/col7.png"),
                new Uri("pack://application:,,/Assets/Haptic/col8.png"),
                new Uri("pack://application:,,/Assets/Haptic/col9.png"),
            };

            // Remove all existing sprites
            mySingleView.RemoveAllSprites();

            // Get the DPI information
            var dpi = VisualTreeHelper.GetDpi(this);
            double bassX = 0;
            // Get the Grid
            if (gridBass == null || gridBass.ColumnDefinitions.Count == 0)
            {
                return;
            }

            // Calculate the grid's position relative to the main window
            Point gridDrumPosition = gridDrum.TransformToVisual(Application.Current.MainWindow).Transform(new Point(0, 0));
            double gridDrumLeft = gridDrumPosition.X;
            double gridDrumTop = gridDrumPosition.Y;

            // Iterate through each column in the grid
            for (int i = 0; i < gridBass.ColumnDefinitions.Count; i++)
            {
                float bassValue = matrices[0][i][mfccIndex];
                float drumValue = matrices[1][i][mfccIndex];
                float otherValue = matrices[2][i][mfccIndex];
                float voiceValue = matrices[3][i][mfccIndex];

                // Determine the URI based on the value range
                Uri uriBass = uris[(int)(bassValue * 10)];
                Uri uriDrum = uris[(int)(drumValue * 10)];
                Uri uriOther = uris[(int)(otherValue * 10)];
                Uri uriVoice = uris[(int)(voiceValue * 10)];

                // Create a new sprite instance for each column
                var bassSprite = PNGToTanvasTouch.CreateSpriteFromPNG(uriBass);
                var drumSprite = PNGToTanvasTouch.CreateSpriteFromPNG(uriDrum);
                var otherSprite = PNGToTanvasTouch.CreateSpriteFromPNG(uriOther);
                var voiceSprite = PNGToTanvasTouch.CreateSpriteFromPNG(uriVoice);

                var column = gridBass.ColumnDefinitions[i];
                double columnWidth = column.ActualWidth; // Width in DIPs
                double columnHeight = gridBass.ActualHeight;  // Height in DIPs

                // Convert DIPs to pixels
                int widthInPixels = (int)(columnWidth * dpi.DpiScaleX);
                int heightInPixels = (int)(columnHeight * dpi.DpiScaleY);

                bassSprite.Width = widthInPixels;
                bassSprite.Height = heightInPixels;

                drumSprite.Width = widthInPixels;
                drumSprite.Height = heightInPixels;

                otherSprite.Width = widthInPixels;
                otherSprite.Height = heightInPixels;

                voiceSprite.Width = widthInPixels;
                voiceSprite.Height = heightInPixels;

                // Set the sprite position
                bassSprite.X = (int)((bassX + SingleStack.ActualWidth) * dpi.DpiScaleX);
                bassSprite.Y = 0; // Align with the top of the grid

                drumSprite.X = (int)((bassX + SingleStack.ActualWidth + gridBass.ActualWidth) * dpi.DpiScaleX);
                drumSprite.Y = 0;

                otherSprite.X = (int)((bassX + SingleStack.ActualWidth) * dpi.DpiScaleX);
                otherSprite.Y = (int)(gridBass.ActualHeight * dpi.DpiScaleY);

                voiceSprite.X = (int)((bassX + SingleStack.ActualWidth + gridBass.ActualWidth) * dpi.DpiScaleX);
                voiceSprite.Y = (int)(gridBass.ActualHeight * dpi.DpiScaleY);

                // Add the sprite to the view
                mySingleView.AddSprite(bassSprite);
                mySingleView.AddSprite(drumSprite);
                mySingleView.AddSprite(otherSprite);
                mySingleView.AddSprite(voiceSprite);

                bassX += columnWidth;
            }

            // Iterate through each column (stripe)
            for (int column = 0; column < numColumns; column++)
            {
                // Iterate through each matrix (bass, drum, other, voice)
                for (int i = 0; i < colorRanges.Length; i++)
                {
                    float value = matrices[i][column][mfccIndex];
                    var (lightR, lightG, lightB, darkR, darkG, darkB) = colorRanges[i];

                    // Calculate the interpolated RGB values
                    byte r = (byte)(darkR + (value * (lightR - darkR)));
                    byte g = (byte)(darkG + (value * (lightG - darkG)));
                    byte b = (byte)(darkB + (value * (lightB - darkB)));

                    byte r_def = (byte)(darkR);
                    byte g_def = (byte)(darkG);
                    byte b_def = (byte)(darkB);

                    // Create the brush
                    var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
                    var default_brush = new SolidColorBrush(Color.FromRgb(r_def, g_def, b_def));

                    // Update the color of the Rectangle (stripe)
                    Dispatcher.Invoke(() =>
                    {
                        if (i == 0)
                        {
                            ((Rectangle)gridBass.Children[column]).Fill = brush;
                        }
                        else if (i == 1) ((Rectangle)gridDrum.Children[column]).Fill = brush;
                        else if (i == 2) ((Rectangle)gridOther.Children[column]).Fill = brush;
                        else if (i == 3) ((Rectangle)gridVoice.Children[column]).Fill = brush;
                    });
                }
            }
        }

        private void UpdateMultiStripesColor()
        {
            var brushesList = new List<SolidColorBrush>();
            // Determine the number of columns and matrices based on the mode
            int numColumns = mfccMode ? 7 : 12;
            var matrices = mfccMode ? mfccMatrix : chromaMatrix;
            myView.RemoveAllSprites();

            // Define the URIs for the images
            var uris = new[]
            {
                new Uri("pack://application:,,/Assets/Haptic/col0.png"),
                new Uri("pack://application:,,/Assets/Haptic/col1.png"),
                new Uri("pack://application:,,/Assets/Haptic/col2.png"),
                new Uri("pack://application:,,/Assets/Haptic/col3.png"),
                new Uri("pack://application:,,/Assets/Haptic/col4.png"),
                new Uri("pack://application:,,/Assets/Haptic/col5.png"),
                new Uri("pack://application:,,/Assets/Haptic/col6.png"),
                new Uri("pack://application:,,/Assets/Haptic/col7.png"),
                new Uri("pack://application:,,/Assets/Haptic/col8.png"),
                new Uri("pack://application:,,/Assets/Haptic/col9.png"),
            };

            // Get the DPI information
            var dpi = VisualTreeHelper.GetDpi(this);
            double startX = 0;
            double startThreeX = 0;

            if (MultiOneScreen.Visibility == Visibility.Visible)
            {
                // Iterate through each column in the grid
                for (int i = 0; i < gridOne.ColumnDefinitions.Count; i++)
                {

                    float oneValue = matrices[0][i][mfccIndex];

                    // Determine the URI based on the value range
                    Uri uriOne = uris[(int)(oneValue * 10)];

                    // Create a new sprite instance for each column
                    var testSprite = PNGToTanvasTouch.CreateSpriteFromPNG(uriOne);

                    var column = gridOne.ColumnDefinitions[i];
                    double columnWidth = column.ActualWidth; // Width in DIPs
                    double columnHeight = gridOne.ActualHeight;  // Height in DIPs

                    // Convert DIPs to pixels
                    int widthInPixels = (int)(columnWidth * dpi.DpiScaleX);
                    int heightInPixels = (int)(columnHeight * dpi.DpiScaleY);

                    testSprite.Width = widthInPixels;
                    testSprite.Height = heightInPixels;


                    // Set the sprite position
                    testSprite.X = (int)((startX + oneStack.ActualWidth) * dpi.DpiScaleX);
                    testSprite.Y = 0; // Align with the top of the grid

                    myView.AddSprite(testSprite);

                    startX += columnWidth;
                }
            }
            else if (MultiTwoScreen.Visibility == Visibility.Visible)
            {
                // Iterate through each column in the grid
                for (int i = 0; i < gridTwoFirst.ColumnDefinitions.Count; i++)
                {

                    float leftValue = matrices[0][i][mfccIndex];
                    float rightValue = matrices[1][i][mfccIndex];

                    // Determine the URI based on the value range
                    Uri uriLeft = uris[(int)(leftValue * 10)];
                    Uri uriRight = uris[(int)(rightValue * 10)];

                    // Create a new sprite instance for each column
                    var leftSprite = PNGToTanvasTouch.CreateSpriteFromPNG(uriLeft);
                    var rightSprite = PNGToTanvasTouch.CreateSpriteFromPNG(uriRight);

                    var column = gridTwoFirst.ColumnDefinitions[i];
                    double columnWidth = column.ActualWidth; // Width in DIPs
                    double columnHeight = gridTwoFirst.ActualHeight;  // Height in DIPs

                    // Convert DIPs to pixels
                    int widthInPixels = (int)(columnWidth * dpi.DpiScaleX);
                    int heightInPixels = (int)(columnHeight * dpi.DpiScaleY);

                    leftSprite.Width = widthInPixels;
                    leftSprite.Height = heightInPixels;

                    rightSprite.Width = widthInPixels;
                    rightSprite.Height = heightInPixels;

                    // Set the sprite position
                    leftSprite.X = (int)((startX + twoStack.ActualWidth) * dpi.DpiScaleX);
                    leftSprite.Y = 0; // Align with the top of the grid

                    rightSprite.X = (int)((startX + twoStack.ActualWidth + gridTwoFirst.ActualWidth) * dpi.DpiScaleX);
                    rightSprite.Y = 0;

                    // Add the sprite to the view
                    myView.AddSprite(leftSprite);
                    myView.AddSprite(rightSprite);

                    startX += columnWidth;
                }

            }
            else if (MultiThreeScreen.Visibility == Visibility.Visible)
            {
                // Iterate through each column in the grid
                for (int i = 0; i < gridThreeFirst.ColumnDefinitions.Count; i++)
                {

                    float firstValue = matrices[0][i][mfccIndex];
                    float secondValue = matrices[1][i][mfccIndex];
                    float thirdValue = matrices[2][i][mfccIndex];

                    // Determine the URI based on the value range
                    Uri uriFirst = uris[(int)(firstValue * 10)];
                    Uri uriSecond = uris[(int)(secondValue * 10)];
                    Uri uriThird = uris[(int)(thirdValue * 10)];

                    // Create a new sprite instance for each column
                    var firstSprite = PNGToTanvasTouch.CreateSpriteFromPNG(uriFirst);
                    var secondSprite = PNGToTanvasTouch.CreateSpriteFromPNG(uriSecond);
                    var thirdSprite = PNGToTanvasTouch.CreateSpriteFromPNG(uriThird);

                    var columnTop = gridThreeFirst.ColumnDefinitions[i];
                    double columnTopWidth = columnTop.ActualWidth; // Width in DIPs
                    double columnTopHeight = gridThreeFirst.ActualHeight;  // Height in DIPs

                    var columnBottom = gridThreeThird.ColumnDefinitions[i];
                    double columnBottomWidth = columnBottom.ActualWidth; // Width in DIPs
                    double columnBottomHeight = gridThreeThird.ActualHeight;  // Height in DIPs

                    // Convert DIPs to pixels
                    int widthTopInPixels = (int)(columnTopWidth * dpi.DpiScaleX);
                    int heightTopInPixels = (int)(columnTopHeight * dpi.DpiScaleY);

                    int widthBottomInPixels = (int)(columnBottomWidth * dpi.DpiScaleX);
                    int heightBottomInPixels = (int)(columnBottomHeight * dpi.DpiScaleY);

                    firstSprite.Width = widthTopInPixels;
                    firstSprite.Height = heightTopInPixels;

                    secondSprite.Width = widthTopInPixels;
                    secondSprite.Height = heightTopInPixels;

                    thirdSprite.Width = widthBottomInPixels;
                    thirdSprite.Height = heightBottomInPixels;

                    // Set the sprite position
                    firstSprite.X = (int)((startX + threeStack.ActualWidth) * dpi.DpiScaleX);
                    firstSprite.Y = 0; // Align with the top of the grid

                    secondSprite.X = (int)((startX + threeStack.ActualWidth + gridThreeFirst.ActualWidth) * dpi.DpiScaleX);
                    secondSprite.Y = 0;

                    thirdSprite.X = (int)((startThreeX + threeStack.ActualWidth) * dpi.DpiScaleX);
                    thirdSprite.Y = heightTopInPixels;

                    // Add the sprite to the view
                    myView.AddSprite(firstSprite);
                    myView.AddSprite(secondSprite);
                    myView.AddSprite(thirdSprite);

                    startX += columnTopWidth;
                    startThreeX += columnBottomWidth;
                }

            }
            else if (MultiFourScreen.Visibility == Visibility.Visible)
            {
                // Iterate through each column in the grid
                for (int i = 0; i < gridFourFirst.ColumnDefinitions.Count; i++)
                {

                    float firstValue = matrices[0][i][mfccIndex];
                    float secondValue = matrices[1][i][mfccIndex];
                    float thirdValue = matrices[2][i][mfccIndex];
                    float fourthValue = matrices[3][i][mfccIndex];

                    // Determine the URI based on the value range
                    Uri uriFirst = uris[(int)(firstValue * 10)];
                    Uri uriSecond = uris[(int)(secondValue * 10)];
                    Uri uriThird = uris[(int)(thirdValue * 10)];
                    Uri uriFourth = uris[(int)(fourthValue * 10)];

                    // Create a new sprite instance for each column
                    var firstSprite = PNGToTanvasTouch.CreateSpriteFromPNG(uriFirst);
                    var secondSprite = PNGToTanvasTouch.CreateSpriteFromPNG(uriSecond);
                    var thirdSprite = PNGToTanvasTouch.CreateSpriteFromPNG(uriThird);
                    var fourthSprite = PNGToTanvasTouch.CreateSpriteFromPNG(uriFourth);

                    var column = gridFourFirst.ColumnDefinitions[i];
                    double columnWidth = column.ActualWidth; // Width in DIPs
                    double columnHeight = gridFourFirst.ActualHeight;  // Height in DIPs

                    // Convert DIPs to pixels
                    int widthInPixels = (int)(columnWidth * dpi.DpiScaleX);
                    int heightInPixels = (int)(columnHeight * dpi.DpiScaleY);

                    firstSprite.Width = widthInPixels;
                    firstSprite.Height = heightInPixels;

                    secondSprite.Width = widthInPixels;
                    secondSprite.Height = heightInPixels;

                    thirdSprite.Width = widthInPixels;
                    thirdSprite.Height = heightInPixels;

                    fourthSprite.Width = widthInPixels;
                    fourthSprite.Height = heightInPixels;

                    // Set the sprite position
                    firstSprite.X = (int)((startX + fourStack.ActualWidth) * dpi.DpiScaleX);
                    firstSprite.Y = 0; // Align with the top of the grid

                    secondSprite.X = (int)((startX + fourStack.ActualWidth + gridFourFirst.ActualWidth) * dpi.DpiScaleX);
                    secondSprite.Y = 0;

                    thirdSprite.X = (int)((startX + fourStack.ActualWidth) * dpi.DpiScaleX);
                    thirdSprite.Y = heightInPixels;

                    fourthSprite.X = (int)((startX + fourStack.ActualWidth + gridFourFirst.ActualWidth) * dpi.DpiScaleX);
                    fourthSprite.Y = heightInPixels;

                    // Add the sprite to the view
                    myView.AddSprite(firstSprite);
                    myView.AddSprite(secondSprite);
                    myView.AddSprite(thirdSprite);
                    myView.AddSprite(fourthSprite);

                    startX += columnWidth;
                }

            }

            // Iterate through each column (stripe)
            for (int column = 0; column < numColumns; column++)
            {
                // Iterate through each matrix (bass, drum, other, voice)
                for (int i = 0; i < instrumentList.Count; i++)
                {
                    string instrument = instrumentList[i];

                    if (InstrumentColorMap.TryGetValue(instrument, out Color instrumentColor))
                    {
                        // Get the intensity value from the matrix
                        float value = matrices[i][column][mfccIndex];

                        // Calculate a darker version of the instrument color
                        byte darkR = (byte)(instrumentColor.R / 2);
                        byte darkG = (byte)(instrumentColor.G / 2);
                        byte darkB = (byte)(instrumentColor.B / 2);

                        // Calculate the interpolated RGB values
                        byte r = (byte)(darkR + (value * (instrumentColor.R - darkR)));
                        byte g = (byte)(darkG + (value * (instrumentColor.G - darkG)));
                        byte b = (byte)(darkB + (value * (instrumentColor.B - darkB)));

                        // Create the brush with the calculated color
                        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
                        brushesList.Add(brush);

                        // Update the color of the Rectangle (stripe) on the UI thread

                        Dispatcher.Invoke(() =>
                        {
                            if (i == 0)
                            {
                                if (MultiOneScreen.Visibility == Visibility.Visible)
                                {
                                    if (column < gridOne.Children.Count)
                                    {
                                        ((Rectangle)gridOne.Children[column]).Fill = brush;
                                    }
                                }
                                else if (MultiTwoScreen.Visibility == Visibility.Visible)
                                {
                                    if (column < gridTwoFirst.Children.Count)
                                    {
                                        ((Rectangle)gridTwoFirst.Children[column]).Fill = brush;
                                    }
                                    else
                                    {
                                    }
                                }
                                else if (MultiThreeScreen.Visibility == Visibility.Visible)
                                {
                                    if (column < gridThreeFirst.Children.Count)
                                    {
                                        ((Rectangle)gridThreeFirst.Children[column]).Fill = brush;
                                    }
                                    else
                                    {
                                    }
                                }
                                else if (MultiFourScreen.Visibility == Visibility.Visible)
                                {
                                    if (column < gridFourFirst.Children.Count)
                                    {
                                        ((Rectangle)gridFourFirst.Children[column]).Fill = brush;
                                    }
                                    else
                                    {
                                    }
                                }
                            }
                            else if (i == 1)
                            {
                                if (MultiTwoScreen.Visibility == Visibility.Visible)
                                {
                                    if (column < gridTwoSecond.Children.Count)
                                    {
                                        ((Rectangle)gridTwoSecond.Children[column]).Fill = brush;
                                    }
                                    else
                                    {
                                    }
                                }
                                else if (MultiThreeScreen.Visibility == Visibility.Visible)
                                {
                                    if (column < gridThreeSecond.Children.Count)
                                    {
                                        ((Rectangle)gridThreeSecond.Children[column]).Fill = brush;
                                    }
                                    else
                                    {
                                    }
                                }
                                else if (MultiFourScreen.Visibility == Visibility.Visible)
                                {
                                    if (column < gridFourSecond.Children.Count)
                                    {
                                        ((Rectangle)gridFourSecond.Children[column]).Fill = brush;
                                    }
                                    else
                                    {
                                    }
                                }
                            }
                            else if (i == 2)
                            {
                                if (MultiThreeScreen.Visibility == Visibility.Visible)
                                {
                                    if (column < gridThreeThird.Children.Count)
                                    {
                                        ((Rectangle)gridThreeThird.Children[column]).Fill = brush;
                                    }
                                    else
                                    {
                                    }
                                }
                                else if (MultiFourScreen.Visibility == Visibility.Visible)
                                {
                                    if (column < gridFourThird.Children.Count)
                                    {
                                        ((Rectangle)gridFourThird.Children[column]).Fill = brush;
                                    }
                                    else
                                    {
                                    }
                                }
                            }
                            else if (i == 3)
                            {
                                if (MultiFourScreen.Visibility == Visibility.Visible)
                                {
                                    if (column < gridFourFourth.Children.Count)
                                    {
                                        ((Rectangle)gridFourFourth.Children[column]).Fill = brush;
                                    }
                                    else
                                    {
                                    }
                                }
                            }


                        });
                    }
                    else
                    {
                        // Handle case where the instrument is not in the map (optional)
                        Console.WriteLine($"Instrument {instrument} not found in map. Skipping color update.");
                    }
                }
            }
        }

        private float SmoothAmplitude(float currentAmplitude, List<float> amplitudeBuffer)
        {
            amplitudeBuffer.Add(currentAmplitude);

            if (amplitudeBuffer.Count > smoothingWindowSize)
            {
                amplitudeBuffer.RemoveAt(0);
            }

            float sum = 0;
            foreach (float amplitude in amplitudeBuffer)
            {
                sum += amplitude;
            }

            return sum / amplitudeBuffer.Count;
        }

        private float GetMaxAmplitude(WaveStream stream, byte[] buffer)
        {
            long currentPosition = stream.Position;
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead == 0)
            {
                return 0;
            }

            float maxAmplitude = 0;
            for (int i = 0; i < bytesRead; i += 4)
            {
                float sample = BitConverter.ToSingle(buffer, i);
                float amplitude = Math.Abs(sample);
                if (amplitude > maxAmplitude)
                    maxAmplitude = amplitude;
            }

            stream.Position = currentPosition;
            return maxAmplitude;
        }

        private void CreateSphere(ref MeshGeometry3D mesh, GeometryModel3D model, double radius)
        {
            mesh = new MeshGeometry3D();
            UpdateSphere(mesh, radius, tDiv, pDiv, 0);
            model.Geometry = mesh;
        }

        private void UpdateSphere(MeshGeometry3D mesh, double radius, int tDiv, int pDiv, double amplitude)
        {
            Point3DCollection positions = new Point3DCollection();
            Int32Collection indices = new Int32Collection();
            Vector3DCollection normals = new Vector3DCollection();

            for (int t = 0; t <= tDiv; t++)
            {
                double theta = t * Math.PI / tDiv;
                double sinTheta = Math.Sin(theta);
                double cosTheta = Math.Cos(theta);

                for (int p = 0; p <= pDiv; p++)
                {
                    double phi = p * 2 * Math.PI / pDiv;
                    double x = sinTheta * Math.Cos(phi);
                    double y = cosTheta;
                    double z = sinTheta * Math.Sin(phi);

                    if (radius > 0.9)
                    {
                        radius = 0.9;
                    }

                    double variableRadius = radius + amplitude * Math.Sin(5 * theta) * Math.Sin(5 * phi);

                    positions.Add(new Point3D(variableRadius * x, variableRadius * y, variableRadius * z));
                    normals.Add(new Vector3D(x, y, z));

                    if (t < tDiv && p < pDiv)
                    {
                        int first = (t * (pDiv + 1)) + p;
                        int second = first + pDiv + 1;

                        indices.Add(first);
                        indices.Add(second);
                        indices.Add(first + 1);

                        indices.Add(second);
                        indices.Add(second + 1);
                        indices.Add(first + 1);
                    }
                }
            }

            mesh.Positions = positions;
            mesh.TriangleIndices = indices;
            mesh.Normals = normals;
        }

        // Method to clear previously initialized audio resources
        private void ClearAudioResources()
        {
            foreach (var output in audioOutputs)
            {
                output.Stop();
                output.Dispose();
            }
            foreach (var stream in audioStreams)
            {
                stream.Dispose();
            }

            audioOutputs.Clear();
            audioStreams.Clear();
            streamPosition.Clear();
        }

        private void FinalizeSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Files have been finalized.", "Finalize", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // SPECTRAL CENTROID
        private List<float> GetSpectralCentroid(string filePath)
        {
            List<float> centroidList = new List<float>();
            using (Py.GIL())
            {
                dynamic np = Py.Import("numpy");
                dynamic librosa = Py.Import("librosa");
                dynamic spectral_centroid = librosa.feature.spectral_centroid(y: librosa.load(filePath, sr: null)[0], sr: librosa.load(filePath, sr: null)[1], n_fft: 1024, hop_length: 512)[0];
                foreach (var value in spectral_centroid)
                {
                    centroidList.Add((float)value);
                }
            }
            return centroidList;
        }

        private void NormalizeSpectralCentroids()
        {

            for (int i = 0; i < audioStreams.Count; i++)
            {
                for (int j = 0; j < spectralCentroid[i].Count; j++)
                {
                    spectralCentroid[i][j] = (spectralCentroid[i][j] / maxCentroid);
                }

            }
        }

        // MFCC
        public (List<List<float>> FullMFCCMatrix, List<List<float>> AggregatedMFCCMatrix) GetMFCCMatrices(string filePath, int n_mfcc = 7, int intervalSeconds = 5)
        {
            List<List<float>> mfccMatrixSingle = new List<List<float>>();
            List<List<float>> fullMfccMatrix = new List<List<float>>();

            using (Py.GIL())
            {
                dynamic np = Py.Import("numpy");
                dynamic librosa = Py.Import("librosa");

                // Load the audio file
                dynamic y_sr = librosa.load(filePath, sr: null);
                dynamic y = y_sr[0];
                dynamic sr = y_sr[1];

                // Compute MFCCs
                //dynamic mfcc = librosa.feature.mfcc(y: y, sr: sr, n_mfcc: n_mfcc, n_fft: 1024, hop_length: 512);
                dynamic mfcc = librosa.feature.mfcc(y: y, sr: sr, n_mfcc: n_mfcc);

                // Find min and max values in the MFCC matrix
                float min = (float)np.min(mfcc);
                float max = (float)np.max(mfcc);

                // Calculate number of frames per 5-second interval
                int frames_per_interval = intervalSeconds * (int)sr / 512;
                int totalFrames = mfcc.shape[1];

                // Calculate number of intervals
                int num_intervals = (int)Math.Ceiling((double)totalFrames / frames_per_interval);

                // Populate the full MFCC matrix
                for (int j = 0; j < n_mfcc; j++)
                {
                    List<float> mfccCoefficients = new List<float>();

                    for (int k = 0; k < totalFrames; k++)
                    {

                        mfccCoefficients.Add((float)mfcc[j][k]);
                    }

                    fullMfccMatrix.Add(mfccCoefficients);
                }

                // Aggregate MFCC values over intervals
                for (int j = 0; j < n_mfcc; j++)
                {
                    List<float> mfccCoefficients = new List<float>();

                    for (int i = 0; i < num_intervals; i++)
                    {
                        int start = i * frames_per_interval;
                        int end = Math.Min(start + frames_per_interval, totalFrames);

                        float sum = 0;
                        int count = 0;

                        for (int k = start; k < end; k++)
                        {
                            sum += (float)mfcc[j][k];
                            count++;
                        }

                        float meanValue = sum / count;
                        // Normalize to range [0, 1]
                        float normalizedValue = (meanValue - min) / (max - min);

                        // Add normalized MFCC value for the interval to the corresponding coefficient list
                        mfccCoefficients.Add(normalizedValue);
                    }

                    mfccMatrixSingle.Add(mfccCoefficients);
                }
            }

            return (FullMFCCMatrix: fullMfccMatrix, AggregatedMFCCMatrix: mfccMatrixSingle);
        }



        public List<List<float>> GetChromagramMatrix(string filePath, int n_chroma = 12, int intervalSeconds = 5)
        {
            List<List<float>> chromaMatrixSingle = new List<List<float>>();

            using (Py.GIL())
            {
                dynamic np = Py.Import("numpy");
                dynamic librosa = Py.Import("librosa");

                // Load the audio file
                dynamic y_sr = librosa.load(filePath, sr: null);
                dynamic y = y_sr[0];
                dynamic sr = y_sr[1];

                // Compute the chromagram
                dynamic chromagram = librosa.feature.chroma_stft(y: y, sr: sr, n_fft: 1024, hop_length: 512);

                // Find min and max values in the chromagram matrix
                float min = (float)np.min(chromagram);
                float max = (float)np.max(chromagram);

                // Calculate number of frames per 5-second interval
                int frames_per_interval = intervalSeconds * (int)sr / 512;
                int totalFrames = chromagram.shape[1];

                // Calculate number of intervals
                int num_intervals = (int)Math.Ceiling((double)totalFrames / frames_per_interval);

                // Aggregate chromagram values over intervals
                for (int j = 0; j < n_chroma; j++)
                {
                    List<float> chromaCoefficients = new List<float>();

                    for (int i = 0; i < num_intervals; i++)
                    {
                        int start = i * frames_per_interval;
                        int end = Math.Min(start + frames_per_interval, totalFrames);

                        float sum = 0;
                        int count = 0;

                        for (int k = start; k < end; k++)
                        {
                            sum += (float)chromagram[j][k];
                            count++;
                        }

                        float meanValue = sum / count;

                        chromaCoefficients.Add(meanValue);
                    }

                    chromaMatrixSingle.Add(chromaCoefficients);
                }
            }

            return chromaMatrixSingle;
        }


        // ONSET DETECTION
        public static List<float> ComputeLocalAverage(List<float> x, float M, float Fs = 1)
        {
            List<float> localAverage = new List<float>();
            using (Py.GIL())
            {
                dynamic np = Py.Import("numpy");
                int L = x.Count;
                M = (float)Math.Ceiling(M * Fs);
                List<float> local_average = new List<float>();
                for (int m = 0; m < L; m++)
                {
                    int a = Math.Max(m - (int)M, 0);
                    int b = Math.Min(m + (int)M + 1, L);
                    dynamic sum = 0.0f;
                    for (int i = a; i < b; i++)
                    {
                        sum += x[i];
                    }
                    local_average.Add((float)(1.0f / (2.0f * M + 1.0f)) * sum);
                }
                foreach (var value in local_average)
                {
                    localAverage.Add((float)value); // Convert each value to float and add to list
                }
            }
            return localAverage;
        }

        public static List<float> AMethodForNovelty(string filePath, float Fs = 1, int N = 1024, int H = 256, float gamma = 100, float M = 10, int norm = 1)
        {
            List<float> noveltySpectrum = new List<float>();
            int length = 0;
            using (Py.GIL())
            {
                dynamic np = Py.Import("numpy");
                dynamic librosa = Py.Import("librosa");

                dynamic y = librosa.load(filePath)[0];
                dynamic X = librosa.stft(y, n_fft: N, hop_length: H, win_length: N, window: "hann");
                float Fs_feature = Fs / H;

                // Calculate Y using NumPy operations
                dynamic Y = np.log(np.add(1, np.multiply(gamma, np.abs(X))));
                length = Y.shape[1];
                dynamic Y_diff = np.diff(Y);
                dynamic zero = np.zeros_like(Y_diff); // Create a zero array of the same shape as Y_diff
                Y_diff = np.maximum(Y_diff, zero); // Replace negative values with zero

                dynamic novelty_spectrum = np.sum(Y_diff, axis: 0);

                // Ensure novelty_spectrum and np.array(new float[] { 0.0f }) are converted to PyObject
                PyObject novelty_spectrum_obj = novelty_spectrum as PyObject;
                PyObject zero_array = np.array(new float[] { 0.0f }) as PyObject;
                novelty_spectrum = np.concatenate(new PyObject[] { novelty_spectrum_obj, zero_array });

                if (M > 0)
                {
                    List<float> noveltySpectrumList = new List<float>();
                    foreach (var value in novelty_spectrum)
                    {
                        noveltySpectrumList.Add((float)value);
                    }
                    List<float> localAverage = ComputeLocalAverage(noveltySpectrumList, M, Fs_feature);
                    for (int i = 0; i < (int)novelty_spectrum.shape[0]; i++)
                    {
                        novelty_spectrum[i] -= localAverage[i];
                        if (novelty_spectrum[i] < 0)
                        {
                            novelty_spectrum[i] = new PyFloat(0.0);
                        }
                    }
                }

                if (norm == 1)
                {
                    float max_value = (float)np.max(novelty_spectrum);
                    if (max_value > 0)
                    {
                        novelty_spectrum = novelty_spectrum / max_value;
                    }
                }

                // Ensure novelty_spectrum remains a list of floats
                noveltySpectrum = new List<float>();
                foreach (var value in novelty_spectrum)
                {
                    noveltySpectrum.Add((float)value);
                }
            }

            return noveltySpectrum;
        }

    }
}
