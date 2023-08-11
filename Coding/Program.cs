using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Device.Gpio;
using AForge.Video;
using AForge.Video.DirectShow;

class Program
{
    static void Main()
    {
        // Check for the trigger signal from the infrared receiver
        string triggerSignal = ReceiveInfraredSignal();
        if (triggerSignal == "avante")
        {
            Console.WriteLine("Trigger signal received. Proceeding with image capture.");
            
            // Take a picture from the USB camera and save it
            string imagePath = "image.jpg";
            TakePicture(imagePath);

            // Compress the image
            string compressedPath = "compressed_image.jpg";
            CompressImage(imagePath, compressedPath);

            // Transmit the compressed image via infrared using GPIO for UART communication
            TransmitInfrared(compressedPath);

            // Clean up the temporary files
            File.Delete(imagePath);
            File.Delete(compressedPath);
        }
        else
        {
            Console.WriteLine("Invalid trigger signal received. Aborting image capture.");
        }
    }

    static string ReceiveInfraredSignal()
    {
        Console.WriteLine("Waiting for infrared signal...");

        // Set up the GPIO pin for input
        int gpioPin = 17; // Replace with the appropriate GPIO pin
        using (var gpioController = new GpioController())
        {
            gpioController.OpenPin(gpioPin, PinMode.Input);

            // Wait for the infrared signal
            while (true)
            {
                if (gpioController.Read(gpioPin) == PinValue.High)
                {
                    // Signal received, start decoding
                    string signal = "";
                    for (int i = 0; i < 8; i++)
                    {
                        // Read each bit of the signal
                        Thread.Sleep(10); // Adjust the delay based on your infrared receiver
                        signal += gpioController.Read(gpioPin) == PinValue.High ? "1" : "0";
                    }
                    return signal;
                }

                Thread.Sleep(100);
            }
        }
    }

    static void TakePicture(string imagePath)
    {
        // Use the AForge.Video library to capture an image from the USB camera and save it to the specified image path
        FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
        VideoCaptureDevice videoSource = new VideoCaptureDevice(videoDevices[0].MonikerString);
        videoSource.NewFrame += (s, e) =>
        {
            // Save the captured frame as an image file
            e.Frame.Save(imagePath, System.Drawing.Imaging.ImageFormat.Jpeg);

            // Stop capturing
            videoSource.SignalToStop();
        };
        videoSource.Start();
        Thread.Sleep(2000); // Adjust the delay to ensure the camera has enough time to capture the frame
    }

    static void CompressImage(string imagePath, string compressedPath)
    {
        // Compress the image using JPEG compression
        using (var inputStream = new FileStream(imagePath, FileMode.Open))
        using (var outputStream = new FileStream(compressedPath, FileMode.Create))
        {
            using (var jpegStream = new MemoryStream())
            {
                inputStream.CopyTo(jpegStream);
                var jpegData = jpegStream.ToArray();
                outputStream.Write(jpegData, 0, jpegData.Length);
            }
        }
    }

    static void TransmitInfrared(string filePath)
    {
        // Use the GPIO pins for UART communication
        int txdPin = 14; // Replace with the appropriate GPIO pin for TXD
        int rxdPin = 15; // Replace with the appropriate GPIO pin for RXD

        using (var gpioController = new GpioController())
        {
            gpioController.OpenPin(txdPin, PinMode.Output);
            gpioController.OpenPin(rxdPin, PinMode.Input);

            // Read the file as raw bytes
            var fileBytes = File.ReadAllBytes(filePath);

            // Transmit each byte via infrared using the TXD pin
            foreach (byte dataByte in fileBytes)
            {
                TransmitByte(dataByte, gpioController, txdPin);
                // Add a delay between each byte transmission if needed
                Thread.Sleep(1);
            }
        }
    }

    static void TransmitByte(byte dataByte, GpioController gpioController, int txdPin)
    {
        // Transmit a single byte via infrared using the TXD pin
        for (int i = 0; i < 8; i++)
        {
            gpioController.Write(txdPin, (dataByte & (1 << i)) != 0 ? PinValue.High : PinValue.Low);
            Thread.Sleep(1); // Adjust the delay based on your infrared transmitter
        }
    }
}
