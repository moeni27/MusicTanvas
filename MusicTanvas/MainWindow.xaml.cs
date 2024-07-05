using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NAudio.Wave;
using Tanvas.TanvasTouch.Resources;
using Tanvas.TanvasTouch.WpfUtilities;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using System.Windows.Shapes;
using Python.Runtime;
using System.Windows.Media.Imaging;
using MahApps.Metro.Controls;
using System.Collections.Generic;
using System.IO;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using System.Numerics;
using System.Windows.Input;

namespace MusicTanvas
{
    public partial class MainWindow : System.Windows.Window
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

        private WaveStream streamWood;
        private WaveOut outWood;

        private WaveStream streamBrass;
        private WaveOut outBrass;

        private WaveStream streamDrum;
        private WaveOut outDrum;

        private WaveStream streamString;
        private WaveOut outString;

        private bool isStopped;
        private float volume;

        private DispatcherTimer sphereUpdateTimer;
        private DispatcherTimer colorUpdateTimer;
        private DispatcherTimer stripesUpdateTimer;

        private bool userInitiatedStop = false;

        private long woodPlaybackPosition = 0;
        private long brassPlaybackPosition = 0;
        private long drumPlaybackPosition = 0;
        private long stringPlaybackPosition = 0;

        private byte[] bufferWood;
        private byte[] bufferBrass;
        private byte[] bufferDrum;
        private byte[] bufferString;
        private int bufferLength = 1024;

        private List<float> amplitudeBufferWood = new List<float>();
        private List<float> amplitudeBufferBrass = new List<float>();
        private List<float> amplitudeBufferDrum = new List<float>();
        private List<float> amplitudeBufferString = new List<float>();

        private const int smoothingWindowSize = 20;

        private MeshGeometry3D meshWind;
        private MeshGeometry3D meshBrass;
        private MeshGeometry3D meshDrum;
        private MeshGeometry3D meshString;

        private int tDiv = 30;
        private int pDiv = 30;
        private double baseRadius = 1;

        private int centroidIndex = 0;
        private int woodMfccIndex = 0;

        private List<float> woodCentroid = new List<float>();
        private List<float> brassCentroid = new List<float>();
        private List<float> drumCentroid = new List<float>();
        private List<float> stringCentroid = new List<float>();

        private List<List<float>> woodMFCC = new List<List<float>>();
        private List<List<float>> brassMFCC = new List<List<float>>();
        private List<List<float>> drumMFCC = new List<List<float>>();
        private List<List<float>> stringMFCC = new List<List<float>>();

        private List<float> woodOnset = new List<float>();
        private List<float> brassOnset = new List<float>();
        private List<float> drumOnset = new List<float>();
        private List<float> stringOnset = new List<float>();

        private int w;


        private float maxCentroid;

        public MainWindow()
        {
            InitializeComponent();
            //RunScript("mfcc");
            DataContext = this;
            Tanvas.TanvasTouch.API.Initialize();
            CreateSphere(ref meshWind, sphereModelWind);
            CreateSphere(ref meshBrass, sphereModelBrass);
            CreateSphere(ref meshDrum, sphereModelDrum);
            CreateSphere(ref meshString, sphereModelString);



            InitializeAudio();

            sphereUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(12)
            };
            sphereUpdateTimer.Tick += SphereUpdateTimer_Tick;

            InitializePython();

            woodCentroid = GetSpectralCentroid(@"..\..\..\assets\Audio\vocals.wav");
            brassCentroid = GetSpectralCentroid(@"..\..\..\assets\Audio\bass.wav");

            drumCentroid = GetSpectralCentroid(@"..\..\..\assets\Audio\drums.wav");
            stringCentroid = GetSpectralCentroid(@"..\..\..\assets\Audio\other.wav");


            maxCentroid = Math.Max(Math.Max(woodCentroid.Max(), brassCentroid.Max()), Math.Max(drumCentroid.Max(), stringCentroid.Max()));

            NormalizeSpectralCentroids();

            woodMFCC = GetMFCCMatrix(@"..\..\..\assets\Audio\vocals.wav", 7);
            brassMFCC = GetMFCCMatrix(@"..\..\..\assets\Audio\bass.wav", 7);
            drumMFCC = GetMFCCMatrix(@"..\..\..\assets\Audio\drums.wav", 7);
            stringMFCC = GetMFCCMatrix(@"..\..\..\assets\Audio\other.wav", 7);

            double audioDurationInSeconds = streamWood.TotalTime.TotalSeconds;
            double updateInterval = audioDurationInSeconds / woodMFCC[0].Count;

            woodOnset = AMethodForNovelty(@"..\..\..\assets\Audio\vocals.wav");
            brassOnset = AMethodForNovelty(@"..\..\..\assets\Audio\bass.wav");
            drumOnset = AMethodForNovelty(@"..\..\..\assets\Audio\drums.wav");
            stringOnset = AMethodForNovelty(@"..\..\..\assets\Audio\other.wav");


            colorUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(updateInterval)
            };
            colorUpdateTimer.Tick += ColorUpdateTimer_Tick;

            Debug.WriteLine("wood" + woodCentroid.Count) ;

            Debug.WriteLine("onset" + woodOnset.Count);

            stripesUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            stripesUpdateTimer.Tick += StripesUpdateTimer_Tick;

        }


        private void SphereUpdateTimer_Tick(object sender, EventArgs e)
        {
            float maxAmplitudeWood = GetMaxAmplitude(streamWood, bufferWood);
            float maxAmplitudeBrass = GetMaxAmplitude(streamBrass, bufferBrass);
            float maxAmplitudeDrum = GetMaxAmplitude(streamDrum, bufferDrum);
            float maxAmplitudeString = GetMaxAmplitude(streamString, bufferString);

            var bluebrush = new SolidColorBrush(Color.FromRgb(0, 0, 119));
            var yellowbrush = new SolidColorBrush(Color.FromRgb(204, 153, 0));
            var redbrush = new SolidColorBrush(Color.FromRgb(139, 0, 0));
            var greenbrush = new SolidColorBrush(Color.FromRgb(0, 100, 0));

            float smoothedAmplitudeWood = SmoothAmplitude(maxAmplitudeWood, amplitudeBufferWood);
            float woodsurface = Math.Min(woodOnset[centroidIndex] * smoothedAmplitudeWood * 10, 0.05f);
            Debug.WriteLine("vocal onset  " + woodsurface);

            if (smoothedAmplitudeWood < 0.01)
            {
                woodsurface = 0;
                for (int i = 0; i <= 6; i++)
                {
                    ((Rectangle)gridStripes.Children[i]).Fill = bluebrush;
                }
            }

            float smoothedAmplitudeBrass = SmoothAmplitude(maxAmplitudeBrass, amplitudeBufferBrass);
            float brasssurface = Math.Min(brassOnset[centroidIndex] * smoothedAmplitudeBrass * 10, 0.05f);
            Debug.WriteLine("bass onset  " + brasssurface);
            if (smoothedAmplitudeBrass < 0.01)
            {
                brasssurface = 0;
                for (int i = 0; i <= 6; i++)
                {
                    ((Rectangle)gridBrass.Children[i]).Fill = yellowbrush;
                }
            }
            float smoothedAmplitudeDrum = SmoothAmplitude(maxAmplitudeDrum, amplitudeBufferDrum);
            float drumsurface = Math.Min(drumOnset[centroidIndex] * smoothedAmplitudeDrum * 10, 0.05f);
            if (smoothedAmplitudeDrum < 0.01)
            {
                drumsurface = 0;
                for (int i = 0; i <= 6; i++)
                {
                    ((Rectangle)gridDrum.Children[i]).Fill = redbrush;
                }
            }

            float smoothedAmplitudeString = SmoothAmplitude(maxAmplitudeString, amplitudeBufferString);
            float stringsurface = Math.Min(stringOnset[centroidIndex] * smoothedAmplitudeString * 10, 0.05f);
            if (smoothedAmplitudeString < 0.01)
            {
                stringsurface = 0;
                for (int i = 0; i <= 6; i++)
                {
                    ((Rectangle)gridString.Children[i]).Fill = greenbrush;
                }
            }

            float radiuswood = Math.Min(smoothedAmplitudeWood * volume * 4, 0.9f);
            float radiusbrass = Math.Min(smoothedAmplitudeBrass * volume * 4, 0.9f);
            float radiusdrum = Math.Min(smoothedAmplitudeDrum * volume * 4, 0.9f);
            float radiusstring = Math.Min(smoothedAmplitudeString * volume * 4, 0.9f);



            UpdateSphere(meshWind, radiuswood * 3, tDiv, pDiv, woodsurface);
            UpdateSphere(meshBrass, radiusbrass * 3, tDiv, pDiv, brasssurface);
            UpdateSphere(meshDrum, radiusdrum * 3, tDiv, pDiv, drumsurface);
            UpdateSphere(meshString, radiusstring * 3, tDiv, pDiv, stringsurface);
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


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            viewTracker = new TanvasTouchViewTracker(this);

            var uri = new Uri("pack://application:,,/Assets/col1.png");
            var mySprite = PNGToTanvasTouch.CreateSpriteFromPNG(uri);
            double x = 0;
            double y = 0;

            Grid gridStripes = FindName("gridStripes") as Grid;

            if (gridStripes != null)
            {
                ColumnDefinition col1 = gridStripes.ColumnDefinitions[0];

                GeneralTransform transform = gridStripes.TransformToAncestor(this);
                Point col1Position = transform.Transform(new Point(0, 0));

                x = col1Position.X;
                y = col1Position.Y;

                MessageBox.Show($"Position of col1 relative to MainWindow: X={x}, Y={y}");
            }

            mySprite.X = (float)x + (float)col1.ActualWidth;  
            mySprite.Y = (float)y;   

            myView.AddSprite(mySprite);

        }


        private void NormalizeSpectralCentroids()
        {

            for (int i = 0; i < woodCentroid.Count; i++)
            {
                woodCentroid[i] = (woodCentroid[i] / woodCentroid.Max());
                brassCentroid[i] = (brassCentroid[i] / maxCentroid);
                drumCentroid[i] = (drumCentroid[i] / maxCentroid);
                stringCentroid[i] = (stringCentroid[i] / maxCentroid);
            }
        }

        private void UpdateBrassStripesColor()
        {
            // Iterate through each column (stripe)
            for (int column = 0; column < 7; column++)
            {
                // Get the current value from brassMFCC for the current frame
                float mfccValue = brassMFCC[column][centroidIndex];

                // Define the RGB values for more vivid light yellow and dark yellow
                byte lightYellowR = 255, lightYellowG = 255, lightYellowB = 0;    // Brighter yellow
                byte darkYellowR = 204, darkYellowG = 153, darkYellowB = 0;      // Deeper yellow

                // Calculate the interpolated RGB values
                byte r = (byte)(darkYellowR + (mfccValue * (lightYellowR - darkYellowR)));
                byte g = (byte)(darkYellowG + (mfccValue * (lightYellowG - darkYellowG)));
                byte b = (byte)(darkYellowB + (mfccValue * (lightYellowB - darkYellowB)));

                // Create the brush
                var brush = new SolidColorBrush(Color.FromRgb(r, g, b));

                // Update the color of the Rectangle (stripe)
                Dispatcher.Invoke(() =>
                {
                    ((Rectangle)gridBrass.Children[column]).Fill = brush;
                });
            }
        }




        private void UpdateStripesColor()
        {
            // Iterate through each column (stripe)
            for (int column = 0; column < 7; column++)
            {
                // Get the current value from woodMFCC for the current frame
                float mfccWoodValue = woodMFCC[column][woodMfccIndex];
                float mfccBrassValue = brassMFCC[column][woodMfccIndex];
                float mfccDrumValue = drumMFCC[column][woodMfccIndex];
                float mfccStringValue = stringMFCC[column][woodMfccIndex];

                // Define the RGB values for light blue and dark blue
                byte lightBlueR = 94, lightBlueG = 164, lightBlueB = 255;
                byte darkBlueR = 0, darkBlueG = 0, darkBlueB = 119;

                byte lightYellowR = 255, lightYellowG = 255, lightYellowB = 0;
                byte darkYellowR = 204, darkYellowG = 153, darkYellowB = 0;

                byte lightRedR = 255, lightRedG = 102, lightRedB = 102;
                byte darkRedR = 139, darkRedG = 0, darkRedB = 0;

                byte lightGreenR = 144, lightGreenG = 238, lightGreenB = 144;
                byte darkGreenR = 0, darkGreenG = 100, darkGreenB = 0;


                // Calculate the interpolated RGB values
                byte r_wood = (byte)(darkBlueR + (mfccWoodValue * (lightBlueR - darkBlueR)));
                byte g_wood = (byte)(darkBlueG + (mfccWoodValue * (lightBlueG - darkBlueG)));
                byte b_wood = (byte)(darkBlueB + (mfccWoodValue * (lightBlueB - darkBlueB)));

                // Calculate the interpolated RGB values
                byte r_brass = (byte)(darkYellowR + (mfccBrassValue * (lightYellowR - darkYellowR)));
                byte g_brass = (byte)(darkYellowG + (mfccBrassValue * (lightYellowG - darkYellowG)));
                byte b_brass = (byte)(darkYellowB + (mfccBrassValue * (lightYellowB - darkYellowB)));

                byte r_drum = (byte)(darkRedR + (mfccDrumValue * (lightRedR - darkRedR)));
                byte g_drum = (byte)(darkRedG + (mfccDrumValue * (lightRedG - darkRedG)));
                byte b_drum = (byte)(darkRedB + (mfccDrumValue * (lightRedB - darkRedB)));

                // Calculate the interpolated RGB values for green
                byte r_string = (byte)(darkGreenR + (mfccStringValue * (lightGreenR - darkGreenR)));
                byte g_string = (byte)(darkGreenG + (mfccStringValue * (lightGreenG - darkGreenG)));
                byte b_string = (byte)(darkGreenB + (mfccStringValue * (lightGreenB - darkGreenB)));

                // Create the brush
                var woodBrush = new SolidColorBrush(Color.FromRgb(r_wood, g_wood, b_wood));
                var brassBrush = new SolidColorBrush(Color.FromRgb(r_brass, g_brass, b_brass));
                var drumBrush = new SolidColorBrush(Color.FromRgb(r_drum, g_drum, b_drum));
                var stringBrush = new SolidColorBrush(Color.FromRgb(r_string, g_string, b_string));

                // Update the color of the Rectangle (stripe)
                Dispatcher.Invoke(() =>
                {
                    ((Rectangle)gridStripes.Children[column]).Fill = woodBrush;
                    ((Rectangle)gridBrass.Children[column]).Fill = brassBrush;
                    ((Rectangle)gridDrum.Children[column]).Fill = drumBrush;
                    ((Rectangle)gridString.Children[column]).Fill = stringBrush;
                });
            }
        }

        private void ColorUpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateModelColor();
            centroidIndex = (centroidIndex + 1) % woodCentroid.Count;
        }


        private void StripesUpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateStripesColor();
            woodMfccIndex = (woodMfccIndex + 1);
        }

        private void UpdateModelColor()
        {
            float wood_intensity = woodCentroid[centroidIndex];
            float brass_intensity = brassCentroid[centroidIndex];
            float drum_intensity = drumCentroid[centroidIndex];
            float string_intensity = stringCentroid[centroidIndex];

            // Define the RGB values for light blue and dark blue
            byte lightBlueR = 94, lightBlueG = 164, lightBlueB = 255;
            byte darkBlueR = 0, darkBlueG = 0, darkBlueB = 119;

            // Define the RGB values for light yellow and dark yellow

            byte lightYellowR = 255, lightYellowG = 255, lightYellowB = 153;
            byte darkYellowR = 139, darkYellowG = 117, darkYellowB = 0;

            // Define the RGB values for light red and dark red
            byte lightRedR = 255, lightRedG = 100, lightRedB = 100;
            byte darkRedR = 139, darkRedG = 0, darkRedB = 0;


            // Define the RGB values for light green and dark green
            byte lightGreenR = 144, lightGreenG = 238, lightGreenB = 144;
            byte darkGreenR = 0, darkGreenG = 100, darkGreenB = 0;

            // Calculate the interpolated RGB values
            byte r_wood = (byte)(darkBlueR + (wood_intensity * (lightBlueR - darkBlueR)));
            byte g_wood = (byte)(darkBlueG + (wood_intensity * (lightBlueG - darkBlueG)));
            byte b_wood = (byte)(darkBlueB + (wood_intensity * (lightBlueB - darkBlueB)));

            byte r_brass = (byte)(darkYellowR + (brass_intensity * (lightYellowR - darkYellowR)));
            byte g_brass = (byte)(darkYellowG + (brass_intensity * (lightYellowG - darkYellowG)));
            byte b_brass = (byte)(darkYellowB + (brass_intensity * (lightYellowB - darkYellowB)));

            // Calculate the interpolated RGB values for red
            byte r_drum = (byte)(darkRedR + (drum_intensity * (lightRedR - darkRedR)));
            byte g_drum = (byte)(darkRedG + (drum_intensity * (lightRedG - darkRedG)));
            byte b_drum = (byte)(darkRedB + (drum_intensity * (lightRedB - darkRedB)));

            // Calculate the interpolated RGB values for green
            byte r_string = (byte)(darkGreenR + (string_intensity * (lightGreenR - darkGreenR)));
            byte g_string = (byte)(darkGreenG + (string_intensity * (lightGreenG - darkGreenG)));
            byte b_string = (byte)(darkGreenB + (string_intensity * (lightGreenB - darkGreenB)));


            // Create the brush and material
            var brush_wood = new SolidColorBrush(Color.FromRgb(r_wood, g_wood, b_wood));
            var material_wood = new DiffuseMaterial(brush_wood);

            var brush_brass = new SolidColorBrush(Color.FromRgb(r_brass, g_brass, b_brass));
            var material_brass = new DiffuseMaterial(brush_brass);

            var brush_drum = new SolidColorBrush(Color.FromRgb(r_drum, g_drum, b_drum));
            var material_drum = new DiffuseMaterial(brush_drum);

            var brush_string = new SolidColorBrush(Color.FromRgb(r_string, g_string, b_string));
            var material_string = new DiffuseMaterial(brush_string);


            Dispatcher.Invoke(() =>
            {
                sphereModelWind.Material = material_wood;
                sphereModelBrass.Material = material_brass;
                sphereModelDrum.Material = material_drum;
                sphereModelString.Material = material_string;
            });

        }

        // Get python .ddl from condig.txt
        private static string GetPythonDllPath(string configFilePath)
        {
            foreach (var line in File.ReadLines(configFilePath))
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


        // MFCC

        public List<List<float>> GetMFCCMatrix(string filePath, int n_mfcc = 7, int intervalSeconds = 5)
        {
            List<List<float>> mfccMatrix = new List<List<float>>();

            using (Py.GIL())
            {
                dynamic np = Py.Import("numpy");
                dynamic librosa = Py.Import("librosa");

                // Load the audio file
                dynamic y_sr = librosa.load(filePath, sr: null);
                dynamic y = y_sr[0];
                dynamic sr = y_sr[1];

                // Compute MFCCs
                dynamic mfcc = librosa.feature.mfcc(y: y, sr: sr, n_mfcc: n_mfcc, n_fft: 1024, hop_length: 512);

                // Find min and max values in the MFCC matrix
                float min = (float)np.min(mfcc);
                float max = (float)np.max(mfcc);

                // Calculate number of frames per 5-second interval
                int frames_per_interval = intervalSeconds * (int)sr / 512;
                int totalFrames = mfcc.shape[1];

                // Calculate number of intervals
                int num_intervals = (int)Math.Ceiling((double)totalFrames / frames_per_interval);

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

                    mfccMatrix.Add(mfccCoefficients);
                }
            }

            return mfccMatrix;
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
                    localAverage.Add((float)value); 
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

                dynamic Y = np.log(np.add(1, np.multiply(gamma, np.abs(X))));
                length = Y.shape[1];
                dynamic Y_diff = np.diff(Y);
                dynamic zero = np.zeros_like(Y_diff); 
                Y_diff = np.maximum(Y_diff, zero); 

                dynamic novelty_spectrum = np.sum(Y_diff, axis: 0);

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

                noveltySpectrum = new List<float>();
                foreach (var value in novelty_spectrum)
                {
                    noveltySpectrum.Add((float)value);
                }
            }

            return noveltySpectrum;
        }


        private void InitializeAudio()
        {
            streamWood = new AudioFileReader(@"..\..\..\assets\Audio\vocals.wav");
            outWood = new WaveOut();
            outWood.Init(streamWood);
            outWood.PlaybackStopped += OnPlaybackStoppedWood;

            streamBrass = new AudioFileReader(@"..\..\..\assets\Audio\bass.wav");
            outBrass = new WaveOut();
            outBrass.Init(streamBrass);
            outBrass.PlaybackStopped += OnPlaybackStoppedBrass;

            streamDrum = new AudioFileReader(@"..\..\..\assets\Audio\drums.wav");
            outDrum = new WaveOut();
            outDrum.Init(streamDrum);
            outDrum.PlaybackStopped += OnPlaybackStoppedDrum;

            streamString = new AudioFileReader(@"..\..\..\assets\Audio\other.wav");
            outString = new WaveOut();
            outString.Init(streamString);
            outString.PlaybackStopped += OnPlaybackStoppedString;

            bufferWood = new byte[bufferLength];
            bufferBrass = new byte[bufferLength];
            bufferDrum = new byte[bufferLength];
            bufferString = new byte[bufferLength];
        }

        public void RunScript(string scriptName)
        {
        }



        private void CreateSphere(ref MeshGeometry3D mesh, GeometryModel3D model)
        {
            mesh = new MeshGeometry3D();
            UpdateSphere(mesh, baseRadius, tDiv, pDiv, 0);
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


        public void onClickPlayButton(object sender, EventArgs e)
        {
            userInitiatedStop = false;

            if (isStopped)
            {
                // Reinitialize audio streams and WaveOut instances
                InitializeAudio();
            }

            streamWood.Position = woodPlaybackPosition;
            streamBrass.Position = brassPlaybackPosition;
            streamDrum.Position = drumPlaybackPosition;
            streamString.Position = stringPlaybackPosition;

            outWood.Play();
            outBrass.Play();
            outDrum.Play();
            outString.Play();

            sphereUpdateTimer.Start();
            colorUpdateTimer.Start();
            stripesUpdateTimer.Start();
            isStopped = false;

            buttonPlay.Visibility = Visibility.Hidden;
            buttonStop.Visibility = Visibility.Visible;
        }

        public void onClickStopButton(object sender, EventArgs e)
        {
            userInitiatedStop = true;

            outWood.Stop();
            outBrass.Stop();
            outDrum.Stop();
            outString.Stop();

            sphereUpdateTimer.Stop();
            colorUpdateTimer.Stop();
            stripesUpdateTimer.Stop();

            woodPlaybackPosition = streamWood.Position;
            brassPlaybackPosition = streamBrass.Position;
            drumPlaybackPosition = streamDrum.Position;
            stringPlaybackPosition = streamString.Position;

            buttonPlay.Visibility = Visibility.Visible;
            buttonStop.Visibility = Visibility.Hidden;
            isStopped = true;
        }

        public void OnVolumeSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            volume = (float)e.NewValue;
            AdjustVolume(outWood, (float)e.NewValue);
            AdjustVolume(outBrass, (float)e.NewValue);
            AdjustVolume(outDrum, (float)e.NewValue);
            AdjustVolume(outString, (float)e.NewValue);
        }

        private void AdjustVolume(WaveOut waveOut, float volume)
        {
            if (waveOut != null)
            {
                waveOut.Volume = volume;
            }
        }

        private void OnPlaybackStoppedWood(object sender, StoppedEventArgs e)
        {
            if (!userInitiatedStop)
            {
                outWood.Dispose();
                streamWood.Dispose();
                woodPlaybackPosition = 0;
                CheckAllPlaybackStopped();
                centroidIndex = 0;
                woodMfccIndex = 0;
            }
        }

        private void OnPlaybackStoppedBrass(object sender, StoppedEventArgs e)
        {
            if (!userInitiatedStop)
            {
                outBrass.Dispose();
                streamBrass.Dispose();
                brassPlaybackPosition = 0;
                CheckAllPlaybackStopped();
                centroidIndex = 0;
                woodMfccIndex = 0;
            }
        }

        private void OnPlaybackStoppedDrum(object sender, StoppedEventArgs e)
        {
            if (!userInitiatedStop)
            {
                outDrum.Dispose();
                streamDrum.Dispose();
                drumPlaybackPosition = 0;
                CheckAllPlaybackStopped();
                centroidIndex = 0;
                woodMfccIndex = 0;
            }
        }

        private void OnPlaybackStoppedString(object sender, StoppedEventArgs e)
        {
            if (!userInitiatedStop)
            {
                outString.Dispose();
                streamString.Dispose();
                stringPlaybackPosition = 0;
                CheckAllPlaybackStopped();
                centroidIndex = 0;
                woodMfccIndex = 0;
            }
        }

        private void CheckAllPlaybackStopped()
        {
            if (woodPlaybackPosition == 0 && brassPlaybackPosition == 0 && drumPlaybackPosition == 0 && stringPlaybackPosition == 0)
            {
                buttonPlay.Visibility = Visibility.Visible;
                buttonStop.Visibility = Visibility.Hidden;
                sphereUpdateTimer.Stop();
                colorUpdateTimer.Stop();
                stripesUpdateTimer.Stop();
                isStopped = true;
            }
        }
    }


}
