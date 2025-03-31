using System.Diagnostics;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace MFR
{
    public class Program
    {
        private static SKPaint _outline = new()
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f
        };
        private static SKPaint _blackkey = new()
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Fill
        };
        static SKColor GenerateTrackColor(int track, int totalTracks)
        {
            // Asegurarse de que el track esté en el rango correcto
            track = Math.Max(0, Math.Min(track, totalTracks - 1));

            // Dividir el espectro de colores (0-360 en HSL) entre los tracks
            float hue = (float)track / totalTracks * 360.0f;

            // Mantener la saturación y luminosidad constantes para un estilo consistente
            float saturation = 0.8f; // 80% de saturación
            float lightness = 0.6f; // 60% de luminosidad

            // Convertir HSL a RGB
            //return SKColor.FromHsl(hue, saturation, lightness);
            return HslToRgb(hue, saturation, lightness);
        }

        static SKColor HslToRgb(float hue, float saturation, float lightness)
        {
            float c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
            float x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
            float m = lightness - c / 2;

            float r = 0, g = 0, b = 0;

            if (hue >= 0 && hue < 60)
            {
                r = c; g = x; b = 0;
            }
            else if (hue >= 60 && hue < 120)
            {
                r = x; g = c; b = 0;
            }
            else if (hue >= 120 && hue < 180)
            {
                r = 0; g = c; b = x;
            }
            else if (hue >= 180 && hue < 240)
            {
                r = 0; g = x; b = c;
            }
            else if (hue >= 240 && hue < 300)
            {
                r = x; g = 0; b = c;
            }
            else if (hue >= 300 && hue < 360)
            {
                r = c; g = 0; b = x;
            }

            return new SKColor(
                (byte)((r + m) * 255),
                (byte)((g + m) * 255),
                (byte)((b + m) * 255)
            );
        }

        private static int _nt = 127;
        static SKPoint[] GetBox(float x, float y, float width, float height)
        {
            return [
                /*
                            A--------------B /DUPLICATE
                            -              -
                            -              -
                            -              -
                 /DUPLICATE C--------------D
                 */
                new SKPoint(x, y),                 // Esquina superior izquierda: A
                new SKPoint(x + width, y),         // Esquina superior derecha: B
                new SKPoint(x, y + height),        // Esquina inferior izquierda : C
             
                new SKPoint(x + width, y),         // Esquina superior derecha (repetido): B
                new SKPoint(x + width, y + height), // Esquina inferior derecha: D
                new SKPoint(x, y + height)         // Esquina inferior izquierda (repetido): C
            ];
        }
        // Método para crear los uniforms de forma segura
        private static SKRuntimeEffectUniforms CreateNoteUniforms(SKColor color, float x, float y, float width, float height)
        {
            var uniforms = new SKRuntimeEffectUniforms(NoteShaderEffect);
    
            // u_color: vec4 (RGBA en formato float 0-1)
            uniforms.Add("u_color", new[] 
            {
                color.Red / 255f,
                color.Green / 255f,
                color.Blue / 255f,
                color.Alpha / 255f
            });
    
            // u_position: vec2 (X, Y)
            uniforms.Add("u_position", new[] { x, y });
    
            // u_size: vec2 (Width, Height)
            uniforms.Add("u_size", new[] { width, height });
    
            return uniforms;
        }
        const string NoteShaderCode = @"
        uniform vec4 u_color;
        uniform vec2 u_position;
        uniform vec2 u_size;

        vec4 main(vec2 fragCoord) {
            // Coordenadas normalizadas (0 a 1 dentro del rectángulo)
            vec2 uv = (fragCoord - u_position) / u_size;
            
            // Efecto de sombra (degradado vertical)
            float shadow = uv.y * 0.2;  // Ajusta el 0.5 para más/menos contraste
            
            // Color final con sombra
            return vec4(u_color.rgb * (1.0 - shadow), u_color.a);
        }";
        static string shaderError = "";
        
        static SKRuntimeEffect NoteShaderEffect = SKRuntimeEffect.CreateShader(NoteShaderCode, out shaderError);
        public static async Task Main(string[] args)
        {
            Console.WriteLine(shaderError);
            Console.Write("Press any key to continue but before check Shader Errors up...");
            Console.ReadKey();
            Console.WriteLine();
            double midiTime = 0;
            
            string midiFilePath = @"C:\Users\HUMAN\Videos\midi\mids\evans black DEATH 10 million.mid"; // Ruta al archivo MIDI
            string outputVideoPath = "output.mp4"; // Ruta de salida del video
            int fps = 60;
            Console.WriteLine("MFR | Midi Free Renderer");
            Console.WriteLine("Credits to:");
            Console.WriteLine("\tSkiaSharp team for making the SkiaSharp wrapper around Skia");
            Console.WriteLine("\tall people who worked/contributed to ffmpeg without this the program wouldn't render the midi as a video");
            Console.WriteLine("\tDeepSeek for giving ideas on how to optimize and Translating some C++ to C# (psst, its the MidiParser)");
            Console.WriteLine("\tRisc-A2 (Main creator of MFR)");
            // Leer el archivo MIDI
            Console.WriteLine("Loading midi...");
            
            Console.Write("Press any key to render...");
            Console.ReadKey();
            Console.WriteLine();
            var fs = File.CreateText($"MFR_{DateTime.Now.ToString("s").Replace(":", "-")}.log");
            fs.AutoFlush = true;
            int width = 1920;
            int height = 1080;
            var info = new SKImageInfo(width, height, SKColorType.Rgb888x, SKAlphaType.Opaque);
            SKBitmap surface = new SKBitmap(info);
            var image = new SKCanvas(surface);
            Console.WriteLine("Starting ffmpeg");
            // Inicializar FFmpeg
            Process ffmpeg = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    //Arguments = $"-y -f rawvideo -pix_fmt rgba -s {width}x{height} -r {fps} -i - -c:v libx264 -pix_fmt yuv420p {outputVideoPath}",
                    //h264_qsv
                    // usar h264_qsv para acceleracion por hardware
                    Arguments = $"-y -f rawvideo -pix_fmt rgba -s {width}x{height} -r {fps} -i - -c:v h264_qsv -q:v 23 -pix_fmt nv12 {outputVideoPath}",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardError = true, // ¡Importante para depurar
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            ffmpeg.OutputDataReceived += (sender, eventArgs) =>
            {
                fs.WriteLine($"ffmpeg stdout: {eventArgs.Data}");
            };
            ffmpeg.ErrorDataReceived += (sender, eventArgs) =>
            {
                fs.WriteLine($"ffmpeg stderr: {eventArgs.Data}");
                Console.WriteLine($"FFMPEG ERROR: {eventArgs.Data}");
            };

            ffmpeg.Start();
            Stream ffmpegInput = ffmpeg.StandardInput.BaseStream;
            Console.WriteLine("Freeing pointer");
            Console.WriteLine("Freeing image & canvas & Vertex & Colors");
            image.Dispose();
            surface.Dispose();
            Console.WriteLine("Waiting ffmpeg to output the video...");
            // Cerrar FFmpeg y guardar el video
            ffmpegInput.Close();
            ffmpeg.WaitForExit();
            fs.Close();
            Console.WriteLine($"video saved in {outputVideoPath}");
            Console.Write("Press any key to exit...");
            Console.ReadKey();
            return;
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        static SKRect[] _wp;
        static SKRect[] _bp;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        private static bool IsBlackKey(int midiNote)
        {
            int[] blackKeysInOctave = { 1, 3, 6, 8, 10 }; // C#, D#, F#, G#, A#
            return blackKeysInOctave.Contains((midiNote) % 12);
        }
    }
}
