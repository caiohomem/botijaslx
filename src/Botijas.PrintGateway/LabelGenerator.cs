using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Botijas.PrintGateway;

public class LabelData
{
    public required string QrContent { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerPhone { get; init; }
}

public class LabelGenerator
{
    // Zebra GK420d - 203 DPI (8 dots per mm)
    private const int DPI = 203;
    private const double DotsPerMm = 8.0;
    
    // Label size in dots (50x75mm default)
    private int _labelWidthDots = 400;  // 50mm
    private int _labelHeightDots = 600; // 75mm

    // Logo data
    private string? _logoGrfCommand;
    private int _logoWidthDots;
    private int _logoHeightDots;

    public LabelGenerator()
    {
    }

    /// <summary>
    /// Configure label size in millimeters
    /// </summary>
    public void SetLabelSize(int widthMm, int heightMm)
    {
        _labelWidthDots = MmToDots(widthMm);
        _labelHeightDots = MmToDots(heightMm);
    }

    /// <summary>
    /// Load and convert logo from PNG/JPG file
    /// </summary>
    public void LoadLogo(string imagePath, int maxWidthMm = 30, int maxHeightMm = 15)
    {
        if (!File.Exists(imagePath))
        {
            Console.WriteLine($"Logo not found: {imagePath}");
            return;
        }

        try
        {
            var maxWidthDots = MmToDots(maxWidthMm);
            var maxHeightDots = MmToDots(maxHeightMm);
            
            _logoGrfCommand = ConvertImageToGrf(imagePath, maxWidthDots, maxHeightDots, 
                out _logoWidthDots, out _logoHeightDots);
            
            Console.WriteLine($"Logo loaded: {_logoWidthDots}x{_logoHeightDots} dots");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading logo: {ex.Message}");
        }
    }

    /// <summary>
    /// Generate ZPL for a label with QR code, customer info
    /// </summary>
    public string GenerateLabel(LabelData data)
    {
        var zpl = new StringBuilder();

        // Start label
        zpl.AppendLine("^XA");
        
        // Set print width and length
        zpl.AppendLine($"^PW{_labelWidthDots}");
        zpl.AppendLine($"^LL{_labelHeightDots}");
        
        // Set encoding for special characters
        zpl.AppendLine("^CI28"); // UTF-8

        var currentY = 10;

        // Logo (top center)
        if (!string.IsNullOrEmpty(_logoGrfCommand))
        {
            var logoX = (_labelWidthDots - _logoWidthDots) / 2;
            zpl.AppendLine($"^FO{logoX},{currentY}");
            zpl.AppendLine(_logoGrfCommand);
            currentY += _logoHeightDots + 15;
        }

        // QR Code (center)
        var qrSize = 6; // Magnification (size)
        var qrApproxWidth = qrSize * 25; // Approximate QR width
        var qrX = (_labelWidthDots - qrApproxWidth) / 2;
        
        zpl.AppendLine($"^FO{qrX},{currentY}");
        zpl.AppendLine($"^BQN,2,{qrSize}"); // QR Code, Model 2, Magnification
        zpl.AppendLine($"^FDLA,{data.QrContent}^FS"); // LA = Low error correction
        
        currentY += qrApproxWidth + 15;

        // Customer Name (center, bold)
        if (!string.IsNullOrEmpty(data.CustomerName))
        {
            var nameClean = SanitizeText(data.CustomerName);
            zpl.AppendLine($"^FO20,{currentY}");
            zpl.AppendLine("^A0N,28,28"); // Font 0, Normal, 28pt
            zpl.AppendLine($"^FB{_labelWidthDots - 40},1,0,C"); // Field block, centered
            zpl.AppendLine($"^FD{nameClean}^FS");
            currentY += 35;
        }

        // Customer Phone (center)
        if (!string.IsNullOrEmpty(data.CustomerPhone))
        {
            var phoneClean = SanitizeText(data.CustomerPhone);
            zpl.AppendLine($"^FO20,{currentY}");
            zpl.AppendLine("^A0N,24,24"); // Font 0, Normal, 24pt
            zpl.AppendLine($"^FB{_labelWidthDots - 40},1,0,C"); // Field block, centered
            zpl.AppendLine($"^FD{phoneClean}^FS");
        }

        // End label
        zpl.AppendLine("^XZ");

        return zpl.ToString();
    }

    /// <summary>
    /// Generate ZPL for multiple labels
    /// </summary>
    public string GenerateLabels(IEnumerable<LabelData> labels)
    {
        var zpl = new StringBuilder();
        foreach (var label in labels)
        {
            zpl.Append(GenerateLabel(label));
        }
        return zpl.ToString();
    }

    /// <summary>
    /// Convert image to ZPL GRF format
    /// </summary>
    private static string ConvertImageToGrf(string imagePath, int maxWidth, int maxHeight, 
        out int actualWidth, out int actualHeight)
    {
        using var image = Image.Load<Rgba32>(imagePath);
        
        // Calculate scale to fit within max dimensions
        var scale = Math.Min((double)maxWidth / image.Width, (double)maxHeight / image.Height);
        var newWidth = (int)(image.Width * scale);
        var newHeight = (int)(image.Height * scale);
        
        // Width must be multiple of 8 for ZPL
        newWidth = (newWidth + 7) / 8 * 8;
        
        // Resize image
        image.Mutate(x => x.Resize(newWidth, newHeight));
        
        actualWidth = newWidth;
        actualHeight = newHeight;
        
        // Convert to monochrome and encode as GRF
        var bytesPerRow = newWidth / 8;
        var totalBytes = bytesPerRow * newHeight;
        var hexData = new StringBuilder();
        
        for (var y = 0; y < newHeight; y++)
        {
            for (var x = 0; x < newWidth; x += 8)
            {
                byte b = 0;
                for (var bit = 0; bit < 8; bit++)
                {
                    var px = x + bit;
                    if (px < image.Width)
                    {
                        var pixel = image[px, y];
                        // Convert to grayscale and threshold
                        var gray = (pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                        var isBlack = gray < 128 && pixel.A > 128;
                        if (isBlack)
                        {
                            b |= (byte)(0x80 >> bit);
                        }
                    }
                }
                hexData.Append(b.ToString("X2"));
            }
        }
        
        // GRF command: ^GFA,total,total,bytesPerRow,data
        return $"^GFA,{totalBytes},{totalBytes},{bytesPerRow},{hexData}^FS";
    }

    /// <summary>
    /// Remove special characters that might break ZPL
    /// </summary>
    private static string SanitizeText(string text)
    {
        return text
            .Replace("^", "")
            .Replace("~", "")
            .Replace("\\", "");
    }

    private static int MmToDots(int mm) => (int)(mm * DotsPerMm);
}

/// <summary>
/// Sends ZPL to Zebra printer via TCP/IP
/// </summary>
public class ZebraPrinter
{
    private readonly string _printerAddress;
    private readonly int _port;

    public ZebraPrinter(string ipAddress, int port = 9100)
    {
        _printerAddress = ipAddress;
        _port = port;
    }

    public async Task<bool> PrintAsync(string zpl, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var connectTask = client.ConnectAsync(_printerAddress, _port);
            
            // Timeout de 5 segundos para conexão
            if (await Task.WhenAny(connectTask, Task.Delay(5000, cancellationToken)) != connectTask)
            {
                Console.WriteLine($"Connection timeout to {_printerAddress}:{_port}");
                return false;
            }
            
            await connectTask; // Propaga exceção se houver
            
            using var stream = client.GetStream();
            var bytes = Encoding.UTF8.GetBytes(zpl);
            await stream.WriteAsync(bytes, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            
            Console.WriteLine($"Sent {bytes.Length} bytes to printer");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Print error: {ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// Sends ZPL to Zebra printer via Windows shared printer
/// </summary>
public class ZebraWindowsPrinter
{
    private readonly string _printerName;

    public ZebraWindowsPrinter(string printerName)
    {
        _printerName = printerName;
    }

    public bool Print(string zpl)
    {
        // This requires Windows-specific code using RawPrinterHelper
        // For cross-platform, use TCP/IP connection instead
        throw new NotImplementedException("Use ZebraPrinter with TCP/IP for cross-platform support");
    }
}
