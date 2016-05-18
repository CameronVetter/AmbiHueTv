using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media;
using Windows.UI.Xaml.Media.Imaging;
using static System.Int16;

namespace zecil.AmbiHueTv
{
    public enum AnalysisAlgorithm
    {
        PureAverage = 0,
        MostFrequentColor = 1,
        MostFrequentWholeColor = 2
    }

    public enum BiasAlgorithm
    {
        None = 0,
        RuleOfThirds = 1,
        GoldenRatio = 2
    }

    public class FrameAnalysis
    {
        public BiasAlgorithm Bias { get; private set; }

        public byte Red { get; set; }
        public byte Green { get; set; }
        public byte Blue { get; set; }
        public byte Alpha { get; set; }

        public void AnalyzeFrame(ref VideoFrame frame, AnalysisAlgorithm algorithm, BiasAlgorithm bias)
        {
            Bias = bias;

            WriteableBitmap bitmap = new WriteableBitmap(frame.SoftwareBitmap.PixelWidth,
                frame.SoftwareBitmap.PixelHeight);
            frame.SoftwareBitmap.CopyToBuffer(bitmap.PixelBuffer);

            switch (algorithm)
            {
                case AnalysisAlgorithm.MostFrequentColor:
                    MostFrequentColor(ref bitmap);
                    break;

                case AnalysisAlgorithm.PureAverage:
                    PureAverage(ref bitmap);
                    break;

                case AnalysisAlgorithm.MostFrequentWholeColor:
                    MostFrequentWholeColor(ref bitmap);
                    break;
            }
        }

        private int BiasValue(int x, int y, int width, int height)
        {
            switch (Bias)
            {
                case BiasAlgorithm.None:
                    return 1;
                case BiasAlgorithm.RuleOfThirds:
                    if ((x >= width / 3) && (x <= width / 3 * 2))
                    {
                        return 20;
                    }
                    if ((y >= height / 3) && (y <= height / 3 * 2))
                    {
                        return 20;
                    }
                    return 1;
                case BiasAlgorithm.GoldenRatio:
                    if ((x >= width * 61 / 161 ) && (x <= width * 100 / 161 ))
                    {
                        return 20;
                    }
                    if ((y >= height * 61 / 161 ) && (y <= height * 100 / 161 ))
                    {
                        return 20;
                    }
                    return 1;
                default:
                    throw new Exception("Unknown Bias Algorithm Selected");
            }
        }

        private void PureAverage(ref WriteableBitmap bitmap)
        {
            long redTotal = 0;
            long greenTotal = 0;
            long blueTotal = 0;
            long alphaTotal = 0;
            int count = 0;
            int x = 0;
            int y = 0;
            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;

            using (var stream = bitmap.PixelBuffer.AsStream())
            {
                int red = 0;

                while (red != -1)
                {
                    red = stream.ReadByte();
                    if (red > -1)
                    {
                        var green = stream.ReadByte();
                        var blue = stream.ReadByte();
                        var alpha = stream.ReadByte();

                        //discard pure black
                        if (!(red == 0 && green == 0 && blue == 0))
                        {
                            if (!IsGray(red, green, blue))
                            {
                                var bias = BiasValue(x, y, width, height);
                                redTotal += (red * bias);
                                greenTotal += (green * bias);
                                blueTotal += (blue * bias);
                                alphaTotal += (alpha * bias);
                                count += bias;
                            }
                        }
                    }

                    // track our location
                    x++;
                    if (x > bitmap.PixelWidth)
                    {
                        x = 0;
                        y++;
                    }
                }
            }

            Red = Convert.ToByte(redTotal / count);
            Green = Convert.ToByte(greenTotal / count);
            Blue = Convert.ToByte(blueTotal / count);
            Alpha = Convert.ToByte(alphaTotal / count);
        }

        private void MostFrequentColor(ref WriteableBitmap bitmap)
        {
            int[] redCount = new int[MaxValue];
            int[] greenCount = new int[MaxValue];
            int[] blueCount = new int[MaxValue];
            int[] alphaCount = new int[MaxValue];
            int x = 0;
            int y = 0;
            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;

            using (var stream = bitmap.PixelBuffer.AsStream())
            {
                int red = 0;

                while (red != -1)
                {
                    red = stream.ReadByte();
                    if (red > -1)
                    {
                        var green = stream.ReadByte();
                        var blue = stream.ReadByte();
                        var alpha = stream.ReadByte();

                        //discard pure black
                        if (!(red == 0 && green == 0 && blue == 0))
                        {
                            if (!IsGray(red, green, blue))
                            {
                                var bias = BiasValue(x, y, width, height);
                                redCount[red] += bias;
                                greenCount[green] += bias;
                                blueCount[blue] += bias;
                                alphaCount[alpha] += bias;
                            }
                        }
                    }

                    // track our location
                    x++;
                    if (x > bitmap.PixelWidth)
                    {
                        x = 0;
                        y++;
                    }
                }
            }

            Red = Convert.ToByte(MostFrequentValue(redCount));
            Green = Convert.ToByte(MostFrequentValue(greenCount));
            Blue = Convert.ToByte(MostFrequentValue(blueCount));
            Alpha = Convert.ToByte(MostFrequentValue(alphaCount));
        }

        private void MostFrequentWholeColor(ref WriteableBitmap bitmap)
        {
            Dictionary<ulong, int> counts = new Dictionary<ulong, int>();
            int x = 0;
            int y = 0;
            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;

            using (var stream = bitmap.PixelBuffer.AsStream())
            {
                int red = 0;

                while (red != -1)
                {
                    red = stream.ReadByte();
                    if (red > -1)
                    {
                        var green = stream.ReadByte();
                        var blue = stream.ReadByte();
                        var alpha = stream.ReadByte();

                        ulong key = ((ulong)alpha << 24) + ((ulong)blue << 16) + ((ulong)green << 8) + (ulong)red;

                        if (key != 0)
                        {
                            if (!IsGray(red, green, blue))
                            {
                                var bias = BiasValue(x, y, width, height);
                                if (counts.ContainsKey(key))
                                {
                                    counts[key] += bias;
                                }
                                else
                                {
                                    counts.Add(key, bias);
                                }
                            }
                        }
                    }

                    // track our location
                    x++;
                    if (x > bitmap.PixelWidth)
                    {
                        x = 0;
                        y++;
                    }
                }
            }


            var mostFrequent = FindMax(counts);

            var mfAlpha = mostFrequent >> 24;
            var mfBlue = (mostFrequent - (mfAlpha << 24)) >> 16;
            var mfGreen = (mostFrequent - (mfAlpha << 24) - (mfBlue << 16)) >> 8;
            var mfRed = (mostFrequent - (mfAlpha << 24) - (mfBlue << 16)) - (mfGreen << 8);

            Red = Convert.ToByte(mfRed);
            Green = Convert.ToByte(mfGreen);
            Blue = Convert.ToByte(mfBlue);
            Alpha = Convert.ToByte(mfAlpha);

        }

        private ulong FindMax(Dictionary<ulong, int> counts)
        {
            ulong result = 0;
            int highCount = 0;

            foreach (var set in counts)
            {
                if (set.Value > highCount)
                {
                    highCount = set.Value;
                    result = set.Key;
                }
            }

            return result;
        }

        private bool IsGray(int red, int green, int blue)
        {

            int rgDiff = red - green;
            int rbDiff = red - blue;

            int tolerance = 10;

            if (rgDiff > tolerance || rgDiff < -tolerance)
                if (rbDiff > tolerance || rbDiff < -tolerance)
                {

                    return false;

                }

            return true;
        }

        private int MostFrequentValue(int[] values)
        {
            int result = 1;
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] > values[result])
                {
                    result = i;
                }
            }

            return result;
        }
    }
}
