import sys
import os
import json
import numpy as np
import librosa
import tensorflow as tf
import tkinter as tk
from tkinter import scrolledtext


# Function to extract chroma features from an audio file
def extract_chroma(file_path):
    y, sr = librosa.load(file_path, sr=None)
    chroma = librosa.feature.chroma_stft(y=y, sr=sr)
    chroma_mean = np.mean(chroma, axis=1)
    return chroma_mean

# Function to predict the instrument using the provided MFCC matrix and computed chroma matrix
def predict_instrument(mfcc_matrix, chroma_matrix):
    script_directory = os.path.dirname(os.path.abspath(__file__))
    model_path = os.path.join(script_directory, "model_recognition.h5")
    model = tf.keras.models.load_model(model_path)

    mfcc_matrix = np.array(mfcc_matrix)
    mfcc_mean = np.mean(mfcc_matrix, axis=1)
    #display_result(mfcc_mean)

    chroma_matrix = np.array(chroma_matrix)
    chroma_mean = np.mean(chroma_matrix, axis=1)

    # Combine MFCC and chroma features
    features = np.concatenate((mfcc_mean, chroma_mean))
    features = np.expand_dims(features, axis=0)

    prediction = model.predict(features)
    predicted_label = np.argmax(prediction)

    label_map_reverse = {
        0: 'cello', 1: 'clarinet', 2: 'flute', 3: 'acoustic guitar', 4: 'electric guitar',
        5: 'organ', 6: 'piano', 7: 'saxophone', 8: 'trumpet', 9: 'violin', 10: 'voice'
    }
    
    return label_map_reverse[predicted_label]

def display_result(result):
    root = tk.Tk()
    root.title("Prediction Result")

    text_area = scrolledtext.ScrolledText(root, wrap=tk.WORD, width=40, height=10)
    text_area.pack(padx=10, pady=10)

    text_area.insert(tk.END, f"Predicted Instrument: {result}")
    text_area.config(state=tk.DISABLED)

    root.mainloop()

if __name__ == "__main__":
    # Read file paths from temp files
    script_directory = os.path.dirname(os.path.abspath(__file__))
    model_path = os.path.join(script_directory, "bin", "Debug", "net8.0-windows", "Assets", "Audio")
    file_path = os.path.join(model_path, sys.argv[3])

    mfcc_file_path = sys.argv[1]
    chroma_file_path = sys.argv[2]
    

    with open(mfcc_file_path, 'r') as f:
        mfcc_matrix = json.load(f)

    with open(chroma_file_path, 'r') as f:
        chroma_matrix = json.load(f)

    # Compute the chroma matrix
    #chroma_matrix = extract_chroma(file_path)

    # Predict the instrument using the provided MFCC matrix and computed chroma matrix
    result = predict_instrument(mfcc_matrix, chroma_matrix)
    print(result)
