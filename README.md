# MusicTanvas
## Description
MusicTanvas is a WPF application designed to integrate visual and tactile feedback into music listening experiences using the Tanvas tablet. It leverages audio processing algorithms and machine learning techniques to provide interactive visual and haptic representations of audio tracks.

This project is developed as part of a thesis in Music and Acoustic Engineering at Politecnico di Milano in association with Politecnico di Torino.

## Table of Contents
- [Description](#description)
- [Installation](#installation)
- [Usage](#usage)
- [Features](#features)
- [Configuration](#configuration)
- [Contact](#contact)

## Installation
### Prerequisites
#### NuGet Packages
- **NAudio**: A powerful audio library for .NET that provides audio playback and manipulation capabilities.
- **Tanvas.TanvasTouch.WpfUtilities**: Utilities for integrating Tanvas touch technology with WPF applications.
- **Python.NET**: A package that allows Python code to interoperate with .NET applications, enabling the use of .NET libraries in Python scripts.
- **MathNet.Numerics**: The numerical foundation of the Math.NET project, providing methods and algorithms for numerical computations in science, engineering, and everyday use.
- **NewtonSoft.Json**: A popular high-performance JSON framework for .NET.

#### Python Libraries
- **Python**: Version 3.6 to 3.9 is required.
- **sys**: Provides access to system-specific parameters and functions.
- **os**: Offers a way to interact with the operating system, including file and directory manipulation.
- **json**: A library for parsing JSON data and converting it to Python objects.
- **numpy**: A fundamental package for numerical computations in Python, supporting large multi-dimensional arrays and matrices.
- **librosa**: A library for audio analysis that provides tools for music and audio analysis tasks.
- **tensorflow**: An open-source platform for machine learning that can be used for developing deep learning models.
- **tkinter**: The standard GUI toolkit for Python, used for creating graphical user interfaces.

### Steps
1. Clone the repository:
    ```bash
    git clone https://github.com/yourusername/musictanvas.git
    ```
2. Navigate to the project directory:
    ```bash
    cd musictanvas
    ```
3. Install necessary Python libraries:
    ```bash
    pip install librosa spleeter numpy tensorflow
    ```
4. Open the solution file (`MusicTanvas.sln`) with Visual Studio.
5. **Modify the `config.txt` File**: Open the `config.txt` file and add your Python DLL path in the following format: pythonDllPath=C:\\Users\\yourusername\\path\\to\\python. Ensure to replace `C:\\Users\\yourusername\\path\\to\\pythonX.dll` with the actual path to your Python DLL file (where `X` corresponds to your Python version, e.g., `python39.dll`).


## Usage
### Running the Application
1. Run the application:
    - Press **F5** or click on **Debug** > **Start Debugging** in Visual Studio.

### Basic Operations
- **Upload Audio Files**: Click the "Upload" button and select audio files to add to the playlist.
- **Play Audio**: Click the "Play" button to start playback.
- **Pause/Resume Audio**: Click the "Pause" button to pause, and the "Play" button to resume playback.
- **Stop Audio**: Click the "Stop" button to stop playback.

### Modes
- **Multi-Track Mode**: Upload up to four audio files simultaneously. The interface displays a section for each track, and all tracks must have the same duration to play or stop simultaneously.
- **Single-Track Mode**: Upload a single audio file. The app uses Spleeter to split it into four parts: vocals, bass, percussion, and other instruments.

## Features
- Visual and tactile feedback through Tanvas tablet.
- Single-Track and Multi-Track Modes.
- Audio processing using chromagram, MFCC, spectral centroid, and loudness extraction.
- Real-time visualization of audio features.
- Integration of Spleeter for source separation.

## Configuration
### App Settings
- The default audio folder is `Assets/Audio`.
- Maximum number of audio files is set to 4.

### How to Add Audio Files
- The uploaded audio files will be copied in the `MusicTanvas/bin/Debug/net8.0-windows/Assets/Audio` directory.

## Contact
- Email: noeemi.mae@gmail.com
- GitHub: [moeni27](https://github.com/moeni27)

---

## Detailed Functionality

### Visual and Haptic Representations

#### Graphical User Interface Structure
The prototype application interface is structured into up to four distinct sections, each representing a prominent instrument group. Users can explore the app in two modes: **Multi-Track Mode** and **Single-Track Mode**.

- **Multi-Track Mode**: The user can upload up to four audio files simultaneously. The interface dynamically adjusts based on the number of tracks uploaded, displaying a distinct section per track. All tracks must have the same duration to be reproduced or stopped simultaneously.

- **Single-Track Mode**: By uploading a single audio file, the app uses a tool called **Spleeter** to split it into four parts: vocals, bass, percussion, and other instruments.

#### Audio Processing Algorithms
To offer meaningful visualization of audio content, our application relies on a number of audio processing algorithms:
- **Chromagram, MFCC, Spectral Centroid, and Loudness Extraction**: Implemented using the `librosa` Python library.
- **Automatic Instrument Recognition and Classification**: Using MFCC as the acoustic feature via a combination of classifiers.
- **Rhythmic Complexity Quantification**: Using an onset extraction algorithm based on the Short-Time Fourier Transform (STFT).

#### Mappings of Audio Features to Visual and Haptic Representations
- **MFCC or Chromagram**: Mapped to color brightness and texture roughness of background stripes.
- **Rhythmic Complexity**: Mapped to the sphere's surface.
- **Spectral Centroid**: Mapped to the sphere's color brightness.
- **Loudness**: Mapped to the sphere's radius.

### Example Representations
- **Foreground Sphere**: Radius dynamically adjusts based on the loudness of the corresponding audio track.
- **Background Stripes**: Color transitions from lighter to darker hues, reflecting the normalized values of MFCC coefficients or chromas.


