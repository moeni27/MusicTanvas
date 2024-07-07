# MusicTanvas
## Description
MusicTanvas is a WPF application designed to integrate visual and tactile feedback into music listening experiences using the Tanvas tablet. It leverages audio processing algorithms and machine learning techniques to provide interactive visual and haptic representations of audio tracks.

## Table of Contents
- [Description](#description)
- [Installation](#installation)
- [Usage](#usage)
- [Features](#features)
- [Configuration](#configuration)
- [Contributing](#contributing)
- [License](#license)
- [Contact](#contact)

## Installation
### Prerequisites
- .NET Core SDK 3.1 or higher
- Visual Studio 2019 or later
- Python 3.6 or higher
- [NAudio](https://github.com/naudio/NAudio)
- [Python.NET](https://pythonnet.github.io/)
- [Sprite](https://www.nuget.org/packages/Sprite/)
- [Tanvas.TanvasTouch.WpfUtilities](https://api-docs.tanvas.co/tanvastouch/dotnet/5.0.1/api/index.html)

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
    pip install librosa spleeter
    ```
4. Open the solution file (`MusicTanvas.sln`) with Visual Studio.

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
- The uploaded audio files will be copied in the `Assets/Audio` directory.
