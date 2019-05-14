package com.mimerse.physiosense.signalproc;

import java.io.BufferedReader;
import java.io.BufferedWriter;
import java.io.File;
import java.io.FileReader;
import java.io.FileWriter;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;

import com.mimerse.physiosense.MainActivity;
import com.mimerse.physiosense.dwt.DWT;
import com.mimerse.physiosense.dwt.FileOps;
import com.mimerse.physiosense.dwt.MatrixOps;
import com.mimerse.physiosense.dwt.StringUtils;
import com.mimerse.physiosense.dwt.Wavelet;

/**
 * Class that implements a peak detector for HRV from PPG signal collected
 * using samsung smartwatch.
 * The algorithm is based on the paper "A novel method for accurate estimation of HRV
 * from smartwatch PPG" by Bhowmik et al.
 */


public class SignalAnalyzer {

    public static void Process() {

        // Original XY signal
        String[] signalTimestamps = new String[2];
        double[] signalValues = new double[2];

        ////////////////////////
        //// LOAD FILE WITH NORMAL DATA STRUCTURE COMING FROM SMARTWATCH
        // The structure is as follows:
        //      PPG,5962532563,23040.00,2
        //or    HR,5862882507,85.00,2
        //being [VariableName, MonotonicTimeStamp, Value, Precision]
        ////////////////////////

        int DATA_BLOCK_TO_PROCESS = 1024;//1024; // Power of 2 to facilitate DWT calculation
        int WINDOW_OVERLAP = 112;//112;   //Allow to process only the 800 samples in the middle using 1024 samples

        File signalFile = new File("D:\\Ludwig\\Google Drive\\MASTER_KI\\THESIS\\Data Processing\\ppg_recorder_logs\\20190113161956_ppg_log.txt");

        File peakOutputFile = new File("D:\\Ludwig\\Google Drive\\MASTER_KI\\THESIS\\Data Processing\\ppg_recorder_logs\\20190113161956_ppg_log_PEAKS.txt");
        File denoisedOutputFile = new File("D:\\Ludwig\\Google Drive\\MASTER_KI\\THESIS\\Data Processing\\ppg_recorder_logs\\20190113161956_ppg_log_DENOISED.txt");

        BufferedReader reader;
        BufferedWriter writerPeaks, writerDenoised;

        try {
            // Input file with signal
            reader = new BufferedReader(new FileReader(signalFile));

            // Result of peaks
            writerPeaks = new BufferedWriter(new FileWriter(peakOutputFile,true));
            writerPeaks.write("lineNumber,timestampValue");
            writerPeaks.newLine();
            writerPeaks.flush();

            // Result of denoise
            writerDenoised = new BufferedWriter(new FileWriter(denoisedOutputFile,true));
            writerDenoised.write("denoisedSignal");
            writerDenoised.newLine();
            writerDenoised.flush();

            // Store timestamps as text to avoid rounding the numbers when trying to map
            // the indexes of the peaks in the original signal again.
            List<String> timeSet = new ArrayList<String>();
            List<Double> valueSet = new ArrayList<Double>();

            // Line read from the file
            String line;
            line = reader.readLine();

            int lineCounter = 0;
            boolean firstProcessingRound = true;
            while (line != null)
            {
                lineCounter++;

                String[] lineValues = line.split(",");
                if(lineValues[0].compareTo("PPG") != 0 || lineValues.length != 4)
                {
                    System.out.println("Line skipped: " + lineCounter + "\t\t\t||NOT PROCESSED!");
                    line = reader.readLine();
                    continue;
                }

                //// IF IT IS A VALID SAMPLE
                // Add timestamps and values of signal
                String timestampStr = lineValues[1];
                String valueStr = lineValues[2];
                if (StringUtils.isNumeric(timestampStr)) {
                    timeSet.add(timestampStr);
                }
                if (StringUtils.isNumeric(valueStr)) {
                    valueSet.add(Double.valueOf(valueStr));
                }

                System.out.println("Line Count: " + lineCounter);

                // When it reaches the amount of samples to process, then calculate peaks.
                int timeSetSize = timeSet.size();
                int valueSetSize = valueSet.size();
                if(valueSetSize == DATA_BLOCK_TO_PROCESS){
                    if(valueSetSize == timeSetSize)
                    {
                        signalValues = new double[valueSetSize];
                        for(int j=0; j<valueSet.size(); j++)
                        {
                            signalValues[j] = valueSet.get(j);
                        }

                        // Calculate peaks, the list contains the indexes where a peak was found, from 0 to DATA_BLOCK_TO_PROCESS (1024)
                        List<Integer> validPeakPositions = SignalAnalyzer.ProcessSignals(signalValues, 50, WINDOW_OVERLAP,firstProcessingRound, writerDenoised);

                        // First samples were already written
                        firstProcessingRound = false;

                        // The indexes are mapped in the array of timestamps to specify the specific time when the peak was detected
                        for(int k=0; k<validPeakPositions.size(); k++) {
                            System.out.println("PEAK " + k + ": " + validPeakPositions.get(k) + " = " + timeSet.get(validPeakPositions.get(k)));
                            // Write the timestamp corresponding to the detected peak
                            writerPeaks.write(Integer.toString(lineCounter - DATA_BLOCK_TO_PROCESS + validPeakPositions.get(k)) + "," + timeSet.get(validPeakPositions.get(k)));
                            writerPeaks.newLine();
                            writerPeaks.flush();
                        }
                        // Clear the samples from the lists for the next calculation, but leaving the overlapping samples necessary for the next calculation
                        timeSet.subList(0,timeSetSize-2*WINDOW_OVERLAP).clear();
                        valueSet.subList(0,valueSetSize-2*WINDOW_OVERLAP).clear();
                        System.out.println("Peaks were calculated, remaining samples in array to overlap with next calculation = " + timeSet.size() + " and " + valueSet.size());
                    }
                    else
                    {
                        System.out.println("Array of timestamps and values are not the same, deleting arrays: " + valueSetSize + " vs. " + timeSetSize);
                        timeSet.clear();
                        valueSet.clear();
                    }
                }

                // Read new line
                line = reader.readLine();

            }
            reader.close();
            writerPeaks.close();
            writerDenoised.close();

        } catch (IOException e) {
            e.printStackTrace();
        }

/*
// TEST LOADING SEQUENCE OF 1024 SAMPLES FROM THE DRIVE

        ////////////////////////
        //// Load signal from file for test purposes
        ////////////////////////
        // Non-normalized signal
        File signalFile = new File("D:\\Ludwig\\Sideprojects\\Master Thesis - Mimerse\\Android-BT_receiver\\PPG_receiver\\math.peakdetector\\src\\main\\java\\ppg_signal_test.csv");
        // Normalized signal
        //File signalFile = new File("D:\\Ludwig\\Sideprojects\\Master Thesis - Mimerse\\Android-BT_receiver\\PPG_receiver\\math.peakdetector\\src\\main\\java\\signal_t_vs_ppg_norm.csv");

        double[][] XYsignal = new double[2][2];
        try {
            XYsignal = FileOps.openMatrix(signalFile);
        } catch (Exception e1) {
            e1.printStackTrace();
        }

        XYsignal = MatrixOps.transpose(XYsignal);
        XYsignal = DWT.padPow2(XYsignal);
        List<Integer> validPeakPositions = PeakDetector.ProcessSignals(XYsignal[1], 50, 112);

        // Display first results
        for(int i=0; i<validPeakPositions.size(); i++) {
            System.out.println("PEAK " + i + ": " + validPeakPositions.get(i));
        }

*/
        /////////////////////////////////////////////////

        /*
        // Display first results of the signal
        int START = 0;
        int NUMBER_OF_ENTRIES = 10;
        for(int i=START; i<START+NUMBER_OF_ENTRIES; i++) {
            System.out.println(i + "= " + signal[i]);
        }
        */

        ///// FINAL SIGNAL
        //processedSignal[1] = signal;
        //WriteFile("D:\\Ludwig\\Sideprojects\\Master Thesis - Mimerse\\Android-BT_receiver\\PPG_receiver\\math.peakdetector\\src\\main\\java\\finalProcessing",processedSignal);


    } // main

    private static void WriteLog(String message)
    {
        MainActivity.WriteLog(message);
    }

    public static List<Integer> ProcessSignals(double[] signalToProcess, int samplingFrequency, int windowOverlap, boolean firstProcessing, BufferedWriter denoisedSignalWriter)
    {
        // Copy of the array that contains the signal, at the end this contains the processed signal
        double[] signal = null;
        // Used for intermediate calculations
        double[] tempArray = null;
        // Sampling frequency
        int Fs = samplingFrequency;
        // Length of the signal
        int N = 0;
        // Number of samples to clear from the DWT
        int filteredSamplesDWT = 32;   // 32 for 1024 samples at 50Hz

        // Iteration helpers
        int leftLimit, rightLimit;
        int peakDetLeftLim, peakDetRightLim;
        int validRangeNextPeakLeftLim, validRangeNextPeakRightLim;


        // Traspose and approximate the length of the signal (N) to closes power of 2 to facilitate
        // calculation of Discrete Wavelet Transform

        // XYsignal = MatrixOps.transpose(XYsignal); // Not necessary when array is populated manually
        //XYsignal = DWT.padPow2(XYsignal);

        // Create the array only in 1D to facilitate calculations
        // Used as final processed signal
        //double[][] processedSignal = new double[2][];
        //processedSignal[0] = XYsignal[0]; // Time array is not going to change in the whole process

        signal = signalToProcess.clone();      // Clone signal array to make the signal processing here
        signal = DWT.padPow2(signal);

        N = signal.length;
        if(N < 2*windowOverlap)
        {
            WriteLog("ERROR: Signal is too short to keep a two sided window. N=" + N + ", windowOverlap=" + windowOverlap);
            return null;
        }


        //System.out.println("Size signal: " + N + " |" + XYsignal[0].length + "x" + XYsignal[1].length);
        //System.out.println("First line: " + XYsignal[0][0] + "," + XYsignal[1][0]);
        //System.out.println("Second line: " + XYsignal[0][1] + "," + XYsignal[1][1]);

        // TODO: NORMALIZE SIGNAL? Not necessary, the peak detector worked the same.

        ////////////////////////
        // Discrete Wavelet Transform.
        // Wavelet to apply: Daubechies 4.
        // It has length 8 samples (parameter = 8)
        // Number of decompositions: LogBase2(N) - scale.
        //      Here N = 1024, LogB2 is 10, hence scale=4 to make 10-4=6 decompositions.
        ////////////////////////

        Wavelet wavelet = Wavelet.Daubechies;
        DWT.Direction direction = DWT.Direction.forward;
        int parameter = 8;
        int scale = 4;

        tempArray = null;
        try {
            tempArray = DWT.transform(signal, wavelet, parameter, scale, direction);
        } catch (Exception e) {
            e.printStackTrace();
        }

        // Signal contains DWT
        signal = tempArray;

        //// Write file with transform
        // Processed signal = contains DWT
        //processedSignal[1] = tempArray;
        //WriteFile("D:\\Ludwig\\Sideprojects\\Master Thesis - Mimerse\\Android-BT_receiver\\PPG_receiver\\math.peakdetector\\src\\main\\java\\signal_norm_fDWT_Db4_8_3",processedSignal);

        ////////////////////////
        //// Filter the first samples in the wavelet to remove frequency noise
        // Filtering 32 samples, assuming that Fs = 50Hz.
        //  - Setting to 0 the first 32 samples filters out the frequencies from [0Hz-0.78125Hz]
        //  - Setting to 0 the first 16 samples filters out the frequencies from [0Hz-0.390625Hz]
        ////////////////////////
        for (int i = 0; i < filteredSamplesDWT; i++)
            signal[i] = 0.0;


        ////////////////////////
        // Reverse DWT
        ////////////////////////
        direction = DWT.Direction.reverse;
        tempArray = null;
        try {
            tempArray = DWT.transform(signal, wavelet, parameter, scale, direction);
        } catch (Exception e) {
            e.printStackTrace();
        }

        // Signal  contains inverse DWT with denoised signal
        signal = tempArray;

        //// Write file after inverse wavelet transform
        //// Processed signal = signal after DWT denoising
        //processedSignal[1] = signal;
        //WriteFile("D:\\Ludwig\\Sideprojects\\Master Thesis - Mimerse\\Android-BT_receiver\\PPG_receiver\\math.peakdetector\\src\\main\\java\\signal_norm_rDWT_Db4_8_4",processedSignal);

        ////////////////////////
        //// Trend removal
        // Finding the shor term trends using a N-tap moving average filter.
        // N is chosen to be equal to Fs to average over 1 second
        ////////////////////////

        // Check odd number of samples to form a symmetric window
        int windowSize = Fs;
        if((Fs % 2) == 0) {
            windowSize = Fs+1; //Even
        } else {
            windowSize = Fs; //Odd
        }

        tempArray = new double[N];
        // Calculate simple moving average
        for(int i=0; i<N; i++)
        {
            // Create a window with the number of elements remaining from the extremes of the signal
            leftLimit  = i - ((windowSize-1)/2);
            rightLimit = i + ((windowSize-1)/2);

            if (leftLimit < 0)
                leftLimit = 0;
            if (rightLimit >= N)
                rightLimit = N-1;

            //System.out.println("Moving average: " + i + "[" + leftLimit + "," + rightLimit + "]");

            // Double cycle to calculate average (trend) around a specific point
            double sum = 0.0;
            int counter = 0;
            for(int k = leftLimit; k<=rightLimit; k++)
            {
                sum = sum + signal[k];
                counter = counter + 1;
            }

            tempArray[i] = sum / counter;
        }

        // Substract trend from original signal
        for(int i=0; i<N; i++) {
            signal[i] = signal[i] - tempArray[i];
        }

        ////////////////////////
        //// Peak search by chunks of 4-seconds
        // finding fundamental frequency in each chunk using correlation.
        // Signal is divided in 4 second segments. The peaks are amplified by substracting the chunk by its minimum and squaring the resulting segment
        // Fs * 4 = 200. Changed to 200 because the last values of the inverse DWT generate noise that makes difficult to recognize peaks after autocorrelation.
        // TODO: Make adjacent windows to calculate only the middle of the signal, overlapping windows between calculations.
        ////////////////////////

        int chunkSize = 4*Fs;   // 4 seconds of signal processing
        int chunks = (int)Math.ceil((N-2*windowOverlap)/chunkSize);

        double[] autocorSignal = new double[N];  // Empty array

        List<Integer> validPeakPositions = new ArrayList<Integer>();

        // The next peak after tau=0 determines the fundamental periodicity T.
        // It is assumed that the heart beat is in the range 40-200bpm, 0.66Hz-3.33Hz.
        // If the next peak of autocorrelation lies in this range and also withing a range epsilon from
        // the fundamental period, it implies that the segment contains valid peak information.

        // # Maximum period change between chunks/segments. e=0.3s corresponds to 200bpm.
        float e = 0.4f;      // seconds
        // Defined such as 2*theta is the maximum deviation in a single 4secs chunk/segment. th=0.2 corresponds to a max deviation of 150bpm in 4 secs
        float theta = 0.4f;  // seconds

        // Translate the previous times into samples
        int eSamples = Math.round(e * Fs);
        int thetaSamples = Math.round(theta * Fs);

        // Peak detector variables
        int posLastDetectedPeak = 0;
        int peakDetWindowSize = 11;

        if((peakDetWindowSize % 2) == 0) {
            peakDetWindowSize = peakDetWindowSize+1; //Even
        } else {
            peakDetWindowSize = peakDetWindowSize; //Odd
        }

        // Iterate chunks
        for (int n = 0; n<chunks; n++) {
            //Segment limits
            leftLimit = windowOverlap + (n * chunkSize);
            rightLimit = windowOverlap + ((n + 1) * chunkSize);

            if(leftLimit < 0)
                leftLimit = 0;
            if (rightLimit >= N)
                rightLimit = N - 1;

            WriteLog("\n\nSEGMENT " + n + ": From index " + leftLimit + " to " + rightLimit); //print indexes

            //Calculate minimum value in the chunk
            double minValue = 0;
            for (int k = leftLimit; k < rightLimit; k++) {
                if (k == leftLimit)
                    minValue = signal[k];
                else if (signal[k] < minValue)
                    minValue = signal[k];
            }

            WriteLog(" - Min:" + minValue); //print indexes

            //Peaks are amplified by substracting the chunk by its minimum and squaring the resulting segment.
            for (int k = leftLimit; k < rightLimit; k++) {
                signal[k] = Math.pow(signal[k] - minValue, 2);
            }

            // UP TO HERE: signal array contains detrended and amplified signal

            //#Autocorrelation of the segment
            int tau = 0; //Tau is the lag for autocorrelation calculation
            double sumValue = 0; // Sum for autocorrelation

            tempArray = new double[N];
            for (int k = leftLimit; k < rightLimit; k++)
            {
                tau = k - leftLimit;    // But "n" moves using the indexes of the original signal, so it's corrected each cycle.

                sumValue = 0;
                for (int d = k; d<rightLimit; d++)
                {
                    sumValue = sumValue + (signal[d] * signal[d - tau]);
                }
                autocorSignal[k] = sumValue;
            }

            //////////////////
            //#Peak detector
            //////////////////

            int firstPeakPosInChunk = 0;
            boolean firstPeakWasFound = false;
            int candidateChunkFundPeriod = 0;
            int chunkFundPeriod = 0;

            boolean dataSegmentIsValid = false; //When TRUE, means that the chunk contains valid information

            //#FIND PEAKS IN AUTOCORRELATION FUNCTION TO GET THE FUNDAMENTAL PERIOD T
            //Iterate chunk
            //skip the first value because there will always be a peak in the autocorrelation with tau = 0
            for (int k = leftLimit; k<rightLimit; k++)
            {
                ////Calculate limits of window for peak detector
                peakDetLeftLim = k - ((peakDetWindowSize - 1) / 2);
                peakDetRightLim = k + ((peakDetWindowSize - 1) / 2);

                if (peakDetLeftLim < leftLimit)
                    peakDetLeftLim = leftLimit;
                if (peakDetRightLim >= rightLimit)
                    peakDetRightLim = rightLimit-1;

                //Find maximum in the window of autocorrelation and check if it is the same k
                double maxValue = 0;
                for (int i = peakDetLeftLim; i < peakDetRightLim; i++) {
                    if (i == peakDetLeftLim)
                        maxValue = autocorSignal[i];
                    else if (autocorSignal[i] > maxValue)
                        maxValue = autocorSignal[i];
                }

                double maxInRange = maxValue;

                //The maximum value in the window is the one that we are analyzing
                if (maxInRange == autocorSignal[k])
                {
                    //Check that the detected maximum is bigger than the window extremes
                    if (autocorSignal[peakDetLeftLim] < autocorSignal[k] && autocorSignal[k] > autocorSignal[peakDetRightLim])
                    {
                        //////PEAK DETECTED !!!
                        WriteLog("Autocor peak found in chunk=" + n + " pos=" + k);

                        //FIND SEGMENT FUNDAMENTAL PERIOD:
                        if (!firstPeakWasFound && !dataSegmentIsValid)
                        {
                            int temporaryPeriod = k - leftLimit;

                            //A valid Heart rate is between 40 - 200 bpm, it is f between 0.66Hz - 3.33 Hz,
                            //hence, T is between 1.5 s - 0.3 s, corresponding to samples between 75 - 15
                            if (temporaryPeriod > 0.3*Fs && temporaryPeriod < 1.5*Fs) {
                                firstPeakWasFound = true;
                                firstPeakPosInChunk = k;

                                candidateChunkFundPeriod = temporaryPeriod;

                                WriteLog("First peak found in chunk=" + n + " pos" + k + " Ts" + candidateChunkFundPeriod);
                            } else {
                                WriteLog(" - Peak dismissed 1: Temp Period=" + temporaryPeriod);
                            }
                        }

                        //Detect second peak in chnk and see if it is between range + / -epsilon
                        else if (firstPeakWasFound && !dataSegmentIsValid) {
                            int temporaryPeriod = k - firstPeakPosInChunk; //Calculate distance from first peak

                            //A valid Heart rate is between 40 - 200 bpm, it is f between 0.66Hz - 3.33 Hz,
                            //hence, T is between 1.5 s - 0.3 s, corresponding to samples between 75 - 15
                            if ((temporaryPeriod > 0.3 * Fs && temporaryPeriod < 1.5 * Fs) &&
                                    (temporaryPeriod > (candidateChunkFundPeriod - eSamples) && temporaryPeriod < (candidateChunkFundPeriod + eSamples))) {

                                dataSegmentIsValid = true;

                                chunkFundPeriod = candidateChunkFundPeriod;

                                WriteLog("Second peak found in chunk=" + n + " pos" + k + " Ts" + temporaryPeriod);
                                WriteLog(" || Chunk contains valid peak information!!! \n");

                                break;
                            }
                            //In case the second peak is very close to the first one, but bigger and still between the range, consider this peak as the first one instead
                            else if(((autocorSignal[k] > autocorSignal[firstPeakPosInChunk]) &&//But this new peak is bigger
                                    (k - leftLimit) > 0.3 * Fs && (k - leftLimit) < 1.5 * Fs)) //and still within the range of accepted fundamental period
                            {
                                firstPeakPosInChunk = k;
                                candidateChunkFundPeriod = (k - leftLimit);

                                WriteLog("First peak replaced in chunk=" + n + " pos" + k + " newTs" + candidateChunkFundPeriod);
                            }
                            else
                            {
                                WriteLog(" - Peak dismissed 2: Temp Period=" + temporaryPeriod);
                            }
                        }
                    }
                }
            }


            //Case: FUNDAMENTAL PERIOD WAS SET AND DATA CHUNK IS VALID.Next peaks must be in neighborhood of(previous peak + fundamental period)
            if (dataSegmentIsValid) {
                WriteLog(" - Valid segment, fundPeriodSamples = " + chunkFundPeriod);

                //neighborhood limits.[lastPeak + T - theta]
                validRangeNextPeakLeftLim = posLastDetectedPeak + chunkFundPeriod - thetaSamples;
                validRangeNextPeakRightLim = posLastDetectedPeak + chunkFundPeriod + thetaSamples;

                for (int k = leftLimit; k < rightLimit; k++)
                {
                    ////Calculate limits of window for peak detector
                    peakDetLeftLim = k - ((peakDetWindowSize - 1) / 2);
                    peakDetRightLim = k + ((peakDetWindowSize - 1) / 2);

                    if (peakDetLeftLim < leftLimit)
                        peakDetLeftLim = leftLimit;
                    if (peakDetRightLim >= rightLimit)
                        peakDetRightLim = rightLimit-1;

                    //////TODO PEAK DETECTOR IN SIGNAL WHEN SEGMENT IS VALID
                    //USE MAXIMUM DETECTOR

                    //Find maximum in the window of amplified signal and check if it is the same k
                    double maxValue = 0;
                    for (int i = peakDetLeftLim; i < peakDetRightLim; i++) {
                        if (i == peakDetLeftLim)
                            maxValue = signal[i];
                        else if (signal[i] > maxValue)
                            maxValue = signal[i];
                    }

                    double maxInRange = maxValue;
                    //The maximum value in the window is the one that we are analyzing
                    if (maxInRange == signal[k]) {
                        //Check that the detected maximum is bigger that the window extremes
                        if (signal[peakDetLeftLim] < signal[k] && signal[k] > signal[peakDetRightLim]) {
                            //////PEAK DETECTED !!!
                            WriteLog("Peak found in chunk=" + n + " pos=" + k);

                            //There is no previous peak, or the previous segment was invalid
                            if (posLastDetectedPeak == 0) {
                                posLastDetectedPeak = k;
                                validRangeNextPeakLeftLim = posLastDetectedPeak + chunkFundPeriod - thetaSamples;
                                validRangeNextPeakRightLim = posLastDetectedPeak + chunkFundPeriod + thetaSamples;
                                WriteLog(" - First Peak set. New limits: " + validRangeNextPeakLeftLim + "-" + validRangeNextPeakRightLim);
                            } else if (k >= validRangeNextPeakLeftLim && k <= validRangeNextPeakRightLim) //Withing neighborhood limits
                            {
                                posLastDetectedPeak = k;
                                validRangeNextPeakLeftLim = posLastDetectedPeak + chunkFundPeriod - thetaSamples;
                                validRangeNextPeakRightLim = posLastDetectedPeak + chunkFundPeriod + thetaSamples;

                                //Add new peak
                                validPeakPositions.add(k);

                                WriteLog(" - PEAK ADDED. New limits: " + validRangeNextPeakLeftLim + "-" + validRangeNextPeakRightLim);
                            }
                            else
                            {
                                WriteLog("Peak dismissed pos:" + k);
                            }
                        } else {
                            WriteLog(" - Peak dismissed - Maximum but not greater than the extremes");
                        }
                    }
                }
            } else {
                WriteLog(" - Invalid segment\n");
                posLastDetectedPeak = 0;
            }
        }

        // WRITE DATA
        if(denoisedSignalWriter != null)
        {
            try {
                int startIdxOfProcessedSegments = windowOverlap;
                if(firstProcessing)
                    startIdxOfProcessedSegments = 0;
                int endIdxOfProcessedSegments = windowOverlap + (chunks * chunkSize);

                WriteLog("Writing denoised signal in file from " + startIdxOfProcessedSegments + " to " + endIdxOfProcessedSegments);
                for (int i = startIdxOfProcessedSegments; i < endIdxOfProcessedSegments; i++) {
                    denoisedSignalWriter.write(Double.toString(signal[i]));
                    denoisedSignalWriter.newLine();
                    denoisedSignalWriter.flush();
                }
            }
            catch (IOException ioe) {
                ioe.printStackTrace();
            }
        }

        return validPeakPositions;
    }


    private static void WriteFile(String path, double[][] matrix)
    {
        try {
            File csvFile = new File(path);
            if (csvFile != null) {
                FileOps.saveCsv(csvFile, MatrixOps.transpose(matrix));
            }
        } catch (IOException e1) {
            e1.printStackTrace();
        }
    }

}
