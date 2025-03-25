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
            Style = SKPaintStyle.Stroke
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
            float shadow = uv.y * 0.5;  // Ajusta el 0.5 para más/menos contraste
            
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
            
            string midiFilePath = @"C:\Users\HUMAN\Videos\midi\mids\097 replication.mid"; // Ruta al archivo MIDI
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
            MidiFile midiFile = new MidiFile(midiFilePath);
            GC.Collect(2, GCCollectionMode.Forced, true, false);
            GC.WaitForPendingFinalizers();
            double dur = midiFile.GetDurationInMilliseconds();
            int evtCount = 0;
            foreach (var trk in midiFile.Tracks)
            {
                evtCount += trk.Events.Length;
            }
            Console.WriteLine($"\tMidi loaded\n\tNote count: {midiFile.Notes.Length:N0} ({(midiFile.Notes.Length * 21):N0} bytes?)\n\tTempo changes count: {midiFile.TempoChanges.Length:N0} ({(midiFile.TempoChanges.Length * 12):N0} Bytes?)\n\tEvent count: {evtCount:N0} ({(evtCount * 8):N0} Bytes?)\n\tDuration (ms): {dur:N2} ({TimeSpan.FromMilliseconds(dur).ToString("c")})");
            Console.Write("Press any key to render...");
            Console.ReadKey();
            Console.WriteLine();
            Console.WriteLine("Creating SKPaint and Delta");
            double deltaTime = 1d / fps;
            double deltaMidi = (midiFile.PPQ * (midiFile.BPM / 60.0) * deltaTime);
            var paint = new SKPaint
            {
                Color = SKColors.White,
                IsAntialias = false,
            };

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
                    Arguments = $"-y -f rawvideo -pix_fmt rgba -s {width}x{height} -r {fps} -i - -c:v libx264 -pix_fmt yuv420p {outputVideoPath}",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                }
            };

            ffmpeg.Start();
            Stream ffmpegInput = ffmpeg.StandardInput.BaseStream;
            Console.WriteLine("Preparing colors");
            bool[] WhitePianoKeys = new bool[_nt];
            bool[] keyLayout = {true, false, true, false, true, true, false, true, false, true, false, true};
            SKColor[] colors = new SKColor[midiFile.Tracks.Count()];
            SKColor[][] colorsvertexHelp = new SKColor[midiFile.Tracks.Count()][];
            SKPaint[] pianoKeys = new SKPaint[_nt];
            SKPaint[] blackpianoKeys = new SKPaint[_nt];
            int lyt = 0;
            for (int i = 0; i < pianoKeys.Length; i++)
            {
                if (lyt >= 12)
                {
                    lyt = 0;
                }   
                WhitePianoKeys[i] = keyLayout[lyt];
                ++lyt;
                pianoKeys[i] = new();
                pianoKeys[i].Color = SKColors.White;
                if (!WhitePianoKeys[i])
                {
                    blackpianoKeys[i] = new();
                    blackpianoKeys[i].Color = SKColors.Black;
                }
            }

            for (int i = 0; i < colors.Length; i++)
            {
                var cc = GenerateTrackColor(i, colors.Length);
                colors[i] = cc;
                colorsvertexHelp[i] = [cc, cc, cc, cc, cc, cc];
            }

            Console.WriteLine("Preparing Piano SKRect's");
            float keyHeight = (float)(height * 0.151);
            float keyWidth = (float)(width / _nt);
            float screenBottom = height - keyHeight;
            _wp = new SKRect[_nt];
            for (int i = 0; i < _wp.Length; i++)
            {
                float x = i * keyWidth;
                _wp[i] = new SKRect(x, screenBottom, x + keyWidth, height);
            }
            _bp = new SKRect[_nt];
            for (int i = 0; i < _bp.Length; i++)
            {
                if (!WhitePianoKeys[i])
                {
                    float x = i * keyWidth;
                    _bp[i] = new SKRect(x + keyWidth * 0.75f - 10, screenBottom, x + keyWidth * 0.75f + 10, height - keyHeight / 2);
                }
            }

            Key[] k = new Key[_nt];
            for (int i = 0; i < k.Length; i++)
            {
                k[i] = new();
            }
            Console.WriteLine("Creating vertices, colors arrays");
            FastList<SKPoint> vertices = new();
            FastList<SKColor> colorsv = new();
            
            var borderColor = SKColors.Black; // Color del borde
            SKColor[] borderColorArray =
                [borderColor, borderColor, borderColor, borderColor, borderColor, borderColor];
            Console.WriteLine("Allocating managed raw image");
            byte[] buffer = new byte[surface.ByteCount]; // 3 bytes por píxel (RGB), 4 bytes 
            Console.WriteLine("Allocating unmanaged raw image pointer");
            var ptr = Marshal.AllocHGlobal(buffer.Length);
            GC.AddMemoryPressure(surface.ByteCount);
            float thickness = 2;
            Console.WriteLine("Rendering...");
            float scaleFactor = 1f;
            double dtCalc = deltaMidi * 4;
            float ss = screenBottom - keyHeight;
            //while (midiTime < dur)
            while (midiTime < 5000)
            {
                image.Clear(SKColors.White);
                vertices.Unlink();
                colorsv.Unlink();
                foreach (var tempo in midiFile.TempoChanges)
                {
                    if (tempo.Ticks >= midiTime && tempo.Ticks < midiTime + deltaMidi)
                    {
                        midiFile.Tempo = tempo.Tempo;
                        midiFile.BPM = tempo.BPM;
                        deltaMidi = (midiFile.PPQ * (midiFile.BPM / 60.0) * deltaTime);
                    }
                }

                foreach (var note in midiFile.Notes)
                {
                    if (note.Key >= _nt)
                        continue;
                    float noteY = (float)(((midiTime - (dtCalc)) - note.StartTime) * scaleFactor) + ss;
                    //float noteY = (float)((midiTime - note.StartTime) * scaleFactor);
                    float noteHeight = (float)((note.EndTime - note.StartTime) * scaleFactor);
                    if (noteY + noteHeight >= 0 && noteY <= screenBottom)
                    {
                        float noteX = note.Key * keyWidth;
                        /*
                        vertices.AddRange(GetBox(noteX - thickness, noteY - thickness,
                            keyWidth + thickness * 2, noteHeight + thickness * 2));
                        colorsv.AddRange(borderColorArray);
                        vertices.AddRange(GetBox(noteX, noteY, keyWidth, noteHeight));
                        colorsv.AddRange(colorsvertexHelp[note.Track - 1]);
                        */
                        var uniforms = CreateNoteUniforms(
                            colors[note.Track - 1],
                            noteX,
                            noteY,
                            keyWidth,
                            noteHeight
                        );

                        // Crea el SKPaint con el shader
                        var notePaint = new SKPaint
                        {
                            Shader = NoteShaderEffect.ToShader(uniforms), // Añade null como segundo parámetro
                            IsAntialias = true
                        };

                        // Dibuja la nota (¡con sombra incluida!)
                        image.DrawRect(noteX, noteY, keyWidth, noteHeight, notePaint);
                        
                        notePaint.Dispose();
                        uniforms.Dispose();
                    }

                    if (!note.onPlayed && note.StartTime >= midiTime && note.StartTime <= (midiTime + deltaMidi))
                    {
                        note.SetonPlayed(true);
                        k[note.Key].AddTrack(note.Track);
                    }
                    if (note.EndTime >= midiTime && note.EndTime <= (midiTime + deltaMidi))
                    {
                        k[note.Key].RemoveTrack(note.Track);
                    }
                }
/*
                var vert = SKVertices.CreateCopy(SKVertexMode.Triangles, vertices.ToArray(),
                    colorsv.ToArray());
                image.DrawVertices(vert, SKBlendMode.Modulate, paint);
                vert.Dispose();
                */
                //image.DrawVertices(SKVertexMode.Triangles, vertices.ToArray(), 
                //            colorsv.ToArray(), paint);
                
                for (int i = 0; i < _nt; i++) // 88 teclas en un piano estándar
                {
                    var track = k[i].GetTopTrack();
                    var c = SKColors.White;
                    if (track == 0)
                    {
                        c = colors[track - 1];
                    }
                    var whiteKeyUniforms = CreateNoteUniforms(
                        c,
                        _wp[i].Left,
                        _wp[i].Top,
                        _wp[i].Width,
                        _wp[i].Height
                    );
                    var whiteKeyPaint = new SKPaint { Shader = NoteShaderEffect.ToShader(whiteKeyUniforms) };
                    image.DrawRect(_wp[i], whiteKeyPaint);
                    whiteKeyPaint.Dispose();
                    whiteKeyUniforms.Dispose();
                    /*image.DrawRect(_wp[i], pianoKeys[i]);
                    image.DrawRect(_wp[i], _outline);*/

                    // Dibuja las teclas negras (simplificación, ajustar para notas reales)
                    if (!WhitePianoKeys[i]) // Teclas negras (omitimos las que están en el espacio)
                    {
                        var blackKeyUniforms = CreateNoteUniforms(
                            c,
                            _bp[i].Left,
                            _bp[i].Top,
                            _bp[i].Width,
                            _bp[i].Height
                        );
                        var blackKeyPaint = new SKPaint { Shader = NoteShaderEffect.ToShader(blackKeyUniforms) };
                        image.DrawRect(_bp[i], blackKeyPaint);
                        blackKeyPaint.Dispose();
                        blackKeyUniforms.Dispose();
                        /*
                        image.DrawRect(_bp[i], blackpianoKeys[i]);
                        image.DrawRect(_bp[i], _outline);*/
                    }
                }
                surface.PeekPixels().ReadPixels(info, ptr, surface.RowBytes, 0, 0);

                Marshal.Copy(ptr, buffer, 0, buffer.Length);

                midiTime += deltaMidi;
                await ffmpegInput.WriteAsync(buffer, 0, buffer.Length);
            }

            Console.WriteLine("Freeing pointer");
            Marshal.FreeHGlobal(ptr);
            GC.RemoveMemoryPressure(surface.ByteCount);
            Console.WriteLine("Freeing image & canvas & Vertex & Colors");
            image.Dispose();
            surface.Dispose();
            vertices.Dispose();
            colorsv.Dispose();
            Console.WriteLine("Waiting ffmpeg to output the video...");
            // Cerrar FFmpeg y guardar el video
            ffmpegInput.Close();
            ffmpeg.WaitForExit();
            Console.WriteLine($"video saved in {outputVideoPath}");
            Console.Write("Press any key to exit...");
            Console.ReadKey();
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        static SKRect[] _wp;
        static SKRect[] _bp;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        private static bool IsBlackKey(int keyIndex)
        {
            // Las teclas negras en un piano siguen un patrón específico
            int note = keyIndex % 12;
            return note == 1 || note == 3 || note == 6 || note == 8 || note == 10;
        }
    }
}
